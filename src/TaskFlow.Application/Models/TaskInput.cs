namespace TaskFlow.Application.Models;

public sealed record TaskInput(
    string? Title,
    string? Description,
    string? Status,
    string? DueDate);
