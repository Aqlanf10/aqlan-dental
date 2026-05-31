namespace AqlanDentalPro.Application.Exceptions;

/// <summary>
/// General-purpose service-layer exception that carries an HTTP-like status code.
/// Used by services to communicate business-rule violations to the controller layer,
/// which can then map to the appropriate IActionResult.
/// </summary>
public class ServiceException : Exception
{
    public int StatusCode { get; }

    public ServiceException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
