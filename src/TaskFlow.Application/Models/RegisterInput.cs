namespace TaskFlow.Application.Models;

public sealed record RegisterInput(
    string? Name,
    string? Email,
    string? Password);
