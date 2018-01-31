module FSharpActors.Types
        
open System
open Microsoft.Extensions.Logging
open App.Metrics

type ActorID = ActorID of string

type Agent<'T> = MailboxProcessor<'T>

type IActor<'Message> =
    inherit IDisposable
    abstract ID : ActorID
    abstract Ask : (AsyncReplyChannel<'Reply> -> 'Message) * ?timeout:int -> Async<'Reply>
    abstract Tell : 'Message -> unit

type ActorHostState<'ActorMessage> = {
    Actors : Map<string, IActor<'ActorMessage>>
} with static member Zero = { Actors = Map.empty<string, IActor<'ActorMessage>> }

type ActorHostMessage<'ActorMessage> =
    | Get of ActorID * AsyncReplyChannel<IActor<'ActorMessage>>
    | Remove of ActorID * RemoveReason
    | GetState of AsyncReplyChannel<ActorHostState<'ActorMessage>>
and RemoveReason = 
    | Timeout
    | Error of exn

type ActorHostAgent<'ActorMessage> = Agent<ActorHostMessage<'ActorMessage>>

type ActorCreator<'ActorMessage> = ActorHostAgent<'ActorMessage> -> ActorID -> Async<IActor<'ActorMessage>>

type IActorHost<'ActorMessage> =
    abstract GetActor : ActorID -> Async<IActor<'ActorMessage>>
    abstract GetState : unit -> Async<ActorHostState<'ActorMessage>>

type ActorHostRuntime<'ActorMessage> = {
    HostIdentity : string
    ActorCreator : ActorCreator<'ActorMessage>
    Logger : ILogger
    Metrics : IMetricsRoot
}
