namespace FSharpActors


module ActorHost =

    open System
    open App.Metrics
    open FSharpActors.Types
    open Metrics

    let private totalActorHostCounter = 
        Counter.create()
        |> Counter.withName "Total Active ActorHost"
        |> Counter.withUnit Unit.Items

    let private totalActorsCounter = 
        Counter.create()
        |> Counter.withName "Total Active Actors"
        |> Counter.withUnit Unit.Items
            
    let private actorRequestsMeter = 
        Meter.create()
        |> Meter.withName "Actor Requests"


    let private actorResolver (runtime: ActorHostRuntime<'T>)
                      (actorCreator : ActorID -> Async<IActor<'T>>)
                      (state : ActorHostState<'T>)
                      (ActorID key as actorID)
                      (replyChannel : AsyncReplyChannel<IActor<'T>>) =
        async {
            runtime.Metrics.Measure.Meter.Mark actorRequestsMeter
            let! newState, actor = 
                match Map.tryFind key state.Actors with
                | Some actor -> async.Return (state, actor)
                | None ->
                    runtime.Metrics.Measure.Counter.Increment totalActorsCounter
                    async {
                        let! actor = actorCreator actorID
                        let actors = Map.add key actor state.Actors
                        return { state with Actors = actors }, actor
                    }
            replyChannel.Reply actor
            return newState
        }

    let private handle<'T> (runtime: ActorHostRuntime<'T>) (hostAgent : ActorHostAgent<'T>) (actorCreator : ActorCreator<'T>) (state : ActorHostState<'T>) message = 
        match message with
        | Get (actorID, replyChannel) -> 
            actorResolver runtime (actorCreator hostAgent) state actorID replyChannel
        | Remove (ActorID actorID, reason) ->
            runtime.Metrics.Measure.Counter.Decrement totalActorsCounter
            match Map.tryFind actorID state.Actors with
            | Some actor -> 
                try (actor :> IDisposable).Dispose()
                with _ -> ()
            | None -> ()
            async.Return { state with Actors = Map.remove actorID state.Actors }
        | GetState replyChannel ->
            replyChannel.Reply state
            async.Return state
          
            
    let create<'T> (runtime: ActorHostRuntime<'T>) (actorCreator : ActorCreator<'T>) =

        runtime.Metrics.Measure.Counter.Increment totalActorHostCounter

        let actorHostAgent = ActorHostAgent<'T>.Start(fun inbox ->
            let rec loop (currentState : ActorHostState<'T>) = async {
                let! message = inbox.Receive ()
                let! newState = handle runtime inbox actorCreator currentState message
                return! loop newState
            }
            loop ActorHostState<'T>.Zero)
        
        { new IActorHost<'T> with
            member __.GetActor actorID = actorHostAgent.PostAndAsyncReply (fun ch -> Get (actorID, ch))
            member __.GetState () = actorHostAgent.PostAndAsyncReply GetState }

