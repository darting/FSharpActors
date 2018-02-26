module FSharpActors.ActorRuntime

open System.Threading
open System.Collections.Concurrent
open FSharpActors.Types

let private defaultTimeOutInMills = 20 * 1000

type Actor<'T> private (actorID : ActorID, mailbox : MailboxProcessor<'T>, cts : CancellationTokenSource) =

    static member Start<'T> actorID job =
        let cts = new CancellationTokenSource ()
        let mailbox = MailboxProcessor<'T>.Start (job, cts.Token)
        new Actor<'T>(actorID, mailbox, cts)

    static member (<?) (actor : IActor<'T>) buildMessage = actor.Ask buildMessage

    interface IActor<'T> with
        member __.ID = actorID
        member __.Ask (buildMessage, ?timeout) = mailbox.PostAndTryAsyncReply (buildMessage, defaultArg timeout defaultTimeOutInMills)
        member __.Tell message = mailbox.Post message
        member __.Dispose () =
            // (mailbox :> IDisposable).Dispose ()
            cts.Cancel ()

type ActorRuntime () =

    let registry = new ConcurrentDictionary<ActorID, IActor<_>>()

    interface IActorRuntime with

    member this.Spawn<'T> (actorID : ActorID) job = 
        async {
            match registry.TryGetValue actorID with
            | true, x -> return x :> IActor<'T>
            | false, _ -> 
                let actor = Actor<'T>.Start actorID job
                registry.TryAdd (actorID, actor)
                return actor :> IActor<'T>
        }
        


