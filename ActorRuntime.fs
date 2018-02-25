module FSharpActors.ActorRuntime

open FSharpActors.Types


type Actor () =
    interface IActor with
    member this.Dispose() = ()





type ActorRuntime () =
    interface IActorRuntime with
    member this.Spawn (actorID : ActorID) = 
        Actor () :> IActor


