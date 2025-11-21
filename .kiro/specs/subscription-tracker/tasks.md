# Implementation Plan

- [x] 1. Set up project structure and core domain models
  - Create ASP.NET Core 10.0 Web API project with clean architecture structure
  - Set up Entity Framework Core 10.0 with SQLite
  - Define core domain entities: User, EmailAccount, Subscription, Alert, VendorMetadata, SubscriptionHistory, EmailMetadata
  - Implement enums: SubscriptionTier, EmailProvider, BillingCycle, SubscriptionStatus, AlertType, AlertStatus
  - Configure dependency injection container
  - _Requirements: 1.1, 2.1, 3.1, 8.1_

- [ ]* 1.1 Write property test for OAuth token encryption
  - **Property 1: OAuth token encryption**
  - **Validates: Requirements 8.1**

- [x] 2. Implement database layer with Entity Framework Core






  - Create DbContext with all entity configurations
  - Configure relationships and navigation properties
  - Set up indexing strategy (UserId, Status, NextRenewalDate, Email)
  - Implement encryption for OAuth tokens (AES-256)
  - Create database migrations
  - Configure SQLite connection and file storage
  - _Requirements: 8.1, FR-3_

- [ ]* 2.1 Write property test for connection establishment after OAuth
  - **Property 2: Connection establishment after OAuth**
  - **Validates: Requirements 1.2**

- [ ]* 2.2 Write property test for independent account management
  - **Property 4: Independent account management**
  - **Validates: Requirements 1.4**

- [x] 3. Implement repository pattern for data access




  - Create IRepository<T> generic interface
  - Implement UserRepository with CRUD operations
  - Implement EmailAccountRepository with token management
  - Implement SubscriptionRepository with filtering and aggregation
  - Implement AlertRepository with scheduling queries
  - Implement VendorMetadataRepository with caching
  - _Requirements: 1.1, 2.1, 3.1_

- [ ]* 3.1 Write property test for token deletion on revocation
  - **Property 5: Token deletion on revocation**
  - **Validates: Requirements 1.5**

- [ ] 4. Implement user authentication and OAuth integration
  - Set up ASP.NET Core Identity
  - Implement Google OAuth 2.0 authentication
  - Create UserService for user management (create, get, update, delete)
  - Implement JWT token generation for API authentication
  - Create authentication middleware
  - _Requirements: 1.1, 1.2, 11.1, FR-6_

- [ ]* 4.1 Write property test for free tier defaults
  - **Property 56: Free tier defaults**
  - **Validates: Requirements 14.1**

- [ ] 5. Implement Gmail API integration
  - Create IGmailClient interface
  - Implement OAuth token storage and refresh logic
  - Implement email retrieval with IMAP fallback
  - Create email filtering logic (sender domain, subject keywords, folders)
  - Implement 12-month historical email retrieval
  - Handle Gmail API rate limiting and errors
  - _Requirements: 1.2, 1.3, FR-1_

- [ ]* 5.1 Write property test for initial email retrieval span
  - **Property 3: Initial email retrieval span**
  - **Validates: Requirements 1.3**

- [ ] 6. Implement email queueing service
  - Create IEmailQueueService interface
  - Implement in-memory queue for MVP
  - Add priority-based queueing (new subscriptions > updates)
  - Implement queue status tracking
  - Add email metadata storage (sender, subject, date)
  - _Requirements: 2.1, 9.3, FR-5_

- [ ]* 6.1 Write property test for email queueing timeliness
  - **Property 6: Email queueing timeliness**
  - **Validates: Requirements 2.1**

- [ ]* 6.2 Write property test for metadata-only storage
  - **Property 11: Metadata-only storage**
  - **Validates: Requirements 8.4**

- [ ] 7. Implement AI extraction service with OpenAI
  - Create IOpenAIClient wrapper for OpenAI API
  - Implement email classification (subscription-related or not)
  - Create structured extraction prompt for service name, price, billing cycle, renewal date, category
  - Implement confidence scoring logic
  - Add multi-language support (English, German, French, Spanish)
  - Handle API errors and rate limiting
  - _Requirements: 2.2, 2.3, 2.5, FR-2_

