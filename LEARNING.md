# WiseSub Learning Log

This document captures key learnings, decisions, and best practices from major implementations and refactors in the WiseSub project. Each entry is chronological and provides context for future developers.

---

## Table of Contents
- [Overview](#overview)
- [Learning Entries](#learning-entries)

---

## Overview

This learning log documents:
- What was implemented or changed
- Why decisions were made
- Best practices, patterns, and principles applied
- Trade-offs evaluated
- Key takeaways for future work

---

## Learning Entries

<!-- New entries will be appended below in chronological order -->

### [2025-11-25] - Property Tests for Task Requirements 7.1, 7.2, 5.1, 6.2, 2.1, 2.2, 3.1

**What Changed:**
- Created comprehensive test coverage for previously untested acceptance criteria
- Added 28+ new unit tests across three test files:
  - `AIExtractionServiceTests.cs` - Task 7.1 (required field extraction) and Task 7.2 (low confidence flagging)
  - `EmailIngestionServiceTests.cs` - Task 5.1 (12-month lookback) and Task 6.2 (metadata-only storage)
  - `GmailClientTests.cs` - Task 2.1 (OAuth connection), Task 2.2 (independent accounts), Task 3.1 (token deletion)
- Migrated repository integration tests from EF Core InMemory to SQLite in-memory database
- Fixed change tracker issue with `ExecuteUpdateAsync`

**Why:**
- The project had documented acceptance criteria in `tasks.md` but lacked corresponding automated tests
- Tests ensure business requirements are verifiable and prevent regression
- SQLite migration was necessary because EF Core's InMemory provider doesn't support `ExecuteUpdateAsync`

**Patterns & Best Practices:**
1. **Structural Tests over Mock-Heavy Tests**: For AI extraction service, used structural tests to verify model properties and constraints rather than complex generic method mocking. This avoids brittle tests tied to implementation details.

2. **Property-Based Testing Approach**: Tests verify properties/contracts (e.g., "low confidence should flag for review") rather than specific implementation paths.

3. **Test Database Selection**: 
   - EF Core InMemory: Fast but limited SQL feature support
   - SQLite in-memory: Slightly slower but supports real SQL operations like `ExecuteUpdateAsync`

4. **Change Tracker Management**: When using `ExecuteUpdateAsync`, the EF Core change tracker doesn't update tracked entities. Must call `ChangeTracker.Clear()` before re-reading to get fresh data.

**Trade-offs:**
| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| Mock OpenAI generic method | Tests actual service flow | Complex generic mocking, brittle | ❌ Rejected |
| Structural/property tests | Simple, stable, focused on contracts | Less integration coverage | ✅ Chosen |
| EF Core InMemory | Fast, simple setup | No `ExecuteUpdateAsync` support | ❌ Rejected |
| SQLite in-memory | Full SQL support | Slightly more setup | ✅ Chosen |

**Key Takeaways:**
1. **Test the contract, not the implementation**: When mocking becomes too complex, step back and test the observable properties and behaviors instead.

2. **Database provider matters for tests**: Production database features (like `ExecuteUpdateAsync`) may not work in test providers. Match your test database to production capabilities.

3. **ExecuteUpdateAsync caveat**: This EF Core method bypasses the change tracker. Always clear tracked entities before re-reading updated data.

4. **Acceptance criteria should drive test creation**: The `tasks.md` file contains verifiable acceptance criteria - each should have corresponding automated tests.

5. **Test file organization**: Group tests by feature/task using `#region` blocks for easy navigation and traceability to requirements.

---



### 2025-01-XX - Task 8: Subscription Management Service Implementation

**What Changed:**
- Created `ISubscriptionService` interface in `WiseSub.Application/Common/Interfaces/`
- Implemented `SubscriptionService` in `WiseSub.Application/Services/`
- Extended `ISubscriptionRepository` with new methods: `FindDuplicateAsync`, `ArchiveByEmailAccountAsync`, `GetRequiringReviewAsync`, `FindPotentialDuplicatesAsync`
- Registered `ISubscriptionService` in Application DI container
- Created `WiseSub.Application.Tests` project with 45 comprehensive tests
- Added `CreateSubscriptionRequest` DTO for subscription creation

**Why:**
- Task 8 required implementing subscription management with deduplication, status tracking, and billing cycle normalization
- Fuzzy matching (85% Levenshtein similarity threshold) prevents duplicate subscriptions for same service
- Low confidence extractions (<80%) are flagged for user review
- History tracking enables audit trail for price and status changes

**Patterns & Best Practices:**
1. **Result Pattern**: All service methods return `Result<T>` or `Result` for consistent error handling
2. **Repository Pattern**: Data access abstracted through `ISubscriptionRepository`
3. **Services Approach**: Business logic centralized in `SubscriptionService`, not in controllers or repositories
4. **Levenshtein Distance**: Used for fuzzy string matching to detect service name duplicates
5. **History Tracking**: Each subscription change is recorded with `SubscriptionHistory` entries
6. **Single Responsibility**: Service handles orchestration, repository handles data access

**Key Implementation Details:**

1. **Fuzzy Matching Algorithm:**
   ```csharp
   public static double CalculateSimilarity(string source, string target)
   {
       // Levenshtein distance normalized to 0-1 range
       int distance = LevenshteinDistance(source.ToLowerInvariant(), target.ToLowerInvariant());
       return 1.0 - ((double)distance / Math.Max(source.Length, target.Length));
   }
   ```

2. **Billing Cycle Normalization:**
   - Annual → Divide by 12
   - Quarterly → Divide by 3
   - Weekly → Multiply by 4.33
   - Monthly → No change
   - Unknown → Return original price

3. **Confidence-Based Review Flagging:**
   - Confidence < 80% → `RequiresUserReview = true`, `Status = PendingReview`
   - Confidence ≥ 80% → `RequiresUserReview = false`, `Status = Active`

**Trade-offs:**
- **85% Similarity Threshold**: Chosen to balance between catching typos/variations and avoiding false positives. Could be configurable.
- **Levenshtein vs Other Algorithms**: Simpler than Jaro-Winkler but sufficient for service names. Could upgrade if needed.
- **In-Memory Duplicate Check**: Loads all user subscriptions to check for duplicates. For users with many subscriptions, could optimize with database-side matching.

**Key Takeaways:**
1. Always use `Result<T>` pattern with `Error` objects - never string messages
2. Repository interface should be in Application layer, implementation in Infrastructure
3. History tracking should capture old/new values and source email ID for traceability
4. Test project structure mirrors source structure (Application.Tests for Application layer)
5. FluentAssertions methods differ across versions (`BeGreaterThanOrEqualTo` not `BeGreaterOrEqualTo`)
6. `IRepository<T>.AddAsync` returns `Task<T>` not `Task` - must mock correctly

**Files Modified/Created:**
- `src/WiseSub.Application/Common/Interfaces/ISubscriptionService.cs` (new)
- `src/WiseSub.Application/Common/Interfaces/ISubscriptionRepository.cs` (extended)
- `src/WiseSub.Application/Services/SubscriptionService.cs` (new)
- `src/WiseSub.Application/DependencyInjection.cs` (updated)
- `src/WiseSub.Infrastructure/Repositories/SubscriptionRepository.cs` (extended)
- `tests/WiseSub.Application.Tests/` (new project)
- `tests/WiseSub.Application.Tests/Services/SubscriptionServiceTests.cs` (new)

**Test Coverage:**
- 45 new tests covering:
  - Task 8.1: Database record creation for extracted subscriptions
  - Task 8.2: Billing cycle normalization (Annual/12, Quarterly/3, Weekly*4.33)
  - Fuzzy matching and deduplication
  - Status management and history tracking
  - Validation and error handling

---

### [2025-01-XX] - Task 9: Hangfire Background Job Infrastructure

**What Changed:**
- Added Hangfire NuGet packages to API and Infrastructure projects:
  - `Hangfire.AspNetCore` (1.8.22) to WiseSub.API
  - `Hangfire.InMemory` (1.0.1) to WiseSub.API
  - `Hangfire.Core` (1.8.22) to WiseSub.Infrastructure
- Created three background job classes in `WiseSub.Infrastructure/BackgroundServices/Jobs/`:
  - `EmailScanningJob` - Scans email accounts for subscription emails
  - `AlertGenerationJob` - Generates renewal alerts (7-day and 3-day warnings)
  - `SubscriptionUpdateJob` - Updates subscription statuses and renewal dates
- Extended repositories with new methods:
  - `IEmailAccountRepository.GetAllActiveAsync()` - Retrieves all active email accounts
  - `IAlertRepository.GetBySubscriptionAndTypeAsync()` - Finds existing alerts to prevent duplicates
- Created `HangfireAuthorizationFilter` for dashboard security
- Configured Hangfire in `Program.cs` with dashboard and recurring jobs

**Why:**
- Task 9 required implementing scheduled background processing for email scanning, alert generation, and subscription maintenance
- Hangfire provides a robust job scheduling framework with built-in retry logic and dashboard monitoring
- In-memory storage is appropriate for MVP; can upgrade to SQL Server/Redis in production

**Patterns & Best Practices:**

1. **AutomaticRetry Attribute**: Each job uses `[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]` for exponential backoff
   ```csharp
   [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
   public async Task ScanSingleAccountAsync(string emailAccountId)
   ```

2. **Job Orchestration Pattern**: Parent jobs schedule child jobs for parallel processing
   ```csharp
   public async Task ScanAllAccountsAsync()
   {
       var accounts = await _emailAccountRepository.GetAllActiveAsync();
       foreach (var account in accounts)
       {
           BackgroundJob.Enqueue<EmailScanningJob>(job => 
               job.ScanSingleAccountAsync(account.Id));
       }
   }
   ```

3. **Alert Deduplication**: AlertGenerationJob checks for existing alerts before creating new ones
   ```csharp
   var existingAlert = await _alertRepository.GetBySubscriptionAndTypeAsync(
       subscription.Id, AlertType.RenewalUpcoming7Days);
   if (existingAlert != null) continue;
   ```

4. **Status Transitions**: SubscriptionUpdateJob properly handles subscription lifecycle
   - `TrialActive` → `Active` when trial ends
   - Advances `NextRenewalDate` by billing cycle period

**Recurring Job Schedule:**
| Job | Schedule | Description |
|-----|----------|-------------|
| `EmailScanningJob` | `*/15 * * * *` | Every 15 minutes |
| `AlertGenerationJob` | `0 8 * * *` | Daily at 8 AM UTC |
| `SubscriptionUpdateJob` | `0 2 * * *` | Daily at 2 AM UTC |

**Dashboard Configuration:**
- Development: Open access at `/hangfire`
- Production: Protected by `HangfireAuthorizationFilter` (requires authentication)

**Trade-offs:**
| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| In-memory storage | Simple, no DB setup | Jobs lost on restart | ✅ Chosen for MVP |
| SQL Server storage | Persistent, scalable | More infrastructure | For production |
| Per-user local time alerts | Better UX | Complex timezone handling | Deferred |
| UTC-based scheduling | Simple, consistent | 8 AM UTC may not suit all users | ✅ Chosen |

**Key Takeaways:**
1. **Separate job classes from hosted services**: Hangfire jobs should be simple classes with methods, not `BackgroundService` implementations
2. **Use `[AutomaticRetry]` for exponential backoff**: Built-in retry is more reliable than manual implementation
3. **Check for duplicates before creating**: Alert generation must be idempotent to prevent duplicate notifications
4. **Dashboard security**: Always protect the Hangfire dashboard in production
5. **Job dependencies**: Use `BackgroundJob.Enqueue` for immediate work, `RecurringJob.AddOrUpdate` for schedules

**Files Modified/Created:**
- `src/WiseSub.API/WiseSub.API.csproj` (added Hangfire packages)
- `src/WiseSub.Infrastructure/WiseSub.Infrastructure.csproj` (added Hangfire.Core)
- `src/WiseSub.Infrastructure/BackgroundServices/Jobs/EmailScanningJob.cs` (new)
- `src/WiseSub.Infrastructure/BackgroundServices/Jobs/AlertGenerationJob.cs` (new)
- `src/WiseSub.Infrastructure/BackgroundServices/Jobs/SubscriptionUpdateJob.cs` (new)
- `src/WiseSub.Application/Common/Interfaces/IEmailAccountRepository.cs` (extended)
- `src/WiseSub.Infrastructure/Repositories/EmailAccountRepository.cs` (extended)
- `src/WiseSub.Application/Common/Interfaces/IAlertRepository.cs` (extended)
- `src/WiseSub.Infrastructure/Repositories/AlertRepository.cs` (extended)
- `src/WiseSub.API/Program.cs` (Hangfire configuration)
- `src/WiseSub.API/Middleware/HangfireAuthorizationFilter.cs` (new)

---

### [2025-12-01] - Database Migration: SQLite to SQL Server

**What Changed:**
- Replaced SQLite database provider with SQL Server in production
- Updated NuGet packages:
  - Removed: `Microsoft.EntityFrameworkCore.Sqlite` from Infrastructure project
  - Added: `Microsoft.EntityFrameworkCore.SqlServer` to Infrastructure project
  - Added: `Microsoft.EntityFrameworkCore.Sqlite` to Test project (for integration tests only)
- Updated connection strings in `appsettings.json`:
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=WiseSub;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
  ```
- Modified `DependencyInjection.cs`: Changed `UseSqlite()` to `UseSqlServer()`
- Modified `WiseSubDbContextFactory.cs`: Changed design-time factory to use SQL Server
- Deleted old SQLite migrations and created fresh SQL Server migration
- Kept SQLite in test project for repository integration tests (required for `ExecuteUpdateAsync` support)

**Why:**
- SQL Server provides enterprise-grade features needed for production:
  - Better concurrent access and locking
  - Full-text search capabilities
  - Advanced query optimization
  - Integration with Azure SQL for cloud deployment
- LocalDB provides zero-configuration development experience on Windows
- SQLite was fine for prototyping but lacks features needed at scale

**Patterns & Best Practices:**

1. **Separate Test Database Provider**: Tests use SQLite in-memory while production uses SQL Server
   ```csharp
   // Test setup (RepositoryIntegrationTests.cs)
   var options = new DbContextOptionsBuilder<WiseSubDbContext>()
       .UseSqlite(_connection)  // SQLite for tests
       .Options;
   
   // Production (DependencyInjection.cs)
   options.UseSqlServer(connectionString);  // SQL Server for production
   ```

2. **Connection String Validation**: Production code now throws if connection string is missing
   ```csharp
   var connectionString = configuration.GetConnectionString("DefaultConnection")
       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
   ```

3. **Migration Strategy**: 
   - Deleted all SQLite migrations
   - Created fresh `InitialCreate` migration for SQL Server
   - EF Core handled migration folder location automatically (`Migrations/` instead of `Data/Migrations/`)

**Connection String Formats:**
| Environment | Connection String |
|-------------|-------------------|
| Development (LocalDB) | `Server=(localdb)\\MSSQLLocalDB;Database=WiseSub;Trusted_Connection=True;...` |
| Development (Docker) | `Server=localhost,1433;Database=WiseSub;User Id=sa;Password=...;...` |
| Production (Azure) | `Server=tcp:xxx.database.windows.net,1433;Database=WiseSub;...` |

**Trade-offs:**
| Aspect | SQLite | SQL Server | Decision |
|--------|--------|------------|----------|
| Setup complexity | Zero | Requires LocalDB/instance | SQL Server for prod |
| Concurrent access | Limited | Excellent | SQL Server |
| Cloud deployment | Manual | Azure SQL native | SQL Server |
| Test speed | Fastest | Fast (LocalDB) | SQLite for tests |
| ExecuteUpdateAsync | ✅ Supported | ✅ Supported | Both work |
| InMemory provider | ❌ No ExecuteUpdateAsync | ❌ No ExecuteUpdateAsync | Use SQLite for tests |

**Key Takeaways:**
1. **Test database can differ from production**: It's acceptable to use SQLite for tests when SQL Server is used in production, as long as EF Core abstracts the differences
2. **ExecuteUpdateAsync requires real database**: EF Core InMemory provider doesn't support `ExecuteUpdateAsync`, so tests must use SQLite or SQL Server
3. **Migration cleanup**: When switching providers, delete all migrations and create fresh ones to avoid provider-specific SQL issues
4. **Connection string security**: Use `TrustServerCertificate=True` for development; use proper certificates in production
5. **LocalDB convenience**: SQL Server LocalDB provides full SQL Server features without installation overhead for Windows development

**Files Modified:**
- `src/WiseSub.Infrastructure/WiseSub.Infrastructure.csproj` - Swapped SQLite → SqlServer
- `src/WiseSub.Infrastructure/DependencyInjection.cs` - UseSqlServer()
- `src/WiseSub.Infrastructure/Data/WiseSubDbContextFactory.cs` - UseSqlServer()
- `src/WiseSub.API/appsettings.json` - SQL Server connection string
- `tests/WiseSub.Infrastructure.Tests/WiseSub.Infrastructure.Tests.csproj` - Added SQLite for tests
- `tests/WiseSub.Infrastructure.Tests/Repositories/RepositoryIntegrationTests.cs` - Updated comments
- `src/WiseSub.Infrastructure/Migrations/*` - New SQL Server migration

---

*Add new entries above this line*
