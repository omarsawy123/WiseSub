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

*Add new entries above this line*
