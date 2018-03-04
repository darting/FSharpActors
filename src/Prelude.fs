[<AutoOpen>] 
module FSharpActors.Prelude

open System.Diagnostics
open System.Threading.Tasks


module Async =
    // https://gist.github.com/theburningmonk/3921623
    let inline AwaitPlainTask (task: Task) = 
        // rethrow exception from preceding task if it fauled
        let continuation (t : Task) : unit =
            match t.IsFaulted with
            | true -> raise t.Exception
            | _ -> ()
        task.ContinueWith continuation |> Async.AwaitTask
        
    let inline StartAsPlainTask (work : Async<unit>) = Task.Factory.StartNew(fun () -> work |> Async.RunSynchronously)

[<Sealed>]
type MaybeBuilder () =
    // 'T -> M<'T>
    [<DebuggerStepThrough>]
    member inline __.Return value: 'T option = Some value

    // M<'T> -> M<'T>
    [<DebuggerStepThrough>]
    member inline __.ReturnFrom value: 'T option = value

    // unit -> M<'T>
    [<DebuggerStepThrough>]
    member inline __.Zero (): unit option = Some ()     // TODO: Should this be None?

    // (unit -> M<'T>) -> M<'T>
    [<DebuggerStepThrough>]
    member __.Delay (f: unit -> 'T option): 'T option = f ()

    // M<'T> -> M<'T> -> M<'T>
    // or
    // M<unit> -> M<'T> -> M<'T>
    [<DebuggerStepThrough>]
    member inline __.Combine (r1, r2: 'T option): 'T option =
        match r1 with
        | None -> None
        | Some () -> r2

    // M<'T> * ('T -> M<'U>) -> M<'U>
    [<DebuggerStepThrough>]
    member inline __.Bind (value, f: 'T -> 'U option): 'U option = Option.bind f value

    // 'T * ('T -> M<'U>) -> M<'U> when 'U :> IDisposable
    [<DebuggerStepThrough>]
    member __.Using (resource: ('T :> System.IDisposable), body: _ -> _ option): _ option =
        try body resource
        finally if not <| obj.ReferenceEquals (null, box resource) then resource.Dispose ()

    // (unit -> bool) * M<'T> -> M<'T>
    [<DebuggerStepThrough>]
    member x.While (guard, body: _ option): _ option =
        if guard () then
            // OPTIMIZE: This could be simplified so we don't need to make calls to Bind and While.
            x.Bind (body, (fun () -> x.While (guard, body)))
        else x.Zero ()

    // seq<'T> * ('T -> M<'U>) -> M<'U>
    // or
    // seq<'T> * ('T -> M<'U>) -> seq<M<'U>>
    [<DebuggerStepThrough>]
    member x.For (sequence: seq<_>, body: 'T -> unit option): _ option =
        // OPTIMIZE: This could be simplified so we don't need to make calls to Using, While, Delay.
        x.Using (sequence.GetEnumerator (), fun enum ->
            x.While (enum.MoveNext,
                x.Delay (fun () -> body enum.Current)
            )
        )

    member __.Either (m1 : 'T option) (m2 : 'T option) = 
        match m1 with
        | Some x -> Some x
        | None -> m2  

let maybe = MaybeBuilder()

module Maybe = 

    module Operators =
        let (>>=) m f = Option.bind f m
        let (>=>) f1 f2 x = f1 x >>= f2
        let (>>%) m v = m >>= (fun _ -> maybe.Return v)
        let (>>.) m1 m2 = m1 >>= (fun _ -> m2)
        let (.>>) m1 m2 = m1 >>= (fun x -> m2 >>% x)
        let (.>>.) m1 m2 = m1 >>= (fun x -> m2 >>= (fun y -> maybe.Return (x, y)))
        let (<|>) m1 m2 = maybe.Either m1 m2



type AsyncChoice<'T, 'Error> = Async<Result<'T, 'Error>>

[<Sealed>]
type AsyncChoiceBuilder () =
    // 'T -> M<'T>
    member (*inline*) __.Return value : AsyncChoice<'T, 'Error> =
        Ok value |> async.Return

    // M<'T> -> M<'T>
    member (*inline*) __.ReturnFrom (asyncChoice : AsyncChoice<'T, 'Error>) =
        asyncChoice

    // unit -> M<'T>
    member inline this.Zero () : AsyncChoice<unit, 'Error> =
        this.Return ()

    // (unit -> M<'T>) -> M<'T>
    member inline __.Delay (generator : unit -> AsyncChoice<'T, 'Error>) : AsyncChoice<'T, 'Error> =
        async.Delay generator

    // M<'T> -> M<'T> -> M<'T>
    // or
    // M<unit> -> M<'T> -> M<'T>
    member (*inline*) __.Combine (r1, r2) : AsyncChoice<'T, 'Error> =
        async {
            let! r1' = r1
            match r1' with
            | Error error ->
                return Error error
            | Ok () ->
                return! r2
        }

    // M<'T> * ('T -> M<'U>) -> M<'U>
    member (*inline*) __.Bind (value : AsyncChoice<'T, 'Error>, binder : 'T -> AsyncChoice<'U, 'Error>)
        : AsyncChoice<'U, 'Error> =
        async {
            let! value' = value
            match value' with
            | Error error ->
                return Error error
            | Ok x ->
                return! binder x
        }

    // M<'T> * (exn -> M<'T>) -> M<'T>
    member inline __.TryWith (computation : AsyncChoice<'T, 'Error>, catchHandler : exn -> AsyncChoice<'T, 'Error>)
        : AsyncChoice<'T, 'Error> =
        async.TryWith(computation, catchHandler)

    // M<'T> * (unit -> unit) -> M<'T>
    member inline __.TryFinally (computation : AsyncChoice<'T, 'Error>, compensation : unit -> unit)
        : AsyncChoice<'T, 'Error> =
        async.TryFinally (computation, compensation)

    // 'T * ('T -> M<'U>) -> M<'U> when 'T :> IDisposable
    member inline __.Using (resource : ('T :> System.IDisposable), binder : _ -> AsyncChoice<'U, 'Error>)
        : AsyncChoice<'U, 'Error> =
        async.Using (resource, binder)

    // (unit -> bool) * M<'T> -> M<'T>
    member this.While (guard, body : AsyncChoice<unit, 'Error>) : AsyncChoice<_,_> =
        if guard () then
            // OPTIMIZE : This could be simplified so we don't need to make calls to Bind and While.
            this.Bind (body, (fun () -> this.While (guard, body)))
        else
            this.Zero ()

    // seq<'T> * ('T -> M<'U>) -> M<'U>
    // or
    // seq<'T> * ('T -> M<'U>) -> seq<M<'U>>
    member this.For (sequence : seq<_>, body : 'T -> AsyncChoice<unit, 'Error>) =
        // OPTIMIZE : This could be simplified so we don't need to make calls to Using, While, Delay.
        this.Using (sequence.GetEnumerator (), fun enum ->
            this.While (
                enum.MoveNext,
                this.Delay (fun () ->
                    body enum.Current)))

    member __.AwaitTask (task : System.Threading.Tasks.Task<'T>) = 
        task |> Async.AwaitTask |> Async.Catch

    member __.Wrap (value : Result<'T, 'Error>) = 
        match value with
        | Ok x -> async.Return (Ok x)
        | Error x -> async.Return (Error x)

let asyncChoice = AsyncChoiceBuilder()

/// Functions for working with AsyncChoice workflows.
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AsyncChoice =
    open Microsoft.FSharp.Control

    /// Creates an AsyncChoice from an error value.
    [<CompiledName("Error")>]
    let inline error value : AsyncChoice<'T, 'Error> =
        async.Return (Error value)

    /// Creates an AsyncChoice representing an error value.
    /// The error value in the Choice is the specified error message.
    [<CompiledName("FailWith")>]
    let inline failwith errorMsg : AsyncChoice<'T, string> =
        async.Return (Error errorMsg)

    /// <summary>
    /// When the choice value is <c>Ok(x)</c>, returns <c>Ok (f x)</c>.
    /// Otherwise, when the choice value is <c>Error(x)</c>, returns <c>Error(x)</c>. 
    /// </summary>
    [<CompiledName("Map")>]
    let map (mapping : 'T -> 'U) (value : AsyncChoice<'T, 'Error>) : AsyncChoice<'U, 'Error> =
        async {
            // Get the input value.
            let! x = value

            // Apply the mapping function and return the result.
            match x with
            | Ok result ->
                return Ok (mapping result)
            | Error error ->
                return (Error error)
        }

    [<CompiledName("Either")>]
    let either m1 m2 = 
        async {
            let! x = m1
            match x with
            | Error _ -> return! m2
            | Ok result -> return Ok result
        }

    /// <summary>
    /// When the choice value is <c>Ok(x)</c>, returns <c>Ok (f x)</c>.
    /// Otherwise, when the choice value is <c>Error(x)</c>, returns <c>Error(x)</c>. 
    /// </summary>
    [<CompiledName("MapAsync")>]
    let mapAsync (mapping : 'T -> Async<'U>) (value : AsyncChoice<'T, 'Error>) : AsyncChoice<'U, 'Error> =
        async {
            // Get the input value.
            let! x = value

            // Apply the mapping function and return the result.
            match x with
            | Ok result ->
                let! mappedResult = mapping result
                return Ok mappedResult
            | Error error ->
                return (Error error)
        }

    module Operators =
        let (>>=) m f = asyncChoice.Bind (m, f)
        let (>=>) f1 f2 x = f1 x >>= f2
        let (>>%) m v = m >>= (fun _ -> asyncChoice.Return v)
        let (>>.) m1 m2 = m1 >>= (fun _ -> m2)
        let (.>>) m1 m2 = m1 >>= (fun x -> m2 >>% x)
        let (.>>.) m1 m2 = m1 >>= (fun x -> m2 >>= (fun y -> asyncChoice.Return (x, y)))
        let (<|>) m1 m2 = either m1 m2