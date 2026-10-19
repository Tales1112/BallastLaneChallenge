namespace TaskFlow.Domain.Models;

public sealed record User(
    Guid Id,
    string Name,
    string Email,
    string PasswordHash,
    DateTimeOffset CreatedAt);
