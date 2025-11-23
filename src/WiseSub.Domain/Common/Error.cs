namespace WiseSub.Domain.Common
{
    public sealed record Error(string Code, string Message)
    {
        public static readonly Error None = new Error(string.Empty, string.Empty);

        public override string ToString() => $"{Code}: {Message}";
    }

    public static class AuthenticationErrors
    {
        public static readonly Error InvalidCredentials = new Error("Auth.InvalidCredentials", "The provided credentials are invalid.");
        public static readonly Error InvalidToken = new Error("Auth.InvalidToken", "The provided token is invalid or expired.");
        public static readonly Error Unauthorized = new Error("Auth.Unauthorized", "You are not authorized to perform this action.");
    }

    public static class UserErrors
    {
        public static readonly Error NotFound = new Error("User.NotFound", "The user was not found.");
        public static readonly Error AlreadyExists = new Error("User.AlreadyExists", "A user with this email already exists.");
        public static readonly Error InvalidEmail = new Error("User.InvalidEmail", "The provided email address is invalid.");
        public static readonly Error TierLimitExceeded = new Error("User.TierLimitExceeded", "The operation exceeds your subscription tier limit.");
    }

    public static class SubscriptionErrors
    {
        public static readonly Error NotFound = new Error("Subscription.NotFound", "The subscription was not found.");
        public static readonly Error AlreadyExists = new Error("Subscription.AlreadyExists", "A subscription with this service already exists.");
        public static readonly Error InvalidPrice = new Error("Subscription.InvalidPrice", "The subscription price must be greater than zero.");
        public static readonly Error InvalidBillingCycle = new Error("Subscription.InvalidBillingCycle", "The billing cycle is invalid.");
        public static readonly Error AlreadyCancelled = new Error("Subscription.AlreadyCancelled", "The subscription is already cancelled.");
        public static readonly Error UpdateFailed = new Error("Subscription.UpdateFailed", "Failed to update the subscription.");
    }

    public static class EmailAccountErrors
    {
        public static readonly Error NotFound = new Error("EmailAccount.NotFound", "The email account was not found.");
        public static readonly Error AlreadyConnected = new Error("EmailAccount.AlreadyConnected", "This email account is already connected.");
        public static readonly Error TokenExpired = new Error("EmailAccount.TokenExpired", "The email account token has expired.");
        public static readonly Error TokenRefreshFailed = new Error("EmailAccount.TokenRefreshFailed", "Failed to refresh the email account token.");
        public static readonly Error InvalidProvider = new Error("EmailAccount.InvalidProvider", "The email provider is invalid.");
        public static readonly Error ConnectionFailed = new Error("EmailAccount.ConnectionFailed", "Failed to connect to the email account.");
        public static readonly Error ScanFailed = new Error("EmailAccount.ScanFailed", "Failed to scan the email account.");
    }

    public static class AlertErrors
    {
        public static readonly Error NotFound = new Error("Alert.NotFound", "The alert was not found.");
        public static readonly Error AlreadySent = new Error("Alert.AlreadySent", "The alert has already been sent.");
        public static readonly Error InvalidSchedule = new Error("Alert.InvalidSchedule", "The alert schedule is invalid.");
        public static readonly Error SendFailed = new Error("Alert.SendFailed", "Failed to send the alert.");
        public static readonly Error MaxRetriesExceeded = new Error("Alert.MaxRetriesExceeded", "Maximum retry attempts exceeded for this alert.");
    }

    public static class VendorErrors
    {
        public static readonly Error NotFound = new Error("Vendor.NotFound", "The vendor was not found.");
        public static readonly Error AlreadyExists = new Error("Vendor.AlreadyExists", "A vendor with this name already exists.");
        public static readonly Error InvalidName = new Error("Vendor.InvalidName", "The vendor name is invalid.");
    }

    public static class EmailMetadataErrors
    {
        public static readonly Error NotFound = new Error("EmailMetadata.NotFound", "The email metadata was not found.");
        public static readonly Error AlreadyProcessed = new Error("EmailMetadata.AlreadyProcessed", "This email has already been processed.");
        public static readonly Error ProcessingFailed = new Error("EmailMetadata.ProcessingFailed", "Failed to process the email.");
        public static readonly Error InvalidFormat = new Error("EmailMetadata.InvalidFormat", "The email format is invalid.");
        public static readonly Error QueueFailed = new Error("EmailMetadata.QueueFailed", "Failed to queue emails for processing.");
    }

    public static class ValidationErrors
    {
        public static readonly Error Required = new Error("Validation.Required", "The field is required.");
        public static readonly Error InvalidFormat = new Error("Validation.InvalidFormat", "The field format is invalid.");
        public static readonly Error OutOfRange = new Error("Validation.OutOfRange", "The value is out of the acceptable range.");
        public static readonly Error TooLong = new Error("Validation.TooLong", "The value exceeds the maximum length.");
        public static readonly Error TooShort = new Error("Validation.TooShort", "The value is below the minimum length.");
    }

    public static class GeneralErrors
    {
        public static readonly Error UnexpectedError = new Error("General.UnexpectedError", "An unexpected error occurred.");
        public static readonly Error DatabaseError = new Error("General.DatabaseError", "A database error occurred.");
        public static readonly Error ConcurrencyError = new Error("General.ConcurrencyError", "The record was modified by another user.");
        public static readonly Error NotImplemented = new Error("General.NotImplemented", "This feature is not yet implemented.");
    }
}
