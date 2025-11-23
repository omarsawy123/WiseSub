namespace WiseSub.Domain.Common;

/// <summary>
/// Represents the result of an operation with success/failure state and optional error information.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string ErrorMessage { get; }

    protected Result(bool isSuccess, string error)
    {
        if (isSuccess && !string.IsNullOrEmpty(error))
            throw new InvalidOperationException("A successful result cannot have an error.");
        if (!isSuccess && string.IsNullOrEmpty(error))
            throw new InvalidOperationException("A failed result must have an error message.");

        IsSuccess = isSuccess;
        ErrorMessage = error;
    }

    protected Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        ErrorMessage = $"{error.Code}: {error.Message}";
    }

    public static Result Success() => new Result(true, Error.None);
    public static Result Failure(Error error) => new Result(false, error);
    public static Result<T> Success<T>(T value) => new Result<T>(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new Result<T>(default!, false, error);
}

/// <summary>
/// Represents the result of an operation that returns a value.
/// </summary>
public class Result<T> : Result
{
    public T Value { get; }

    protected internal Result(T value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        Value = value;
    }
}
