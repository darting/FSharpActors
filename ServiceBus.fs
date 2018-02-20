module FSharpActors.ServiceBus

open System.Threading
open System.Text
open System.Threading.Tasks
open Microsoft.Azure.ServiceBus


module Data = 
    let load () = [ 1 .. 10 ]

module SB =
    
    let connectionString = ""
    let queueName = ""

    let sendMessage (client : QueueClient) (msg : string) =
        async {
            let message = Message (Encoding.UTF8.GetBytes msg)
            do! client.SendAsync (message) |> Async.AwaitTask
        }

    let send (data : string list) =
        async {
            let client = QueueClient(connectionString, queueName)
            let sender = sendMessage client
            do! data |> List.map sender |> Async.Parallel |> Async.Ignore
            do! client.CloseAsync() |> Async.AwaitTask
        }

    let receive () =
        let errHandler (x : ExceptionReceivedEventArgs) = 
            printfn "%A" x
            Task.CompletedTask

        let processMessage (client : QueueClient) (message : Message) (token : CancellationToken) =
            printfn "Received message: %s" (Encoding.UTF8.GetString(message.Body))
            client.CompleteAsync (message.SystemProperties.LockToken)

        async {
            let client = QueueClient(connectionString, queueName)
            
            let options = MessageHandlerOptions (System.Func<_, Task> errHandler)
            options.AutoComplete <- false

            let processer = processMessage client

            client.RegisterMessageHandler (processer, options)

            do! Async.Sleep 3000

            do! client.CloseAsync() |> Async.AwaitTask
        }