- [ ]* 7.1 Write property test for required field extraction
  - **Property 8: Required field extraction**
  - **Validates: Requirements 2.3**

- [ ]* 7.2 Write property test for low confidence flagging
  - **Property 10: Low confidence flagging**
  - **Validates: Requirements 2.5**

- [ ] 8. Implement subscription management service
  - Create SubscriptionService with business logic
  - Implement CreateOrUpdateAsync with deduplication (fuzzy matching 85% similarity)
  - Implement GetUserSubscriptionsAsync with filtering
  - Add subscription status management (Active, Cancelled, Archived, PendingReview)
  - Implement billing cycle normalization (Annual/12, Quarterly/3, Weekly*4.33)
  - Create subscription history tracking
  - _Requirements: 2.4, 3.1, 3.3_

- [ ]* 8.1 Write property test for database record creation
  - **Property 9: Database record creation**
  - **Validates: Requirements 2.4**

- [ ]* 8.2 Write property test for billing cycle normalization
  - **Property 14: Billing cycle normalization**
  - **Validates: Requirements 3.3**

- [ ] 9. Set up Hangfire for background jobs
  - Install and configure Hangfire with in-memory storage
  - Create EmailScanningJob (runs every 15 minutes per account)
  - Create AlertGenerationJob (runs daily at 8 AM user local time)
  - Create SubscriptionUpdateJob (runs daily at 2 AM)
  - Implement retry logic with exponential backoff
  - Add job monitoring and logging
  - _Requirements: 9.1, 9.2, NFR-3_

- [ ]* 9.1 Write property test for email check frequency
  - **Property 39: Email check frequency**
  - **Validates: Requirements 9.1**

- [ ]* 9.2 Write property test for retry with exponential backoff
  - **Property 40: Retry with exponential backoff**
  - **Validates: Requirements 9.2**

- [ ]* 9.3 Write property test for priority-based processing
  - **Property 41: Priority-based processing**
  - **Validates: Requirements 9.3**

- [ ] 10. Implement dashboard service
  - Create DashboardService for data aggregation
  - Implement GetDashboardDataAsync (active subscriptions, total spend, categories)
  - Implement category grouping logic
  - Create spending insights calculation (total monthly, breakdown by category)
  - Implement renewal timeline generation (next 12 months, chronological)
  - Add upcoming renewal highlighting (within 30 days)
  - _Requirements: 3.1, 3.2, 3.4, 3.5, 5.1, 5.2_

- [ ]* 10.1 Write property test for active subscription display
  - **Property 12: Active subscription display**
  - **Validates: Requirements 3.1**

- [ ]* 10.2 Write property test for category grouping
  - **Property 13: Category grouping**
  - **Validates: Requirements 3.2**

- [ ]* 10.3 Write property test for upcoming renewal highlighting
  - **Property 15: Upcoming renewal highlighting**
  - **Validates: Requirements 3.4**

- [ ]* 10.4 Write property test for timeline chronological ordering
  - **Property 16: Timeline chronological ordering**
  - **Validates: Requirements 3.5**

- [ ]* 10.5 Write property test for total monthly cost calculation
  - **Property 22: Total monthly cost calculation**
  - **Validates: Requirements 5.1**

- [ ]* 10.6 Write property test for category percentage accuracy
  - **Property 23: Category percentage accuracy**
  - **Validates: Requirements 5.2**

- [ ] 11. Implement alert service
  - Create AlertService for alert generation
  - Implement renewal alert logic (7 days, 3 days before)
  - Implement price change detection and alerts
  - Implement trial ending alerts (3 days before)
  - Implement unused subscription detection (6 months no activity)
  - Create alert preference management
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

- [ ]* 11.1 Write property test for 7-day renewal alert generation
  - **Property 17: 7-day renewal alert generation**
  - **Validates: Requirements 4.1**

