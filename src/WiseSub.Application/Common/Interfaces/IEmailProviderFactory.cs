using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Factory for creating email provider clients based on provider type
/// </summary>
public interface IEmailProviderFactory
{
    /// <summary>
    /// Gets the appropriate email provider client for the specified provider
    /// </summary>
    /// <param name="provider">The email provider type</param>
    /// <returns>Email provider client implementation</returns>
    /// <exception cref="NotSupportedException">Thrown when provider is not supported</exception>
    IEmailProviderClient GetProvider(EmailProvider provider);
}
