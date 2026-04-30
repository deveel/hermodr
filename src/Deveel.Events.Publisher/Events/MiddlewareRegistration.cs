namespace Deveel.Events;

public sealed class MiddlewareRegistration
{
    internal MiddlewareRegistration(Type middlewareType, object[] activationArguments, Func<EventContext, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);
        MiddlewareType = middlewareType;
        ActivationArguments = activationArguments;
        Predicate = predicate;
    }
            
    public Type MiddlewareType { get; }

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