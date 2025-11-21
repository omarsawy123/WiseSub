# Subscription Management SaaS - Project Vision

## Executive Summary

This document serves as the comprehensive reference guide for building a subscription management SaaS platform. The system helps consumers in EU/US markets track, manage, and control their recurring subscriptions by automatically detecting them from email communications and providing proactive alerts and management tools.

**Target Market:** Consumers (B2C) with path to B2B
**Primary Markets:** EU and US
**Revenue Model:** Freemium ($10-15/month for premium)
**MVP Timeline:** 8-12 weeks
**Tech Stack:** .NET 10 + Azure + Next.js 15
**Initial Scale:** 100 users with minimal operational costs

---

## Problem Statement

### Consumer Pain Points
1. **Hidden Renewals:** Subscriptions auto-renew without clear warning
2. **Free Trial Traps:** Trials convert to paid subscriptions silently
3. **Price Creep:** Services increase prices without prominent notification
4. **Subscription Sprawl:** Multiple subscriptions across different email accounts
5. **Forgotten Services:** Paying for unused subscriptions for months/years
6. **Cancellation Friction:** Difficult to find and execute cancellation processes

### Market Opportunity
- Average consumer has 12+ active subscriptions
- 42% of consumers forget about subscriptions they're paying for
- $1.8B wasted annually on forgotten subscriptions in US alone
- Growing subscription fatigue creates demand for management tools
- EU consumer protection laws favor transparency tools

---

## Product Vision

### Core Value Proposition
**"Never get surprised by a subscription charge again"**

The platform automatically discovers subscriptions from email, alerts users before renewals, detects price increases, identifies unused services, and assists with cancellations—all without requiring bank account access.

### Key Differentiators
1. **Email-First Approach:** Easiest onboarding, no bank access required
2. **AI-Powered Detection:** Automatically extracts subscription details from emails
3. **Proactive Alerts:** Warns before charges, not after
4. **Privacy-Focused:** Read-only email access, no full email storage, GDPR compliant
5. **Cancellation Assistant:** Helps users actually cancel, not just track
6. **EU-Friendly:** Built with European consumer protection laws in mind

---

## MVP Strategy

### Beta Launch Strategy (Before Phase 1)
**Objective:** Validate market demand and product-market fit

**Approach:**
- Launch MVP to 50-100 beta users
- Offer free lifetime access or 50% discount as incentive
- Collect feedback through integrated surveys
- Track engagement metrics rigorously
- Make go/no-go decision after 4-6 weeks

**Beta Success Metrics:**
- 80%+ activation rate (users connect email)
- 50%+ weekly return rate
- 8+ subscriptions discovered per user
- 4.0+ satisfaction rating
- 20%+ conversion intent at $10-15/month
- 60%+ retention after 4 weeks

**Decision Framework:**
- ✅ **GO (3+ metrics met):** Proceed to Phase 2 and monetization
- ❌ **NO-GO (< 2 metrics met):** Pivot or shut down
- 🔄 **ITERATE (2 metrics met):** Adjust and extend beta

### Phase 1: Core MVP (Weeks 1-4)
**Objective:** Launch beta to validate core value proposition

**Features:**
- Gmail OAuth integration
- Email scanning (IMAP-based)
- AI extraction of subscription details
- Basic subscription dashboard
- Manual CRUD operations
- User authentication
- Integrated feedback form
- Basic analytics tracking

**Success Metrics:**
- 50-100 beta users acquired
- 70%+ subscription detection accuracy
- < 2 minute onboarding time
- 50%+ users check dashboard weekly
- 80%+ feedback response rate

### Phase 2: Alerts (Weeks 5-6) - Only if Beta Succeeds
**Objective:** Add proactive value that drives engagement

**Prerequisites:**
- Beta success metrics achieved
- Positive user feedback
- Clear demand validated

**Features:**
- Renewal alerts (7-day, 3-day)
- Price increase detection
- Free trial ending alerts
- Email notification system

**Success Metrics:**
- 95%+ alert delivery rate
- 50%+ alert open rate
- 30% increase in dashboard engagement
- 70%+ users find alerts valuable

### Phase 3: Monetization (Weeks 7-8) - Only if Willingness to Pay Validated
**Objective:** Enable revenue generation