- [ ]* 11.2 Write property test for 3-day renewal alert generation
  - **Property 18: 3-day renewal alert generation**
  - **Validates: Requirements 4.2**

- [ ]* 11.3 Write property test for price increase alert completeness
  - **Property 19: Price increase alert completeness**
  - **Validates: Requirements 4.3**

- [ ]* 11.4 Write property test for trial ending alert information
  - **Property 20: Trial ending alert information**
  - **Validates: Requirements 4.4**

- [ ]* 11.5 Write property test for unused subscription detection
  - **Property 21: Unused subscription detection**
  - **Validates: Requirements 4.5**

- [ ] 12. Implement email notification service with SendGrid
  - Create IEmailNotificationService interface
  - Integrate SendGrid API
  - Create email templates for alerts (renewal, price change, trial ending, unused)
  - Implement alert delivery with retry logic
  - Add daily digest batching option
  - Track delivery status
  - _Requirements: 4.1, 4.2, 4.3, 4.4, FR-4_

- [ ] 13. Implement vendor metadata service
  - Create VendorMetadataService
  - Implement vendor matching logic (normalized name comparison)
  - Add vendor enrichment background job
  - Implement logo and website URL retrieval
  - Create fallback logic for unknown vendors
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

- [ ]* 13.1 Write property test for vendor metadata matching
  - **Property 44: Vendor metadata matching**
  - **Validates: Requirements 10.1**

- [ ]* 13.2 Write property test for fallback to service name
  - **Property 46: Fallback to service name**
  - **Validates: Requirements 10.3**

- [ ] 14. Implement subscription tier management
  - Add tier validation in SubscriptionService
  - Implement free tier limits (1 email account, 5 subscriptions)
  - Create upgrade/downgrade logic
  - Add feature access control based on tier
  - Implement upgrade prompts when limits reached
  - _Requirements: 14.1, 14.2, 14.3, 14.4_

- [ ]* 14.1 Write property test for free tier limit enforcement
  - **Property 57: Free tier limit enforcement**
  - **Validates: Requirements 14.2**

- [ ]* 14.2 Write property test for paid tier feature unlock
  - **Property 58: Paid tier feature unlock**
  - **Validates: Requirements 14.3**

- [ ]* 14.3 Write property test for downgrade data preservation
  - **Property 59: Downgrade data preservation**
  - **Validates: Requirements 14.4**

- [ ] 15. Implement data privacy and GDPR compliance
  - Create DataExportService for JSON export
  - Implement DataDeletionService for complete user data removal
  - Add data deletion background job (complete within 24 hours)
  - Implement audit logging for data access and modifications
  - Create privacy policy and consent management
  - _Requirements: 8.3, 8.5, NFR-7_

- [ ]* 15.1 Write property test for data deletion completeness
  - **Property 37: Data deletion completeness**
  - **Validates: Requirements 8.3**

- [ ]* 15.2 Write property test for GDPR data export
  - **Property 38: GDPR data export**
  - **Validates: Requirements 8.5**

- [ ] 16. Implement API controllers
  - Create AuthController (login, logout, token refresh)
  - Create EmailAccountController (connect, disconnect, list)
  - Create SubscriptionController (list, get, update, delete, manual add)
  - Create DashboardController (dashboard data, insights, timeline)
  - Create AlertController (preferences, snooze)
  - Create UserController (profile, preferences, export, delete)
  - Add input validation and error handling
  - _Requirements: 1.1, 1.4, 1.5, 3.1, 3.5, 5.1_

- [ ]* 16.1 Write property test for unified subscription aggregation
  - **Property 32: Unified subscription aggregation**
  - **Validates: Requirements 7.1**

- [ ]* 16.2 Write property test for subscription archival on disconnect
  - **Property 36: Subscription archival on disconnect**
  - **Validates: Requirements 7.5**

- [ ] 17. Implement error handling and logging
  - Create global exception handler middleware
  - Implement structured error response format
  - Set up Serilog with console sink
  - Add correlation IDs for request tracing
  - Implement circuit breaker for external services
  - Add comprehensive logging for all operations
  - _Requirements: 9.5, NFR-5_

