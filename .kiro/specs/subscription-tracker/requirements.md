# Requirements Document

## Introduction

This document specifies the requirements for a subscription management SaaS platform that helps consumers in EU/US track, manage, and control their recurring subscriptions. The system addresses the problem of hidden renewals, forgotten subscriptions, silent free trial conversions, and unexpected price increases by automatically detecting subscriptions from email communications and providing proactive alerts and management tools.

The platform focuses on email-based detection as the primary data source, making onboarding simple and non-intrusive. An AI extraction layer processes email content to identify subscription details, renewal dates, pricing, and usage patterns. The system is designed with EU transparency laws in mind and provides a clear path to scale from consumer-focused to B2B expense management.

## Glossary

- **Subscription Management System (SMS)**: The complete SaaS platform for tracking and managing recurring subscriptions
- **Email Ingestion Service (EIS)**: The background service that connects to user email accounts and retrieves subscription-related emails
- **AI Extraction Engine (AEE)**: The AI-powered component that parses email content and extracts subscription metadata
- **Subscription Record (SR)**: A normalized database entity representing a single subscription with all its attributes
- **Renewal Alert (RA)**: A notification sent to users about upcoming subscription renewals
- **Cancellation Assistant (CA)**: The AI-powered feature that helps users cancel subscriptions
- **Dashboard (DB)**: The web-based user interface for viewing and managing subscriptions
- **User Account (UA)**: A registered user with one or more connected email accounts
- **Email Provider (EP)**: External email services like Gmail or Outlook
- **Background Processor (BP)**: The worker service that queues and processes email parsing jobs
- **Vendor Metadata (VM)**: Information about subscription service providers including logos, categories, and policies

## Requirements

### Requirement 1

**User Story:** As a consumer, I want to connect my Gmail or Outlook account, so that the system can automatically discover my subscriptions from email communications

#### Acceptance Criteria

1. WHEN a user initiates email account connection THEN the SMS SHALL authenticate using OAuth without storing passwords
2. WHEN OAuth authentication succeeds THEN the SMS SHALL store the access token securely and establish IMAP or API connection to the EP
3. WHEN the connection is established THEN the EIS SHALL retrieve emails from the past 12 months for initial scanning
4. WHEN a user has multiple email accounts THEN the SMS SHALL allow connecting and managing each account independently
5. WHEN a user revokes access THEN the SMS SHALL immediately delete all tokens and cease email access

### Requirement 2

**User Story:** As a consumer, I want the system to automatically detect subscriptions from my emails, so that I don't have to manually enter each subscription

#### Acceptance Criteria

1. WHEN the EIS retrieves new emails THEN the BP SHALL queue them for AI processing within 5 minutes
2. WHEN the AEE processes an email THEN the AEE SHALL classify whether the email is subscription-related with at least 90% accuracy
3. WHEN an email is classified as subscription-related THEN the AEE SHALL extract service name, billing cycle, price, next renewal date, and category
4. WHEN extraction completes THEN the SMS SHALL create or update the corresponding SR in the database
5. WHEN the AEE cannot extract required fields with confidence THEN the SMS SHALL flag the SR for user review

### Requirement 3

**User Story:** As a consumer, I want to see all my subscriptions in a dashboard, so that I have a clear overview of my recurring expenses

#### Acceptance Criteria

1. WHEN a user accesses the DB THEN the SMS SHALL display all active subscriptions with service name, price, billing cycle, and next renewal date
2. WHEN displaying subscriptions THEN the SMS SHALL group them by category (entertainment, productivity, utilities, etc.)
3. WHEN calculating totals THEN the SMS SHALL normalize all billing cycles to monthly equivalents for comparison
4. WHEN a subscription has upcoming renewal THEN the SMS SHALL highlight it in the danger zone if renewal occurs within 30 days
5. WHEN the user views the timeline THEN the SMS SHALL display renewals chronologically for the next 12 months

### Requirement 4

**User Story:** As a consumer, I want to receive alerts about upcoming renewals and price changes, so that I can make informed decisions before being charged

#### Acceptance Criteria