**Prerequisites:**
- Beta shows 20%+ conversion intent
- Alerts feature increases engagement
- Positive ROI projection

**Features:**
- Free tier (1 email, 5 subscriptions)
- Paid tier ($10-15/month, unlimited)
- Stripe payment integration
- Basic spending insights
- Clear pricing page

**Success Metrics:**
- 10%+ conversion to paid (based on beta intent)
- < 10% monthly churn
- Positive cash flow (revenue > costs)
- $10-15 ARPU

### Phase 4: Enhancement (Weeks 9-12)
**Objective:** Differentiate and reduce churn

**Features:**
- Outlook integration
- Cancellation assistant
- PDF exports
- Vendor metadata
- Unused subscription detection

**Success Metrics:**
- 30%+ feature adoption
- 4.0+ satisfaction score
- < 5% monthly churn

---

## Technical Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         Frontend                             │
│                  Next.js + TypeScript                        │
│              (Azure Static Web Apps / Vercel)                │
└────────────────────┬────────────────────────────────────────┘
                     │ HTTPS/REST
┌────────────────────▼────────────────────────────────────────┐
│                      API Gateway                             │
│                  ASP.NET Core Web API                        │
│              (Azure App Service)                             │
└─────┬──────────────┬──────────────┬────────────────────────┘
      │              │              │
      │              │              │
┌─────▼─────┐  ┌────▼─────┐  ┌────▼──────────────────────────┐
│   Auth    │  │  Email   │  │    Background Worker          │
│  Service  │  │ Service  │  │   (Hangfire Worker Service)   │
│           │  │          │  │                                │
│  OAuth    │  │  IMAP/   │  │  - Email Scanning             │
│  Identity │  │  Gmail   │  │  - AI Extraction              │
│           │  │  API     │  │  - Alert Generation           │
└───────────┘  └──────────┘  └───────────────────────────────┘
      │              │                      │
      │              │                      │
┌─────▼──────────────▼──────────────────────▼─────────────────┐
│                    Data Layer                                │
│              Entity Framework Core                           │
└─────┬────────────────────────────────────────────────────────┘
      │
┌─────▼─────────────────────────────────────────────────────┐
│                   Azure SQL Database                        │
│                                                             │
│  Tables: Users, EmailAccounts, Subscriptions,              │
│          Alerts, VendorMetadata, AuditLogs                 │
└─────────────────────────────────────────────────────────────┘

External Services:
- Azure Key Vault (secrets)
- Azure Service Bus (message queue)
- Redis Cache (caching)
- OpenAI API (AI extraction)
- SendGrid (email alerts)
- Stripe (payments)
```

### Backend Architecture (.NET)

**Clean Architecture Layers:**

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation Layer                    │
│                                                          │
│  - API Controllers                                       │
│  - SignalR Hubs (real-time updates)                     │
│  - Middleware (auth, logging, error handling)           │
│  - DTOs and View Models                                 │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                   Application Layer                      │
│                                                          │
│  - Use Cases / Commands / Queries (CQRS pattern)        │
│  - Application Services                                 │
│  - Validators (FluentValidation)                        │
│  - Mapping (AutoMapper)                                 │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                    Domain Layer                          │
│                                                          │
│  - Entities (User, Subscription, Alert, etc.)           │
│  - Value Objects (Money, BillingCycle, etc.)            │
│  - Domain Services                                      │
│  - Repository Interfaces                                │
│  - Domain Events                                        │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                 Infrastructure Layer                     │
│                                                          │
│  - EF Core DbContext                                    │
│  - Repository Implementations                           │
│  - External Service Clients (Email, AI, Payment)        │
│  - Background Jobs (Hangfire)                           │
│  - Caching (Redis)                                      │
└─────────────────────────────────────────────────────────┘
```

### Database Schema (Core Tables)

