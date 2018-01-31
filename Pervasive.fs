module FSharpActors.Pervasive

open System
open System.Diagnostics

/// https://github.com/fsharp-editing/FSharp.Editing/blob/master/src/FSharp.Editing.Core/Pervasive.fs


/// Maybe computation expression builder, copied from ExtCore library
/// https://github.com/jack-pappas/ExtCore/blob/master/ExtCore/Control.fs
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

    member x.Either (m1 : 'T option) (m2 : 'T option) = 
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


[<Sealed>]
type AsyncMaybeBuilder () =
    [<DebuggerStepThrough>]
    member __.Return value : Async<'T option> = Some value |> async.Return

    [<DebuggerStepThrough>]
    member __.ReturnFrom value : Async<'T option> = value

    [<DebuggerStepThrough>]
    member __.ReturnFrom (value: 'T option) : Async<'T option> = async.Return value

    [<DebuggerStepThrough>]
    member __.Zero () : Async<unit option> = Some () |> async.Return

    [<DebuggerStepThrough>]
    member __.Delay (f : unit -> Async<'T option>) : Async<'T option> = async.Delay f

    [<DebuggerStepThrough>]
    member __.Combine (r1, r2 : Async<'T option>) : Async<'T option> = async {
        let! r1' = r1
        match r1' with
        | None -> return None
        | Some () -> return! r2
    }

    [<DebuggerStepThrough>]
    member __.Bind (value: Async<'T option>, f : 'T -> Async<'U option>) : Async<'U option> = async {
        let! value' = value
        match value' with
        | None -> return None
        | Some result -> return! f result
    }

    [<DebuggerStepThrough>]
    member __.Bind (value: System.Threading.Tasks.Task<'T>, f : 'T -> Async<'U option>) : Async<'U option> = async {
        let! value' = Async.AwaitTask value
        return! f value'
    }

    [<DebuggerStepThrough>]
    member __.Bind (value: 'T option, f : 'T -> Async<'U option>) : Async<'U option> = async {
        match value with
        | None -> return None
        | Some result -> return! f result
    }

    [<DebuggerStepThrough>]
    member __.Using (resource : ('T :> IDisposable), body : _ -> Async<_ option>) : Async<_ option> =
        try body resource
        finally if not (isNull resource) then resource.Dispose ()

    [<DebuggerStepThrough>]
    member x.While (guard, body : Async<_ option>) : Async<_ option> =
        if guard () then x.Bind (body, (fun () -> x.While (guard, body)))
        else x.Zero ()

    [<DebuggerStepThrough>]
    member x.For (sequence : seq<_>, body : 'T -> Async<unit option>) : Async<_ option> =
        x.Using (sequence.GetEnumerator (), fun enum ->
            x.While (enum.MoveNext, 
                x.Delay (fun () -> body enum.Current)
            )
        )

    [<DebuggerStepThrough>]
    member inline __.TryWith (computation : Async<'T option>, catchHandler : exn -> Async<'T option>) : Async<'T option> =
        async.TryWith (computation, catchHandler)

    [<DebuggerStepThrough>]
    member inline __.TryFinally (computation : Async<'T option>, compensation : unit -> unit) : Async<'T option> =
        async.TryFinally (computation, compensation)

let asyncMaybe = AsyncMaybeBuilder()

type AsyncChoice<'T, 'Error> =
    Async<Choice<'T, 'Error>>

[<Sealed>]
type AsyncChoiceBuilder () =
    // 'T -> M<'T>
    member (*inline*) __.Return value : Async<Choice<'T, 'Error>> =
        Choice1Of2 value
        |> async.Return

    // M<'T> -> M<'T>
    member (*inline*) __.ReturnFrom (asyncChoice : Async<Choice<'T, 'Error>>) =
        asyncChoice

    // unit -> M<'T>
    member inline this.Zero () : Async<Choice<unit, 'Error>> =
        this.Return ()

    // (unit -> M<'T>) -> M<'T>
    member inline this.Delay (generator : unit -> Async<Choice<'T, 'Error>>) : Async<Choice<'T, 'Error>> =
        async.Delay generator

    // M<'T> -> M<'T> -> M<'T>
    // or
    // M<unit> -> M<'T> -> M<'T>
    member (*inline*) __.Combine (r1, r2) : Async<Choice<'T, 'Error>> =
        async {
        let! r1' = r1
        match r1' with
        | Choice2Of2 error ->
            return Choice2Of2 error
        | Choice1Of2 () ->
            return! r2
        }

    // M<'T> * ('T -> M<'U>) -> M<'U>
    member (*inline*) __.Bind (value : Async<Choice<'T, 'Error>>, binder : 'T -> Async<Choice<'U, 'Error>>)
        : Async<Choice<'U, 'Error>> =
        async {
        let! value' = value
        match value' with
        | Choice2Of2 error ->
            return Choice2Of2 error
        | Choice1Of2 x ->
            return! binder x
        }

    // M<'T> * (exn -> M<'T>) -> M<'T>
    member inline __.TryWith (computation : Async<Choice<'T, 'Error>>, catchHandler : exn -> Async<Choice<'T, 'Error>>)
        : Async<Choice<'T, 'Error>> =
        async.TryWith(computation, catchHandler)

    // M<'T> * (unit -> unit) -> M<'T>
    member inline __.TryFinally (computation : Async<Choice<'T, 'Error>>, compensation : unit -> unit)
        : Async<Choice<'T, 'Error>> =
        async.TryFinally (computation, compensation)

    // 'T * ('T -> M<'U>) -> M<'U> when 'T :> IDisposable
    member inline __.Using (resource : ('T :> System.IDisposable), binder : _ -> Async<Choice<'U, 'Error>>)
        : Async<Choice<'U, 'Error>> =
        async.Using (resource, binder)

    // (unit -> bool) * M<'T> -> M<'T>
    member this.While (guard, body : Async<Choice<unit, 'Error>>) : Async<Choice<_,_>> =
        if guard () then
            // OPTIMIZE : This could be simplified so we don't need to make calls to Bind and While.
            this.Bind (body, (fun () -> this.While (guard, body)))
        else
            this.Zero ()

    // seq<'T> * ('T -> M<'U>) -> M<'U>
    // or
    // seq<'T> * ('T -> M<'U>) -> seq<M<'U>>
    member this.For (sequence : seq<_>, body : 'T -> Async<Choice<unit, 'Error>>) =
        // OPTIMIZE : This could be simplified so we don't need to make calls to Using, While, Delay.
        this.Using (sequence.GetEnumerator (), fun enum ->
            this.While (
                enum.MoveNext,
                this.Delay (fun () ->
                    body enum.Current)))

    member this.AwaitTask (task : System.Threading.Tasks.Task<'T>) = 
        task |> Async.AwaitTask |> Async.Catch

    member this.Wrap (value : Choice<'T, 'Error>) = 
        match value with
        | Choice1Of2 x -> async.Return (Choice1Of2 x)
        | Choice2Of2 x -> async.Return (Choice2Of2 x)

let asyncChoice = AsyncChoiceBuilder()

/// Functions for working with AsyncChoice workflows.
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AsyncChoice =
    open Microsoft.FSharp.Control

    /// Creates an AsyncChoice from an error value.
    [<CompiledName("Error")>]
    let inline error value : AsyncChoice<'T, 'Error> =
        async.Return (Choice2Of2 value)

    /// Creates an AsyncChoice representing an error value.
    /// The error value in the Choice is the specified error message.
    [<CompiledName("FailWith")>]
    let inline failwith errorMsg : AsyncChoice<'T, string> =
        async.Return (Choice2Of2 errorMsg)

    /// <summary>
    /// When the choice value is <c>Choice1Of2(x)</c>, returns <c>Choice1Of2 (f x)</c>.
    /// Otherwise, when the choice value is <c>Choice2Of2(x)</c>, returns <c>Choice2Of2(x)</c>. 
    /// </summary>
    [<CompiledName("Map")>]
    let map (mapping : 'T -> 'U) (value : AsyncChoice<'T, 'Error>) : AsyncChoice<'U, 'Error> =
        async {
        // Get the input value.
        let! x = value

        // Apply the mapping function and return the result.
        match x with
        | Choice1Of2 result ->
            return Choice1Of2 (mapping result)
        | Choice2Of2 error ->
            return (Choice2Of2 error)
        }

    [<CompiledName("Either")>]
    let either m1 m2 = 
        async {
            let! x = m1
            match x with
            | Choice2Of2 _ -> return! m2
            | Choice1Of2 result -> return Choice1Of2 result
        }

    /// <summary>
    /// When the choice value is <c>Choice1Of2(x)</c>, returns <c>Choice1Of2 (f x)</c>.
    /// Otherwise, when the choice value is <c>Choice2Of2(x)</c>, returns <c>Choice2Of2(x)</c>. 
    /// </summary>
    [<CompiledName("MapAsync")>]
    let mapAsync (mapping : 'T -> Async<'U>) (value : AsyncChoice<'T, 'Error>) : AsyncChoice<'U, 'Error> =
        async {
        // Get the input value.
        let! x = value

        // Apply the mapping function and return the result.
        match x with
        | Choice1Of2 result ->
            let! mappedResult = mapping result
            return Choice1Of2 mappedResult
        | Choice2Of2 error ->
            return (Choice2Of2 error)
        }

    module Operators =
        let (>>=) m f = asyncChoice.Bind (m, f)
        let (>=>) f1 f2 x = f1 x >>= f2
        let (>>%) m v = m >>= (fun _ -> asyncChoice.Return v)
        let (>>.) m1 m2 = m1 >>= (fun _ -> m2)
        let (.>>) m1 m2 = m1 >>= (fun x -> m2 >>% x)
        let (.>>.) m1 m2 = m1 >>= (fun x -> m2 >>= (fun y -> asyncChoice.Return (x, y)))
        let (<|>) m1 m2 = either m1 m2