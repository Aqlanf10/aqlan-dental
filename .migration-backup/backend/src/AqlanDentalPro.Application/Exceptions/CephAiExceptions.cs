namespace AqlanDentalPro.Application.Exceptions;

/// <summary>
/// Thrown when the AI draft assistant cannot run at all — feature flag disabled
/// or API key not configured. Message is an honest Arabic explanation; the
/// controller maps this to 403.
/// </summary>
public class CephAiUnavailableException : Exception
{
    public CephAiUnavailableException(string message) : base(message) { }
}

/// <summary>
/// Thrown when the call to the upstream AI API failed (non-success HTTP status,
/// network error, timeout, or unparseable response). The controller maps this
/// to an Arabic 502 — exception details never reach the client.
/// </summary>
public class CephAiUpstreamException : Exception
{
    public CephAiUpstreamException(string message) : base(message) { }
    public CephAiUpstreamException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when the configurable monthly AI usage limit (Settings key
/// "ai.monthly_limit") has been reached. Message is the honest Arabic
/// explanation; the controller maps this to 429.
/// </summary>
public class CephAiLimitReachedException : Exception
{
    public CephAiLimitReachedException(string message) : base(message) { }
}
