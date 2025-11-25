# GitHub Copilot Instructions for WiseSub

> **For AI Agents**: This document contains critical project rules. Read this ENTIRELY before making any code changes.

## 📋 Before Making Any Changes

**ALWAYS read these documents first to understand the project context:**

1. **`README.md`** - Project overview, architecture layers, and technology stack
2. **`docs/ARCHITECTURE.md`** - Detailed architecture, design patterns, and dependency rules
3. **`docs/AUTHENTICATION.md`** - Authentication flow and security configuration
4. **`docs/DATABASE_IMPLEMENTATION.md`** - Database schema and implementation details
5. **`docs/AI_EXTRACTION_SERVICE.md`** - AI/OpenAI integration for email extraction

## 🏗️ Architecture Rules

### Layer Dependencies (MUST FOLLOW)
```
API → Application → Domain
  ↓
Infrastructure → Application → Domain
```

**Rules:**
- Domain layer has NO dependencies on other layers
- Application layer depends ONLY on Domain
- Infrastructure depends on Domain and Application
- API depends on Application and Infrastructure
- Dependencies flow INWARD toward Domain

### Project Structure (Full Paths)
```
src/
├── WiseSub.Domain/           # Core domain (NO external dependencies)
│   ├── Common/               # Result.cs, Error.cs
│   ├── Entities/             # User, Subscription, Alert, EmailAccount, etc.
│   └── Enums/                # SubscriptionTier, BillingCycle, AlertType, etc.
│
├── WiseSub.Application/      # Business logic layer (depends on Domain only)
│   ├── Common/
│   │   ├── Interfaces/       # ALL service & repository interfaces
│   │   ├── Models/           # DTOs: AuthenticationResult, ClassificationResult, etc.
│   │   └── Configuration/    # EmailScanConfiguration
│   ├── Services/             # UserService, HealthService, EmailMetadataService
│   └── DependencyInjection.cs
│
├── WiseSub.Infrastructure/   # External implementations
│   ├── AI/                   # AIExtractionService, OpenAIClient
│   ├── Authentication/       # GoogleAuthenticationService
│   ├── BackgroundServices/   # EmailProcessorService
│   ├── Data/                 # WiseSubDbContext, Migrations
│   ├── Email/                # GmailClient, EmailIngestionService, EmailProviderFactory
│   ├── Repositories/         # UserRepository, SubscriptionRepository, etc.
│   ├── Security/             # TokenEncryptionService
│   └── DependencyInjection.cs
│
└── WiseSub.API/              # Presentation layer
    ├── Controllers/          # AuthController, HealthController
    ├── Middleware/           # GlobalExceptionHandler
    └── Program.cs            # Entry point & DI configuration
```

## ✅ Code Standards

### 1. Use Result Pattern with Error Objects
**CRITICAL**: Always use the `Error` record class, NOT plain strings:

```csharp
// ✅ CORRECT - Use predefined Error objects from WiseSub.Domain.Common
using WiseSub.Domain.Common;

public async Task<Result<User>> GetUserAsync(string id)
{
    var user = await _userRepository.GetByIdAsync(id);
    if (user == null)
        return Result.Failure<User>(UserErrors.NotFound);  // Use Error object
    
    return Result.Success(user);
}

// ❌ WRONG - Don't use string messages
return Result.Failure<User>("User not found");
```

**Existing Error Categories** (in `WiseSub.Domain/Common/Error.cs`):
- `UserErrors` - NotFound, AlreadyExists, InvalidEmail, TierLimitExceeded
- `SubscriptionErrors` - NotFound, AlreadyExists, InvalidPrice, InvalidBillingCycle
- `EmailAccountErrors` - NotFound, AlreadyConnected, TokenExpired, ConnectionFailed
- `AlertErrors` - NotFound, AlreadySent, InvalidSchedule, SendFailed
- `AuthenticationErrors` - InvalidCredentials, InvalidToken, Unauthorized
- `VendorErrors` - NotFound, AlreadyExists, InvalidName

**Add new errors** by extending the appropriate static class in `Error.cs`.

### 2. Repository Pattern
- **Interfaces** go in `WiseSub.Application/Common/Interfaces/`
- **Implementations** go in `WiseSub.Infrastructure/Repositories/`
- Use the generic `IRepository<T>` base interface for common CRUD operations
- Create specific repository interfaces (e.g., `IUserRepository`) for entity-specific queries

