namespace JobNet.Infrastructure.Common;

public enum ErrorCode
{
    None = 0,
    NotFound = 1,
    Validation = 2,
    Conflict = 3,
    Forbidden = 4,
    Unauthorized = 5,
    Unknown = 99,
}

public class Result
{
    public bool Success { get; protected init; }
    public string? Error { get; protected init; }
    public ErrorCode Code { get; protected init; }

    public static Result Ok() => new() { Success = true };
    public static Result Fail(string message, ErrorCode code = ErrorCode.Unknown) =>
        new() { Success = false, Error = message, Code = code };

    public static Result<T> Ok<T>(T value) => new() { Success = true, Value = value };
    public static Result<T> Fail<T>(string message, ErrorCode code = ErrorCode.Unknown) =>
        new() { Success = false, Error = message, Code = code };
}

public class Result<T> : Result
{
    public T? Value { get; init; }
}
