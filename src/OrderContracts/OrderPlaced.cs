namespace OrderContracts;

public sealed record OrderPlaced(Guid Id, string CustomerId, decimal Amount, DateTimeOffset PlacedAt);