```sql
-- Users
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    DisplayName NVARCHAR(255),
    SubscriptionTier NVARCHAR(50), -- Free, Paid
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL,
    IsDeleted BIT DEFAULT 0
);

-- EmailAccounts
CREATE TABLE EmailAccounts (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Provider NVARCHAR(50) NOT NULL, -- Gmail, Outlook
    EmailAddress NVARCHAR(255) NOT NULL,
    AccessTokenKeyVaultId NVARCHAR(500), -- Reference to Key Vault
    LastSyncedAt DATETIME2,
    Status NVARCHAR(50), -- Active, Disconnected, Error
    CreatedAt DATETIME2 NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- Subscriptions
CREATE TABLE Subscriptions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    EmailAccountId UNIQUEIDENTIFIER NOT NULL,
    ServiceName NVARCHAR(255) NOT NULL,
    BillingCycle NVARCHAR(50), -- Monthly, Yearly, Quarterly
    Price DECIMAL(10,2),
    Currency NVARCHAR(10),
    NextRenewalDate DATETIME2,
    Category NVARCHAR(100), -- Entertainment, Productivity, etc.
    Status NVARCHAR(50), -- Active, Cancelled, Trial
    CancellationLink NVARCHAR(1000),
    VendorId UNIQUEIDENTIFIER,
    LastActivityDetectedAt DATETIME2,
    ExtractionConfidence DECIMAL(3,2), -- 0.00 to 1.00
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL,
    IsDeleted BIT DEFAULT 0,
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    FOREIGN KEY (EmailAccountId) REFERENCES EmailAccounts(Id)
);

-- Alerts
CREATE TABLE Alerts (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    SubscriptionId UNIQUEIDENTIFIER NOT NULL,
    AlertType NVARCHAR(50), -- Renewal, PriceIncrease, TrialEnding, Unused
    ScheduledFor DATETIME2 NOT NULL,
    SentAt DATETIME2,
    Status NVARCHAR(50), -- Pending, Sent, Failed, Snoozed
    Message NVARCHAR(MAX),
    CreatedAt DATETIME2 NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    FOREIGN KEY (SubscriptionId) REFERENCES Subscriptions(Id)
);

-- VendorMetadata
CREATE TABLE VendorMetadata (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ServiceName NVARCHAR(255) NOT NULL UNIQUE,
    LogoUrl NVARCHAR(1000),
    WebsiteUrl NVARCHAR(1000),
    AccountManagementUrl NVARCHAR(1000),
    Category NVARCHAR(100),
    Country NVARCHAR(100),
    RefundPolicy NVARCHAR(MAX),
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL
);

-- EmailMetadata (for tracking processed emails)
CREATE TABLE EmailMetadata (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    EmailAccountId UNIQUEIDENTIFIER NOT NULL,
    MessageId NVARCHAR(500) NOT NULL, -- Email provider's message ID
    Subject NVARCHAR(500),
    Sender NVARCHAR(255),
    ReceivedDate DATETIME2,
    ProcessedAt DATETIME2,
    IsSubscriptionRelated BIT,
    ExtractedData NVARCHAR(MAX), -- JSON
    FOREIGN KEY (EmailAccountId) REFERENCES EmailAccounts(Id)
);

-- AuditLogs
CREATE TABLE AuditLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER,
    EntityType NVARCHAR(100),
    EntityId UNIQUEIDENTIFIER,
    Action NVARCHAR(50), -- Create, Update, Delete
    Changes NVARCHAR(MAX), -- JSON
    Timestamp DATETIME2 NOT NULL,
    IpAddress NVARCHAR(50)
);
```

### Key Services & Responsibilities

**1. EmailIngestionService**
- Connects to Gmail/Outlook via OAuth
- Retrieves emails using IMAP or official APIs
- Filters for subscription-related emails (receipts, renewals, etc.)
- Queues emails for AI processing
- Handles rate limiting and retries

**2. AIExtractionService**
- Calls OpenAI API with email content
- Extracts structured data (service name, price, date, etc.)
- Classifies email type
- Returns confidence scores
- Handles extraction failures gracefully

**3. SubscriptionService**
- CRUD operations for subscriptions
- Normalizes billing cycles for comparison
- Calculates spending totals
- Manages subscription lifecycle (active → cancelled)
- Handles duplicate detection

**4. AlertService**
- Schedules alerts based on renewal dates
- Detects price increases by comparing historical data
- Identifies unused subscriptions (no activity emails)
- Sends email notifications via SendGrid
- Tracks alert delivery status

