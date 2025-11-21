# Design Document: Subscription Management System

## Overview

The Subscription Management System (SMS) is a SaaS platform that automatically discovers, tracks, and manages recurring subscriptions by analyzing email communications. The system addresses the common problem of forgotten subscriptions, hidden renewals, and unexpected charges by providing proactive alerts and management tools.

The platform is designed with a phased MVP approach, starting with core subscription tracking for Gmail users (Phase 1 beta), then expanding to alerts (Phase 2), monetization (Phase 3), and advanced features (Phase 4). The architecture prioritizes simplicity and cost-effectiveness for the MVP while maintaining clear paths to scale.

**Key Design Principles:**
- Email-first approach: Subscriptions are discovered from email receipts and notifications
- AI-powered extraction: LLM-based parsing handles diverse email formats and languages
- Privacy-focused: Only metadata is stored, never full email content
- Incremental value: Each phase adds measurable user value
- Cost-optimized: Free/low-cost infrastructure for MVP validation

## Architecture

### High-Level Architecture

The system follows a clean architecture pattern with clear separation between layers:

```
┌─────────────────────────────────────────────────────────────┐
│                        Frontend (Next.js)                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │  Dashboard   │  │   Settings   │  │  Onboarding  │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ HTTPS/REST API
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Backend (ASP.NET Core)                     │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              API Controllers Layer                    │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │           Application Services Layer                  │   │
│  │  • SubscriptionService  • EmailService               │   │
│  │  • AlertService         • UserService                │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Domain Layer                             │   │
│  │  • Subscription  • EmailAccount  • Alert             │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         Infrastructure Layer                          │   │
│  │  • Repositories  • Email Clients  • AI Client        │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌──────────────┐    ┌──────────────┐      ┌──────────────┐
│   SQLite     │    │  Background  │      │  External    │
│   Database   │    │  Jobs        │      │  Services    │
│              │    │  (Hangfire)  │      │  • Gmail API │
│              │    │              │      │  • OpenAI    │
└──────────────┘    └──────────────┘      │  • SendGrid  │
                                           └──────────────┘
```

### Architecture Decisions

**Decision 1: Monolithic Backend with Background Jobs**
- Rationale: For MVP with <1000 users, a monolithic ASP.NET Core application with Hangfire for background processing provides simplicity and low operational overhead
- Trade-off: Limits horizontal scaling, but acceptable for MVP phase
- Future: Can migrate to microservices (Email Ingestion Service, AI Extraction Service, Alert Service) when scaling beyond 10K users

**Decision 2: SQLite for MVP Database**
- Rationale: Zero-cost, zero-configuration database perfect for MVP validation with 100 users
- Trade-off: Single-file database limits concurrent writes and scalability
- Migration Path: Clear upgrade path to Azure SQL Database when user base grows
- Data partitioning by user_id prepares for future sharding

**Decision 3: Email-First Discovery (No Bank Integration)**
- Rationale: Email receipts are universal, require no financial institution partnerships, and provide rich subscription metadata
- Trade-off: May miss subscriptions without email notifications
- Future: Can add Plaid integration in Phase 4+ for bank transaction matching

**Decision 4: API-Based LLM (OpenAI GPT-4o-mini)**
- Rationale: Leverages state-of-art language understanding without ML infrastructure
- Cost: ~$0.10-0.20 per 1000 emails processed (affordable for MVP)
- Trade-off: External dependency and per-request cost
- Alternative: Can switch to Azure OpenAI for enterprise compliance

**Decision 5: Next.js Frontend with Vercel Hosting**
- Rationale: Modern React framework with excellent DX, free hosting, and TypeScript support
- Trade-off: Adds JavaScript ecosystem complexity
- Benefit: Rapid UI development with shadcn/ui components and Tailwind CSS

## Components and Interfaces

### Core Components

#### 1. Email Ingestion Service (EIS)

**Responsibility:** Connect to email providers, retrieve subscription-related emails, and queue them for processing

**Key Classes:**
- `EmailAccountManager`: Manages OAuth connections and token refresh
- `GmailClient`: Implements Gmail API integration
- `EmailScanner`: Retrieves and filters emails based on heuristics
- `EmailQueueService`: Queues emails for AI processing

**Interfaces:**
```csharp
public interface IEmailProvider
{
    Task<AuthenticationResult> AuthenticateAsync(string authCode);
    Task<IEnumerable<EmailMessage>> GetEmailsAsync(DateTime since, EmailFilter filter);
    Task RevokeAccessAsync(string accountId);
}

public interface IEmailQueueService
{
    Task QueueEmailForProcessingAsync(EmailMessage email, Priority priority);
    Task<ProcessingStatus> GetQueueStatusAsync();
}
```

**Email Filtering Strategy:**
- Sender domain matching (e.g., *@netflix.com, *@spotify.com)
- Subject line keywords ("subscription", "renewal", "invoice", "receipt")
- Folder-based filtering (Purchases, Receipts folders)
- Initial scan: Past 12 months
- Ongoing: Check every 15 minutes for new emails

#### 2. AI Extraction Engine (AEE)

**Responsibility:** Parse email content using LLM and extract structured subscription metadata

**Key Classes:**
- `OpenAIClient`: Wrapper for OpenAI API calls
- `EmailClassifier`: Determines if email is subscription-related
- `SubscriptionExtractor`: Extracts structured data from email
- `ExtractionValidator`: Validates confidence scores and completeness

