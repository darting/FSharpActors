module FSharpActors.Types

open System

type Job<'Message> = MailboxProcessor<'Message> -> Async<unit>

type ActorID = int32

type IActor<'T> = 
    inherit IDisposable
    abstract ID : ActorID
    abstract Ask : (AsyncReplyChannel<'Reply> -> 'T) * ?timeout:int -> Async<'Reply option>
    abstract Tell : 'T -> unit
    
type IActorProxy<'T> = interface end

type IActorHost =
    abstract Spawn<'T> : ActorID -> Job<'T> -> Async<IActor<'T>>


