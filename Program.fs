open System
open FSharp.Actors
open FSharp.Actors.ActorHost


type GameStateForList = string list
type GameStateForInt = int

type Command =
    | Increase
    | Decrease

type Event<'State> =
    | Increased of 'State
    | Decreased of 'State

let listReducer state cmd =
    match cmd, state with
    | Increase, _ -> "+" :: state
    | Decrease, [] -> []
    | Decrease, _ :: t -> t

let intReducer state cmd =
    match cmd with
    | Increase -> state + 1
    | Decrease -> state - 1

let listStore : StateStore<GameStateForList> = 
    let mutable data : string list = []
    { Read = fun () -> async.Return data
      Write = fun x -> async { data <- x }}

let intStore : StateStore<GameStateForInt> = 
    let mutable data : int = 0
    { Read = fun () -> async.Return data
      Write = fun x -> async { data <- x } }

let listCfg = {
    Store = listStore
    Reducer = listReducer
    TimeOutInMills = None
}

let intCfg = {
    Store = intStore
    Reducer = intReducer
    TimeOutInMills = Some 2_00
}


[<EntryPoint>]
let main argv =

    printfn "Hello World from F#!"

    let timeOutInMills = 2_000

    let host1 = ActorHost.create listCfg
    let host2 = ActorHost.create intCfg

    let worker (actorHost : IActorHost<ActorMessage<'State, Command>>) = 
        async {
            let actorID = ActorID "game1"

            use! actor = actorHost.GetActor actorID
            let! rsp1 = actor.Ask (fun ch -> Increase, ch)
            System.Console.WriteLine ("1> {0}", rsp1)
            let! rsp2 = actor.Ask (fun ch -> Increase, ch)
            System.Console.WriteLine ("2> {0}", rsp2)

            do! Async.Sleep 3_000
            
            use! actor = actorHost.GetActor actorID
            let! rsp3 = actor.Ask ((fun ch -> Increase, ch), timeOutInMills)
            System.Console.WriteLine ("3> {0}", rsp3)
            let! rsp4 = actor.Ask (fun ch -> Decrease, ch)
            System.Console.WriteLine ("4> {0}", rsp4)
        }

    host1 |> worker |> Async.RunSynchronously
    host2 |> worker |> Async.RunSynchronously

    0