**Interfaces:**
```csharp
public interface IAIExtractionService
{
    Task<ClassificationResult> ClassifyEmailAsync(EmailMessage email);
    Task<ExtractionResult> ExtractSubscriptionDataAsync(EmailMessage email);
}

public class ExtractionResult
{
    public string ServiceName { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public DateTime? NextRenewalDate { get; set; }
    public string Category { get; set; }
    public string CancellationLink { get; set; }
    public double ConfidenceScore { get; set; }
    public bool RequiresUserReview { get; set; }
}
```

**Extraction Prompt Strategy:**
- Structured JSON output format for consistency
- Few-shot examples for common subscription types
- Confidence scoring for each extracted field
- Fallback to "requires review" for low-confidence extractions
- Multi-language support (English, German, French, Spanish)

**Confidence Thresholds:**
- High confidence (>0.85): Auto-create subscription
- Medium confidence (0.60-0.85): Create with "review" flag
- Low confidence (<0.60): Skip and log for manual review

#### 3. Subscription Management Service

**Responsibility:** CRUD operations for subscriptions, deduplication, and status management

**Key Classes:**
- `SubscriptionService`: Business logic for subscription operations
- `SubscriptionRepository`: Data access layer
- `DeduplicationEngine`: Prevents duplicate subscriptions
- `SubscriptionNormalizer`: Normalizes billing cycles and currencies

**Interfaces:**
```csharp
public interface ISubscriptionService
{
    Task<Subscription> CreateOrUpdateAsync(SubscriptionData data);
    Task<IEnumerable<Subscription>> GetUserSubscriptionsAsync(string userId);
    Task<Subscription> UpdateAsync(string subscriptionId, SubscriptionUpdate update);
    Task ArchiveAsync(string subscriptionId);
    Task<MonthlySpendingSummary> GetSpendingSummaryAsync(string userId);
}
```

**Deduplication Logic:**
- Match by service name + email account (fuzzy matching with 85% similarity)
- If duplicate found: Update with newer information
- Track update history for audit trail

**Status Management:**
- Active: Currently subscribed
- Cancelled: User cancelled, awaiting confirmation
- Archived: Confirmed cancelled or expired
- PendingReview: Low confidence extraction

#### 4. Alert Service

**Responsibility:** Generate and send proactive alerts for renewals, price changes, and unused subscriptions

**Key Classes:**
- `AlertScheduler`: Background job that checks for alert conditions
- `AlertGenerator`: Creates alert messages
- `EmailNotificationService`: Sends alerts via SendGrid
- `AlertPreferenceManager`: Manages user alert settings

**Interfaces:**
```csharp
public interface IAlertService
{
    Task CheckAndSendRenewalAlertsAsync();
    Task SendPriceChangeAlertAsync(Subscription subscription, decimal oldPrice, decimal newPrice);
    Task SendTrialEndingAlertAsync(Subscription subscription);
    Task SendUnusedSubscriptionAlertAsync(Subscription subscription);
}

public interface IAlertPreferenceService
{
    Task<AlertPreferences> GetUserPreferencesAsync(string userId);
    Task UpdatePreferencesAsync(string userId, AlertPreferences preferences);
}
```

**Alert Scheduling:**
- Renewal alerts: 7 days before, 3 days before
- Price change: Immediate upon detection
- Trial ending: 3 days before conversion
- Unused subscription: After 6 months of no activity emails
- Batch mode: Daily digest option for users who prefer consolidated alerts

#### 5. Dashboard Service

**Responsibility:** Aggregate and present subscription data with insights

**Key Classes:**
- `DashboardService`: Aggregates data for UI
- `SpendingAnalyzer`: Calculates spending patterns
- `TimelineGenerator`: Creates renewal timeline
- `CategoryAggregator`: Groups subscriptions by category

**Interfaces:**
```csharp
public interface IDashboardService
{
    Task<DashboardData> GetDashboardDataAsync(string userId);
    Task<SpendingInsights> GetSpendingInsightsAsync(string userId);
    Task<IEnumerable<RenewalEvent>> GetRenewalTimelineAsync(string userId, int months);
}

public class DashboardData
{
    public IEnumerable<Subscription> ActiveSubscriptions { get; set; }
    public decimal TotalMonthlySpend { get; set; }
    public Dictionary<string, decimal> SpendingByCategory { get; set; }
    public IEnumerable<Subscription> UpcomingRenewals { get; set; }
    public int TotalSubscriptionCount { get; set; }
}
```

**Spending Normalization:**
- Convert all billing cycles to monthly equivalents
- Annual: Divide by 12
- Quarterly: Divide by 3
- Weekly: Multiply by 4.33
- Currency conversion using daily exchange rates (cached)

#### 6. User Management Service

**Responsibility:** User authentication, profile management, and data privacy

**Key Classes:**
- `UserService`: User CRUD operations
- `AuthenticationService`: OAuth integration
- `DataExportService`: GDPR data export
- `DataDeletionService`: Complete data removal

**Interfaces:**
```csharp
public interface IUserService
{
    Task<User> CreateUserAsync(OAuthProfile profile);
    Task<User> GetUserAsync(string userId);
    Task UpdatePreferencesAsync(string userId, UserPreferences preferences);
    Task<byte[]> ExportUserDataAsync(string userId);
    Task DeleteUserDataAsync(string userId);
}
```

### Background Jobs (Hangfire)

**Job 1: Email Scanning Job**
- Schedule: Every 15 minutes per connected email account
- Priority: New subscriptions > Updates
- Retry: Exponential backoff (1min, 5min, 15min)
- Rate limiting: Respect Gmail API limits (250 quota units/user/second)

**Job 2: Alert Generation Job**
- Schedule: Daily at 8 AM user local time
- Checks: Upcoming renewals, price changes, trial endings
- Batch: Consolidate multiple alerts if user prefers digest mode

