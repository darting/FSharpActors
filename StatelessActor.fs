namespace FSharpActors


module StatelessActor =

    open FSharpActors.Types
    open FSharpActors.AutoCancelActor

    type Handler<'Command, 'Event> = 'Command -> Async<'Event>

    type Configuration<'Command, 'Event> = {
        Handler : Handler<'Command, 'Event>
        TimeOutInMills : int
    }

    let creator<'Command, 'Event> 
        (configuration : Configuration<'Command, 'Event>)
        (hostAgent : ActorHostAgent<'Command * AsyncReplyChannel<'Event> option>) 
        (actorID : ActorID) =
        async {
            let handler = configuration.Handler
            let agent = AutoCancelActor<'Command * AsyncReplyChannel<'Event> option>.Start actorID 
                            (fun inbox -> 
                                let rec shuttingDown () = async {
                                    hostAgent.Post (Remove actorID)
                                }
                                let rec running () = async {
                                    try
                                        let! command, channel = inbox.Receive configuration.TimeOutInMills
                                        let! event = handler command
                                        channel |> Option.iter (fun ch -> event |> ch.Reply)
                                        return! running ()
                                    with
                                    | ex -> 
                                        return! shuttingDown ()
                                }
                                running ())
            return agent :> IActor<'Command * AsyncReplyChannel<'Event> option>
        }
