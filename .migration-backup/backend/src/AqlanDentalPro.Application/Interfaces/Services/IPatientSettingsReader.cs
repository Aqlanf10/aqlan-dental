namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IPatientSettingsReader
{
    Task<string> GetNumberPrefixAsync(CancellationToken cancellationToken = default);
}