1. WHEN a subscription renewal is 7 days away THEN the SMS SHALL send an RA to the user via email
2. WHEN a subscription renewal is 3 days away THEN the SMS SHALL send a second RA to the user
3. WHEN the AEE detects a price increase THEN the SMS SHALL send an immediate alert with old price, new price, and percentage change
4. WHEN a free trial is ending within 3 days THEN the SMS SHALL send an alert indicating the trial end date and post-trial price
5. WHEN the SMS detects no activity emails for a subscription in 6 months THEN the SMS SHALL alert the user about a potentially unused subscription

### Requirement 5

**User Story:** As a consumer, I want to understand my subscription spending patterns, so that I can identify opportunities to reduce costs

#### Acceptance Criteria

1. WHEN a user views spending insights THEN the SMS SHALL calculate total monthly subscription cost across all services
2. WHEN displaying insights THEN the SMS SHALL show spending breakdown by category with percentages
3. WHEN multiple renewals occur in the same week THEN the SMS SHALL highlight the concentration and total amount
4. WHEN the user requests a report THEN the SMS SHALL generate a PDF export of all subscriptions with dates and costs
5. WHEN sufficient data exists THEN the SMS SHALL compare user spending to anonymized benchmarks and display the variance

### Requirement 6

**User Story:** As a consumer, I want the system to help me cancel unwanted subscriptions, so that I can easily stop recurring charges

#### Acceptance Criteria

1. WHEN a user selects a subscription to cancel THEN the CA SHALL retrieve cancellation instructions from the vendor
2. WHEN cancellation instructions are available THEN the CA SHALL generate a pre-filled cancellation email or form
3. WHEN the user confirms cancellation THEN the CA SHALL send the cancellation request on behalf of the user
4. WHEN cancellation is submitted THEN the CA SHALL create a follow-up reminder to verify cancellation within 7 days
5. WHEN the CA detects cancellation confirmation email THEN the SMS SHALL mark the subscription as cancelled and archive it

### Requirement 7

**User Story:** As a consumer, I want to manage subscriptions across multiple email accounts, so that I can track all my subscriptions in one place

#### Acceptance Criteria

1. WHEN a user has multiple connected email accounts THEN the SMS SHALL aggregate subscriptions from all accounts in a unified view
2. WHEN displaying subscriptions THEN the SMS SHALL indicate which email account each subscription is associated with
3. WHEN a user enables family mode THEN the SMS SHALL allow adding email accounts from family members under one UA
4. WHEN family mode is active THEN the SMS SHALL maintain separate permission controls for each email account
5. WHEN a user disconnects an email account THEN the SMS SHALL archive associated subscriptions but retain historical data

### Requirement 8

**User Story:** As a consumer, I want my data to be secure and deletable, so that I can trust the system with my email access

#### Acceptance Criteria

1. WHEN the SMS stores access tokens THEN the SMS SHALL encrypt them using industry-standard encryption (AES-256)
2. WHEN the SMS accesses email THEN the SMS SHALL use read-only permissions and never modify or delete user emails
3. WHEN a user requests data deletion THEN the SMS SHALL permanently delete all user data including tokens, subscriptions, and emails within 24 hours
4. WHEN the SMS processes emails THEN the SMS SHALL only store extracted metadata and SHALL NOT store full email content
5. WHEN the SMS handles user data THEN the SMS SHALL comply with GDPR requirements for EU users and provide data portability

### Requirement 9

**User Story:** As a system administrator, I want the email ingestion service to run reliably in the background, so that subscriptions are always up to date

#### Acceptance Criteria

1. WHEN the EIS is running THEN the BP SHALL check for new emails every 15 minutes for each connected account
2. WHEN email retrieval fails THEN the BP SHALL retry with exponential backoff up to 3 attempts before alerting the user
3. WHEN the BP queues parsing jobs THEN the BP SHALL process them in priority order (new subscriptions before updates)
4. WHEN the BP encounters rate limits THEN the BP SHALL throttle requests to stay within EP API limits
5. WHEN the EIS processes emails THEN the BP SHALL log all operations for debugging and audit purposes

### Requirement 10

**User Story:** As a consumer, I want to receive vendor information for my subscriptions, so that I can easily identify services and access their websites

