namespace Deveel.Events;

public sealed class MiddlewareRegistration
{
    internal MiddlewareRegistration (Type middlewareType, object[] activationArguments)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);
        MiddlewareType = middlewareType;
        ActivationArguments = activationArguments;
    }
            
    public Type MiddlewareType { get; }

    public object[] ActivationArguments { get; }
}