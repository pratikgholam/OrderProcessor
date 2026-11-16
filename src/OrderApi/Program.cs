    using RabbitMQ.Client;
    using System.Text.Json;
    using OrderApi;
    using OrderContracts;
    
    var builder = WebApplication.CreateBuilder(args);

    var factory = new RabbitMQ.Client.ConnectionFactory() { HostName = "localhost", UserName = "user", Password = "password" };
    await using var connection = await factory.CreateConnectionAsync();
    await using (var startupChannel = await connection.CreateChannelAsync())
    {
        await startupChannel.QueueDeclareAsync(queue: "order_placed", durable: true,
            exclusive: false, autoDelete: false, arguments: null);
    }

    var app = builder.Build();

    app.MapGet("/health", () => Results.Ok("Healthy"));

    app.MapPost("/orders", async (CreateOrderRequest request) => 
    {

        if (string.IsNullOrWhiteSpace(request.CustomerId))
            return Results.BadRequest("CustomerId is required");
        if (request.Amount <= 0)
            return Results.BadRequest("Amount must be greater than zero");

        var order = new OrderPlaced(
            Guid.NewGuid(),
            request.CustomerId,
            request.Amount,
            DateTimeOffset.UtcNow);

        var body = JsonSerializer.SerializeToUtf8Bytes(order);   // default options, both sides
        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = order.Id.ToString(),
        };

        await using var channel = await connection.CreateChannelAsync();
        

        await channel.BasicPublishAsync(exchange: "", routingKey: "order_placed",
        mandatory: false, basicProperties: props, body: body);

        return Results.Accepted(value: new { orderId = order.Id });
    });

    app.Run();