**Existing Repositories:**
- `IRepository<T>` / `Repository<T>` - Generic base
- `IUserRepository` / `UserRepository` - User queries (GetByEmail, GetByOAuth)
- `ISubscriptionRepository` / `SubscriptionRepository`
- `IEmailAccountRepository` / `EmailAccountRepository`
- `IAlertRepository` / `AlertRepository`
- `IVendorMetadataRepository` / `VendorMetadataRepository`
- `IEmailMetadataRepository` / `EmailMetadataRepository`

### 3. Dependency Injection Registration

**Application Layer** (`WiseSub.Application/DependencyInjection.cs`):
```csharp
services.AddScoped<IHealthService, HealthService>();
services.AddScoped<IUserService, UserService>();
services.AddScoped<IEmailMetadataService, EmailMetadataService>();
```

**Infrastructure Layer** (`WiseSub.Infrastructure/DependencyInjection.cs`):
```csharp
// Repositories
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
services.AddScoped<IUserRepository, UserRepository>();

// External Services
services.AddScoped<IAuthenticationService, GoogleAuthenticationService>();
services.AddScoped<IGmailClient, GmailClient>();
services.AddSingleton<IOpenAIClient, OpenAIClient>();
services.AddScoped<IAIExtractionService, AIExtractionService>();
```

### 4. Application Layer: Services Approach
The Application layer uses the **Services approach** (not use-cases/CQRS). Services group related operations together by domain concept.

**Service Design Guidelines:**
- One service per domain aggregate (e.g., `UserService`, `SubscriptionService`)
- Services contain business logic orchestration
- Services call repositories for data access
- All public methods return `Result<T>` or `Result`
- Services are registered as `Scoped` in DI

```csharp
// ✅ CORRECT - Service groups related operations
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    
    public async Task<Result<User>> GetUserByIdAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return Result.Failure<User>(UserErrors.NotFound);
        return Result.Success(user);
    }
    
    public async Task<Result<User>> CreateUserAsync(string email, string name, ...) { ... }
    public async Task<Result> UpdateUserAsync(User user) { ... }
    public async Task<Result> DeleteUserDataAsync(string userId) { ... }
}
```

**Existing Application Services:**
- `UserService` - User CRUD, preferences, GDPR export/deletion
- `HealthService` - Health check operations
- `EmailMetadataService` - Email processing metadata

### 5. Controller Implementation
Controllers should ONLY orchestrate - delegate to services, no business logic:

```csharp
[Authorize]
[HttpGet("me")]
public async Task<IActionResult> GetCurrentUser()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Unauthorized();

    var result = await _userService.GetUserByIdAsync(userId);
    
    if (result.IsFailure)
        return NotFound(new { error = result.ErrorMessage });

    return Ok(result.Value);
}
```

## 🔐 Security Requirements

- Use `ITokenEncryptionService` (AES-256) for OAuth tokens
- Store sensitive config in `appsettings.json` (use Azure Key Vault in production)
- Never commit secrets to version control
- Use JWT tokens for API authentication (configured in `Program.cs`)
- Use `GlobalExceptionHandler` for unexpected exceptions only
- Return `Result.Failure()` for expected business errors

## 💾 Database Guidelines

- Database: SQLite (MVP) with `WiseSubDbContext`
- Use Entity Framework Core for data access
- Apply migrations: 
  ```bash
  dotnet ef migrations add <name> --project src/WiseSub.Infrastructure --startup-project src/WiseSub.API
  ```
- All entities should have proper indexes (defined in `WiseSubDbContext`)
- Use navigation properties for relationships

**Existing Entities:**
- `User` - Core user with preferences stored as JSON
- `EmailAccount` - Connected email accounts (Gmail)
- `Subscription` - Tracked subscriptions
- `Alert` - Renewal/price change alerts
- `VendorMetadata` - Subscription vendor information
- `EmailMetadata` - Processed email tracking
- `SubscriptionHistory` - Price/status change history

## 🤖 AI/OpenAI Integration

Located in `WiseSub.Infrastructure/AI/`:
- `IOpenAIClient` / `OpenAIClient` - Low-level API wrapper
- `IAIExtractionService` / `AIExtractionService` - Email classification & extraction

