namespace Synapse.Foundation.Abstractions;

public sealed class Result<T>
{
    private readonly T? _value;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value => IsSuccess ? _value : throw new InvalidOperationException("Cannot access Value on a failed Result.");
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    public static Result<T> Success(T value) =>
        new(true, value, null);

    public static Result<T> Failure(string error) =>
        new(false, default, error);

    public static implicit operator Result<T>(T value) =>
        Success(value);
}
