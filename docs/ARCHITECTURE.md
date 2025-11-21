# WiseSub Architecture Documentation

## Overview

WiseSub follows **Clean Architecture** principles, ensuring a maintainable, testable, and scalable codebase. The architecture is organized into four distinct layers, each with specific responsibilities and dependencies.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│                      (WiseSub.API)                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │ Controllers  │  │  Middleware  │  │   Program.cs │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Application Layer                          │
│                  (WiseSub.Application)                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  Services    │  │  Interfaces  │  │     DTOs     │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                 Infrastructure Layer                         │
│                (WiseSub.Infrastructure)                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  DbContext   │  │ Repositories │  │ External APIs│     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Domain Layer                            │
│                     (WiseSub.Domain)                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   Entities   │  │     Enums    │  │    Result    │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

## Layer Responsibilities

### 1. Domain Layer (Core)

**Location**: `src/WiseSub.Domain/`

**Purpose**: Contains the core business logic and domain model. This is the heart of the application.

**Characteristics**:
- ✅ No dependencies on other layers
- ✅ Contains only pure C# code
- ✅ Framework-agnostic
- ✅ Highly testable

**Contents**:
- **Entities**: Core business objects (User, Subscription, Alert, etc.)
- **Enums**: Domain-specific enumerations
- **Value Objects**: Immutable objects defined by their values
- **Domain Events**: Events that occur within the domain
- **Result Pattern**: Consistent error handling mechanism

**Example**:
```csharp
namespace WiseSub.Domain.Entities;

public class Subscription
{
    public string Id { get; set; }
    public string ServiceName { get; set; }
    public decimal Price { get; set; }
    public BillingCycle BillingCycle { get; set; }
    // ... other properties
}
```

### 2. Application Layer (Use Cases)

**Location**: `src/WiseSub.Application/`

**Purpose**: Orchestrates the flow of data and implements application-specific business rules.

**Dependencies**: Domain layer only

**Contents**:
- **Service Interfaces**: Contracts for application services
- **Service Implementations**: Business logic orchestration
- **DTOs**: Data transfer objects for cross-layer communication
- **Validators**: Input validation logic
- **Mappers**: Entity-to-DTO mapping

**Example**:
```csharp
namespace WiseSub.Application.Services;

public class HealthService : IHealthService
{
    private readonly DbContext _dbContext;

    public async Task<Result<HealthCheckResponse>> CheckHealthAsync()
    {
        // Application logic here
        return Result.Success(response);
    }
}
```

**Dependency Injection**:
```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IHealthService, HealthService>();
        return services;
    }
}
```

### 3. Infrastructure Layer (External Concerns)

**Location**: `src/WiseSub.Infrastructure/`

**Purpose**: Implements interfaces defined in the Application layer and handles external concerns.

**Dependencies**: Domain and Application layers

**Contents**:
- **DbContext**: Entity Framework database context
- **Repositories**: Data access implementations
- **External Services**: Third-party API integrations (Gmail, OpenAI, SendGrid)
- **File System**: File storage implementations
- **Caching**: Cache implementations

**Example**:
```csharp
namespace WiseSub.Infrastructure.Data;

public class WiseSubDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    // ... other DbSets
}
```

**Dependency Injection**:
```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<WiseSubDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));
        
        return services;
    }
}
```

### 4. Presentation Layer (API)

**Location**: `src/WiseSub.API/`

**Purpose**: Exposes the application functionality via REST API endpoints.

**Dependencies**: Application and Infrastructure layers

**Contents**:
- **Controllers**: API endpoints
- **Middleware**: Request/response pipeline components
- **Filters**: Cross-cutting concerns (authentication, validation)
- **Configuration**: Application startup and configuration

**Example**:
```csharp
namespace WiseSub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _healthService.CheckHealthAsync();
        
        if (result.IsFailure)
            return StatusCode(500, new { error = result.Error });
        
        return Ok(result.Value);
    }
}
```

## Design Patterns

### 1. Result Pattern

**Purpose**: Provide explicit error handling without exceptions for business logic failures.

**Implementation**:
```csharp
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }
    
    public static Result Success() => new Result(true, string.Empty);
    public static Result Failure(string error) => new Result(false, error);
}

public class Result<T> : Result
{
    public T Value { get; }
}
```

**Benefits**:
- Explicit error handling
- Type-safe error propagation
- No hidden control flow (unlike exceptions)
- Better testability
- Consistent API responses

**Usage**:
```csharp
// Service layer
public async Task<Result<User>> GetUserAsync(string id)
{
    var user = await _repository.FindAsync(id);
    
    if (user == null)
        return Result.Failure<User>("User not found");
    
    return Result.Success(user);
}

// Controller layer
var result = await _userService.GetUserAsync(id);

if (result.IsFailure)
    return NotFound(new { error = result.Error });

return Ok(result.Value);
```

