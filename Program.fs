open System
open Metrics


[<EntryPoint>]
let main argv =

    printfn "Hello World from F#!"

    Metric.Config.WithHttpEndpoint("http://localhost:1234/") |> ignore


    Console.ReadKey () |> ignore

    0