**Configuration** (in `appsettings.json`):
```json
{
  "OpenAI": {
    "ApiKey": "YOUR_KEY",
    "Model": "gpt-4o-mini",
    "MaxRetries": 3,
    "InitialRetryDelayMs": 1000
  }
}
```

## 📧 Email Services

- `IGmailClient` / `GmailClient` - Gmail API integration
- `IEmailProviderFactory` / `EmailProviderFactory` - Provider abstraction
- `IEmailIngestionService` / `EmailIngestionService` - Email processing pipeline
- `IEmailQueueService` / `EmailQueueService` - Async email queue
- `EmailProcessorService` - Background service for processing

## 🧪 Testing

- Test projects: `tests/WiseSub.*.Tests/`
- Run tests: `dotnet test`
- Test structure mirrors source structure
- Use mocks for repository and external service dependencies

## 📝 Naming Conventions

- **Entities**: Singular names (`User`, `Subscription`, `Alert`)
- **Services**: End with `Service` (`UserService`, `AIExtractionService`)
- **Interfaces**: Start with `I` (`IUserService`, `IOpenAIClient`)
- **Repositories**: End with `Repository` (`UserRepository`)
- **Controllers**: End with `Controller` (`AuthController`)
- **DTOs/Models**: Descriptive names (`AuthenticationResult`, `ClassificationResult`)
- **Errors**: `{Domain}Errors.{ErrorName}` (`UserErrors.NotFound`)

## 📚 Documentation Requirements

### LEARNING.md - Knowledge Capture

**CRITICAL**: After each major implementation or refactor, you MUST append a new section to `LEARNING.md` documenting:

1. **What was implemented or changed** - Specific files, classes, methods modified
2. **Why the decision was made** - Business requirements, technical reasons, problem being solved
3. **Best practices, patterns, and principles applied** - Design patterns, SOLID principles, architectural decisions
4. **Trade-offs evaluated** - Alternative approaches considered and why they were rejected
5. **Key learnings** - What future developers should understand from this change

**Path** to file: `LEARNING.md` in root directory.

**Format for each entry:**
```markdown
### [YYYY-MM-DD] - [Brief Title]

**What Changed:**
- List specific changes made

**Why:**
- Explain the reasoning and context

**Patterns & Best Practices:**
- Document patterns used (Result pattern, Repository pattern, etc.)
- Note any architectural principles followed

**Trade-offs:**
- What alternatives were considered?
- Why was this approach chosen?

**Key Takeaways:**
- Important lessons learned
- Things to remember for future work
```

**When to create an entry:**
- Adding a new service or repository
- Implementing a new feature
- Major refactoring (e.g., changing error handling approach)
- Performance optimizations
- Security improvements
- Fixing critical bugs that required design changes

**Example scenarios requiring LEARNING.md entries:**
- Implementing the Result pattern across services
- Adding OpenAI integration for email extraction
- Fixing the AES encryption IV vulnerability
- Implementing rate limiting
- Adding background email processing service

Keep entries chronological and concise but informative. The goal is to build institutional knowledge for the project.

## 🚫 What NOT to Do

- ❌ Don't add dependencies to the Domain layer
- ❌ Don't put business logic in Controllers
- ❌ Don't use exceptions for business logic failures (use Result pattern)
- ❌ Don't use string messages with Result.Failure() - use Error objects
- ❌ Don't hardcode configuration values
- ❌ Don't skip reading the documentation before making changes
- ❌ Don't modify database schema without creating migrations
- ❌ Don't create interfaces outside `WiseSub.Application/Common/Interfaces/`
- ❌ Don't implement external service clients in Application layer

## ✨ Task Workflows

### Adding a New Entity
1. Create entity in `WiseSub.Domain/Entities/`
2. Add `DbSet<T>` to `WiseSubDbContext`
3. Configure entity in `OnModelCreating` (indexes, relationships)
4. Create migration
5. Create repository interface in `WiseSub.Application/Common/Interfaces/`
6. Implement repository in `WiseSub.Infrastructure/Repositories/`
7. Register in `DependencyInjection.cs`

### Adding a New Service
1. Create interface in `WiseSub.Application/Common/Interfaces/`
2. Implement service in `WiseSub.Application/Services/` (business logic) 
   OR `WiseSub.Infrastructure/` (external integrations)