**Job 3: Subscription Update Job**
- Schedule: Daily at 2 AM
- Updates: Recalculate next renewal dates for recurring subscriptions
- Cleanup: Archive subscriptions past cancellation date

**Job 4: Vendor Metadata Enrichment Job**
- Schedule: Weekly
- Updates: Fetch logos, categories, and website URLs for new vendors
- Source: Public APIs or web scraping (with rate limiting)

## Data Models

### Core Entities

#### User
```csharp
public class User
{
    public string Id { get; set; } // GUID
    public string Email { get; set; }
    public string Name { get; set; }
    public string OAuthProvider { get; set; } // "Google", "Microsoft"
    public string OAuthSubjectId { get; set; }
    public SubscriptionTier Tier { get; set; } // Free, Paid
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public UserPreferences Preferences { get; set; }
    
    // Navigation
    public ICollection<EmailAccount> EmailAccounts { get; set; }
    public ICollection<Subscription> Subscriptions { get; set; }
}

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

public enum SubscriptionTier
{
    Free,
    Paid
}
```

#### EmailAccount
```csharp
public class EmailAccount
{
    public string Id { get; set; } // GUID
    public string UserId { get; set; }
    public string EmailAddress { get; set; }
    public EmailProvider Provider { get; set; } // Gmail, Outlook
    public string EncryptedAccessToken { get; set; }
    public string EncryptedRefreshToken { get; set; }
    public DateTime TokenExpiresAt { get; set; }
    public DateTime LastScanAt { get; set; }
    public DateTime ConnectedAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation
    public User User { get; set; }
    public ICollection<Subscription> Subscriptions { get; set; }
}

public enum EmailProvider
{
    Gmail,
    Outlook
}
```

#### Subscription
```csharp
public class Subscription
{
    public string Id { get; set; } // GUID
    public string UserId { get; set; }
    public string EmailAccountId { get; set; }
    
    // Core subscription data
    public string ServiceName { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public DateTime? NextRenewalDate { get; set; }
    public string Category { get; set; }
    public SubscriptionStatus Status { get; set; }
    
    // Metadata
    public string VendorId { get; set; } // FK to VendorMetadata
    public string CancellationLink { get; set; }
    public bool RequiresUserReview { get; set; }
    public double ExtractionConfidence { get; set; }
    
    // Tracking
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? LastActivityEmailAt { get; set; }
    
    // Navigation
    public User User { get; set; }
    public EmailAccount EmailAccount { get; set; }
    public VendorMetadata Vendor { get; set; }
    public ICollection<SubscriptionHistory> History { get; set; }
    public ICollection<Alert> Alerts { get; set; }
}

public enum BillingCycle
{
    Weekly,
    Monthly,
    Quarterly,
    Annual,
    Unknown
}

public enum SubscriptionStatus
{
    Active,
    Cancelled,
    Archived,
    PendingReview,
    TrialActive
}
```

#### SubscriptionHistory
```csharp
public class SubscriptionHistory
{
    public string Id { get; set; }
    public string SubscriptionId { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangeType { get; set; } // "PriceChange", "RenewalDateUpdate", "StatusChange"
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public string SourceEmailId { get; set; }
    
    // Navigation
    public Subscription Subscription { get; set; }
}
```

#### Alert
```csharp
public class Alert
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string SubscriptionId { get; set; }
    public AlertType Type { get; set; }
    public string Message { get; set; }
    public DateTime ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public AlertStatus Status { get; set; }
    public int RetryCount { get; set; }
    
    // Navigation
    public User User { get; set; }
    public Subscription Subscription { get; set; }
}

public enum AlertType
{
    RenewalUpcoming7Days,
    RenewalUpcoming3Days,
    PriceIncrease,
    TrialEnding,
    UnusedSubscription
}

public enum AlertStatus
{
    Pending,
    Sent,
    Failed,
    Snoozed
}
```

#### VendorMetadata
```csharp
public class VendorMetadata
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NormalizedName { get; set; } // Lowercase, no spaces for matching
    public string LogoUrl { get; set; }
    public string WebsiteUrl { get; set; }
    public string AccountManagementUrl { get; set; }
    public string Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation
    public ICollection<Subscription> Subscriptions { get; set; }
}
```

#### EmailMetadata (Stored, not full content)
```csharp
public class EmailMetadata
{
    public string Id { get; set; }
    public string EmailAccountId { get; set; }
    public string ExternalEmailId { get; set; } // Gmail message ID
    public string Sender { get; set; }
    public string Subject { get; set; }
    public DateTime ReceivedAt { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string SubscriptionId { get; set; } // If created subscription
    
    // Navigation
    public EmailAccount EmailAccount { get; set; }
}
```

### Database Schema Considerations

**Indexing Strategy:**
- User.Email (unique)
- Subscription.UserId + Status (for dashboard queries)
- Subscription.NextRenewalDate (for alert generation)
- EmailAccount.UserId (for multi-account queries)
- Alert.ScheduledFor + Status (for job processing)

**Data Partitioning:**
- All tables include UserId for future sharding by user
- Subscription data is self-contained per user (no cross-user queries)

**Soft Deletion:**
- Subscriptions use Status = Archived instead of hard delete
- Maintains historical data for reporting
- EmailAccounts use IsActive flag

**Encryption:**
- OAuth tokens encrypted at rest using AES-256
- Encryption keys stored in Azure Key Vault (production) or User Secrets (dev)

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*


### Email Integration Properties

Property 1: OAuth token encryption
*For any* stored OAuth access token, the token SHALL be encrypted using AES-256 encryption before storage
**Validates: Requirements 8.1**

