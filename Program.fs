open FSharpActors
open FSharpActors.ActorHost
open System

type Message = 
    | Ping

[<EntryPoint>]
let main _ = 

    let actorHost = ActorHost.Start ()

    let actor = actorHost.Spawn 1 (fun (inbox : MailboxProcessor<Message>) -> 
                    let rec loop () = async {
                        let! msg = inbox.Receive()
                        printfn "received : %A" msg
                        return! loop ()
                    }
                    loop ()) 
                    |> Async.RunSynchronously

    actor.Tell Ping
    actor.Tell Ping
    actor.Tell Ping

    Console.Read () |> ignore
    0
