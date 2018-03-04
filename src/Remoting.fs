module FSharpActors.Remoting

open System.Text
open Grpc.Core
open Grpc.Core.Utils
open MBrace.FsPickler.Json
open FSharpActors.Remoting
open Types



let pickler = FsPickler.CreateJsonSerializer(indent = false)

let resolver<'T> (actorHost : IActorHost) (pid : ActorPID) = 
    actorHost.Resolve<'T> pid.ID

let inline tell (message : MessageEnvelope) (actor : IActor<'T>) = 
    actor.Tell (pickler.UnPickle (message.Payload.ToByteArray(), encoding=Encoding.UTF8))

type MessageReceiver (actorHost : IActorHost) = 
    inherit ActorRuntime.ActorRuntimeBase ()

    let resolver = resolver actorHost
    let sender (message : MessageEnvelope) =
        async {
            let! actor = resolver message.Target
            Option.iter (tell message) actor
        }

    override __.Receive(requestStream : IAsyncStreamReader<MessageBatch>
                         , _ : IServerStreamWriter<Unit>
                         , _ : ServerCallContext) = 
        requestStream.ForEachAsync (fun batch ->
            async {
                batch.Envelopes
                |> Seq.map sender
                |> Async.Parallel
                |> Async.Ignore
                |> Async.Start
            } 
            |> Async.StartAsPlainTask)


let start (actorHost : IActorHost) host port =
    let server = Server ()
    server.Services.Add(ActorRuntime.BindService(MessageReceiver actorHost))
    server.Ports.Add(ServerPort(host, port, ServerCredentials.Insecure)) |> ignore
    server.Start()
    server


