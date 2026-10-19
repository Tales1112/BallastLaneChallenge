using TaskFlow.Application.Interfaces;
using TaskFlow.Application;
using TaskFlow.Domain.Models;
using TaskFlow.Infrastructure;
using Xunit;

namespace TaskFlow.Tests;

public sealed class AuthServiceTests
{
    private readonly MemoryUsers users = new();
    private readonly PasswordService passwords = new();

    [Fact]
    public async Task Registration_normalizes_email_and_stores_only_a_salted_hash()
    {
        var service = new AuthService(users, passwords);
        var info = await service.Register(new(" Alex Morgan ", " Alex@Example.com ", "GoodPassword123"), default);
        Assert.Equal("Alex Morgan", info.Name);
        Assert.Equal("alex@example.com", info.Email);
        Assert.NotEqual("GoodPassword123", users.Item!.PasswordHash);
        Assert.True(passwords.Verify(users.Item.PasswordHash, "GoodPassword123"));
        Assert.NotEqual(passwords.Hash("GoodPassword123"), passwords.Hash("GoodPassword123"));
    }

    [Fact]
    public async Task Login_returns_no_user_for_wrong_password_or_unknown_email()
    {
        var service = new AuthService(users, passwords);
        await service.Register(new("Alex", "alex@example.com", "GoodPassword123"), default);
        Assert.Null(await service.Login(new("alex@example.com", "wrong"), default));
        Assert.Null(await service.Login(new("unknown@example.com", "GoodPassword123"), default));
        Assert.NotNull(await service.Login(new("ALEX@example.com", "GoodPassword123"), default));
    }

    [Theory]
    [InlineData("A", "alex@example.com", "GoodPassword123")]
    [InlineData("Alex", "not-an-email", "GoodPassword123")]
    [InlineData("Alex", "alex@example.com", "short1")]
    [InlineData("Alex", "alex@example.com", "onlyletterslong")]
    [InlineData("Alex", "alex@example.com", "12345678901")]
    public async Task Invalid_registration_never_writes_a_user(string name, string email, string password)
    {
        await Assert.ThrowsAsync<ValidationException>(() => new AuthService(users, passwords).Register(new(name, email, password), default));
        Assert.Null(users.Item);
    }

    [Fact]
    public async Task Duplicate_registration_is_a_conflict()
    {
        var service = new AuthService(users, passwords);
        await service.Register(new("Alex", "alex@example.com", "GoodPassword123"), default);
        await Assert.ThrowsAsync<ConflictException>(() => service.Register(new("Alex", "ALEX@example.com", "GoodPassword123"), default));
    }

    private sealed class MemoryUsers : IUserRepository
    {
        public User? Item { get; private set; }
        public Task<User?> FindByEmail(string email, CancellationToken ct) => Task.FromResult(Item?.Email == email ? Item : null);
        public Task<User?> FindById(Guid id, CancellationToken ct) => Task.FromResult(Item?.Id == id ? Item : null);
        public Task<bool> TryAdd(User user, CancellationToken ct) { if (Item is not null) return Task.FromResult(false); Item = user; return Task.FromResult(true); }
    }
}