Property 2: Connection establishment after OAuth
*For any* successful OAuth authentication, the system SHALL store an encrypted token and successfully establish an email provider connection
**Validates: Requirements 1.2**

Property 3: Initial email retrieval span
*For any* newly established email connection, the system SHALL retrieve emails from exactly the past 12 months
**Validates: Requirements 1.3**

Property 4: Independent account management
*For any* user with multiple email accounts, operations on one account SHALL NOT affect the state or data of other accounts
**Validates: Requirements 1.4**

Property 5: Token deletion on revocation
*For any* email account revocation, all associated OAuth tokens SHALL be immediately deleted from the database
**Validates: Requirements 1.5**

### Email Processing Properties

Property 6: Email queueing timeliness
*For any* newly retrieved email, the system SHALL queue it for AI processing within 5 minutes
**Validates: Requirements 2.1**

Property 7: Classification accuracy
*For any* set of labeled test emails, the AI extraction engine SHALL correctly classify at least 90% as subscription-related or not subscription-related
**Validates: Requirements 2.2**

Property 8: Required field extraction
*For any* email classified as subscription-related, the extraction result SHALL include service name, billing cycle, price, next renewal date, and category (or mark as requiring review)
**Validates: Requirements 2.3**

Property 9: Database record creation
*For any* completed extraction with high confidence, a corresponding subscription record SHALL exist in the database
**Validates: Requirements 2.4**

Property 10: Low confidence flagging
*For any* extraction with confidence score below 0.60, the resulting subscription record SHALL be flagged with RequiresUserReview = true
**Validates: Requirements 2.5**

Property 11: Metadata-only storage
*For any* processed email, only metadata (sender, subject, date, extracted fields) SHALL be stored, never the full email body content
**Validates: Requirements 8.4**

### Dashboard Properties

Property 12: Active subscription display
*For any* user dashboard access, all subscriptions with Status = Active SHALL be displayed with service name, price, billing cycle, and next renewal date
**Validates: Requirements 3.1**

Property 13: Category grouping
*For any* set of displayed subscriptions, they SHALL be grouped by their category field
**Validates: Requirements 3.2**

Property 14: Billing cycle normalization
*For any* subscription with billing cycle of Annual, Quarterly, or Weekly, the monthly equivalent SHALL be calculated as Price/12, Price/3, or Price*4.33 respectively
**Validates: Requirements 3.3**

Property 15: Upcoming renewal highlighting
*For any* subscription with NextRenewalDate within 30 days of today, it SHALL be highlighted in the danger zone
**Validates: Requirements 3.4**

Property 16: Timeline chronological ordering
*For any* renewal timeline view, all renewal events SHALL be sorted chronologically and span the next 12 months
**Validates: Requirements 3.5**

### Alert Properties

Property 17: 7-day renewal alert generation
*For any* active subscription with NextRenewalDate exactly 7 days in the future, a renewal alert SHALL be generated and sent
**Validates: Requirements 4.1**

Property 18: 3-day renewal alert generation
*For any* active subscription with NextRenewalDate exactly 3 days in the future, a second renewal alert SHALL be generated and sent
**Validates: Requirements 4.2**

Property 19: Price increase alert completeness
*For any* detected price increase, the alert SHALL include old price, new price, and percentage change
**Validates: Requirements 4.3**

Property 20: Trial ending alert information
*For any* subscription with Status = TrialActive and NextRenewalDate within 3 days, an alert SHALL include trial end date and post-trial price
**Validates: Requirements 4.4**

Property 21: Unused subscription detection
*For any* subscription with LastActivityEmailAt more than 6 months in the past, an unused subscription alert SHALL be generated
**Validates: Requirements 4.5**

### Spending Insights Properties

Property 22: Total monthly cost calculation
*For any* user with subscriptions, the total monthly cost SHALL equal the sum of all normalized monthly equivalents
**Validates: Requirements 5.1**

Property 23: Category percentage accuracy
*For any* spending breakdown by category, the sum of all category percentages SHALL equal 100%
**Validates: Requirements 5.2**

Property 24: Weekly renewal concentration
*For any* week with 2 or more renewals, the system SHALL highlight the concentration and display the correct total amount
**Validates: Requirements 5.3**

Property 25: PDF report completeness
*For any* generated PDF report, it SHALL contain all active subscriptions with their dates and costs
**Validates: Requirements 5.4**

Property 26: Benchmark comparison availability
*For any* user with 5 or more subscriptions, a spending comparison to anonymized benchmarks SHALL be displayed
**Validates: Requirements 5.5**

### Cancellation Assistant Properties

Property 27: Cancellation instruction retrieval
*For any* subscription selected for cancellation, the system SHALL attempt to retrieve cancellation instructions from vendor metadata
**Validates: Requirements 6.1**

Property 28: Pre-filled template generation
*For any* subscription with available cancellation instructions, a pre-filled cancellation email or form SHALL be generated
**Validates: Requirements 6.2**

Property 29: Cancellation request submission
*For any* user-confirmed cancellation, a cancellation request SHALL be sent to the vendor
**Validates: Requirements 6.3**

Property 30: Follow-up reminder creation
*For any* submitted cancellation, a follow-up reminder SHALL be created with ScheduledFor = 7 days from submission
**Validates: Requirements 6.4**

Property 31: Cancellation confirmation detection
*For any* detected cancellation confirmation email, the associated subscription Status SHALL be updated to Cancelled
**Validates: Requirements 6.5**

### Multi-Account Properties

Property 32: Unified subscription aggregation
*For any* user with multiple connected email accounts, the dashboard SHALL display subscriptions from all accounts
**Validates: Requirements 7.1**

