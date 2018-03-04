module FSharpActors.Types

open System

type Job<'Message> = MailboxProcessor<'Message> -> Async<unit>

type ActorID = string

type IActor<'T> = 
    inherit IDisposable
    abstract ID : ActorID
    abstract Ask : (AsyncReplyChannel<'Reply> -> 'T) * ?timeout:int -> Async<'Reply option>
    abstract Tell : 'T -> unit
    
type IActorProxy<'T> = interface end

type IActorHost =
    inherit IDisposable
    abstract Spawn<'T> : ActorID -> Job<'T> -> Async<IActor<'T>>
    abstract Register<'T> : ActorID -> Job<'T> -> unit
    abstract Resolve : ActorID -> Async<IActor<'T> option>