- [ ]* 17.1 Write property test for operation logging
  - **Property 43: Operation logging**
  - **Validates: Requirements 9.5**

- [ ] 18. Implement beta program features
  - Create FeedbackController for survey submission
  - Add beta welcome message in onboarding
  - Implement 7-day feedback prompt logic
  - Create feedback storage and retrieval
  - Add analytics event tracking (signup, email connected, subscriptions discovered, dashboard visits)
  - _Requirements: 12.1, 12.2, 12.3, 12.4, 13.1_

- [ ]* 18.1 Write property test for beta user survey timing
  - **Property 51: Beta user survey timing**
  - **Validates: Requirements 12.2**

- [ ]* 18.2 Write property test for feedback storage
  - **Property 52: Feedback storage**
  - **Validates: Requirements 12.4**

- [ ]* 18.3 Write property test for event tracking
  - **Property 53: Event tracking**
  - **Validates: Requirements 13.1**

- [ ] 19. Set up API documentation with Swagger
  - Install Swashbuckle
  - Configure Swagger UI
  - Add XML documentation comments to controllers
  - Document request/response models
  - Add authentication configuration to Swagger
  - _Requirements: NFR-5_

- [ ] 20. Checkpoint - Ensure all backend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 21. Set up Next.js frontend project
  - Create Next.js 15 project with TypeScript and App Router
  - Install Tailwind CSS and shadcn/ui components
  - Set up NextAuth.js for authentication
  - Configure React Query (TanStack Query) for API calls
  - Set up environment variables for API base URL
  - Create layout with navigation
  - _Requirements: 11.1, NFR-6_

- [ ] 22. Implement authentication pages
  - Create login page with Google OAuth button
  - Implement NextAuth.js configuration
  - Create protected route wrapper
  - Add logout functionality
  - Handle authentication errors
  - _Requirements: 1.1, 11.1_

- [ ] 23. Implement onboarding flow
  - Create onboarding wizard component
  - Add email connection step with OAuth flow
  - Show progress indicators during initial scan
  - Display discovered subscriptions for verification
  - Add brief tutorial highlighting key features
  - Implement beta welcome message
  - _Requirements: 11.2, 11.3, 11.4, 11.5, 12.1_

- [ ]* 23.1 Write property test for immediate scan initiation
  - **Property 49: Immediate scan initiation**
  - **Validates: Requirements 11.3**

- [ ]* 23.2 Write property test for subscription display after scan
  - **Property 50: Subscription display after scan**
  - **Validates: Requirements 11.4**

- [ ] 24. Implement dashboard page
  - Create dashboard layout with subscription list
  - Implement category grouping UI
  - Add total monthly spending display
  - Show upcoming renewals with danger zone highlighting
  - Add filtering by category, price range, billing cycle, status
  - Add sorting by name, price, next renewal date
  - Display vendor logos and icons
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 10.2_

- [ ]* 24.1 Write property test for logo and icon display
  - **Property 45: Logo and icon display**
  - **Validates: Requirements 10.2**

- [ ]* 24.2 Write property test for vendor link provision
  - **Property 47: Vendor link provision**
  - **Validates: Requirements 10.4**

- [ ] 25. Implement subscription detail and management
  - Create subscription detail modal/page
  - Add manual subscription add form with validation
  - Implement subscription edit functionality
  - Add subscription delete/archive functionality
  - Show subscription history
  - Display email account association
  - _Requirements: 3.1, 7.2_

- [ ]* 25.1 Write property test for email account association display
  - **Property 33: Email account association display**
  - **Validates: Requirements 7.2**

- [ ] 26. Implement spending insights page
  - Create insights page layout
  - Display spending breakdown by category with percentages
  - Show weekly renewal concentration highlights
  - Add spending comparison to benchmarks (when available)
  - Implement PDF export functionality (paid tier only)
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

- [ ]* 26.1 Write property test for weekly renewal concentration
  - **Property 24: Weekly renewal concentration**
  - **Validates: Requirements 5.3**

