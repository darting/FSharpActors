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

    type ActionMessage<'Command, 'Event, 'Error> = 
        | WriteState 
        | ReadState 
        | Message of 'Command * AsyncReplyChannel<Choice<'Event, 'Error>> option

    type Props<'Command, 'Event, 'State, 'Error> = {
        Store : Store<'State, 'Error>
        Activating : unit -> Async<unit>
        Activated : unit -> Async<unit> 
        Deactivating : unit -> Async<unit> 
        Deactivated : unit -> Async<unit> 
        HandleMessage : Handler<'Command, 'Event, 'State, 'Error>
        HandleError : 'Error -> Async<unit>
        TimeOutInMills : int
    }

    let creator<'Command, 'Event, 'State, 'Error> 
        (props : Props<'Command, 'Event, 'State, 'Error>)
        (hostAgent : ActorHostAgent<ActionMessage<'Command, 'Event, 'Error>>)
        (actorID : ActorID) =
        async {
            let store = props.Store
            let handler = props.HandleMessage
            let agent = AutoCancelActor<ActionMessage<'Command, 'Event, 'Error>>.Start actorID 
                            (fun inbox -> 
                                let rec remove reason = async {
                                        hostAgent.Post (Remove (actorID, reason))                                    
                                    }
                                and writeState state = async {
                                        let! x = store.Write state
                                        match x with
                                        | Choice2Of2 err -> 
                                            do! props.HandleError err
                                            return! remove (Error (exn "can not write state"))
                                        | _ -> ()
                                    }
                                and readState () = async {
                                        let! state = store.Read ()
                                        match state with
                                        | Choice1Of2 x -> 
                                            do! props.Activated ()
                                            return! running x
                                        | Choice2Of2 err -> 
                                            do! props.HandleError err
                                            return! remove (Error (exn "can not read state"))
                                    }
                                and shuttingDown state reason = async {
                                        do! writeState state
                                        return! remove reason
                                    }
                                and running previousState = async {
                                    try
                                        let! message = inbox.Receive props.TimeOutInMills
                                        match message with
                                        | WriteState -> 
                                            do! writeState previousState
                                            return! running previousState
                                        | ReadState -> 
                                            return! readState ()
                                        | Message (command, reply) ->
                                            let! result = handler previousState command
                                            let state = match result with
                                                        | Choice1Of2 (newState, event) -> 
                                                            reply |> Option.iter (fun ch -> event |> Choice1Of2 |> ch.Reply)
                                                            newState
                                                        | Choice2Of2 error ->
                                                            reply |> Option.iter (fun ch -> error |> Choice2Of2 |> ch.Reply)
                                                            previousState
                                            return! running state
                                    with
                                    | ex -> 
                                        return! shuttingDown previousState (Error ex)
                                }
                                async {
                                    do! props.Activating ()
                                    return! readState()
                                })
            return agent :> IActor<ActionMessage<'Command, 'Event, 'Error>>
        }
