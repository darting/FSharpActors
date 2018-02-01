namespace FSharpActors


module StatelessActor =

    open FSharpActors.Types
    open FSharpActors.AutoCancelActor

    type Handler<'Command, 'Event> = 'Command -> Async<'Event>

    type ActorMessage<'Command, 'Event> = 'Command * AsyncReplyChannel<'Event> option

    type Configuration<'Command, 'Event> = {
        Handler : Handler<'Command, 'Event>
        TimeOutInMills : int
    }

    let creator<'Command, 'Event> 
        (configuration : Configuration<'Command, 'Event>)
        (hostAgent : ActorHostAgent<ActorMessage<'Command, 'Event>>) 
        (actorID : ActorID) =
        async {
            let handler = configuration.Handler
            let agent = AutoCancelActor<ActorMessage<'Command, 'Event>>.Start actorID 
                            (fun inbox -> 
                                let rec shuttingDown reason = async {
                                    hostAgent.Post (Remove (actorID, reason))
                                }
                                let rec running () = async {
                                    try
                                        let! command, channel = inbox.Receive configuration.TimeOutInMills
                                        let! event = handler command
                                        channel |> Option.iter (fun ch -> event |> ch.Reply)
                                        return! running ()
                                    with
                                    | ex -> 
                                        return! shuttingDown (Error ex)
                                }
                                running ())
            return agent :> IActor<ActorMessage<'Command, 'Event>>
        }