#### Acceptance Criteria

1. WHEN the AEE identifies a service name THEN the SMS SHALL match it against the VM database to retrieve logo, category, and website
2. WHEN displaying a subscription THEN the SMS SHALL show the vendor logo and category icon
3. WHEN VM is unavailable for a service THEN the SMS SHALL use the extracted service name and allow user to manually add details
4. WHEN the user clicks on a subscription THEN the SMS SHALL provide a direct link to the vendor website and account management page
5. WHEN the SMS detects a new vendor THEN the SMS SHALL automatically enrich VM with publicly available information

### Requirement 11

**User Story:** As a new user, I want a simple onboarding process, so that I can start tracking subscriptions quickly

#### Acceptance Criteria

1. WHEN a user signs up THEN the SMS SHALL require only email authentication via OAuth
2. WHEN onboarding begins THEN the SMS SHALL guide the user through connecting their first email account with clear instructions
3. WHEN the first email account is connected THEN the SMS SHALL immediately start scanning and show progress indicators
4. WHEN initial scanning completes THEN the SMS SHALL display discovered subscriptions and prompt user to verify them
5. WHEN the user completes onboarding THEN the SMS SHALL offer a brief tutorial highlighting key features (alerts, cancellation assistant, insights)

### Requirement 12

**User Story:** As a beta tester, I want to provide feedback on my experience, so that the product team can improve the platform before full launch

#### Acceptance Criteria

1. WHEN a beta user completes onboarding THEN the SMS SHALL display a welcome message explaining the beta program and feedback importance
2. WHEN a beta user has used the platform for 7 days THEN the SMS SHALL prompt them to complete a feedback survey
3. WHEN a user accesses the feedback form THEN the SMS SHALL collect ratings on: ease of use, subscription detection accuracy, value provided, willingness to pay, and feature requests
4. WHEN a user submits feedback THEN the SMS SHALL store it securely and thank the user
5. WHEN the beta period ends THEN the SMS SHALL analyze feedback to determine go/no-go decision for Phase 2

### Requirement 13

**User Story:** As a product owner, I want to track beta user engagement metrics, so that I can validate product-market fit before scaling

#### Acceptance Criteria

1. WHEN a user performs key actions THEN the SMS SHALL track events: signup, email connected, subscriptions discovered, dashboard visits, feedback submitted
2. WHEN calculating engagement THEN the SMS SHALL measure: daily active users, weekly active users, retention rate, average subscriptions per user
3. WHEN evaluating beta success THEN the SMS SHALL compare actual metrics against target metrics defined in beta strategy
4. WHEN the beta period ends THEN the SMS SHALL generate a report with all success metrics and recommendations
5. WHEN metrics indicate low engagement THEN the SMS SHALL flag specific areas for improvement based on user behavior patterns

### Requirement 14

**User Story:** As a consumer, I want to choose between free and paid tiers, so that I can start with basic features and upgrade when needed

#### Acceptance Criteria

1. WHEN a user signs up THEN the SMS SHALL default to the free tier with 1 email account and 5 subscription limit
2. WHEN a free tier user reaches 5 subscriptions THEN the SMS SHALL prompt upgrade and prevent tracking additional subscriptions
3. WHEN a user upgrades to paid tier THEN the SMS SHALL unlock unlimited email accounts, unlimited subscriptions, CA, and PDF exports
4. WHEN a paid user downgrades THEN the SMS SHALL maintain all data but restrict features to free tier limits
5. WHEN processing payments THEN the SMS SHALL use a secure payment provider and never store credit card information

## Functional Requirements

### FR-1: Email Integration
- The system SHALL support Gmail and Outlook as email providers
- The system SHALL use OAuth 2.0 for authentication
- The system SHALL support IMAP protocol as fallback to official APIs
- The system SHALL retrieve emails from inbox and specific folders (Purchases, Receipts)
- The system SHALL process emails in batches of 50 to optimize performance

