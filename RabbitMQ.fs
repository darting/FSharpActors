module FSharpActors.TryRabbitMQ

open System.Text
open RabbitMQ.Client
open System.Threading
open RabbitMQ.Client.Events
open Microsoft.Azure.Amqp.Framing

let send () =
    let factory = ConnectionFactory(HostName="localhost")
    use connection = factory.CreateConnection()
    use channel = connection.CreateModel()
    channel.QueueDeclare(queue = "hello",
                         durable = false,
                         exclusive = false,
                         autoDelete = false) |> ignore
    let message = "hello, world"
    let body = Encoding.UTF8.GetBytes message
    
    channel.BasicPublish(exchange = "",
                         routingKey = "hello",
                         basicProperties = null,
                         body = body) |> ignore
    printfn "message sent: %s" message                         


let receive () =
    let factory = ConnectionFactory(HostName = "localhost")
    use connection = factory.CreateConnection()
    use channel = connection.CreateModel()
    channel.QueueDeclare(queue = "hello",
                         durable = false,
                         exclusive = false,
                         autoDelete = false) |> ignore
    let consumer = EventingBasicConsumer (channel)
    consumer.Received.Add (fun x -> 
        let body = x.Body
        let message = Encoding.UTF8.GetString body
        printfn "Received: %s" message
    )
    channel.BasicConsume(queue = "hello", autoAck = true, consumer = consumer) |> ignore
    

