module FSharpActors.Types
        
open System

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
    | Remove of ActorID
    | GetState of AsyncReplyChannel<ActorHostState<'ActorMessage>>

type ActorHostAgent<'ActorMessage> = Agent<ActorHostMessage<'ActorMessage>>

type ActorCreator<'ActorMessage> = ActorHostAgent<'ActorMessage> -> ActorID -> Async<IActor<'ActorMessage>>

type IActorHost<'ActorMessage> =
    abstract GetActor : ActorID -> Async<IActor<'ActorMessage>>
    abstract RemoveActor : ActorID -> unit
    abstract GetState : unit -> Async<ActorHostState<'ActorMessage>>


