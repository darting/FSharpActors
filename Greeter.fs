namespace FSharpActors.Examples

module Greeter =

    open App.Metrics
    open Microsoft.Extensions.Logging
    open FSharpActors
    open FSharpActors.Types
    open FSharpActors.StatefulActor

    type Message = 
        | Hi
        | Greet of string

    let run () =

        let loggerFactory = new LoggerFactory()

        let metrics = MetricsBuilder().Build()

        let commandHandler command state = async {
            match command with
            | Hi -> return "", state
            | Greet name -> return "", name
        } 

        let configuration = {
            CommandHandler = commandHandler
            TimeOutInMills = -1
            LoadState = fun () -> async.Return ""
        }

        let creator = StatefulActor.creator configuration

        let runtime = {
            HostIdentity = "greeter"
            ActorCreator = creator
            Logger = loggerFactory.CreateLogger()
            Metrics = metrics
        }

        let actorHost = ActorHost.create runtime
        actorHost

        