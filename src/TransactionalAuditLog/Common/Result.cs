namespace TransactionalAuditLog.Common;

public sealed record Result<T>
{
    public bool IsSuccess => Error is null;
    public T? Value { get; init; }
    public string? Error { get; init; }
    public ResultErrorType ErrorType { get; init; }

    public static Result<T> Success(T value) =>
        new() { Value = value };

    public static Result<T> Failure(string error, ResultErrorType errorType) =>
        new() { Error = error, ErrorType = errorType };
}

public enum ResultErrorType
{
    Validation,
    Conflict,
    NotFound
}
