namespace TaskFlow.Application.Interfaces;

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string hash, string password);
}
