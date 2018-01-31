namespace FSharpActors


module Metrics = 

    open System
    open App.Metrics
    open App.Metrics.Counter
    open App.Metrics.Meter
    open App.Metrics.Apdex

    let private withContext (options : MetricValueOptionsBase) =
        options.Context <- "Tigers"
        options
        
    let private withName name (options : MetricValueOptionsBase) =
        options.Name <- name
        options

    let private withUnit unit (options : MetricValueOptionsBase) =
        options.MeasurementUnit <- unit
        options

    module Counter =
        let create () = CounterOptions () |> withContext :?> CounterOptions
        let withName name (options : CounterOptions) = withName name options :?> CounterOptions
        let withUnit unit (options : CounterOptions) = withUnit unit options :?> CounterOptions

    module Meter = 
        let create () = MeterOptions () |> withContext :?> MeterOptions
        let withName name (options : MeterOptions) = withName name options :?> MeterOptions
        let withUnit unit (options : MeterOptions) = withUnit unit options :?> MeterOptions
            
    module Apdex = 
        let create () = ApdexOptions () |> withContext :?> ApdexOptions
        let withName name (options : ApdexOptions) = withName name options :?> ApdexOptions
        let withUnit unit (options : ApdexOptions) = withUnit unit options :?> ApdexOptions

        let trace (metrics : IMetricsRoot) (options : ApdexOptions) (job : Async<'T>) =
            async {
                use _ = metrics.Measure.Apdex.Track(options)
                return! job
            }

