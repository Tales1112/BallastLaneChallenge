using Microsoft.AspNetCore.Identity;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Infrastructure;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> hasher = new();
    private readonly object subject = new();
    public string Hash(string password) => hasher.HashPassword(subject, password);
    public bool Verify(string hash, string password) => hasher.VerifyHashedPassword(subject, hash, password) != PasswordVerificationResult.Failed;
}