Property 33: Email account association display
*For any* displayed subscription, the associated email account SHALL be indicated
**Validates: Requirements 7.2**

Property 34: Family mode account addition
*For any* user with family mode enabled, the system SHALL allow adding email accounts from different OAuth identities
**Validates: Requirements 7.3**

Property 35: Independent permission controls
*For any* family mode user, each email account SHALL maintain separate permission controls
**Validates: Requirements 7.4**

Property 36: Subscription archival on disconnect
*For any* disconnected email account, all associated subscriptions SHALL have Status = Archived and historical data SHALL be retained
**Validates: Requirements 7.5**

### Security and Privacy Properties

Property 37: Data deletion completeness
*For any* user data deletion request, all user records (User, EmailAccount, Subscription, Alert) SHALL be permanently deleted within 24 hours
**Validates: Requirements 8.3**

Property 38: GDPR data export
*For any* user data export request, the system SHALL generate a valid JSON file containing all user data
**Validates: Requirements 8.5**

### Background Processing Properties

Property 39: Email check frequency
*For any* connected email account, the background processor SHALL check for new emails every 15 minutes
**Validates: Requirements 9.1**

Property 40: Retry with exponential backoff
*For any* failed email retrieval, the system SHALL retry with delays of 1 minute, 5 minutes, and 15 minutes before alerting the user
**Validates: Requirements 9.2**

Property 41: Priority-based processing
*For any* set of queued parsing jobs, jobs marked as "new subscription" SHALL be processed before jobs marked as "update"
**Validates: Requirements 9.3**

Property 42: Rate limit throttling
*For any* email provider API call, the system SHALL respect rate limits and throttle requests to stay within limits
**Validates: Requirements 9.4**

Property 43: Operation logging
*For any* email processing operation, a log entry SHALL be created with timestamp, operation type, and result
**Validates: Requirements 9.5**

### Vendor Metadata Properties

Property 44: Vendor metadata matching
*For any* identified service name, the system SHALL attempt to match it against the VendorMetadata database
**Validates: Requirements 10.1**

Property 45: Logo and icon display
*For any* subscription with associated vendor metadata, the dashboard SHALL display the vendor logo and category icon
**Validates: Requirements 10.2**

Property 46: Fallback to service name
*For any* subscription without vendor metadata, the extracted service name SHALL still be displayed
**Validates: Requirements 10.3**

Property 47: Vendor link provision
*For any* subscription with vendor metadata containing WebsiteUrl, a clickable link SHALL be provided
**Validates: Requirements 10.4**

Property 48: Automatic vendor enrichment
*For any* newly detected vendor, the system SHALL queue a background job to enrich vendor metadata
**Validates: Requirements 10.5**

### Onboarding Properties

Property 49: Immediate scan initiation
*For any* first email account connection, the email scanning process SHALL start immediately (within 1 minute)
**Validates: Requirements 11.3**

Property 50: Subscription display after scan
*For any* completed initial scan, all discovered subscriptions SHALL be displayed in the dashboard
**Validates: Requirements 11.4**

### Beta Program Properties

Property 51: Beta user survey timing
*For any* beta user who has been active for 7 days, a feedback survey prompt SHALL be displayed
**Validates: Requirements 12.2**

Property 52: Feedback storage
*For any* submitted feedback form, the data SHALL be stored in the database with timestamp and user ID
**Validates: Requirements 12.4**

### Analytics Properties

Property 53: Event tracking
*For any* key user action (signup, email connected, subscriptions discovered, dashboard visit, feedback submitted), an analytics event SHALL be recorded
**Validates: Requirements 13.1**

Property 54: Engagement metric calculation
*For any* time period, the system SHALL be able to calculate DAU, WAU, retention rate, and average subscriptions per user
**Validates: Requirements 13.2**

Property 55: Metric comparison
*For any* set of actual metrics and target metrics, the system SHALL correctly compare them and identify variances
**Validates: Requirements 13.3**

### Subscription Tier Properties

Property 56: Free tier defaults
*For any* new user signup, the user SHALL be assigned SubscriptionTier = Free with limits of 1 email account and 5 subscriptions
**Validates: Requirements 14.1**

Property 57: Free tier limit enforcement
*For any* free tier user with 5 subscriptions, attempts to add a 6th subscription SHALL be blocked with an upgrade prompt
**Validates: Requirements 14.2**

Property 58: Paid tier feature unlock
*For any* user upgrade from Free to Paid tier, all paid features (unlimited accounts, unlimited subscriptions, cancellation assistant, PDF exports) SHALL be immediately accessible
**Validates: Requirements 14.3**

Property 59: Downgrade data preservation
*For any* user downgrade from Paid to Free tier, all existing subscription data SHALL be preserved but features SHALL be restricted to free tier limits
**Validates: Requirements 14.4**

## Error Handling

### Error Categories

**1. External Service Errors**
- Email provider API failures (Gmail, Outlook)
- AI extraction API failures (OpenAI)
- Email delivery failures (SendGrid)

**Strategy:**
- Retry with exponential backoff (1min, 5min, 15min)
- Circuit breaker pattern after 3 consecutive failures
- Fallback to degraded mode (e.g., skip AI extraction, queue for later)
- User notification after all retries exhausted

**2. Authentication Errors**
- OAuth token expiration
- OAuth token revocation by user
- Invalid or malformed tokens

