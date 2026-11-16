namespace OrderContracts;

public record OrderPlaced(Guid Id, string CustomerId, decimal Amount, DateTimeOffset PlacedAt);