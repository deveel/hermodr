namespace Hermodr;

/// <summary>
/// Holds the registration metadata for a single <see cref="IEventMiddleware"/> entry
/// in the <see cref="EventPublisherBuilder"/> pipeline.
/// </summary>
public sealed class MiddlewareRegistration
{
    internal MiddlewareRegistration(Type middlewareType, object[] activationArguments, Func<EventContext, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);
        MiddlewareType = middlewareType;
        ActivationArguments = activationArguments;
        Predicate = predicate;
    }

    /// <summary>
    /// Gets the <see cref="Type"/> of the middleware class to activate.
    /// </summary>
    public Type MiddlewareType { get; }

    /// <summary>
    /// Gets the additional constructor arguments (beyond those resolved from DI)
    /// passed to <see cref="ActivatorUtilities"/> when the middleware is instantiated.
    /// </summary>
    public object[] ActivationArguments { get; }

    /// <summary>
    /// An optional predicate that is evaluated against the <see cref="EventContext"/>
    /// before the middleware is invoked. When <c>null</c> the middleware is always executed.
    /// </summary>
    public Func<EventContext, bool>? Predicate { get; }

    /// <summary>
    /// Returns <c>true</c> when this registration has a conditional predicate attached.
    /// </summary>
    public bool IsConditional => Predicate is not null;
}