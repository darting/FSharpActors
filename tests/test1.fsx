#r @"./netstandard.dll"
#r @"./Grpc.Core.dll"
#r @"./Google.Protobuf.dll"
#r @"./System.Reactive.dll"
#r @"./System.Interactive.Async.dll"
#r "../src/bin/Debug/netstandard2.0/actor.dll"
#r "../src/bin/Debug/netstandard2.0/FSharpActors.dll"

open System
open Grpc.Core
open Google.Protobuf
open FSharpActors.Prelude
open FSharpActors.Types
open FSharpActors.ActorHost
open FSharpActors.Remoting

type Message = 
    | Ping

let actorHost = ActorHost.Start ()
let actorID = "actor"

actorHost.Register actorID (fun inbox -> 
    let rec loop () = async {
        let! msg = inbox.Receive()
        printfn "received : %A" msg
        return! loop ()
    }
    loop ())

let server = start actorHost "localhost" 5432

let channel = Channel("localhost:5432", ChannelCredentials.Insecure)
let client = FSharpActors.Remoting.ActorRuntime.ActorRuntimeClient(channel)
let call = client.Receive()

let batch = MessageBatch()

for x in 1 .. 10 do
    let sender = ActorPID(Address = "client", ID = "client" + x.ToString())
    let target = ActorPID(Address = "server", ID = actorID)
    let payload = ByteString.CopyFromUtf8 ("hello" + x.ToString())
    let envelop = MessageEnvelope (
                    Kind = "msgkind",
                    Sender = sender, 
                    Target = target, 
                    Payload = payload)
    batch.Envelopes.Add(envelop)

call.RequestStream.WriteAsync(batch).Wait()
call.RequestStream.CompleteAsync().Wait()





Console.Read() |> ignore
server.ShutdownAsync().Wait()
