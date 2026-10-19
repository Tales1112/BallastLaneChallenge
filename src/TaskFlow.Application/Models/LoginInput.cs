namespace TaskFlow.Application.Models;

public sealed record LoginInput(
    string? Email,
    string? Password);
