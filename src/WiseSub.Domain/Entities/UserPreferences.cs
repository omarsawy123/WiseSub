namespace WiseSub.Domain.Entities;

public class UserPreferences
{
    public bool EnableRenewalAlerts { get; set; } = true;
    public bool EnablePriceChangeAlerts { get; set; } = true;
    public bool EnableTrialEndingAlerts { get; set; } = true;
    public bool EnableUnusedSubscriptionAlerts { get; set; } = true;
    public bool UseDailyDigest { get; set; } = false;
    public string TimeZone { get; set; } = "UTC";
    public string PreferredCurrency { get; set; } = "USD";
}
