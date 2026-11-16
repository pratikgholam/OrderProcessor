    using RabbitMQ.Client;
    
    var builder = WebApplication.CreateBuilder(args);

    var factory = new RabbitMQ.Client.ConnectionFactory() { HostName = "localhost", UserName = "user", Password = "password" };
    await using var connection = await factory.CreateConnectionAsync();
    await using var channel = await connection.CreateChannelAsync();

    var app = builder.Build();

    app.MapGet("/", () => "Hello World!");

    app.MapGet("/health", () => Results.Ok("Healthy"));

    app.MapPost("/order", async () => 
    {
        var props = new BasicProperties { Persistent = true };
        await channel.QueueDeclareAsync(queue: "order_placed",
                            durable: true,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);

        string message = "Order Placed!";
        var body = System.Text.Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(exchange: "",
                            routingKey: "order_placed",
                            mandatory: false,
                            basicProperties: props,
                            body: body);

        return Results.Accepted(value: "Order placed and message sent to RabbitMQ.");
    });

    app.Run();
