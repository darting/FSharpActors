open System
open FSharpActors
open FSharpActors.ActorHost
open System.Threading
open App.Metrics




[<EntryPoint>]
let main argv =

    printfn "Hello World from F#!"

   
    let globalMetrics = MetricsBuilder()
                          .Report.ToConsole()
                          .Build()


   

    Console.ReadKey () |> ignore

    0