**5. CancellationAssistantService**
- Retrieves cancellation instructions from vendor metadata
- Generates pre-filled cancellation emails
- Sends cancellation requests on behalf of user
- Creates follow-up reminders
- Detects cancellation confirmations

**6. BackgroundWorkerService**
- Runs scheduled jobs (email scanning every 15 min)
- Processes queued emails
- Generates and sends alerts
- Performs cleanup tasks
- Monitors system health

---

## Frontend Architecture (Next.js)

### Page Structure

```
/app
  /auth
    /login
    /callback
  /dashboard
    page.tsx              # Main dashboard
  /subscriptions
    page.tsx              # List view
    /[id]
      page.tsx            # Detail view
  /settings
    /email-accounts
    /alerts
    /billing
  /onboarding
    page.tsx
  /api
    /auth
      [...nextauth].ts
```

### Key Components

```
/components
  /ui                     # shadcn/ui components
  /dashboard
    SubscriptionCard.tsx
    SpendingChart.tsx
    UpcomingRenewals.tsx
    DangerZone.tsx
  /subscriptions
    SubscriptionList.tsx
    SubscriptionFilter.tsx
    SubscriptionDetail.tsx
  /alerts
    AlertBanner.tsx
    AlertSettings.tsx
  /onboarding
    EmailConnectStep.tsx
    ScanningProgress.tsx
```

### State Management

- **Server State:** React Query for API data fetching and caching
- **Client State:** React Context for UI state (theme, sidebar, etc.)
- **Form State:** React Hook Form for all forms
- **Auth State:** NextAuth.js session management

---

## AI Extraction Strategy

### Email Classification Prompt

```
You are an AI assistant that classifies emails related to subscriptions.

Analyze the following email and determine if it is subscription-related.

Email Subject: {subject}
Email Sender: {sender}
Email Body: {body}

Classify this email into ONE of the following categories:
1. PURCHASE_RECEIPT - Initial purchase or subscription signup
2. RENEWAL_NOTICE - Upcoming or completed renewal
3. TRIAL_CONFIRMATION - Free trial started
4. TRIAL_ENDING - Free trial ending soon
5. PRICE_CHANGE - Price increase or change notification
6. CANCELLATION_CONFIRMATION - Subscription cancelled
7. NOT_SUBSCRIPTION_RELATED - Not related to subscriptions

Return JSON:
{
  "category": "CATEGORY_NAME",
  "confidence": 0.95,
  "reasoning": "Brief explanation"
}
```

### Data Extraction Prompt

```
You are an AI assistant that extracts subscription details from emails.

Extract the following information from this email:

Email Subject: {subject}
Email Body: {body}

Extract:
1. Service Name (e.g., "Netflix", "Spotify Premium")
2. Billing Cycle (Monthly, Yearly, Quarterly, or null)
3. Price (numeric value only)
4. Currency (USD, EUR, GBP, etc.)
5. Next Renewal Date (ISO 8601 format or null)
6. Category (Entertainment, Productivity, Utilities, Shopping, Health, Education, Other)
7. Cancellation Link (URL if present)

Return JSON:
{
  "serviceName": "string",
  "billingCycle": "string or null",
  "price": number or null,
  "currency": "string or null",
  "nextRenewalDate": "ISO date or null",
  "category": "string",
  "cancellationLink": "string or null",
  "confidence": 0.95
}

If you cannot extract a field with confidence, set it to null.
```

### Confidence Thresholds

- **High Confidence (≥ 0.85):** Auto-create subscription
- **Medium Confidence (0.60-0.84):** Create subscription, flag for review
- **Low Confidence (< 0.60):** Store as potential subscription, require user confirmation

---

## Security Considerations

### OAuth Token Management
- Store tokens in Azure Key Vault, never in database
- Use managed identities for Key Vault access
- Rotate tokens before expiration
- Revoke tokens immediately on user disconnect

### Email Access
- Request minimum required scopes (read-only)
- Never modify or delete user emails
- Process emails in memory, don't persist full content
- Implement audit logging for all email access

### Data Protection
- Encrypt sensitive data at rest (AES-256)
- Use TLS 1.3 for all data in transit
- Implement row-level security in database
- Regular security audits and penetration testing

