using TaskFlow.Domain.Models;

namespace TaskFlow.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByEmail(string email, CancellationToken ct);
    Task<User?> FindById(Guid id, CancellationToken ct);
    Task<bool> TryAdd(User user, CancellationToken ct);
}
