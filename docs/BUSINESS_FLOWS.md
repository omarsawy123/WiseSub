# WiseSub Business Flows

> **Last Updated**: December 1, 2025  
> **Maintainer**: AI Agents must update this document when adding/modifying flows

This document describes all business flows in the WiseSub application. Each flow includes an overview and ASCII diagram showing the sequence of operations.

---

## Table of Contents

1. [Authentication Flow](#1-authentication-flow)
2. [Email Account Connection Flow](#2-email-account-connection-flow)
3. [Email Scanning Flow](#3-email-scanning-flow)
4. [AI Extraction Flow](#4-ai-extraction-flow)
5. [Subscription Management Flow](#5-subscription-management-flow)
6. [Alert Generation Flow](#6-alert-generation-flow)
7. [Email Notification Flow](#7-email-notification-flow)
8. [Dashboard & Insights Flow](#8-dashboard--insights-flow)
9. [User Data Management Flow (GDPR)](#9-user-data-management-flow-gdpr)
10. [Vendor Metadata Flow](#10-vendor-metadata-flow)
11. [Subscription Tier Management Flow](#11-subscription-tier-management-flow)

---

## 1. Authentication Flow

### Overview
Users authenticate via Google OAuth 2.0. The system exchanges authorization codes for tokens, creates/retrieves user records, and issues JWT tokens for API access.

### Components
- `AuthController` - API endpoints
- `GoogleAuthenticationService` - OAuth implementation
- `UserService` - User management
- `TokenEncryptionService` - Token security

### Endpoints
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/google` | Exchange auth code for JWT |
| POST | `/api/auth/refresh` | Refresh expired JWT |
| POST | `/api/auth/logout` | Logout and revoke tokens |
| GET | `/api/auth/me` | Get current user profile |

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         AUTHENTICATION FLOW                                  │
└─────────────────────────────────────────────────────────────────────────────┘

  Frontend                   Backend                      Google
     │                          │                            │
     │  1. Click "Login"        │                            │
     │─────────────────────────>│                            │
     │                          │                            │
     │  2. Redirect URL         │                            │
     │<─────────────────────────│                            │
     │                          │                            │
     │  3. Redirect to Google   │                            │
     │──────────────────────────────────────────────────────>│
     │                          │                            │
     │  4. User authorizes      │                            │
     │<──────────────────────────────────────────────────────│
     │                          │                            │
     │  5. POST /auth/google    │                            │
     │     {authorizationCode}  │                            │
     │─────────────────────────>│                            │
     │                          │  6. Exchange code          │
     │                          │───────────────────────────>│
     │                          │                            │
     │                          │  7. Access + Refresh Token │
     │                          │<───────────────────────────│
     │                          │                            │
     │                          │  8. Get user info          │
     │                          │───────────────────────────>│
     │                          │                            │
     │                          │  9. User profile           │
     │                          │<───────────────────────────│
     │                          │                            │
     │                          │  10. Create/Update User    │
     │                          │      (UserService)         │
     │                          │                            │
     │                          │  11. Encrypt tokens        │
     │                          │      (AES-256)             │
     │                          │                            │
     │                          │  12. Generate JWT          │
     │                          │      (24h expiry)          │
     │                          │                            │
     │  13. JWT + User Info     │                            │
     │<─────────────────────────│                            │
     │                          │                            │
     ▼                          ▼                            ▼

TOKEN REFRESH FLOW:
┌──────────────────────────────────────────────────────────────┐
│  Frontend                    Backend                         │
│     │                           │                            │
│     │  POST /auth/refresh       │                            │
│     │  {refreshToken}           │                            │
│     │──────────────────────────>│                            │
│     │                           │                            │
│     │                           │  Validate refresh token    │
│     │                           │  Generate new JWT          │
│     │                           │                            │
│     │  New JWT + Refresh Token  │                            │
│     │<──────────────────────────│                            │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. Email Account Connection Flow

### Overview
Users connect their Gmail accounts to allow subscription scanning. OAuth tokens are encrypted and stored for background processing.

### Components
- `EmailAccountController` - API endpoints (planned)
- `GmailClient` - Gmail API integration
- `EmailAccountRepository` - Account storage
- `TokenEncryptionService` - Token security

### Endpoints
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/email-accounts/connect` | Connect Gmail account |
| GET | `/api/email-accounts` | List connected accounts |
| DELETE | `/api/email-accounts/{id}` | Disconnect account |
| POST | `/api/email-accounts/{id}/refresh` | Refresh OAuth tokens |

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      EMAIL ACCOUNT CONNECTION FLOW                           │
└─────────────────────────────────────────────────────────────────────────────┘

  Frontend                   Backend                      Gmail API
     │                          │                            │
     │  1. Click "Connect       │                            │
     │     Gmail"               │                            │
     │─────────────────────────>│                            │
     │                          │                            │
     │  2. OAuth URL (Gmail     │                            │
     │     scopes: readonly)    │                            │
     │<─────────────────────────│                            │
     │                          │                            │
     │  3. User authorizes      │                            │
     │     Gmail access         │                            │
     │──────────────────────────────────────────────────────>│
     │                          │                            │
     │  4. Authorization code   │                            │
     │<──────────────────────────────────────────────────────│
     │                          │                            │
     │  5. POST /email-accounts │                            │
     │     /connect             │                            │
     │─────────────────────────>│                            │
     │                          │  6. Exchange code          │
     │                          │───────────────────────────>│
     │                          │                            │
     │                          │  7. Access + Refresh Token │
     │                          │<───────────────────────────│
     │                          │                            │
     │                          │  8. Get email profile      │
     │                          │───────────────────────────>│
     │                          │                            │
     │                          │  9. Email address          │
     │                          │<───────────────────────────│
     │                          │                            │
     │                          │  10. Check duplicate       │
     │                          │      (EmailAccountRepo)    │
     │                          │                            │
     │                          │  11. Encrypt tokens        │
     │                          │      (AES-256)             │
     │                          │                            │
     │                          │  12. Save EmailAccount     │
     │                          │      entity                │
     │                          │                            │
     │  13. Account connected   │                            │
     │      {id, email}         │                            │
     │<─────────────────────────│                            │
     │                          │                            │
     │                          │  14. Trigger initial       │
     │                          │      email scan (12 mo)    │
     │                          │                            │
     ▼                          ▼                            ▼
```

---

## 3. Email Scanning Flow

### Overview
Background service scans connected email accounts for subscription-related emails. Emails are queued for AI processing with priority handling.

### Components
- `EmailIngestionService` - Email fetching
- `EmailQueueService` - Priority queue
- `EmailProcessorService` - Background worker
- `GmailClient` - Gmail API
- `EmailMetadataService` - Deduplication

### Configuration
```json
{
  "EmailScan": {
    "InitialScanMonths": 12,
    "BatchSize": 50,
    "ProcessingIntervalSeconds": 30
  }
}
```

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          EMAIL SCANNING FLOW                                 │
└─────────────────────────────────────────────────────────────────────────────┘

                    ┌─────────────────────────────────────┐
                    │     EmailProcessorService           │
                    │     (Background Worker)             │
                    └─────────────────────────────────────┘
                                    │
                                    │ 1. WaitForEmailAsync()
                                    │    (event-driven, not polling)
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         EMAIL INGESTION PIPELINE                             │
│                                                                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐                  │
│  │ EmailAccount │───>│ GmailClient  │───>│ EmailQueue   │                  │
│  │  Repository  │    │              │    │   Service    │                  │
│  └──────────────┘    └──────────────┘    └──────────────┘                  │
│         │                   │                   │                           │
│         │                   │                   │                           │
│  2. Get accounts     3. Fetch emails     4. Queue with                     │
│     with valid          (batch of 50)       priority                       │
│     tokens                                                                  │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         EMAIL QUEUE PROCESSING                               │
│                                                                              │
│  Priority Levels:                                                            │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  HIGH (1)    │  Renewal notices, payment confirmations              │   │
│  │  MEDIUM (2)  │  Subscription updates, price changes                 │   │
│  │  LOW (3)     │  Marketing, newsletters                              │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  Queue Structure:                                                            │
│  ┌────────┬────────┬────────┬────────┬────────┐                            │
│  │ HIGH   │ HIGH   │ MEDIUM │ MEDIUM │  LOW   │  ──> Process order         │
│  └────────┴────────┴────────┴────────┴────────┘                            │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 5. DequeueNextEmailAsync()
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         DEDUPLICATION CHECK                                  │
│                                                                              │
│  EmailMetadataService:                                                       │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  Check ExternalEmailId (unique index)                               │   │
│  │  If exists → Skip (already processed)                               │   │
│  │  If new → Continue to AI extraction                                 │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 6. Pass to AI Extraction
                                    ▼
                         [See AI Extraction Flow]
```

---

## 4. AI Extraction Flow

### Overview
OpenAI classifies emails as subscription-related and extracts structured data. Confidence scoring determines if user review is needed.

### Components
- `AIExtractionService` - Business logic
- `OpenAIClient` - API wrapper
- `ClassificationResult` - Classification output
- `ExtractionResult` - Extracted data

### Confidence Scoring
| Field | Weight |
|-------|--------|
| Service Name | 25% |
| Price | 25% |
| Billing Cycle | 20% |
| Next Renewal | 15% |
| Category | 10% |
| Currency | 5% |

**Threshold**: < 60% confidence → Flag for user review

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          AI EXTRACTION FLOW                                  │
└─────────────────────────────────────────────────────────────────────────────┘

  Email Content                AIExtractionService              OpenAI API
       │                              │                              │
       │  1. Email body               │                              │
       │     (truncated to 2000 ch)   │                              │
       │─────────────────────────────>│                              │
       │                              │                              │
       │                              │  2. ClassifyEmailAsync()     │
       │                              │     Prompt: "Is this         │
       │                              │     subscription-related?"   │
       │                              │─────────────────────────────>│
       │                              │                              │
       │                              │  3. Classification result    │
       │                              │     {isSubscription: bool,   │
       │                              │      confidence: 0.0-1.0,    │
       │                              │      emailType: string}      │
       │                              │<─────────────────────────────│
       │                              │                              │
       │                              │                              │
       │              ┌───────────────┴───────────────┐              │
       │              │                               │              │
       │         NOT subscription              IS subscription       │
       │              │                               │              │
       │              ▼                               ▼              │
       │     ┌─────────────┐              ┌─────────────────┐       │
       │     │ Skip email  │              │ Continue to     │       │
       │     │ Log & exit  │              │ extraction      │       │
       │     └─────────────┘              └─────────────────┘       │
       │                                          │                  │
       │                                          │                  │
       │                              │  4. ExtractSubscriptionData  │
       │                              │     Prompt: Extract fields   │
       │                              │     (body truncated 3000 ch) │
       │                              │─────────────────────────────>│
       │                              │                              │
       │                              │  5. Extraction result        │
       │                              │<─────────────────────────────│
       │                              │                              │
       ▼                              ▼                              ▼

┌─────────────────────────────────────────────────────────────────────────────┐
│                         EXTRACTION RESULT                                    │
│                                                                              │
│  {                                                                           │
│    "serviceName": "Netflix",                                                 │
│    "price": 15.99,                                                          │
│    "currency": "USD",                                                        │
│    "billingCycle": "Monthly",                                               │
│    "nextRenewalDate": "2025-01-15",                                         │
│    "category": "Entertainment",                                              │
│    "cancellationLink": "https://...",                                       │
│    "overallConfidence": 0.85,                                               │
│    "requiresUserReview": false                                              │
│  }                                                                           │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │
              ┌─────────────────────┴─────────────────────┐
              │                                           │
         Confidence >= 60%                         Confidence < 60%
              │                                           │
              ▼                                           ▼
    ┌─────────────────┐                       ┌─────────────────┐
    │ Auto-create     │                       │ Create pending  │
    │ subscription    │                       │ subscription    │
    │                 │                       │ (needs review)  │
    └─────────────────┘                       └─────────────────┘
              │                                           │
              └─────────────────────┬─────────────────────┘
                                    │
                                    ▼
                      [See Subscription Management Flow]
```

---

## 5. Subscription Management Flow

### Overview
Manages subscription lifecycle: creation, updates, deduplication, and archival. Handles both auto-detected and manually added subscriptions.

### Components
- `SubscriptionController` - API endpoints (planned)
- `SubscriptionService` - Business logic (planned)
- `SubscriptionRepository` - Data access
- `VendorMetadataRepository` - Vendor matching

### Endpoints
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/subscriptions` | List user subscriptions |
| GET | `/api/subscriptions/{id}` | Get subscription details |
| POST | `/api/subscriptions` | Add manual subscription |
| PUT | `/api/subscriptions/{id}` | Update subscription |
| DELETE | `/api/subscriptions/{id}` | Archive subscription |
| POST | `/api/subscriptions/{id}/confirm` | Confirm pending subscription |

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      SUBSCRIPTION MANAGEMENT FLOW                            │
└─────────────────────────────────────────────────────────────────────────────┘

                         ┌─────────────────────┐
                         │   Subscription      │
                         │   Input Source      │
                         └─────────────────────┘
                                    │
              ┌─────────────────────┼─────────────────────┐
              │                     │                     │
              ▼                     ▼                     ▼
    ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐
    │ AI Extraction   │   │ Manual Entry    │   │ Pending Review  │
    │ (auto-detected) │   │ (user input)    │   │ (low confidence)│
    └─────────────────┘   └─────────────────┘   └─────────────────┘
              │                     │                     │
              └─────────────────────┼─────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         DEDUPLICATION CHECK                                  │
│                                                                              │
│  VendorMetadataRepository:                                                   │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  1. Normalize service name (lowercase, trim)                        │   │
│  │  2. Check VendorMetadata.NormalizedName index                       │   │
│  │  3. Match against existing subscriptions for user                   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  Matching Logic:                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  - Exact name match → Update existing                               │   │
│  │  - Vendor alias match → Update existing                             │   │
│  │  - No match → Create new subscription                               │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
              ┌─────────────────────┴─────────────────────┐
              │                                           │
         New Subscription                        Existing Subscription
              │                                           │
              ▼                                           ▼
┌─────────────────────────────┐           ┌─────────────────────────────┐
│  CREATE SUBSCRIPTION        │           │  UPDATE SUBSCRIPTION        │
│                             │           │                             │
│  - Generate GUID            │           │  - Compare price            │
│  - Set Status = Active      │           │  - Update renewal date      │
│  - Set DetectionSource      │           │  - Log to History           │
│  - Calculate next renewal   │           │                             │
│  - Save to database         │           │                             │
└─────────────────────────────┘           └─────────────────────────────┘
              │                                           │
              └─────────────────────┬─────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      SUBSCRIPTION HISTORY TRACKING                           │
│                                                                              │
│  SubscriptionHistory entity records:                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  - Price changes (old price → new price)                            │   │
│  │  - Status changes (Active → Cancelled → Active)                     │   │
│  │  - Billing cycle changes                                            │   │
│  │  - Timestamp of each change                                         │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
                         [Trigger Alert Generation]

SUBSCRIPTION STATES:
┌─────────────────────────────────────────────────────────────────────────────┐
│                                                                              │
│    ┌────────┐     ┌────────────┐     ┌───────────┐     ┌──────────┐       │
│    │ Active │────>│ Cancelled  │────>│ Archived  │     │ Pending  │       │
│    └────────┘     └────────────┘     └───────────┘     └──────────┘       │
│         │              │                                     │             │
│         │              │                                     │             │
│         └──────────────┘                                     │             │
│              (reactivate)                                    │             │
│                                                              │             │
│         ┌────────────────────────────────────────────────────┘             │
│         │ (user confirms)                                                   │
│         ▼                                                                   │
│    ┌────────┐                                                               │
│    │ Active │                                                               │
│    └────────┘                                                               │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 6. Alert Generation Flow

### Overview
System generates alerts for upcoming renewals, price changes, trial expirations, and unused subscriptions. Alerts are scheduled and processed by background jobs.

### Components
- `AlertService` - Alert generation (planned)
- `AlertRepository` - Alert storage
- `AlertProcessorService` - Background worker (planned)

### Alert Types
| Type | Trigger | Default Lead Time |
|------|---------|-------------------|
| Renewal | Upcoming renewal date | 7 days |
| PriceChange | Price differs from last | Immediate |
| TrialExpiring | Trial end date approaching | 3 days |
| UnusedSubscription | No activity detected | 30 days |

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          ALERT GENERATION FLOW                               │
└─────────────────────────────────────────────────────────────────────────────┘

                    ┌─────────────────────────────────────┐
                    │     AlertProcessorService           │
                    │     (Background Worker)             │
                    │     Runs: Every hour                │
                    └─────────────────────────────────────┘
                                    │
                                    │ 1. Get all active subscriptions
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ALERT EVALUATION                                     │
│                                                                              │
│  For each subscription:                                                      │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐ │   │
│  │  │ Check Renewal   │    │ Check Price     │    │ Check Trial     │ │   │
│  │  │ Date            │    │ Change          │    │ Expiry          │ │   │
│  │  └────────┬────────┘    └────────┬────────┘    └────────┬────────┘ │   │
│  │           │                      │                      │          │   │
│  │           ▼                      ▼                      ▼          │   │
│  │  NextRenewal within      Price != LastPrice     IsTrial &&        │   │
│  │  user's lead time?       in History?            TrialEnd within   │   │
│  │                                                 3 days?           │   │
│  │                                                                    │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  ┌─────────────────┐                                                │   │
│  │  │ Check Unused    │                                                │   │
│  │  │ Subscription    │                                                │   │
│  │  └────────┬────────┘                                                │   │
│  │           │                                                          │   │
│  │           ▼                                                          │   │
│  │  No email activity                                                   │   │
│  │  for 30+ days?                                                       │   │
│  │                                                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 2. Create alerts for triggered conditions
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ALERT CREATION                                       │
│                                                                              │
│  Alert Entity:                                                               │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  {                                                                   │   │
│  │    "id": "guid",                                                     │   │
│  │    "userId": "user-guid",                                            │   │
│  │    "subscriptionId": "sub-guid",                                     │   │
│  │    "type": "Renewal | PriceChange | TrialExpiring | Unused",        │   │
│  │    "message": "Netflix renews in 7 days for $15.99",                │   │
│  │    "scheduledFor": "2025-01-08T09:00:00Z",                          │   │
│  │    "status": "Pending | Sent | Failed | Dismissed"                  │   │
│  │  }                                                                   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  Deduplication:                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  - Check if alert already exists for same subscription + type       │   │
│  │  - Check if alert already sent within cooldown period               │   │
│  │  - Skip if user dismissed similar alert recently                    │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 3. Queue for notification
                                    ▼
                      [See Email Notification Flow]

USER PREFERENCES (stored in User.PreferencesJson):
┌─────────────────────────────────────────────────────────────────────────────┐
│  {                                                                           │
│    "enableRenewalAlerts": true,                                             │
│    "renewalAlertDays": 7,                                                   │
│    "enablePriceChangeAlerts": true,                                         │
│    "enableTrialAlerts": true,                                               │
│    "enableUnusedAlerts": true,                                              │
│    "unusedThresholdDays": 30,                                               │
│    "alertDeliveryMethod": "Email | Push | Both"                             │
│  }                                                                           │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 7. Email Notification Flow

### Overview
Sends alert notifications via email using SendGrid. Handles templating, delivery tracking, and retry logic.

### Components
- `NotificationService` - Notification orchestration (planned)
- `SendGridClient` - Email delivery (planned)
- `EmailTemplateService` - Template rendering (planned)

### Configuration
```json
{
  "SendGrid": {
    "ApiKey": "SG.xxx",
    "FromEmail": "alerts@wisesub.com",
    "FromName": "WiseSub Alerts"
  }
}
```

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       EMAIL NOTIFICATION FLOW                                │
└─────────────────────────────────────────────────────────────────────────────┘

                    ┌─────────────────────────────────────┐
                    │     NotificationService             │
                    │     (Background Worker)             │
                    │     Runs: Every 5 minutes           │
                    └─────────────────────────────────────┘
                                    │
                                    │ 1. Get pending alerts
                                    │    WHERE ScheduledFor <= NOW
                                    │    AND Status = Pending
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ALERT PROCESSING                                     │
│                                                                              │
│  For each pending alert:                                                     │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  1. Load user preferences                                           │   │
│  │  2. Check if alert type is enabled                                  │   │
│  │  3. Get user email address                                          │   │
│  │  4. Load subscription details                                       │   │
│  │                                                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 2. Render email template
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         EMAIL TEMPLATING                                     │
│                                                                              │
│  Template Variables:                                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  {{userName}}        - User's display name                          │   │
│  │  {{serviceName}}     - Subscription service name                    │   │
│  │  {{price}}           - Current price with currency                  │   │
│  │  {{renewalDate}}     - Next renewal date formatted                  │   │
│  │  {{daysUntil}}       - Days until renewal                           │   │
│  │  {{oldPrice}}        - Previous price (for price change)            │   │
│  │  {{priceChange}}     - Difference (+$2.00 or -$1.00)                │   │
│  │  {{cancellationUrl}} - Direct link to cancel                        │   │
│  │  {{dashboardUrl}}    - Link to WiseSub dashboard                    │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  Templates by Alert Type:                                                    │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  - renewal_reminder.html                                            │   │
│  │  - price_change_alert.html                                          │   │
│  │  - trial_expiring.html                                              │   │
│  │  - unused_subscription.html                                         │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 3. Send via SendGrid
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         SENDGRID DELIVERY                                    │
│                                                                              │
│  NotificationService                SendGrid API                             │
│        │                                 │                                   │
│        │  POST /v3/mail/send             │                                   │
│        │  {                              │                                   │
│        │    "to": "user@email.com",      │                                   │
│        │    "from": "alerts@wisesub.com",│                                   │
│        │    "subject": "...",            │                                   │
│        │    "html": "..."                │                                   │
│        │  }                              │                                   │
│        │────────────────────────────────>│                                   │
│        │                                 │                                   │
│        │  Response: 202 Accepted         │                                   │
│        │<────────────────────────────────│                                   │
│        │                                 │                                   │
│                                                                              │
│  Retry Logic:                                                                │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  - Max retries: 3                                                   │   │
│  │  - Backoff: 1s, 2s, 4s (exponential)                               │   │
│  │  - On failure: Set Alert.Status = Failed                           │   │
│  │  - On success: Set Alert.Status = Sent, SentAt = NOW               │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 4. Update alert status
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         STATUS UPDATE                                        │
│                                                                              │
│  AlertRepository.UpdateAsync(alert):                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  alert.Status = Sent                                                │   │
│  │  alert.SentAt = DateTime.UtcNow                                     │   │
│  │  alert.DeliveryAttempts++                                           │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 8. Dashboard & Insights Flow

### Overview
Aggregates subscription data to provide spending insights, trends, and analytics for the user dashboard.

### Components
- `DashboardController` - API endpoints (planned)
- `InsightsService` - Analytics calculations (planned)
- `SubscriptionRepository` - Data queries

### Endpoints
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/dashboard/summary` | Overview stats |
| GET | `/api/dashboard/spending` | Spending breakdown |
| GET | `/api/dashboard/trends` | Historical trends |
| GET | `/api/dashboard/upcoming` | Upcoming renewals |

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       DASHBOARD & INSIGHTS FLOW                              │
└─────────────────────────────────────────────────────────────────────────────┘

  Frontend                   Backend                      Database
     │                          │                            │
     │  GET /dashboard/summary  │                            │
     │─────────────────────────>│                            │
     │                          │                            │
     │                          │  Query active subs         │
     │                          │───────────────────────────>│
     │                          │                            │
     │                          │  Subscription list         │
     │                          │<───────────────────────────│
     │                          │                            │
     │                          │                            │
     ▼                          ▼                            ▼

┌─────────────────────────────────────────────────────────────────────────────┐
│                         SUMMARY CALCULATION                                  │
│                                                                              │
│  InsightsService.GetSummaryAsync():                                          │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  Total Monthly Spend:                                               │   │
│  │  ┌─────────────────────────────────────────────────────────────┐   │   │
│  │  │  SUM(                                                        │   │   │
│  │  │    CASE BillingCycle                                         │   │   │
│  │  │      WHEN Weekly   THEN Price * 4.33                         │   │   │
│  │  │      WHEN Monthly  THEN Price                                │   │   │
│  │  │      WHEN Quarterly THEN Price / 3                           │   │   │
│  │  │      WHEN Annual   THEN Price / 12                           │   │   │
│  │  │    END                                                       │   │   │
│  │  │  ) WHERE Status = Active                                     │   │   │
│  │  └─────────────────────────────────────────────────────────────┘   │   │
│  │                                                                      │   │
│  │  Total Annual Spend:                                                │   │
│  │  ┌─────────────────────────────────────────────────────────────┐   │   │
│  │  │  Monthly Spend * 12                                          │   │   │
│  │  └─────────────────────────────────────────────────────────────┘   │   │
│  │                                                                      │   │
│  │  Active Subscriptions Count:                                        │   │
│  │  ┌─────────────────────────────────────────────────────────────┐   │   │
│  │  │  COUNT(*) WHERE Status = Active                              │   │   │
│  │  └─────────────────────────────────────────────────────────────┘   │   │
│  │                                                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         SPENDING BREAKDOWN                                   │
│                                                                              │
│  GET /dashboard/spending?groupBy=category                                    │
│                                                                              │
│  Response:                                                                   │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  {                                                                   │   │
│  │    "totalMonthly": 156.99,                                          │   │
│  │    "currency": "USD",                                                │   │
│  │    "breakdown": [                                                    │   │
│  │      { "category": "Entertainment", "amount": 45.99, "percent": 29 },│   │
│  │      { "category": "Productivity", "amount": 35.00, "percent": 22 }, │   │
│  │      { "category": "Cloud Storage", "amount": 25.00, "percent": 16 },│   │
│  │      { "category": "News", "amount": 20.00, "percent": 13 },        │   │
│  │      { "category": "Other", "amount": 31.00, "percent": 20 }        │   │
│  │    ]                                                                 │   │
│  │  }                                                                   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         HISTORICAL TRENDS                                    │
│                                                                              │
│  GET /dashboard/trends?months=12                                             │
│                                                                              │
│  Data Source: SubscriptionHistory table                                      │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  Monthly Spending Over Time:                                        │   │
│  │                                                                      │   │
│  │  $200 │                                    ╭─────╮                  │   │
│  │       │                              ╭─────╯     │                  │   │
│  │  $150 │                        ╭─────╯           │                  │   │
│  │       │                  ╭─────╯                 │                  │   │
│  │  $100 │            ╭─────╯                       ╰─────╮            │   │
│  │       │      ╭─────╯                                   │            │   │
│  │   $50 │╭─────╯                                         ╰─────       │   │
│  │       │                                                             │   │
│  │    $0 └─────────────────────────────────────────────────────────   │   │
│  │        Jan  Feb  Mar  Apr  May  Jun  Jul  Aug  Sep  Oct  Nov  Dec   │   │
│  │                                                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         UPCOMING RENEWALS                                    │
│                                                                              │
│  GET /dashboard/upcoming?days=30                                             │
│                                                                              │
│  Response:                                                                   │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  {                                                                   │   │
│  │    "upcomingTotal": 89.97,                                          │   │
│  │    "renewals": [                                                     │   │
│  │      {                                                               │   │
│  │        "serviceName": "Netflix",                                     │   │
│  │        "price": 15.99,                                              │   │
│  │        "renewalDate": "2025-12-08",                                 │   │
│  │        "daysUntil": 7                                               │   │
│  │      },                                                              │   │
│  │      {                                                               │   │
│  │        "serviceName": "Spotify",                                     │   │
│  │        "price": 10.99,                                              │   │
│  │        "renewalDate": "2025-12-15",                                 │   │
│  │        "daysUntil": 14                                              │   │
│  │      }                                                               │   │
│  │    ]                                                                 │   │
│  │  }                                                                   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 9. User Data Management Flow (GDPR)

### Overview
Handles GDPR compliance including data export (Right to Access) and data deletion (Right to be Forgotten).

### Components
- `UserService` - Data export/deletion
- `UserController` - API endpoints (planned)
- All repositories - Cascading deletion

### Endpoints
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users/me/export` | Export all user data |
| DELETE | `/api/users/me` | Delete all user data |

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    USER DATA MANAGEMENT FLOW (GDPR)                          │
└─────────────────────────────────────────────────────────────────────────────┘

═══════════════════════════════════════════════════════════════════════════════
                         DATA EXPORT (Right to Access)
═══════════════════════════════════════════════════════════════════════════════

  Frontend                   Backend                      Database
     │                          │                            │
     │  GET /users/me/export    │                            │
     │─────────────────────────>│                            │
     │                          │                            │
     │                          │  UserService               │
     │                          │  .ExportUserDataAsync()    │
     │                          │                            │
     │                          │  1. Get User               │
     │                          │───────────────────────────>│
     │                          │<───────────────────────────│
     │                          │                            │
     │                          │  2. Get EmailAccounts      │
     │                          │───────────────────────────>│
     │                          │<───────────────────────────│
     │                          │                            │
     │                          │  3. Get Subscriptions      │
     │                          │───────────────────────────>│
     │                          │<───────────────────────────│
     │                          │                            │
     │                          │  4. Get Alerts             │
     │                          │───────────────────────────>│
     │                          │<───────────────────────────│
     │                          │                            │
     │                          │  5. Build JSON export      │
     │                          │                            │
     │  JSON file download      │                            │
     │<─────────────────────────│                            │
     │                          │                            │

Export Format:
┌─────────────────────────────────────────────────────────────────────────────┐
│  {                                                                           │
│    "exportDate": "2025-12-01T10:00:00Z",                                    │
│    "user": {                                                                 │
│      "id": "user-guid",                                                      │
│      "email": "user@example.com",                                           │
│      "name": "John Doe",                                                     │
│      "tier": "Free",                                                         │
│      "createdAt": "2025-01-15T08:00:00Z",                                   │
│      "preferences": { ... }                                                  │
│    },                                                                        │
│    "emailAccounts": [                                                        │
│      { "email": "john@gmail.com", "connectedAt": "..." }                    │
│    ],                                                                        │
│    "subscriptions": [                                                        │
│      { "serviceName": "Netflix", "price": 15.99, ... }                      │
│    ],                                                                        │
│    "alerts": [                                                               │
│      { "type": "Renewal", "message": "...", "sentAt": "..." }               │
│    ]                                                                         │
│  }                                                                           │
└─────────────────────────────────────────────────────────────────────────────┘

═══════════════════════════════════════════════════════════════════════════════
                      DATA DELETION (Right to be Forgotten)
═══════════════════════════════════════════════════════════════════════════════

  Frontend                   Backend                      Database
     │                          │                            │
     │  DELETE /users/me        │                            │
     │  (with confirmation)     │                            │
     │─────────────────────────>│                            │
     │                          │                            │
     │                          │  UserService               │
     │                          │  .DeleteUserDataAsync()    │
     │                          │                            │
     │                          │                            │
     ▼                          ▼                            ▼

┌─────────────────────────────────────────────────────────────────────────────┐
│                         CASCADING DELETION ORDER                             │
│                                                                              │
│  Transaction Scope (all or nothing):                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  Step 1: Delete EmailMetadata                                       │   │
│  │  ┌─────────────────────────────────────────────────────────────┐   │   │
│  │  │  DELETE FROM EmailMetadata                                   │   │   │
│  │  │  WHERE EmailAccountId IN (user's accounts)                   │   │   │
│  │  └─────────────────────────────────────────────────────────────┘   │   │
│  │                                                                      │   │
│  │  Step 2: Delete Alerts                                              │   │
│  │  ┌─────────────────────────────────────────────────────────────┐   │   │
│  │  │  DELETE FROM Alerts WHERE UserId = @userId                   │   │   │
│  │  └─────────────────────────────────────────────────────────────┘   │   │
│  │                                                                      │   │
│  │  Step 3: Delete SubscriptionHistory                                 │   │
│  │  ┌─────────────────────────────────────────────────────────────┐   │   │
│  │  │  DELETE FROM SubscriptionHistory                             │   │   │
│  │  │  WHERE SubscriptionId IN (user's subscriptions)              │   │   │
│  │  └─────────────────────────────────────────────────────────────┘   │   │
│  │                                                                      │   │
│  │  Step 4: Delete Subscriptions                                       │   │
│  │  ┌─────────────────────────────────────────────────────────────┐   │   │
│  │  │  DELETE FROM Subscriptions WHERE UserId = @userId            │   │   │
│  │  └─────────────────────────────────────────────────────────────┘   │   │
│  │                                                                      │   │
│  │  Step 5: Delete EmailAccounts (includes encrypted tokens)           │   │
│  │  ┌─────────────────────────────────────────────────────────────┐   │   │
│  │  │  DELETE FROM EmailAccounts WHERE UserId = @userId            │   │   │
│  │  └─────────────────────────────────────────────────────────────┘   │   │
│  │                                                                      │   │
│  │  Step 6: Delete User                                                │   │
│  │  ┌─────────────────────────────────────────────────────────────┐   │   │
│  │  │  DELETE FROM Users WHERE Id = @userId                        │   │   │
│  │  └─────────────────────────────────────────────────────────────┘   │   │
│  │                                                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  On Success: Commit transaction                                              │
│  On Failure: Rollback all changes                                            │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         POST-DELETION ACTIONS                                │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  1. Revoke Google OAuth tokens (if still valid)                     │   │
│  │  2. Invalidate all JWT tokens (via token blacklist)                 │   │
│  │  3. Log deletion event for audit trail                              │   │
│  │  4. Send confirmation email (optional, to backup email)             │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 10. Vendor Metadata Flow

### Overview
Manages vendor information including matching service names to known vendors, creating fallback entries for unknown vendors, and enriching vendor data with logos and website URLs.

### Components
- `VendorMetadataService` - Business logic for vendor matching and enrichment
- `VendorMetadataRepository` - Data access with caching
- `VendorEnrichmentJob` - Background job for enrichment

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         VENDOR METADATA FLOW                                 │
└─────────────────────────────────────────────────────────────────────────────┘

  Subscription Service              VendorMetadataService           Database
         │                                  │                          │
         │  1. MatchVendorAsync             │                          │
         │     ("Netflix")                  │                          │
         │─────────────────────────────────>│                          │
         │                                  │                          │
         │                                  │  2. Normalize name       │
         │                                  │     "netflix"            │
         │                                  │                          │
         │                                  │  3. Exact match lookup   │
         │                                  │─────────────────────────>│
         │                                  │                          │
         │                                  │  4. Vendor found?        │
         │                                  │<─────────────────────────│
         │                                  │                          │
         │              ┌───────────────────┴───────────────────┐      │
         │              │                                       │      │
         │         YES (exact)                             NO (fuzzy)  │
         │              │                                       │      │
         │              ▼                                       ▼      │
         │     ┌─────────────┐                      ┌─────────────────┐│
         │     │ Return      │                      │ Get all vendors ││
         │     │ vendor      │                      │ from cache      ││
         │     └─────────────┘                      └─────────────────┘│
         │                                                  │          │
         │                                                  ▼          │
         │                                      ┌─────────────────────┐│
         │                                      │ Fuzzy match (85%    ││
         │                                      │ similarity)         ││
         │                                      └─────────────────────┘│
         │                                                  │          │
         │              ┌───────────────────────────────────┤          │
         │              │                                   │          │
         │         MATCH FOUND                        NO MATCH         │
         │              │                                   │          │
         │              ▼                                   ▼          │
         │     ┌─────────────┐                      ┌─────────────────┐│
         │     │ Return      │                      │ Return null     ││
         │     │ vendor      │                      │ (fallback)      ││
         │     └─────────────┘                      └─────────────────┘│
         │                                                             │
         │<────────────────────────────────────────────────────────────│
         │                                                             │
         ▼                                                             ▼

┌─────────────────────────────────────────────────────────────────────────────┐
│                      FALLBACK: CREATE FROM SERVICE NAME                      │
│                                                                              │
│  GetOrCreateFromServiceNameAsync("Unknown Service"):                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  1. Try MatchVendorAsync() first                                    │   │
│  │  2. If no match → Create new VendorMetadata                         │   │
│  │     - Name = service name                                           │   │
│  │     - NormalizedName = lowercase, no suffixes                       │   │
│  │     - Category = "Other" (default)                                  │   │
│  │  3. Queue for background enrichment                                 │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                      BACKGROUND: VENDOR ENRICHMENT                           │
│                                                                              │
│  VendorEnrichmentJob (runs weekly):                                          │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  For each vendor missing logo/website:                              │   │
│  │  ┌─────────────────────────────────────────────────────────────┐   │   │
│  │  │  1. Check known vendor mappings (Netflix → netflix.com)      │   │   │
│  │  │  2. Generate website URL from name                           │   │   │
│  │  │  3. Generate favicon URL using Google's service              │   │   │
│  │  │  4. Update vendor record                                     │   │   │
│  │  │  5. Invalidate cache                                         │   │   │
│  │  └─────────────────────────────────────────────────────────────┘   │   │
│  │                                                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

NAME NORMALIZATION:
┌─────────────────────────────────────────────────────────────────────────────┐
│  Input                    │  Output                                         │
│  ─────────────────────────┼─────────────────────────────────────────────── │
│  "Netflix Inc"            │  "netflix"                                      │
│  "Netflix Inc."           │  "netflix"                                      │
│  "Spotify LLC"            │  "spotify"                                      │
│  "Disney+"                │  "disney"                                       │
│  "HBO Max"                │  "hbo max"                                      │
│  "Apple TV+"              │  "apple tv"                                     │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 11. Subscription Tier Management Flow

### Overview
Manages user subscription tiers (Free/Paid), enforces limits, controls feature access, and handles upgrades/downgrades while preserving user data.

### Components
- `TierService` - Business logic for tier management
- `UserRepository` - User data access
- `EmailAccountRepository` - Email account counting
- `SubscriptionRepository` - Subscription counting

### Tier Limits
| Tier | Email Accounts | Subscriptions | Features |
|------|----------------|---------------|----------|
| Free | 1 | 5 | Basic dashboard |
| Paid | Unlimited | Unlimited | All features (PDF export, Cancellation Assistant) |

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    SUBSCRIPTION TIER MANAGEMENT FLOW                         │
└─────────────────────────────────────────────────────────────────────────────┘

═══════════════════════════════════════════════════════════════════════════════
                         OPERATION VALIDATION
═══════════════════════════════════════════════════════════════════════════════

  Any Service                    TierService                    Database
       │                              │                            │
       │  1. ValidateOperationAsync   │                            │
       │     (userId, AddSubscription)│                            │
       │─────────────────────────────>│                            │
       │                              │                            │
       │                              │  2. Get user tier          │
       │                              │─────────────────────────────>│
       │                              │<─────────────────────────────│
       │                              │                            │
       │                              │  3. Get current counts     │
       │                              │─────────────────────────────>│
       │                              │<─────────────────────────────│
       │                              │                            │
       │                              │  4. Check against limits   │
       │                              │                            │
       │              ┌───────────────┴───────────────┐            │
       │              │                               │            │
       │         UNDER LIMIT                     AT LIMIT          │
       │              │                               │            │
       │              ▼                               ▼            │
       │     ┌─────────────┐              ┌─────────────────┐     │
       │     │ Return      │              │ Return Failure  │     │
       │     │ Success     │              │ TierLimitExceeded│     │
       │     └─────────────┘              └─────────────────┘     │
       │                                          │               │
       │                                          ▼               │
       │                              ┌─────────────────────┐     │
       │                              │ Show upgrade prompt │     │
       │                              │ to user             │     │
       │                              └─────────────────────┘     │
       │                                                          │
       │<─────────────────────────────────────────────────────────│
       │                                                          │
       ▼                                                          ▼

┌─────────────────────────────────────────────────────────────────────────────┐
│                         FREE TIER LIMITS                                     │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  Max Email Accounts: 1                                              │   │
│  │  Max Subscriptions: 5                                               │   │
│  │  Cancellation Assistant: ❌ Not Available                           │   │
│  │  PDF Export: ❌ Not Available                                       │   │
│  │  Advanced Insights: ❌ Not Available                                │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         PAID TIER FEATURES                                   │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  Max Email Accounts: Unlimited                                      │   │
│  │  Max Subscriptions: Unlimited                                       │   │
│  │  Cancellation Assistant: ✅ Available                               │   │
│  │  PDF Export: ✅ Available                                           │   │
│  │  Advanced Insights: ✅ Available                                    │   │
│  │  Unlimited History: ✅ Available                                    │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

═══════════════════════════════════════════════════════════════════════════════
                              UPGRADE FLOW
═══════════════════════════════════════════════════════════════════════════════

  Frontend                   TierService                    Database
       │                          │                            │
       │  1. UpgradeToPaydAsync   │                            │
       │     (userId)             │                            │
       │─────────────────────────>│                            │
       │                          │                            │
       │                          │  2. Get user               │
       │                          │─────────────────────────────>│
       │                          │<─────────────────────────────│
       │                          │                            │
       │                          │  3. Check current tier     │
       │                          │                            │
       │                          │  4. If Free → Update to Paid│
       │                          │─────────────────────────────>│
       │                          │<─────────────────────────────│
       │                          │                            │
       │  5. Success              │                            │
       │     (All features now    │                            │
       │      accessible)         │                            │
       │<─────────────────────────│                            │
       │                          │                            │
       ▼                          ▼                            ▼

═══════════════════════════════════════════════════════════════════════════════
                         DOWNGRADE FLOW (Data Preserved)
═══════════════════════════════════════════════════════════════════════════════

  Frontend                   TierService                    Database
       │                          │                            │
       │  1. DowngradeToFreeAsync │                            │
       │     (userId)             │                            │
       │─────────────────────────>│                            │
       │                          │                            │
       │                          │  2. Get user               │
       │                          │─────────────────────────────>│
       │                          │<─────────────────────────────│
       │                          │                            │
       │                          │  3. Update tier to Free    │
       │                          │     (NO data deletion!)    │
       │                          │─────────────────────────────>│
       │                          │<─────────────────────────────│
       │                          │                            │
       │  4. Success              │                            │
       │     (Data preserved,     │                            │
       │      features restricted)│                            │
       │<─────────────────────────│                            │
       │                          │                            │
       ▼                          ▼                            ▼

DATA PRESERVATION ON DOWNGRADE:
┌─────────────────────────────────────────────────────────────────────────────┐
│                                                                              │
│  ✅ All email accounts preserved (but can't add new ones)                   │
│  ✅ All subscriptions preserved (but can't add new ones)                    │
│  ✅ All alerts preserved                                                     │
│  ✅ All history preserved                                                    │
│  ❌ Premium features disabled (PDF export, Cancellation Assistant)          │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

USAGE TRACKING:
┌─────────────────────────────────────────────────────────────────────────────┐
│                                                                              │
│  GetUsageAsync(userId) returns:                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  {                                                                   │   │
│  │    "currentTier": "Free",                                           │   │
│  │    "emailAccountCount": 1,                                          │   │
│  │    "subscriptionCount": 3,                                          │   │
│  │    "limits": {                                                       │   │
│  │      "maxEmailAccounts": 1,                                         │   │
│  │      "maxSubscriptions": 5                                          │   │
│  │    },                                                                │   │
│  │    "isAtEmailLimit": true,                                          │   │
│  │    "isAtSubscriptionLimit": false                                   │   │
│  │  }                                                                   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Flow Maintenance Guidelines

> **For AI Agents**: Follow these rules when modifying this document.

### When to Update This Document

1. **New Flow Added**: Create a new section following the existing format
2. **Flow Modified**: Update the relevant diagram and description
3. **Endpoint Changed**: Update the endpoint table
4. **Component Added/Removed**: Update the Components list

### Update Checklist

- [ ] Update "Last Updated" date at top of document
- [ ] Add/modify flow diagram with ASCII art
- [ ] Update component list
- [ ] Update endpoint table (if applicable)
- [ ] Ensure consistency with actual implementation
- [ ] Cross-reference with `docs/ARCHITECTURE.md`

### ASCII Diagram Standards

- Use box-drawing characters: `┌ ┐ └ ┘ │ ─ ├ ┤ ┬ ┴ ┼`
- Arrow characters: `→ ← ↑ ↓ ▶ ◀ ▲ ▼`
- Maximum width: 77 characters (fits in most editors)
- Include legend if using special symbols

---

## Version History

| Date | Author | Changes |
|------|--------|---------|
| 2025-12-01 | Kiro | Initial creation with 9 business flows |
| 2025-12-01 | Kiro | Added Vendor Metadata Flow (Task 13) |
| 2025-12-01 | Kiro | Added Subscription Tier Management Flow (Task 14) |