### GDPR Compliance
- Explicit consent for email access
- Clear privacy policy
- Data portability (JSON export)
- Right to be forgotten (complete deletion within 24 hours)
- Data processing agreements with third parties

---

## Monitoring & Observability

### Key Metrics to Track

**Business Metrics:**
- Daily/Monthly Active Users (DAU/MAU)
- Subscription detection rate
- Alert delivery rate
- Conversion rate (free → paid)
- Churn rate
- Average subscriptions per user
- Average monthly spend per user

**Technical Metrics:**
- API response times (p50, p95, p99)
- Email processing rate (emails/minute)
- AI extraction accuracy
- Background job success rate
- Database query performance
- Error rates by endpoint
- Cache hit rates

**Infrastructure Metrics:**
- CPU/Memory utilization
- Database connections
- Queue depth
- Storage usage
- Network throughput

### Alerting Rules

**Critical Alerts (Page immediately):**
- API down (> 5xx errors for 5 minutes)
- Database connection failures
- Email ingestion stopped
- Payment processing failures

**Warning Alerts (Notify during business hours):**
- AI extraction accuracy < 70%
- Alert delivery rate < 90%
- Background job delays > 30 minutes
- High error rates (> 5%)

### Logging Strategy

- **Structured Logging:** Use Serilog with JSON formatting
- **Correlation IDs:** Track requests across services
- **Log Levels:** Debug (dev), Information (prod), Warning, Error, Critical
- **Sensitive Data:** Never log passwords, tokens, or full email content
- **Retention:** 90 days in Application Insights

---

## Deployment Strategy

### Environments

1. **Development:** Local development with Docker Compose
2. **Staging:** Azure environment mirroring production
3. **Production:** Azure with high availability

### CI/CD Pipeline (GitHub Actions)

```yaml
# .github/workflows/deploy.yml
name: Deploy

on:
  push:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - Checkout code
      - Setup .NET 8
      - Restore dependencies
      - Run unit tests
      - Run integration tests
      - Code coverage report

  build:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - Build API project
      - Build Worker project
      - Build Next.js frontend
      - Push Docker images (optional)

  deploy-staging:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - Deploy API to Azure App Service (staging)
      - Deploy Worker to Azure App Service (staging)
      - Deploy Frontend to Vercel (preview)
      - Run smoke tests

  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - Deploy API to Azure App Service (production)
      - Deploy Worker to Azure App Service (production)
      - Deploy Frontend to Vercel (production)
      - Run smoke tests
      - Notify team
```

### Infrastructure as Code (Azure Bicep)

```bicep
// main.bicep
param location string = 'eastus'
param environment string = 'prod'

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: 'asp-subscription-tracker-${environment}'
  location: location
  sku: {
    name: 'B1' // Basic tier for MVP
    tier: 'Basic'
  }
}

// API App Service
resource apiAppService 'Microsoft.Web/sites@2022-03-01' = {
  name: 'app-subscription-api-${environment}'
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
  }
}

// Worker App Service
resource workerAppService 'Microsoft.Web/sites@2022-03-01' = {
  name: 'app-subscription-worker-${environment}'
  location: location
  properties: {
    serverFarmId: appServicePlan.id
  }
}

// Azure SQL Database
resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: 'sql-subscription-${environment}'
  location: location
  properties: {
    administratorLogin: 'sqladmin'
    administratorLoginPassword: '${uniqueString(resourceGroup().id)}'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: 'subscriptions'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: 'kv-subscription-${environment}'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'ai-subscription-${environment}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}
```

---

## Testing Strategy

### Unit Tests (80% coverage target)
- Test all business logic in isolation
- Mock external dependencies
- Use xUnit + FluentAssertions
- Fast execution (< 5 seconds for full suite)

### Integration Tests
- Test API endpoints end-to-end
- Use in-memory database or test database
- Test email ingestion flow
- Test AI extraction with mock responses
- Test background jobs

### Property-Based Tests
- Test data extraction with various email formats
- Test billing cycle normalization
- Test spending calculations
- Use FsCheck or similar library

