namespace TaskFlow.Application.Models;

public sealed record UserInfo(
    Guid Id,
    string Name,
    string Email);
