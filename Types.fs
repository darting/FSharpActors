module FSharpActors.Types

open System

type ActorID = int32

type IActor = 
    inherit IDisposable
    
type IActorProxy = 
    abstract ActorID : ActorID

type IActorRuntime =
    abstract Spawn : ActorID -> IActor


