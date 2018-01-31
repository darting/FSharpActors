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


    let private actorResolver (metrics : IMetricsRoot)
                      (actorCreator : ActorID -> Async<IActor<'T>>)
                      (state : ActorHostState<'T>)
                      (ActorID key as actorID)
                      (replyChannel : AsyncReplyChannel<IActor<'T>>) =
        async {
            metrics.Measure.Meter.Mark actorRequestsMeter
            let! newState, actor = 
                match Map.tryFind key state.Actors with
                | Some actor -> async.Return (state, actor)
                | None ->
                    metrics.Measure.Counter.Increment totalActorsCounter
                    async {
                        let! actor = actorCreator actorID
                        let actors = Map.add key actor state.Actors
                        return { state with Actors = actors }, actor
                    }
            replyChannel.Reply actor
            return newState
        }

    let private handle<'T> (metrics : IMetricsRoot) (hostAgent : ActorHostAgent<'T>) (actorCreator : ActorCreator<'T>) (state : ActorHostState<'T>) message = 
        match message with
        | Get (actorID, replyChannel) -> actorResolver metrics (actorCreator hostAgent) state actorID replyChannel
        | Remove (ActorID actorID) ->
            metrics.Measure.Counter.Decrement totalActorsCounter
            match Map.tryFind actorID state.Actors with
            | Some actor -> (actor :> IDisposable).Dispose()
            | None -> ()
            async.Return { state with Actors = Map.remove actorID state.Actors }
        | GetState replyChannel ->
            replyChannel.Reply state
            async.Return state
          
            
    let create<'T> (metrics : IMetricsRoot) (actorCreator : ActorCreator<'T>) =

        metrics.Measure.Counter.Increment totalActorHostCounter

        let actorHost = MailboxProcessor<ActorHostMessage<'T>>.Start(fun inbox ->
            let rec loop (previousState : ActorHostState<'T>) = async {
                let! message = inbox.Receive ()
                let! newState = handle metrics inbox actorCreator previousState message
                return! loop newState
            }
            loop ActorHostState<'T>.Zero)
        
        { new IActorHost<'T> with
            member __.GetActor actorID = actorHost.PostAndAsyncReply (fun ch -> Get (actorID, ch))
            member __.RemoveActor actorID = actorHost.Post (Remove actorID)
            member __.GetState () = actorHost.PostAndAsyncReply GetState }

