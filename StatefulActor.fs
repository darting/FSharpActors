namespace FSharpActors


module StatefulActor =

    open Pervasive
    open Types
    open AutoCancelActor
    
    type Store<'State, 'Error> = {
        Read : unit -> AsyncChoice<'State, 'Error>
        Write : 'State -> AsyncChoice<unit, 'Error>
    }

    type Handler<'Command, 'Event, 'State, 'Error> = 'State -> 'Command -> AsyncChoice<'State * 'Event, 'Error>

    type ActionMessage<'Command, 'Event, 'Error> = 'Command * AsyncReplyChannel<Choice<'Event, 'Error>> option

    type Configuration<'Command, 'Event, 'State, 'Error> = {
        Store : Store<'State, 'Error>
        Handler : Handler<'Command, 'Event, 'State, 'Error>
        TimeOutInMills : int
    }

    let creator<'Command, 'Event, 'State, 'Error> 
        (configuration : Configuration<'Command, 'Event, 'State, 'Error>)
        (hostAgent : ActorHostAgent<ActionMessage<'Command, 'Event, 'Error>>)
        (actorID : ActorID) =
        async {
            let store = configuration.Store
            let handler = configuration.Handler
            let agent = AutoCancelActor<ActionMessage<'Command, 'Event, 'Error>>.Start actorID 
                            (fun inbox -> 
                                let rec shuttingDown state = async {
                                    try 
                                        do! store.Write state |> Async.Ignore
                                    with _ -> ()
                                    hostAgent.Post (Remove actorID)
                                }
                                let rec running previousState = async {
                                    try
                                        let! command, channel = inbox.Receive configuration.TimeOutInMills
                                        let! result = handler previousState command
                                        let state = match result with
                                                    | Choice1Of2 (newState, event) -> 
                                                        channel |> Option.iter (fun ch -> event |> Choice1Of2 |> ch.Reply)
                                                        newState
                                                    | Choice2Of2 error ->
                                                        channel |> Option.iter (fun ch -> error |> Choice2Of2 |> ch.Reply)
                                                        previousState
                                        return! running state
                                    with
                                    | ex -> 
                                        return! shuttingDown previousState
                                }
                                async {
                                    let! state = store.Read ()
                                    match state with
                                    | Choice1Of2 x -> return! running x
                                    | Choice2Of2 code -> 
                                        // TODO report reason and log error
                                        hostAgent.Post (Remove actorID)
                                })
            return agent :> IActor<ActionMessage<'Command, 'Event, 'Error>>
        }