**Strategy:**
- Automatic token refresh using refresh token
- If refresh fails, mark account as "requires re-authentication"
- Send email notification to user with re-authentication link
- Gracefully handle revoked access (don't crash, just log and notify)

**3. Data Validation Errors**
- Invalid email format
- Missing required fields in extraction
- Invalid date formats
- Currency conversion failures

**Strategy:**
- Validate all inputs at API boundary
- Return clear error messages with field-level details
- For AI extraction, use confidence scores to flag uncertain data
- Allow manual correction by user

**4. Rate Limiting Errors**
- Gmail API quota exceeded
- OpenAI API rate limits
- SendGrid email limits

**Strategy:**
- Implement request throttling to stay within limits
- Queue requests and process with delays
- Prioritize critical operations (new subscriptions > updates)
- Notify user if delays exceed acceptable thresholds

**5. Database Errors**
- Connection failures
- Constraint violations (duplicate keys)
- Transaction deadlocks

**Strategy:**
- Connection pooling with automatic retry
- Idempotent operations where possible
- Optimistic concurrency control for updates
- Comprehensive logging for debugging

**6. Business Logic Errors**
- Subscription already exists (deduplication)
- User at subscription limit (free tier)
- Invalid state transitions (e.g., cancel already cancelled subscription)

**Strategy:**
- Clear error messages explaining the issue
- Suggest corrective actions (e.g., "Upgrade to add more subscriptions")
- Prevent invalid state transitions at domain layer
- Log all business rule violations for analysis

### Error Response Format

All API errors follow a consistent format:

```json
{
  "error": {
    "code": "SUBSCRIPTION_LIMIT_REACHED",
    "message": "You've reached the 5 subscription limit for free tier",
    "details": {
      "currentCount": 5,
      "limit": 5,
      "tier": "Free"
    },
    "suggestedAction": "Upgrade to Paid tier for unlimited subscriptions",
    "timestamp": "2025-11-20T10:30:00Z",
    "correlationId": "abc-123-def"
  }
}
```

### Logging Strategy

**Log Levels:**
- ERROR: Unhandled exceptions, critical failures
- WARN: Handled errors, retry attempts, degraded mode
- INFO: Key business events (subscription created, alert sent)
- DEBUG: Detailed execution flow (dev/staging only)

**Structured Logging:**
- Include correlation ID for request tracing
- Include user ID for user-specific debugging
- Include operation name and duration
- Include error details and stack traces

**Log Retention:**
- 90 days for all logs
- Longer retention for audit logs (user data access, deletions)

## Testing Strategy

The Subscription Management System requires comprehensive testing to ensure correctness, reliability, and user trust. We employ a dual testing approach combining unit tests for specific scenarios and property-based tests for universal correctness properties.

### Testing Philosophy

**Unit tests** verify specific examples, edge cases, and integration points. They catch concrete bugs and validate that specific scenarios work correctly.

**Property-based tests** verify universal properties that should hold across all inputs. They catch general correctness issues by testing the system with hundreds of randomly generated inputs.

Together, these approaches provide comprehensive coverage: unit tests ensure specific behaviors work, while property tests ensure general correctness across the input space.

### Property-Based Testing Framework

**Framework:** CsCheck (C# property-based testing library)
- Mature, well-maintained library for .NET
- Supports custom generators for domain objects
- Integrates with xUnit test framework
- Configurable iteration count (minimum 100 iterations per property)

**Configuration:**
```csharp
[Property(Iterations = 100)]
public void PropertyTest(...)
{
    // Test implementation
}
```

### Property Test Implementation Guidelines

1. Each correctness property from the design document MUST be implemented as a property-based test
2. Each property test MUST be tagged with a comment referencing the design document property
3. Tag format: `// Feature: subscription-tracker, Property {number}: {property_text}`
4. Property tests MUST run at least 100 iterations to ensure statistical confidence
5. Custom generators MUST be created for domain objects (Subscription, EmailAccount, User, etc.)
6. Generators SHOULD produce realistic data distributions (e.g., common billing cycles more frequent)

### Test Organization

```
tests/
├── SubscriptionTracker.UnitTests/
│   ├── Services/
│   │   ├── SubscriptionServiceTests.cs
│   │   ├── EmailIngestionServiceTests.cs
│   │   ├── AlertServiceTests.cs
│   │   └── DashboardServiceTests.cs
│   ├── Domain/
│   │   ├── SubscriptionTests.cs
│   │   └── BillingCycleNormalizationTests.cs
│   └── Infrastructure/
│       ├── EmailProviderTests.cs
│       └── AIExtractionTests.cs
│
├── SubscriptionTracker.PropertyTests/
│   ├── Generators/
│   │   ├── SubscriptionGenerator.cs
│   │   ├── UserGenerator.cs
│   │   └── EmailAccountGenerator.cs
│   ├── EmailIntegrationProperties.cs
│   ├── DashboardProperties.cs
│   ├── AlertProperties.cs
│   ├── SecurityProperties.cs
│   └── SubscriptionTierProperties.cs
│
└── SubscriptionTracker.IntegrationTests/
    ├── EmailIngestionFlowTests.cs
    ├── OnboardingFlowTests.cs
    └── CancellationFlowTests.cs
```

### Unit Testing Strategy

**Services Layer:**
- Mock external dependencies (email providers, AI client, repositories)
- Test business logic in isolation
- Verify correct method calls and state changes
- Test error handling and edge cases

**Domain Layer:**
- Test entity validation rules
- Test domain logic (e.g., billing cycle normalization)
- Test state transitions (e.g., Active → Cancelled)
- No mocking required (pure domain logic)

**Infrastructure Layer:**
- Test repository implementations with in-memory database
- Test email provider clients with mock HTTP responses
- Test AI extraction with sample email fixtures
- Verify correct error handling and retries

**Example Unit Tests:**
- Subscription creation with valid data
- Subscription creation with missing required fields (should fail)
- Billing cycle normalization (Annual → Monthly)
- Alert generation for subscription 7 days before renewal
- Token encryption and decryption
- User data deletion removes all related records

### Property-Based Testing Strategy

**Email Integration Properties (Properties 1-5):**
- Generate random OAuth tokens and verify encryption
- Generate random email accounts and verify independent operations
- Generate random connection events and verify 12-month retrieval

**Dashboard Properties (Properties 12-16):**
- Generate random subscription sets and verify display completeness
- Generate random billing cycles and verify normalization correctness
- Generate random dates and verify renewal highlighting logic

**Alert Properties (Properties 17-21):**
- Generate random subscriptions with various renewal dates
- Verify alerts are generated at correct times (7 days, 3 days)
- Generate price changes and verify alert completeness

**Security Properties (Properties 37-38):**
- Generate random user data and verify complete deletion
- Generate random user data and verify export completeness

**Subscription Tier Properties (Properties 56-59):**
- Generate random user signups and verify free tier defaults
- Generate random subscription additions and verify limit enforcement
- Generate random tier changes and verify feature access

### Integration Testing Strategy

**End-to-End Flows:**
1. Onboarding flow: User signup → Email connection → Initial scan → Dashboard display
2. Email ingestion flow: New email arrives → Queued → AI extraction → Subscription created
3. Alert flow: Subscription approaching renewal → Alert generated → Email sent
4. Cancellation flow: User requests cancellation → Instructions retrieved → Request sent → Confirmation detected

**Test Environment:**
- Use test email accounts (Gmail test user)
- Use OpenAI test API key with low rate limits
- Use in-memory database (SQLite :memory:)
- Use test SendGrid account

**Integration Test Scope:**
- Verify all components work together correctly
- Test actual external API integrations (with test accounts)
- Verify database transactions and consistency
- Test background job execution

### Test Data Management

**Fixtures:**
- Sample email HTML for various subscription types (Netflix, Spotify, Adobe, etc.)
- Sample AI extraction responses (high confidence, low confidence, errors)
- Sample vendor metadata records
- Sample user profiles with various tier configurations

**Generators (for property tests):**
- `SubscriptionGenerator`: Generates valid subscriptions with realistic data
- `UserGenerator`: Generates users with various tier and preference combinations
- `EmailAccountGenerator`: Generates email accounts with various providers
- `DateGenerator`: Generates dates relative to "today" for testing time-based logic

### Test Coverage Goals

- **Unit Test Coverage:** Minimum 80% code coverage for business logic
- **Property Test Coverage:** All 59 correctness properties implemented as property tests
- **Integration Test Coverage:** All critical user flows covered
- **Edge Case Coverage:** Boundary conditions, null values, empty collections, extreme dates

### Continuous Testing

**CI/CD Pipeline:**
1. Run unit tests on every commit (fast feedback)
2. Run property tests on every PR (comprehensive validation)
3. Run integration tests nightly (slower, external dependencies)
4. Generate coverage reports and fail build if below 80%

**Test Execution Time:**
- Unit tests: < 30 seconds
- Property tests: < 5 minutes (100 iterations × 59 properties)
- Integration tests: < 10 minutes

### Testing Anti-Patterns to Avoid

❌ **Don't** mock everything in unit tests (test real domain logic)
❌ **Don't** write property tests that are just parameterized unit tests
❌ **Don't** skip property tests because they're "too slow" (they catch real bugs)
❌ **Don't** use production API keys in tests (use test accounts)
❌ **Don't** write tests that depend on specific dates (use relative dates)
❌ **Don't** write tests that depend on external state (use isolated test data)

### Testing Success Criteria

✅ All 59 correctness properties have corresponding property-based tests
✅ All property tests run at least 100 iterations
✅ Unit test coverage is at least 80% for business logic
✅ All critical user flows have integration tests
✅ Tests run in CI/CD pipeline and block merges on failure
✅ Test execution time is acceptable (< 15 minutes total)

## Performance Considerations

### Response Time Targets

- Dashboard load: < 2 seconds
- API endpoints: < 500ms (p95)
- Email scanning (initial 12 months): < 5 minutes for 10,000 emails
- Background email check: < 15 minutes per account
- Alert generation: < 1 minute for all users

### Optimization Strategies

**Database:**
- Index on frequently queried fields (UserId, Status, NextRenewalDate)
- Use pagination for large result sets (50 subscriptions per page)
- Cache vendor metadata (rarely changes)
- Use connection pooling

**AI Extraction:**
- Batch email processing (process 10 emails per API call)
- Cache extraction results (don't re-process same email)
- Use cheaper model (GPT-4o-mini) for cost optimization
- Implement request queuing to avoid rate limits

**Background Jobs:**
- Use priority queues (new subscriptions > updates)
- Implement job throttling to avoid overwhelming external APIs
- Use distributed locks to prevent duplicate processing
- Monitor job queue depth and alert on backlog

**Caching:**
- Cache vendor metadata (1 hour TTL)
- Cache user preferences (5 minutes TTL)
- Cache dashboard data (1 minute TTL)
- Use in-memory cache for MVP, Redis for scale

### Scalability Considerations

**Horizontal Scaling:**
- Stateless API servers (can add more instances)
- Background job workers (can add more workers)
- Database read replicas for reporting queries

**Vertical Scaling:**
- Upgrade database tier as data grows
- Upgrade app service tier for more CPU/memory

**Data Partitioning:**
- All tables include UserId for future sharding
- Subscriptions are self-contained per user (no cross-user queries)

## Security Considerations

### Authentication & Authorization

- OAuth 2.0 for user authentication (Google, Microsoft)
- JWT tokens for API authentication
- Role-based access control (User, Admin)
- Email account ownership verification

### Data Protection

- AES-256 encryption for OAuth tokens at rest
- TLS 1.3 for all data in transit
- Secure key storage (Azure Key Vault in production)
- No storage of full email content (metadata only)

### Privacy Compliance

- GDPR compliance for EU users
- CCPA compliance for California users
- Right to be forgotten (complete data deletion)
- Data portability (JSON export)
- Explicit consent for email access

### Security Best Practices

- Input validation on all API endpoints
- SQL injection prevention (parameterized queries via EF Core)
- XSS prevention (output encoding in frontend)
- CSRF protection (SameSite cookies)
- Rate limiting (100 requests/minute per user)
- Security headers (CSP, HSTS, X-Frame-Options)

### Audit Logging

- Log all authentication attempts
- Log all data access (especially email account connections)
- Log all data modifications (subscription updates)
- Log all data deletions
- Retain audit logs for 90 days

## Deployment Architecture

### MVP Deployment (Phase 1)

**Infrastructure:**
- Azure App Service Free Tier (F1) for backend
- Vercel Free Tier for frontend
- SQLite file-based database
- In-memory Hangfire for background jobs
- SendGrid Free Tier for emails

**Cost:** $10-20/month (primarily OpenAI API)

**Limitations:**
- Single instance (no high availability)
- Limited to 60 CPU minutes/day
- 1 GB RAM
- Suitable for 100 beta users

### Production Deployment (Post-MVP)

**Infrastructure:**
- Azure App Service Basic B1 ($13/month)
- Azure SQL Database Basic ($5/month)
- Azure Application Insights ($10/month)
- SendGrid Essentials ($15/month)
- Azure Key Vault for secrets

**Cost:** $93-143/month (including OpenAI API)

**Capabilities:**
- 1.75 GB RAM
- Always-on instances
- Custom domains and SSL
- Suitable for 1,000 users

### CI/CD Pipeline

**GitHub Actions Workflow:**
1. Run tests (unit, property, integration)
2. Build Docker image
3. Push to Azure Container Registry
4. Deploy to Azure App Service
5. Run smoke tests
6. Notify team on Slack

**Deployment Strategy:**
- Blue-green deployment for zero downtime
- Automatic rollback on health check failure
- Database migrations run before deployment

### Monitoring & Observability

**Metrics:**
- API response times (p50, p95, p99)
- Error rates by endpoint
- Background job success/failure rates
- Email processing throughput
- AI extraction accuracy
- Alert delivery rate

**Alerts:**
- API error rate > 5%
- Background job queue depth > 1000
- Email provider API failures
- Database connection failures
- Disk space < 10%

**Dashboards:**
- Real-time system health
- User engagement metrics
- Cost tracking (OpenAI API usage)
- Beta program metrics

## Future Enhancements

### Phase 4+ Features

**Bank Transaction Integration:**
- Connect to Plaid for bank transaction matching
- Automatically detect subscriptions from bank charges
- Cross-reference with email-detected subscriptions

**Mobile App:**
- React Native app for iOS and Android
- Push notifications for alerts
- Quick subscription overview widget

**Browser Extension:**
- Chrome/Firefox extension
- Detect subscription signups in real-time
- Warn about free trial conversions

**Advanced AI Features:**
- Predict subscription cancellation likelihood
- Recommend cheaper alternatives
- Detect duplicate subscriptions across services

**B2B Features:**
- Team/organization accounts
- Centralized expense management
- Approval workflows for new subscriptions
- Budget tracking and alerts

**Social Features:**
- Share subscription recommendations
- Compare spending with friends (anonymized)
- Group subscriptions (family plans)

### Technical Debt to Address

- Migrate from SQLite to Azure SQL Database
- Implement proper caching layer (Redis)
- Add comprehensive API documentation
- Improve error messages and user guidance
- Add more comprehensive logging
- Implement feature flags for gradual rollouts

## Appendix

### Technology Stack Summary

**Backend:**
- ASP.NET Core 10.0
- Entity Framework Core 10.0
- Hangfire for background jobs
- Serilog for logging
- xUnit + CsCheck for testing

**Frontend:**
- Next.js 15 with TypeScript
- Tailwind CSS + shadcn/ui
- React Query for state management
- NextAuth.js for authentication

**Infrastructure:**
- Azure App Service
- Azure SQL Database (post-MVP)
- Azure Key Vault
- Application Insights
- SendGrid for emails

**External Services:**
- Gmail API
- OpenAI API (GPT-4o-mini)
- SendGrid API

### Key Design Decisions Summary

1. **Monolithic architecture** for MVP simplicity, with clear paths to microservices
2. **SQLite database** for zero-cost MVP, with migration path to Azure SQL
3. **Email-first discovery** to avoid bank integration complexity
4. **API-based LLM** for state-of-art extraction without ML infrastructure
5. **Next.js frontend** for rapid development and free hosting
6. **Phased MVP approach** to validate market before scaling
7. **Property-based testing** to ensure correctness across input space
8. **Privacy-focused design** with metadata-only storage and GDPR compliance

### Success Metrics

**Beta Phase:**
- 80%+ activation rate (users connect email and discover subscriptions)
- 50%+ weekly engagement rate
- 8+ average subscriptions discovered per user
- 4.0+ satisfaction rating
- 20%+ conversion intent at $10-15/month
- 60%+ retention after 4 weeks

**Post-MVP:**
- 10%+ free-to-paid conversion rate
- < 10% monthly churn rate
- Positive cash flow (revenue > costs)
- 99.5% uptime
- < 2 second dashboard load time
- 90%+ AI extraction accuracy
