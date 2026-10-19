using TaskFlow.Application.Interfaces;
using System.Net.Mail;
using TaskFlow.Application.Models;
using TaskFlow.Domain.Models;

namespace TaskFlow.Application;

public sealed class AuthService(IUserRepository users, IPasswordService passwords)
{
    // A dummy hash ensures an unknown email still performs password verification.
    private readonly string dummyHash = passwords.Hash("not-a-real-user-password");

    public async Task<UserInfo> Register(RegisterInput input, CancellationToken ct)
    {
        var name = input.Name?.Trim() ?? "";

        var email = NormalizeEmail(input.Email);
        var password = input.Password ?? "";

        if (name.Length is < 2 or > 80) throw new ValidationException("Name must contain 2 to 80 characters.");

        if (password.Length is < 10 or > 128 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ValidationException("Password must contain 10 to 128 characters, including a letter and a number.");

        var user = new User(Guid.NewGuid(), name, email, passwords.Hash(password), DateTimeOffset.UtcNow);

        if (!await users.TryAdd(user, ct)) throw new ConflictException("An account with this email already exists.");
        return ToInfo(user);
    }

    public async Task<UserInfo?> Login(LoginInput input, CancellationToken ct)
    {
        var email = NormalizeEmail(input.Email);
        if (input.Password is null || input.Password.Length > 128) return null;
        var user = await users.FindByEmail(email, ct);
        var verified = passwords.Verify(user?.PasswordHash ?? dummyHash, input.Password);
        return user is not null && verified ? ToInfo(user) : null;
    }

    public async Task<UserInfo?> GetUser(Guid id, CancellationToken ct)
    {
        var user = await users.FindById(id, ct);
        return user is null ? null : ToInfo(user);
    }

    private static UserInfo ToInfo(User user) => new(user.Id, user.Name, user.Email);

    private static string NormalizeEmail(string? value)
    {
        var email = value?.Trim().ToLowerInvariant() ?? "";
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email || !email.Contains('@'))
            throw new ValidationException("Enter a valid email address.");

        return email;
    }
}
