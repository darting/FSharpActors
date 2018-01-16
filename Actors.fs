namespace FSharp


module Actors =

    open System
    open System.Threading

    type ActorID = ActorID of string

    type Store<'State> = {
        Read : unit -> Async<'State>
        Write : 'State -> Async<unit>
    }

    type Reducer<'State, 'Action> = 'State -> 'Action -> 'State

    type ActorConfiguration<'State, 'Action> = {
        Store : Store<'State>
        Reducer : Reducer<'State, 'Action>
        TimeOutInMills : int option
    }

    type Agent<'T> private (actorID : ActorID, processor : MailboxProcessor<'T>, cts : CancellationTokenSource) =
        static member Start actorID job =
            let cts = new CancellationTokenSource ()
            let proc = new MailboxProcessor<'T> (job, cts.Token)
            do
                // proc.Error.Add (fun ex -> Console.WriteLine ex.Message)
                proc.Start ()
            new Agent<'T>(actorID, proc, cts)

        member __.Ask (buildMessage, ?timeout) = processor.PostAndAsyncReply (buildMessage, ?timeout=timeout)
        member __.TryAsk (buildMessage, ?timeout) = processor.PostAndTryAsyncReply (buildMessage, ?timeout=timeout)
        member __.Post message = processor.Post message
        member __.ID = actorID
        interface IDisposable with
            member __.Dispose () =
                (processor :> IDisposable).Dispose ()
                cts.Cancel ()



    module ActorHost = 

        type ActorMessage<'State, 'Action> = 'Action * AsyncReplyChannel<'State>

        type ActorHostState<'State, 'Action> = Map<string, Agent<ActorMessage<'State, 'Action>>>

        type private ActorHostMessage<'State, 'Action> =
            | Get of ActorID * AsyncReplyChannel<Agent<ActorMessage<'State, 'Action>>>
            | Remove of ActorID
            | GetState of AsyncReplyChannel<ActorHostState<'State, 'Action>>

        type IActorHost<'State, 'Action> =
            abstract GetActor : ActorID -> Async<Agent<ActorMessage<'State, 'Action>>>
            abstract RemoveActor : ActorID -> unit
            abstract GetState : unit -> Async<ActorHostState<'State, 'Action>>

        let private workerCreator<'State, 'Action>
                (configuration : ActorConfiguration<'State, 'Action>)
                (actorHost : MailboxProcessor<ActorHostMessage<'State, 'Action>>)
                (actorID : ActorID) =
            let timeOutInMills = match configuration.TimeOutInMills with
                                 | Some x -> x
                                 | None -> -1
            let store = configuration.Store
            let reducer = configuration.Reducer
            let worker = Agent<'Action * AsyncReplyChannel<'State>>.Start actorID
                                (fun inbox -> 
                                    let rec loop previousState = async {
                                        try
                                            let! action, channel = inbox.Receive timeOutInMills
                                            let newState = reducer previousState action
                                            channel.Reply newState
                                            return! loop newState
                                        with
                                        // | :? TimeoutException -> 
                                        //     actorHost.RemoveActor actorID
                                        | ex -> 
                                            try do! store.Write previousState
                                            with _ -> ()
                                            actorHost.Post (Remove actorID)
                                    }
                                    async {
                                        let! state = store.Read ()
                                        return! loop state
                                    })
            worker
        
        let create<'State, 'Action> (actorConfiguration : ActorConfiguration<'State, 'Action>) =
            let actorHost = 
                new MailboxProcessor<ActorHostMessage<'State, 'Action>> (fun inbox -> 
                    let rec loop (state : ActorHostState<'State, 'Action>) = async {
                        let! message = inbox.Receive()
                        let newState = match message with
                                        | Get (ActorID actorID, replyChannel) ->
                                            let state', actor = 
                                                match Map.tryFind actorID state with
                                                | Some actor -> state, actor
                                                | None -> 
                                                    let actor = workerCreator actorConfiguration inbox (ActorID actorID)
                                                    Map.add actorID actor state, actor
                                            replyChannel.Reply actor
                                            state'
                                        | Remove (ActorID actorID) ->
                                            match Map.tryFind actorID state with
                                            | Some actor -> (actor :> IDisposable).Dispose()
                                            | None -> ()
                                            Map.remove actorID state
                                        | GetState replyChannel ->
                                            replyChannel.Reply state
                                            state
                        return! loop newState
                    }
                    loop Map.empty)
            actorHost.Start ()
            { new IActorHost<'State, 'Action> with
                member __.GetActor actorID = actorHost.PostAndAsyncReply (fun ch -> Get (actorID, ch))
                member __.RemoveActor actorID = actorHost.Post (Remove actorID)
                member __.GetState () = actorHost.PostAndAsyncReply GetState }