- [ ]* 26.2 Write property test for PDF report completeness
  - **Property 25: PDF report completeness**
  - **Validates: Requirements 5.4**

- [ ]* 26.3 Write property test for benchmark comparison availability
  - **Property 26: Benchmark comparison availability**
  - **Validates: Requirements 5.5**

- [ ] 27. Implement renewal timeline view
  - Create timeline/calendar view component
  - Display renewals chronologically for next 12 months
  - Add month/week/day view options
  - Show renewal details on hover/click
  - Highlight high-spend periods
  - _Requirements: 3.5_

- [ ] 28. Implement settings page
  - Create settings page layout
  - Add email account management (connect, disconnect, list)
  - Implement alert preferences (enable/disable by type, daily digest)
  - Add user profile settings (timezone, preferred currency)
  - Show subscription tier and upgrade option
  - Add data export and account deletion options
  - _Requirements: 1.4, 1.5, 4.1, 8.3, 8.5_

- [ ] 29. Implement feedback form for beta users
  - Create feedback form component
  - Add rating fields (ease of use, accuracy, value, willingness to pay)
  - Add feature request text area
  - Implement 7-day prompt logic
  - Show thank you message after submission
  - _Requirements: 12.2, 12.3, 12.4_

- [ ] 30. Implement error handling and loading states
  - Create error boundary components
  - Add loading skeletons for all pages
  - Implement toast notifications for success/error messages
  - Handle API errors gracefully with user-friendly messages
  - Add retry logic for failed requests
  - _Requirements: NFR-6_

- [ ] 31. Implement responsive design
  - Ensure all pages work on mobile devices
  - Test on desktop and mobile viewports
  - Optimize touch interactions for mobile
  - Ensure accessibility (WCAG 2.1 Level AA)
  - _Requirements: NFR-6_

- [ ] 32. Set up frontend testing
  - Install testing libraries (Jest, React Testing Library)
  - Write component tests for key UI components
  - Test form validation and submission
  - Test authentication flows
  - Test error handling
  - _Requirements: NFR-5_

- [ ] 33. Checkpoint - Ensure all frontend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 34. Set up deployment configuration
  - Create Dockerfile for backend
  - Configure Azure App Service deployment
  - Set up environment variables and secrets
  - Configure Vercel deployment for frontend
  - Set up database connection string
  - Configure CORS for frontend-backend communication
  - _Requirements: NFR-3_

- [ ] 35. Implement health check endpoints
  - Create health check endpoint for API
  - Add database connectivity check
  - Add external service checks (Gmail API, OpenAI, SendGrid)
  - Configure health check monitoring
  - _Requirements: NFR-8_

- [ ] 36. Set up monitoring and logging
  - Configure Application Insights (or console logging for MVP)
  - Set up error tracking
  - Add performance monitoring
  - Create alerts for critical errors
  - Track key metrics (API response times, job success rates, extraction accuracy)
  - _Requirements: NFR-8_

- [ ] 37. Create seed data for development
  - Create sample vendor metadata records
  - Add sample subscriptions for testing
  - Create test user accounts
  - Add sample email fixtures for AI extraction testing
  - _Requirements: Testing_

- [ ] 38. Write integration tests
  - Test onboarding flow end-to-end
  - Test email ingestion flow
  - Test alert generation flow
  - Test subscription CRUD operations
  - Test tier upgrade/downgrade
  - _Requirements: NFR-5_

- [ ] 39. Perform security audit
  - Review authentication implementation
  - Test OAuth token encryption
  - Verify input validation on all endpoints
  - Test rate limiting
  - Review CORS configuration
  - Check for common vulnerabilities (OWASP Top 10)
  - _Requirements: NFR-4_

- [ ] 40. Final checkpoint - Complete system test
  - Ensure all tests pass, ask the user if questions arise.
  - Verify all 59 correctness properties are tested
  - Run full integration test suite
  - Test with real Gmail account
  - Verify all features work end-to-end
  - Check performance meets targets
