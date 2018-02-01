namespace FSharpActors


module StatefulActor =

    open System
    open FSharpActors.Types
    open FSharpActors.AutoCancelActor

    type CommandHandler<'State, 'Command, 'Event> = 'Command -> 'State -> Async<'Event * 'State>

    type ActorMessage<'Command, 'Event> = 'Command * AsyncReplyChannel<'Event> option

    type Configuration<'State, 'Command, 'Event> = {
        CommandHandler : CommandHandler<'State, 'Command, 'Event>
        TimeOutInMills : int
        LoadState : unit -> Async<'State>
    }

    let creator<'State, 'Command, 'Event>
        (configuration : Configuration<'State, 'Command, 'Event>)
        (hostAgent : ActorHostAgent<ActorMessage<'Command, 'Event>>) 
        (actorID : ActorID) =
        async {
            let handler = configuration.CommandHandler
            let agent = AutoCancelActor<ActorMessage<'Command, 'Event>>.Start actorID 
                            (fun inbox -> 
                                let shuttingDown reason = async {
                                    hostAgent.Post (Remove (actorID, reason))
                                }
                                let rec running state = async {
                                    try
                                        let! command, channel = inbox.Receive configuration.TimeOutInMills
                                        let! event, newState = handler command state
                                        channel |> Option.iter (fun ch -> event |> ch.Reply)
                                        return! running newState
                                    with
                                    | :? TimeoutException -> return! shuttingDown Timeout
                                    | ex -> return! shuttingDown (Error ex)
                                }
                                async {
                                    let! state = configuration.LoadState()
                                    return! running state
                                })
            return agent :> IActor<ActorMessage<'Command, 'Event>>
        }
