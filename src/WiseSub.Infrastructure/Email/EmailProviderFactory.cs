using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Enums;

namespace WiseSub.Infrastructure.Email;

/// <summary>
/// Factory for selecting the appropriate email provider client
/// </summary>
public class EmailProviderFactory : IEmailProviderFactory
{
    private readonly IEnumerable<IEmailProviderClient> _providers;

    public EmailProviderFactory(IEnumerable<IEmailProviderClient> providers)
    {
        _providers = providers;
    }

    public IEmailProviderClient GetProvider(EmailProvider provider)
    {
        var providerClient = _providers.FirstOrDefault(p => p.SupportsProvider(provider));
        
        if (providerClient == null)
        {
            throw new NotSupportedException(
                $"Email provider '{provider}' is not supported. " +
                $"Available providers: {string.Join(", ", _providers.Select(p => p.GetType().Name))}");
        }

        return providerClient;
    }
}
