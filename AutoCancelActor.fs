namespace FSharpActors


module AutoCancelActor =

    open System
    open System.Threading
    open FSharpActors.Types

    type AutoCancelActor<'T> private (actorID : ActorID, agent : MailboxProcessor<'T>, cts : CancellationTokenSource) =

        static member Start<'T> actorID job =
            let cts = new CancellationTokenSource ()
            let agent = MailboxProcessor<'T>.Start (job, cts.Token)
            new AutoCancelActor<'T>(actorID, agent, cts)

        interface IActor<'T> with
            member __.ID = actorID
            member __.Ask (buildMessage, ?timeout) = agent.PostAndTryAsyncReply (buildMessage, ?timeout=timeout)
            member __.Tell message = agent.Post message
            member __.Dispose () =
                (agent :> IDisposable).Dispose ()
                cts.Cancel ()