### End-to-End Tests (Minimal)
- Critical user flows only
- User registration → email connect → subscription discovery
- Subscription creation → alert generation → alert delivery
- Use Playwright or Selenium

### Performance Tests
- Load test API endpoints (100 concurrent users)
- Test email processing throughput
- Test database query performance
- Use k6 or JMeter

---

## Cost Estimation (Optimized for 100 Users)

### MVP Phase (0-100 users) - Ultra-Low Cost

**Azure Services (Monthly):**
- App Service Free Tier (F1): **$0/month**
  - 60 CPU minutes/day
  - 1 GB RAM
  - 1 GB storage
  - Sufficient for 100 users with light usage
- SQLite Database: **$0/month**
  - File-based, no separate database service needed
  - Suitable for < 1000 users
- In-memory caching: **$0/month**
- In-memory job queue: **$0/month**
- **Total Azure: $0/month**

**Third-Party Services (Monthly):**
- OpenAI GPT-4o-mini: **$10-20/month**
  - $0.150 per 1M input tokens
  - $0.600 per 1M output tokens
  - Estimate: 2000 emails/month × 500 tokens = 1M tokens ≈ $15
- SendGrid Free Tier: **$0/month**
  - 100 emails/day = 3000/month
  - Sufficient for 100 users with 2-3 alerts/week each
- Vercel Free Tier: **$0/month**
  - Unlimited bandwidth
  - 100 GB-hours compute
- Stripe: **2.9% + $0.30 per transaction** (only when revenue starts)
- **Total Third-Party: $10-20/month**

### **Total MVP Cost: $10-20/month** 🎉

### Growth Phase (100-1000 users)

**Azure Services (Monthly):**
- App Service Basic B1: **$13/month**
  - 1.75 GB RAM
  - 10 GB storage
  - Always-on capability
- Azure SQL Basic: **$5/month**
  - 2 GB storage
  - 5 DTUs
- Application Insights: **$10/month**
  - 5 GB data ingestion included
- **Total Azure: $28/month**

**Third-Party Services (Monthly):**
- OpenAI GPT-4o-mini: **$50-100/month**
  - 20,000 emails/month processing
- SendGrid Essentials: **$15/month**
  - 50,000 emails/month
- Vercel Pro (optional): **$20/month** or stay on free tier
- **Total Third-Party: $65-115/month**

### **Total Growth Cost: $93-143/month**

### Break-even Analysis

**At $10/month subscription:**
- MVP Phase: Need 1-2 paid users to break even
- Growth Phase: Need 10-15 paid users to break even

**At $15/month subscription:**
- MVP Phase: Need 1 paid user to break even
- Growth Phase: Need 7-10 paid users to break even

**Target Conversion Rate:** 10% (10 paid users out of 100)
**Monthly Revenue at 10% conversion:** $100-150
**Profit Margin at MVP scale:** 80-90%

---

## Success Metrics & KPIs

### MVP Launch (Week 4)
- ✅ 100 beta users signed up
- ✅ 70%+ subscription detection accuracy
- ✅ < 3 minute average onboarding time
- ✅ 2+ dashboard visits per week per user
- ✅ < 5% error rate

### Post-Alerts (Week 6)
- ✅ 95%+ alert delivery rate
- ✅ 50%+ alert open rate
- ✅ 3+ dashboard visits per week per user
- ✅ 80%+ user satisfaction (survey)

### Post-Monetization (Week 8)
- ✅ 5%+ conversion to paid tier
- ✅ < 10% monthly churn
- ✅ $10-20 ARPU
- ✅ 500+ total users

### Post-Enhancement (Week 12)
- ✅ 30%+ cancellation assistant usage
- ✅ 4.0+ satisfaction score
- ✅ < 5% monthly churn
- ✅ 1,000+ total users
- ✅ 10%+ conversion rate

---

## Risk Management

### Technical Risks

**Risk:** Email provider API changes or rate limits
**Mitigation:** Use official APIs with fallback to IMAP, implement robust retry logic

**Risk:** AI extraction accuracy too low
**Mitigation:** Start with high-confidence extractions only, allow manual corrections, improve prompts iteratively

**Risk:** Performance issues with large email volumes
**Mitigation:** Implement pagination, batch processing, caching, and background jobs

