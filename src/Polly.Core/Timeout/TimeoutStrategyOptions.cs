using System.ComponentModel.DataAnnotations;

namespace Polly.Timeout;

/// <summary>
/// Represents the options for the timeout strategy.
/// </summary>
public class TimeoutStrategyOptions : ResilienceStrategyOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutStrategyOptions"/> class.
    /// </summary>
    public TimeoutStrategyOptions() => Name = TimeoutConstants.DefaultName;

#pragma warning disable IL2026 // Addressed with DynamicDependency on ValidationHelper.Validate method
    /// <summary>
    /// Gets or sets the default timeout.
    /// </summary>
    /// <value>
    /// This value must be greater than 1 second and less than 24 hours. The default value is 30 seconds.
    /// </value>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
#pragma warning restore IL2026

    /// <summary>
    /// Gets or sets a timeout generator that generates the timeout for a given execution.
    /// </summary>
    /// <remarks>
    /// If generator returns a <see cref="TimeSpan"/> value that is less or equal to <see cref="TimeSpan.Zero"/>
    /// its value is ignored and <see cref="Timeout"/> is used instead. When generator is <see langword="null"/> the <see cref="Timeout"/> is used.
    /// <para>
    /// Return <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to disable the timeout for the given execution.
    /// </para>
    /// </remarks>
    /// <value>
    /// The default value is <see langword="null"/>.
    /// </value>
    public Func<TimeoutGeneratorArguments, ValueTask<TimeSpan>>? TimeoutGenerator { get; set; }

    /// <summary>
    /// Gets or sets the timeout delegate that raised when timeout occurs.
    /// </summary>
    /// <value>
    /// The default value is <see langword="null"/>.
    /// </value>
    public Func<OnTimeoutArguments, ValueTask>? OnTimeout { get; set; }
}