### FR-2: AI Extraction
- The system SHALL use an API-based LLM for email content analysis
- The system SHALL extract: service name, billing cycle, price, currency, next renewal date, category, cancellation link
- The system SHALL classify emails into: purchase receipt, renewal notice, welcome email, free trial confirmation, price change notification
- The system SHALL handle multiple languages (English, German, French, Spanish for EU/US markets)
- The system SHALL maintain extraction confidence scores and flag low-confidence results

### FR-3: Data Storage
- The system SHALL store subscription records with: ID, user ID, email account ID, service name, billing cycle, price, currency, next renewal date, category, status, created date, updated date
- The system SHALL store email metadata only (sender, subject, date, extracted data) and NOT full email content
- The system SHALL maintain audit logs for all data modifications
- The system SHALL support soft deletion for subscriptions
- The system SHALL archive cancelled subscriptions for historical reporting

### FR-4: Alert System
- The system SHALL send alerts via email as primary channel
- The system SHALL support configurable alert preferences per user
- The system SHALL batch multiple alerts into daily digest if user prefers
- The system SHALL track alert delivery status and retry failed deliveries
- The system SHALL allow users to snooze alerts for specific subscriptions

### FR-5: Dashboard & Reporting
- The system SHALL display subscriptions in list and calendar views
- The system SHALL support filtering by category, price range, billing cycle, and status
- The system SHALL support sorting by name, price, next renewal date
- The system SHALL calculate monthly spending totals with currency conversion
- The system SHALL generate PDF reports with subscription details and spending analysis

### FR-6: User Management
- The system SHALL support user registration via OAuth (Google, Microsoft)
- The system SHALL maintain user profiles with preferences and settings
- The system SHALL support account deletion with complete data removal
- The system SHALL provide data export in JSON format for GDPR compliance
- The system SHALL support email verification for security-sensitive operations

## Non-Functional Requirements

### NFR-1: Performance
- The system SHALL process email scanning for initial 12-month history within 5 minutes for accounts with up to 10,000 emails
- The system SHALL load the dashboard with all subscriptions within 2 seconds
- The system SHALL process new emails within 15 minutes of receipt
- The system SHALL support at least 1,000 concurrent users during MVP phase
- The system SHALL handle up to 100 email accounts being scanned simultaneously

### NFR-2: Scalability
- The system SHALL be designed to scale horizontally by adding worker instances
- The system SHALL use message queues for asynchronous email processing
- The system SHALL partition database by user ID for future sharding
- The system SHALL cache vendor metadata to reduce database queries
- The system SHALL support scaling to 100,000 users within 12 months post-MVP

### NFR-3: Reliability
- The system SHALL maintain 99.5% uptime during business hours (EU/US time zones)
- The system SHALL implement retry logic with exponential backoff for all external API calls
- The system SHALL gracefully handle email provider outages without data loss
- The system SHALL backup database daily with 30-day retention
- The system SHALL implement circuit breakers for external service dependencies

### NFR-4: Security
- The system SHALL encrypt all data at rest using AES-256
- The system SHALL encrypt all data in transit using TLS 1.3
- The system SHALL store OAuth tokens in secure key vault (Azure Key Vault)
- The system SHALL implement rate limiting to prevent abuse (100 requests per minute per user)
- The system SHALL log all authentication attempts and security events
- The system SHALL comply with OWASP Top 10 security standards

### NFR-5: Maintainability
- The system SHALL follow clean architecture principles with clear separation of concerns
- The system SHALL maintain at least 80% code coverage for business logic
- The system SHALL use dependency injection for all services
- The system SHALL implement structured logging with correlation IDs
- The system SHALL document all public APIs using OpenAPI/Swagger

### NFR-6: Usability
- The system SHALL provide a responsive web interface that works on desktop and mobile
- The system SHALL support modern browsers (Chrome, Firefox, Safari, Edge - latest 2 versions)
- The system SHALL provide clear error messages with actionable guidance
- The system SHALL complete onboarding flow in under 3 minutes
- The system SHALL follow WCAG 2.1 Level AA accessibility guidelines

### NFR-7: Compliance
- The system SHALL comply with GDPR for EU users
- The system SHALL comply with CCPA for California users
- The system SHALL provide privacy policy and terms of service
- The system SHALL obtain explicit consent before accessing email accounts
- The system SHALL support right to be forgotten with complete data deletion