### 2. Dependency Injection

**Purpose**: Achieve loose coupling and improve testability.

**Implementation**: Each layer has a `DependencyInjection.cs` file that registers its services.

**Usage in Program.cs**:
```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

### 3. Repository Pattern (Planned)

**Purpose**: Abstract data access logic and provide a collection-like interface.

**Future Implementation**:
```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(string id);
}
```

## Dependency Rules

### The Dependency Rule

**Core Principle**: Dependencies must point inward toward the Domain layer.

```
API → Application → Domain
  ↓
Infrastructure → Application → Domain
```

**Rules**:
1. Domain layer has NO dependencies on other layers
2. Application layer depends ONLY on Domain
3. Infrastructure layer depends on Domain and Application
4. API layer depends on Application and Infrastructure
5. Outer layers can depend on inner layers, but NOT vice versa

**Why This Matters**:
- Domain logic remains pure and testable
- Business rules are independent of frameworks
- Easy to swap out infrastructure (e.g., change database)
- Clear separation of concerns

## Data Flow

### Request Flow (Inbound)

```
1. HTTP Request
   ↓
2. Controller (API Layer)
   ↓
3. Service (Application Layer)
   ↓
4. Repository (Infrastructure Layer)
   ↓
5. Database
```

### Response Flow (Outbound)

```
1. Database
   ↓
2. Repository (Infrastructure Layer)
   ↓
3. Service (Application Layer)
   ↓
4. Controller (API Layer)
   ↓
5. HTTP Response (with Result pattern)
```

## Error Handling Strategy

### Layers of Error Handling

1. **Domain Layer**: Business rule violations return Result.Failure
2. **Application Layer**: Orchestration errors return Result.Failure
3. **Infrastructure Layer**: External service failures return Result.Failure
4. **API Layer**: Converts Result to appropriate HTTP status codes

### HTTP Status Code Mapping

```csharp
if (result.IsFailure)
{
    // Map error to appropriate status code
    if (result.Error.Contains("not found"))
        return NotFound(new { error = result.Error });
    
    if (result.Error.Contains("unauthorized"))
        return Unauthorized(new { error = result.Error });
    
    return BadRequest(new { error = result.Error });
}
```

## Testing Strategy

### Unit Testing

- **Domain Layer**: Test entities and business logic in isolation
- **Application Layer**: Test services with mocked dependencies
- **Infrastructure Layer**: Test repositories with in-memory database
- **API Layer**: Test controllers with mocked services

### Integration Testing

- Test complete request/response flow
- Use test database (SQLite in-memory)
- Verify layer interactions

### Property-Based Testing

- Verify correctness properties across all inputs
- Test invariants and business rules
- Ensure data consistency

## Configuration Management

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=wisesub.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Environment-Specific Configuration

- `appsettings.json`: Base configuration
- `appsettings.Development.json`: Development overrides
- `appsettings.Production.json`: Production overrides

## Security Considerations

### Current Implementation

- CORS configured for frontend integration
- HTTPS redirection enabled
- Connection strings in configuration (not hardcoded)

### Planned Security Features

- OAuth 2.0 authentication
- JWT token-based authorization
- AES-256 encryption for sensitive data
- Rate limiting
- Input validation and sanitization

## Performance Considerations

### Database Optimization

- Indexes on frequently queried fields
- Pagination for large result sets
- Connection pooling
- Lazy loading disabled (explicit loading)

### Caching Strategy (Planned)

- In-memory caching for frequently accessed data
- Distributed caching (Redis) for scale
- Cache invalidation strategies

## Scalability

### Horizontal Scaling

- Stateless API servers (can add more instances)
- Background job workers (can add more workers)
- Database read replicas for reporting

### Vertical Scaling

- Upgrade database tier as data grows
- Upgrade app service tier for more CPU/memory

## Monitoring and Observability (Planned)

- Health check endpoints
- Structured logging with correlation IDs
- Application Insights integration
- Performance metrics tracking
- Error tracking and alerting

## Future Enhancements

1. **CQRS Pattern**: Separate read and write models
2. **Event Sourcing**: Store state changes as events
3. **Domain Events**: Decouple domain logic with events
4. **Mediator Pattern**: Simplify request handling
5. **Specification Pattern**: Encapsulate query logic

## References

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Domain-Driven Design by Eric Evans](https://www.domainlanguage.com/ddd/)
- [Microsoft .NET Architecture Guides](https://dotnet.microsoft.com/learn/dotnet/architecture-guides)
