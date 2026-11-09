namespace FlightBooking.Core.Common;

public enum ResultStatus
{
    Ok = 0,
    Created = 1,
    NoContent = 2,
    BadRequest = 3,
    Unauthorized = 4,
    Forbidden = 5,
    NotFound = 6,
    Conflict = 7
}

public class Result
{
    public ResultStatus Status { get; }
    public bool IsSuccess => Status is ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent;
    public string? ErrorMessage { get; }

    protected Result(ResultStatus status, string? errorMessage = null)
    {
        Status = status;
        ErrorMessage = errorMessage;
    }

    public static Result Ok() => new(ResultStatus.Ok);
    public static Result Created() => new(ResultStatus.Created);
    public static Result NoContent() => new(ResultStatus.NoContent);

    public static Result BadRequest(string message) => new(ResultStatus.BadRequest, message);
    public static Result Unauthorized(string message) => new(ResultStatus.Unauthorized, message);
    public static Result Forbidden(string message) => new(ResultStatus.Forbidden, message);
    public static Result NotFound(string message) => new(ResultStatus.NotFound, message);
    public static Result Conflict(string message) => new(ResultStatus.Conflict, message);
}

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(ResultStatus status, T? value = default, string? errorMessage = null)
        : base(status, errorMessage)
    {
        Value = value;
    }

    public static Result<T> Ok(T value) => new(ResultStatus.Ok, value);
    public static Result<T> Created(T value) => new(ResultStatus.Created, value);
    public static new Result<T> BadRequest(string message) => new(ResultStatus.BadRequest, default, message);
    public static new Result<T> Unauthorized(string message) => new(ResultStatus.Unauthorized, default, message);
    public static new Result<T> Forbidden(string message) => new(ResultStatus.Forbidden, default, message);
    public static new Result<T> NotFound(string message) => new(ResultStatus.NotFound, default, message);
    public static new Result<T> Conflict(string message) => new(ResultStatus.Conflict, default, message);
}