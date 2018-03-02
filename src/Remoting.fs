module FSharpActors.Remoting

open System.Threading
open System.Threading.Tasks
open Grpc.Core
open FSharp.Control.Tasks.ContextInsensitive
open FSharpActors.Remoting


type Remoting () = 
    inherit ActorRuntime.ActorRuntimeBase ()

    override this.Receive(requestStream : IAsyncStreamReader<MessageBatch>
                         , responseStream : IServerStreamWriter<Unit>
                         , context : ServerCallContext) = 

        task {
            let cts = new CancellationTokenSource()                         
            let! x = requestStream.MoveNext cts.Token
            ()
        } 


    