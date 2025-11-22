# GitHub Copilot Instructions for WiseSub

## 📋 Before Making Any Changes

**ALWAYS read these documents first to understand the project context:**

1. **`README.md`** - Project overview, architecture layers, and technology stack
2. **`docs/ARCHITECTURE.md`** - Detailed architecture, design patterns, and dependency rules
3. **`docs/AUTHENTICATION.md`** - Authentication flow and security configuration
4. **`docs/DATABASE_IMPLEMENTATION.md`** - Database schema and implementation details

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

### Project Structure
- `WiseSub.Domain/` - Entities, enums, business logic (NO dependencies)
- `WiseSub.Application/` - Services, interfaces, DTOs (depends on Domain)
- `WiseSub.Infrastructure/` - Database, external APIs, repositories
- `WiseSub.API/` - Controllers, middleware, API configuration

## ✅ Code Standards

### 1. Use Result Pattern
Always return `Result<T>` or `Result` from service methods:
```csharp
public async Task<Result<User>> GetUserAsync(string id)
{
    if (user == null)
        return Result.Failure<User>("User not found");
    
    return Result.Success(user);
}
```

### 2. Dependency Injection
Register services in `DependencyInjection.cs` files:
- Application services → `WiseSub.Application/DependencyInjection.cs`
- Infrastructure services → `WiseSub.Infrastructure/DependencyInjection.cs`

### 3. Clean Architecture
- Keep Domain layer pure (no framework dependencies)
- Define interfaces in Application layer
- Implement interfaces in Infrastructure layer
- Controllers should only orchestrate, not contain business logic

## 🔐 Security Requirements

- Use AES-256 encryption for OAuth tokens via `ITokenEncryptionService`
- Store sensitive config in `appsettings.json` (use Azure Key Vault in production)
- Never commit secrets to version control
- Use JWT tokens for API authentication
- Implement proper error handling (no sensitive data in error messages)

## 💾 Database Guidelines

- Database: SQLite (MVP), upgradeable to Azure SQL
- Use Entity Framework Core for data access
- Apply migrations: `dotnet ef migrations add <name>`
- All entities should have proper indexes for performance
- Use navigation properties for relationships

## 🧪 Testing

- Write unit tests for all services
- Test location: `tests/WiseSub.*.Tests/`
- Run tests before committing: `dotnet test`
- Aim for high coverage on business logic

## 📝 Naming Conventions

- **Entities**: Singular names (User, Subscription, Alert)
- **Services**: End with "Service" (UserService, HealthService)
- **Interfaces**: Start with "I" (IUserService, IHealthService)
- **Controllers**: End with "Controller" (AuthController)
- **DTOs**: Descriptive names (LoginRequest, AuthResponse)

## 🚫 What NOT to Do

- ❌ Don't add dependencies to the Domain layer
- ❌ Don't put business logic in Controllers
- ❌ Don't use exceptions for business logic failures (use Result pattern)
- ❌ Don't hardcode configuration values
- ❌ Don't skip reading the documentation before making changes
- ❌ Don't modify database schema without creating migrations

## ✨ Quick Reference

**Add a new feature:**
1. Read relevant docs first
2. Add entities to `WiseSub.Domain/Entities/`
3. Create interface in `WiseSub.Application/Common/Interfaces/`
4. Implement service in `WiseSub.Application/Services/`
5. Register in `DependencyInjection.cs`
6. Create controller in `WiseSub.API/Controllers/`
7. Use Result pattern for all service methods
8. Write unit tests

**Current Tech Stack:**
- .NET 10.0
- ASP.NET Core Web API
- Entity Framework Core 10.0
- SQLite database
- OAuth 2.0 (Google)
- JWT authentication
- AES-256 encryption

---

**Remember:** When in doubt, check the docs folder first! 📚