**Risk:** Security breach or data leak
**Mitigation:** Follow security best practices, regular audits, penetration testing, bug bounty program

### Business Risks

**Risk:** Low user adoption
**Mitigation:** Focus on clear value proposition, easy onboarding, referral program

**Risk:** High churn rate
**Mitigation:** Deliver consistent value through alerts, add sticky features (insights, cancellation assistant)

**Risk:** Competitors with better features
**Mitigation:** Move fast, focus on differentiation (email-first, privacy-focused), build community

**Risk:** Regulatory changes (GDPR, email access)
**Mitigation:** Stay informed, build compliance from day one, work with legal counsel

---

## Development Workflow for AI Agent

### When Starting a New Feature

1. **Read Context:**
   - Review requirements.md for acceptance criteria
   - Review design.md for architecture decisions
   - Review this PROJECT_VISION.md for overall context

2. **Plan Implementation:**
   - Identify affected layers (Domain, Application, Infrastructure, Presentation)
   - List files to create/modify
   - Consider database migrations needed
   - Plan tests (unit, integration)

3. **Implement Bottom-Up:**
   - Start with Domain entities and value objects
   - Add repository interfaces
   - Implement application services
   - Add infrastructure implementations
   - Create API controllers
   - Write tests throughout

4. **Verify:**
   - Run unit tests
   - Run integration tests
   - Test API endpoints manually
   - Check code coverage
   - Review for security issues

5. **Document:**
   - Update API documentation (Swagger)
   - Add code comments for complex logic
   - Update README if needed

### Code Quality Standards

- **Naming:** Use clear, descriptive names (PascalCase for classes, camelCase for variables)
- **Methods:** Keep methods small (< 20 lines), single responsibility
- **Classes:** Keep classes focused, avoid god objects
- **Comments:** Explain "why", not "what"
- **Error Handling:** Use exceptions for exceptional cases, return results for expected failures
- **Async:** Use async/await for I/O operations
- **Nullability:** Leverage C# nullable reference types
- **Validation:** Validate at boundaries (API, database)

### Git Commit Messages

```
feat: Add email ingestion service for Gmail
fix: Correct billing cycle normalization logic
refactor: Extract AI prompt templates to configuration
test: Add unit tests for subscription service
docs: Update API documentation for alerts endpoint
chore: Upgrade EF Core to 8.0.1
```

---

## Quick Reference Commands

### Development

```bash
# Run API locally (.NET 10)
cd src/SubscriptionTracker.API
dotnet run

# Run Worker locally (.NET 10)
cd src/SubscriptionTracker.Worker
dotnet run

# Run Frontend locally (Next.js 15)
cd frontend
npm run dev

# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Apply migrations (SQLite for MVP)
dotnet ef database update

# Create new migration
dotnet ef migrations add MigrationName

# Switch to Azure SQL (when scaling)
# Update connection string in appsettings.json
# Run: dotnet ef database update
```

### Azure Deployment

```bash
# Login to Azure
az login

# Deploy infrastructure
az deployment group create \
  --resource-group rg-subscription-tracker \
  --template-file infrastructure/main.bicep

# Deploy API
az webapp deployment source config-zip \
  --resource-group rg-subscription-tracker \
  --name app-subscription-api-prod \
  --src api.zip

# View logs
az webapp log tail \
  --resource-group rg-subscription-tracker \
  --name app-subscription-api-prod
```

---

## Conclusion

This project vision document provides a comprehensive blueprint for building the subscription management SaaS platform. The MVP-first approach ensures rapid time-to-market while maintaining extensibility for future enhancements.

**Key Principles:**
- Start small, iterate fast
- Focus on core value proposition
- Build for scale from day one
- Prioritize security and privacy
- Measure everything
- Listen to users

**Next Steps:**
1. Review and approve requirements.md
2. Create detailed design.md
3. Break down into implementation tasks
4. Start with Phase 1 MVP features
5. Launch beta, gather feedback, iterate

**Remember:** The goal is to ship a working MVP in 4 weeks that solves the core problem: helping users avoid surprise subscription charges. Everything else is secondary.

---

*Last Updated: 2025-11-19*
*Version: 1.0*
*Status: Ready for Implementation*
