namespace BatatasFritas.Infrastructure.Options;

public class MercadoPagoOptions
{
    public string AccessToken { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string NotificationUrl { get; set; } = string.Empty;
}
