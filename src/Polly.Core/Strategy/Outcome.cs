using System.Diagnostics.CodeAnalysis;

#pragma warning disable CA1815 // Override equals and operator equals on value types

namespace Polly.Strategy;

/// <summary>
/// Represents the outcome of an operation that returns a result of type TResult or an exception.
/// </summary>
/// <typeparam name="TResult">The type of the result produced by the operation.</typeparam>
public readonly struct Outcome<TResult>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Outcome{TResult}"/> struct with the specified exception.
    /// </summary>
    /// <param name="exception">The exception that occurred during the operation.</param>
    public Outcome(Exception exception)
    : this() => Exception = Guard.NotNull(exception);

    /// <summary>
    /// Initializes a new instance of the <see cref="Outcome{TResult}"/> struct with the specified result.
    /// </summary>
    /// <param name="result">The result produced by the operation.</param>
    public Outcome(TResult result)
    : this() => Result = result;

    /// <summary>
    /// Gets the exception that occurred during the operation, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the result produced by the operation, if any.
    /// </summary>
    public TResult? Result { get; }

    /// <summary>
    /// Gets a value indicating whether the operation produced a result.
    /// </summary>
    /// <remarks>
    /// If the operation returned a void result the value will be <c>true</c>.
    /// You can use <see cref="IsVoidResult"/> to determine if the result is a void result.
    /// </remarks>
    public bool HasResult => Exception == null;

    /// <summary>
    /// Gets a value indicating whether the operation produced a void result.
    /// </summary>
    public bool IsVoidResult => Result is VoidResult;

    /// <summary>
    /// Tries to get a result if available.
    /// </summary>
    /// <param name="result">The result instance.</param>
    /// <returns>True if result is available, false otherwise.</returns>
    public bool TryGetResult([NotNullWhen(true)] out TResult? result)
    {
        if (HasResult && !IsVoidResult)
        {
            result = Result!;
            return true;
        }

        result = default;
        return false;
    }
}