3. Register in appropriate `DependencyInjection.cs`
4. Use Result pattern with Error objects for all public methods
5. Write unit tests

### Adding a New API Endpoint
1. Add method to existing controller or create new controller
2. Inject required services via constructor
3. Validate input, call service, map Result to HTTP response
4. Add `[Authorize]` attribute if authentication required
5. Test endpoint

### Adding New Error Types
1. Add new `Error` record to appropriate class in `WiseSub.Domain/Common/Error.cs`
2. Use format: `new Error("{Domain}.{ErrorCode}", "Human readable message")`

## 🔧 Current Tech Stack
- .NET 10.0
- ASP.NET Core Web API
- Entity Framework Core 10.0
- SQLite database
- OAuth 2.0 (Google) via `GoogleAuthenticationService`
- JWT authentication (24-hour expiry)
- AES-256 encryption via `TokenEncryptionService`
- OpenAI API (gpt-4o-mini) via `OpenAIClient`
- Gmail API via `GmailClient`

---

## ⚠️ Known Issues & Technical Debt (Tasks 1-7)

> **For AI Agents**: These are documented issues that need fixing. Reference this when working on related code.

### 🔴 CRITICAL - Must Fix

| Issue | Location | Description |
|-------|----------|-------------|
| ~~**DequeueNextEmailAsync returns null**~~ | ~~`EmailQueueService.cs`~~ | ✅ **FIXED** - Now properly dequeues by priority |
| ~~**Static IV in AES encryption**~~ | ~~`TokenEncryptionService.cs`~~ | ✅ **FIXED** - Now uses random IV per encryption |
| ~~**No API rate limiting**~~ | ~~`Program.cs`~~ | ✅ **FIXED** - Added rate limiting (10 req/min for auth, 100 req/min global) |
| **No OpenAI retry logic** | `OpenAIClient.cs` | Config has `MaxRetries` but not implemented |

### 🟡 MEDIUM - Should Fix

| Issue | Location | Description |
|-------|----------|-------------|
| ~~N+1 queries in spending~~ | ~~`SubscriptionRepository.cs`~~ | ✅ **FIXED** - Now uses projection to load only required fields |
| Multiple DB calls for updates | `EmailAccountRepository.cs` | ~~`UpdateTokensAsync` does GET + UPDATE instead of single call~~ ✅ **FIXED** - Now uses ExecuteUpdateAsync |
| ~~Missing unique index~~ | ~~`WiseSubDbContext.cs`~~ | ✅ **FIXED** - `EmailMetadata.ExternalEmailId` now unique |
| No AI classification caching | `AIExtractionService.cs` | Every email triggers API call - should cache patterns |
| Sequential AI calls | `AIExtractionService.cs` | Classification + extraction should be single API call |
| ~~Exception details in production~~ | ~~`GlobalExceptionHandler.cs`~~ | ✅ **FIXED** - Now hides details in production |
| Health endpoints unprotected | `HealthController.cs` | `/api/health/db` exposes database status |
| ~~Missing input validation~~ | ~~`AuthController.cs`~~ | ✅ **FIXED** - Added `[Required]` attributes to DTOs |
| ~~Inefficient polling~~ | ~~`EmailProcessorService.cs`~~ | ✅ **FIXED** - Now uses WaitForEmailAsync instead of 5s polling |

### 🧪 Missing Tests (Property Tests from tasks.md)

| Task | Test | Status |
|------|------|--------|
| 7.1 | Required field extraction (AI) | ❌ Missing |
| 7.2 | Low confidence flagging (AI) | ❌ Missing |
| 5.1 | Initial email retrieval span (12 months) | ❌ Missing |
| 6.2 | Metadata-only storage verification | ❌ Missing |
| 2.1 | Connection establishment after OAuth | ❌ Missing |
| 2.2 | Independent account management | ❌ Missing |
| 3.1 | Token deletion on revocation | ⚠️ Partial |

### 📁 Services Without Tests

- `OpenAIClient` - No tests
- `EmailIngestionService` - No tests  
- `EmailProviderFactory` - No tests
- `EmailMetadataService` - No tests
- `HealthService` - No tests
- `AIExtractionService` - Only constructor test (needs full coverage)

---

**Remember:** When in doubt, check the `docs/` folder first! 📚
