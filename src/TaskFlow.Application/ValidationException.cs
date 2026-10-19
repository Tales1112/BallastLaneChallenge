namespace TaskFlow.Application;

public sealed class ValidationException(string message) : Exception(message);
