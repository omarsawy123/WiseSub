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