### NFR-8: Monitoring & Observability
- The system SHALL implement health check endpoints for all services
- The system SHALL track key metrics: email processing rate, extraction accuracy, alert delivery rate, API response times
- The system SHALL send alerts for critical errors and service degradation
- The system SHALL integrate with Application Insights for telemetry
- The system SHALL maintain logs for at least 90 days

## MVP Prioritization

### Beta Launch Strategy

**Objective:** Validate market demand and product-market fit before full launch

**Beta Program Goals:**
1. Test core value proposition with real users
2. Gather feedback on subscription detection accuracy
3. Validate pricing and willingness to pay
4. Identify critical bugs and usability issues
5. Measure engagement and retention metrics
6. Determine if the idea is profitable before scaling

**Beta User Acquisition:**
- Target: 50-100 beta users
- Channels: Product Hunt, Reddit (r/personalfinance), Twitter, LinkedIn
- Incentive: Free lifetime access or 50% discount for first year
- Duration: 4-6 weeks

**Beta Success Metrics:**
- **Activation:** 80%+ users connect email and discover subscriptions
- **Engagement:** 50%+ users return weekly to check dashboard
- **Value:** Users discover average of 8+ subscriptions
- **Satisfaction:** 4.0+ rating on feedback surveys
- **Conversion Intent:** 20%+ users indicate willingness to pay $10-15/month
- **Retention:** 60%+ users still active after 4 weeks

**Go/No-Go Decision Criteria:**
- ✅ **GO:** If 3+ success metrics are met → proceed to Phase 2 (Alerts) and monetization
- ❌ **NO-GO:** If < 2 success metrics are met → pivot or iterate based on feedback
- 🔄 **ITERATE:** If 2 success metrics are met → make adjustments and extend beta

### Phase 1: Core MVP (Weeks 1-4) - MUST HAVE
**Goal:** Launch beta with basic subscription tracking from Gmail

**Scope:**
- Gmail OAuth integration only (defer Outlook)
- Email ingestion service with IMAP
- Basic AI extraction (service name, price, next renewal date only)
- Simple subscription table in SQLite
- Basic dashboard showing list of subscriptions
- Manual subscription add/edit/delete
- User authentication via Google OAuth
- Deploy to Azure App Service Free Tier
- Beta user feedback form integrated in dashboard

**Deferred:**
- Outlook integration
- Advanced AI features (usage detection, category classification)
- Alerts and notifications
- Cancellation assistant
- PDF reports
- Family mode
- Payment/subscription tiers

**Success Criteria:**
- User can connect Gmail account in < 2 minutes
- System discovers at least 70% of subscriptions from emails
- Dashboard displays subscriptions with basic details
- System handles 100 beta users
- Collect feedback from 80%+ of beta users

### Phase 2: Alerts & Value (Weeks 5-6) - SHOULD HAVE
**Goal:** Add proactive value through alerts (only if beta is successful)

**Prerequisites:** Beta success metrics met, positive user feedback

**Scope:**
- Email alerts for upcoming renewals (7 days, 3 days)
- Price increase detection and alerts
- Free trial ending alerts
- Alert preferences in user settings
- Background job scheduler (Hangfire)

**Success Criteria:**
- Users receive timely renewal alerts
- Alert delivery rate > 95%
- User engagement increases by 30% (measured by dashboard visits)
- 70%+ users find alerts valuable (survey)

### Phase 3: Monetization (Weeks 7-8) - SHOULD HAVE
**Goal:** Enable revenue generation (only if beta validates willingness to pay)

**Prerequisites:** 
- Beta users indicate 20%+ conversion intent
- Alerts feature increases engagement
- Positive ROI projection

**Scope:**
- Free tier: 1 email account, 5 subscriptions
- Paid tier ($10-15/month): unlimited accounts/subscriptions
- Stripe integration for payments
- Subscription management (upgrade/downgrade)
- Basic analytics dashboard for insights
- Pricing page with clear value proposition

**Success Criteria:**
- Payment flow works end-to-end
- At least 10% of users convert to paid tier (based on beta intent)
- Churn rate < 10% monthly
- Positive cash flow (revenue > costs)

