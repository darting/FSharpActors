open System
open Metrics
open FSharpActors
open System.Threading


[<EntryPoint>]
let main argv =

    printfn "Hello World from F#!"

    Metric.Config.WithHttpEndpoint("http://localhost:1234/") |> ignore

    // let data = ServiceBus.Data.load () |> List.map (fun x -> x.ToString())
    // ServiceBus.SB.send data |> Async.RunSynchronously

    // ServiceBus.SB.receive () |> Async.RunSynchronously

    TryRabbitMQ.send ()

    let cts = new CancellationTokenSource ()

    Async.Start (async {
        TryRabbitMQ.send ()
    }, cts.Token)

    TryRabbitMQ.receive ()

    printfn "Press any key to exit"
    Console.ReadKey () |> ignore

    cts.Cancel()

    Console.ReadKey () |> ignore

    0
