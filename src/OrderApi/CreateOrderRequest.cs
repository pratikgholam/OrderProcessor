namespace OrderApi;

public sealed record CreateOrderRequest(string CustomerId, decimal Amount);