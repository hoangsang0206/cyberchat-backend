using System;
using System.Collections.Generic;

namespace CyberChat.Application.Common.Models;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }
    public IDictionary<string, string[]>? Errors { get; }

    protected Result(bool isSuccess, string error, IDictionary<string, string[]>? errors = null)
    {
        if (isSuccess && error != string.Empty)
        {
            throw new InvalidOperationException("A successful result cannot have an error message.");
        }

        if (!isSuccess && error == string.Empty)
        {
            throw new InvalidOperationException("A failed result must have an error message.");
        }

        IsSuccess = isSuccess;
        Error = error;
        Errors = errors;
    }

    public static Result Success() => new(true, string.Empty);
    public static Result Failure(string error) => new(false, error);
    public static Result Failure(string error, IDictionary<string, string[]> errors) => new(false, error, errors);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, string.Empty);
    public static Result<TValue> Failure<TValue>(string error) => new(default, false, error);
    public static Result<TValue> Failure<TValue>(string error, IDictionary<string, string[]> errors) => new(default, false, error, errors);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    public TValue? Value => IsSuccess
        ? _value
        : default;

    protected internal Result(TValue? value, bool isSuccess, string error, IDictionary<string, string[]>? errors = null)
        : base(isSuccess, error, errors)
    {
        _value = value;
    }

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
