open FSharpActors
open FSharpActors.ActorHost
open System
open Consul


type Message = 
    | Ping
    | Connect 

let connect () =
    async {
        let id = "node-1"
        let name = "node-1-name"
        use client = new ConsulClient (fun x -> x.Address <- Uri "http://localhost:32769")
        let register = AgentServiceRegistration(ID = id, Name = name, Tags = [||])
        let! _ = client.Agent.ServiceRegister register |> Async.AwaitTask
        return ()
    }

[<EntryPoint>]
let main _ = 

    use actorHost = ActorHost.Start ()

    let actor = actorHost.Spawn 1 (fun inbox -> 
                    let rec loop () = async {
                        let! msg = inbox.Receive()
                        match msg with 
                        | Connect ->
                            do! connect()
                        | Ping ->
                            printfn "received : %A" msg
                        return! loop ()
                    }
                    loop ()) 
                    |> Async.RunSynchronously

    actor.Tell Ping
    actor.Tell Connect
    actor.Tell Ping
    actor.Tell Ping

    Console.Read () |> ignore
    
    0