### Phase 4: Enhancement (Weeks 9-12) - COULD HAVE
**Goal:** Differentiate with advanced features

**Scope:**
- Outlook integration
- Cancellation assistant (basic version)
- PDF export
- Vendor metadata enrichment
- Category-based insights
- Unused subscription detection

**Success Criteria:**
- Feature adoption > 30% for cancellation assistant
- User satisfaction score > 4.0/5.0

### Future Phases (Post-MVP)
- Family mode
- Bank transaction integration (Plaid)
- Mobile app
- B2B expense management features
- Advanced AI insights and recommendations
- Browser extension for quick subscription checks

## Technology Stack Recommendations

### Backend (.NET)
- **Framework:** ASP.NET Core 10.0 Web API
- **ORM:** Entity Framework Core 10.0
- **Database:** SQLite (MVP) → Azure SQL Database (scale)
- **Background Jobs:** Hangfire with in-memory storage (MVP) → SQL storage (scale)
- **Caching:** In-memory caching (MVP) → Redis (scale)
- **Message Queue:** In-memory queue (MVP) → Azure Service Bus (scale)
- **Authentication:** ASP.NET Core Identity + OAuth 2.0
- **API Documentation:** Swashbuckle (Swagger)
- **Logging:** Serilog + Console (MVP) → Application Insights (scale)
- **Testing:** xUnit + FluentAssertions + Moq

### Frontend
- **Framework:** Next.js 15 (React) with TypeScript
- **Styling:** Tailwind CSS + shadcn/ui components
- **State Management:** React Query (TanStack Query) for server state
- **Forms:** React Hook Form + Zod validation
- **Charts:** Recharts (lightweight)
- **Authentication:** NextAuth.js
- **Deployment:** Vercel (free tier for MVP)

**Why Next.js 15:**
- Easy to learn for backend developers
- Excellent TypeScript support
- Built-in API routes (can proxy to .NET backend)
- Great performance with App Router and Server Components
- Large ecosystem and community
- Free hosting on Vercel for MVP

### AI/ML
- **LLM API:** OpenAI GPT-4o-mini (cost-effective) or Azure OpenAI Service
- **Alternative:** Anthropic Claude Haiku (cheaper tier)
- **Strategy:** Batch processing to minimize API calls

### Infrastructure (Azure - Cost Optimized for 100 Users)
- **Hosting:** Azure App Service Free Tier F1 (MVP) → Basic B1 (scale)
- **Database:** SQLite file storage (MVP) → Azure SQL Basic (scale)
- **Storage:** Local file system (MVP) → Azure Blob Storage (scale)
- **Secrets:** appsettings.json with user secrets (MVP) → Azure Key Vault (scale)
- **Monitoring:** Console logging (MVP) → Application Insights (scale)
- **Email:** SendGrid Free Tier (100 emails/day)

### DevOps
- **CI/CD:** GitHub Actions (free for public repos)
- **Containerization:** Docker (optional for MVP)
- **IaC:** Azure Bicep (when scaling)
- **Version Control:** Git + GitHub

### Cost Optimization Strategy for 100 Users

**MVP Phase (0-100 users):**
- Azure App Service Free Tier (F1) - **$0/month**
- SQLite for database - **$0/month**
- In-memory caching and queuing - **$0/month**
- SendGrid Free Tier (100 emails/day) - **$0/month**
- OpenAI GPT-4o-mini (~2000 emails/month) - **$10-20/month**
- Vercel Free Tier for frontend - **$0/month**
- **Total MVP Cost: $10-20/month**

**Growth Phase (100-1000 users):**
- Azure App Service Basic B1 - **$13/month**
- Azure SQL Basic - **$5/month**
- Application Insights - **$10/month**
- SendGrid Essentials - **$15/month**
- OpenAI GPT-4o-mini (increased usage) - **$50-100/month**
- **Total Growth Cost: $93-143/month**

**Break-even Analysis:**
- At $10/month subscription price: Need 10-15 paid users to break even at MVP scale
- At $15/month subscription price: Need 7-10 paid users to break even at MVP scale
