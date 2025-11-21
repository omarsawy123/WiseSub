# WiseSub - Subscription Management Platform

A subscription management SaaS platform that helps consumers track, manage, and control their recurring subscriptions.

## 🏗️ Architecture

This project follows **Clean Architecture** principles with clear separation of concerns across multiple layers:

```
WiseSub/
├── src/
│   ├── WiseSub.Domain/              # Domain Layer (Core Business Logic)
│   │   ├── Common/                  # Shared domain concepts
│   │   │   └── Result.cs           # Result pattern for operation outcomes
│   │   ├── Entities/                # Domain entities
│   │   │   ├── User.cs
│   │   │   ├── EmailAccount.cs
│   │   │   ├── Subscription.cs
│   │   │   ├── Alert.cs
│   │   │   ├── VendorMetadata.cs
│   │   │   ├── SubscriptionHistory.cs
│   │   │   ├── EmailMetadata.cs
│   │   │   └── UserPreferences.cs
│   │   └── Enums/                   # Domain enumerations
│   │       ├── SubscriptionTier.cs
│   │       ├── EmailProvider.cs
│   │       ├── BillingCycle.cs
│   │       ├── SubscriptionStatus.cs
│   │       ├── AlertType.cs
│   │       └── AlertStatus.cs
│   │
│   ├── WiseSub.Application/         # Application Layer (Use Cases)
│   │   ├── Common/
│   │   │   └── Interfaces/          # Service interfaces
│   │   │       └── IHealthService.cs
│   │   ├── Services/                # Service implementations
│   │   │   └── HealthService.cs
│   │   └── DependencyInjection.cs   # Application service registration
│   │
│   ├── WiseSub.Infrastructure/      # Infrastructure Layer (External Concerns)
│   │   ├── Data/
│   │   │   └── WiseSubDbContext.cs  # Entity Framework DbContext
│   │   └── DependencyInjection.cs   # Infrastructure service registration
│   │
│   └── WiseSub.API/                 # Presentation Layer (Web API)
│       ├── Controllers/
│       │   └── HealthController.cs
│       ├── Program.cs               # Application entry point
│       ├── appsettings.json
│       └── appsettings.Development.json
│
├── WiseSub.sln                      # Solution file
└── README.md
```

## 🎯 Clean Architecture Layers

### 1. Domain Layer (`WiseSub.Domain`)
- **Purpose**: Contains core business logic and domain entities
- **Dependencies**: None (completely independent)
- **Contents**:
  - Domain entities (User, Subscription, Alert, etc.)
  - Domain enums (SubscriptionTier, BillingCycle, etc.)
  - Result pattern for consistent error handling
  - Business rules and domain logic

### 2. Application Layer (`WiseSub.Application`)
- **Purpose**: Contains application business logic and use cases
- **Dependencies**: Domain layer only
- **Contents**:
  - Service interfaces (contracts)
  - Service implementations (business logic)
  - DTOs and response models
  - Application-level orchestration

### 3. Infrastructure Layer (`WiseSub.Infrastructure`)
- **Purpose**: Implements external concerns (database, external APIs, etc.)
- **Dependencies**: Domain and Application layers
- **Contents**:
  - Entity Framework DbContext
  - Repository implementations
  - External service integrations
  - Data access logic

### 4. Presentation Layer (`WiseSub.API`)
- **Purpose**: Exposes the application via REST API
- **Dependencies**: Application and Infrastructure layers
- **Contents**:
  - API Controllers
  - Request/Response models
  - Middleware
  - API configuration

## 🔄 Result Pattern

The project uses the **Result Pattern** for consistent error handling across all layers:

```csharp
// Success case
var result = Result.Success(data);

// Failure case
var result = Result.Failure<T>("Error message");

// Usage in controllers
if (result.IsFailure)
    return StatusCode(500, new { error = result.Error });

return Ok(result.Value);
```

**Benefits**:
- Explicit error handling (no exceptions for business logic failures)
- Type-safe error propagation
- Consistent API responses
- Better testability

## 🛠️ Technology Stack

- **Framework**: ASP.NET Core 10.0 Web API
- **Database**: SQLite with Entity Framework Core 10.0
- **Architecture**: Clean Architecture with CQRS principles
- **Patterns**: Result Pattern, Dependency Injection, Repository Pattern

## 🚀 Getting Started

### Prerequisites

- .NET 10.0 SDK

### Running the Application

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd WiseSub
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the solution**
   ```bash
   dotnet build
   ```

4. **Run the API**
   ```bash
   dotnet run --project src/WiseSub.API/WiseSub.API.csproj
   ```

5. **Access the API**
   - API: `http://localhost:5000`

### Health Check Endpoints

- `GET /api/health` - Basic health check
- `GET /api/health/db` - Database connectivity check

## 📦 Project Dependencies

### Domain Layer
- No external dependencies (pure C#)

### Application Layer
- `Microsoft.EntityFrameworkCore` (for DbContext abstraction)
- `Microsoft.Extensions.DependencyInjection.Abstractions`

### Infrastructure Layer
- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.Extensions.Configuration.Abstractions`

### API Layer
- References to Application and Infrastructure projects
- ASP.NET Core Web API framework

## 🗄️ Database

The application uses **SQLite** for data storage:
- Database file: `wisesub.db` (created automatically on first run)
- Migrations: Managed by Entity Framework Core
- Schema: Defined in `WiseSubDbContext.cs`

### Entity Relationships

```
User (1) ──→ (N) EmailAccount
User (1) ──→ (N) Subscription
User (1) ──→ (N) Alert

EmailAccount (1) ──→ (N) Subscription
EmailAccount (1) ──→ (N) EmailMetadata

Subscription (1) ──→ (N) Alert
Subscription (1) ──→ (N) SubscriptionHistory
Subscription (N) ──→ (1) VendorMetadata [optional]
```

## 🧪 Testing Strategy

The project is designed for comprehensive testing:

- **Unit Tests**: Test individual components in isolation
- **Integration Tests**: Test layer interactions
- **Property-Based Tests**: Verify correctness properties (planned)

## 📝 Development Guidelines

### Adding a New Feature

1. **Define domain entities** in `WiseSub.Domain/Entities/`
2. **Create service interface** in `WiseSub.Application/Common/Interfaces/`
3. **Implement service** in `WiseSub.Application/Services/`
4. **Register service** in `WiseSub.Application/DependencyInjection.cs`
5. **Create controller** in `WiseSub.API/Controllers/`
6. **Use Result pattern** for all service methods

### Dependency Flow

```
API → Application → Domain
  ↓
Infrastructure → Application → Domain
```

**Rules**:
- Domain layer has NO dependencies
- Application layer depends ONLY on Domain
- Infrastructure layer depends on Domain and Application
- API layer depends on Application and Infrastructure
- Dependencies flow inward (toward Domain)

## 🔐 Security Considerations

- OAuth tokens encrypted with AES-256 (planned)
- CORS configured for frontend integration
- Input validation at API boundary
- Secure connection strings in configuration

## 📚 Additional Documentation

- [Architecture Decision Records](docs/architecture/README.md) (planned)
- [API Documentation](docs/api/README.md) (planned)
- [Database Schema](docs/database/README.md) (planned)

## 🎯 Requirements Satisfied

This implementation satisfies the following requirements from the specification:
- **1.1**: User authentication structure
- **2.1**: Email ingestion data models
- **3.1**: Subscription management entities
- **8.1**: Security-focused data models with encryption fields

## 🚧 Next Steps

Future tasks will implement:
- Repository pattern for data access
- User authentication and OAuth integration
- Email ingestion service
- AI extraction engine
- Alert service
- Dashboard service
- And more...

## 📄 License

[Your License Here]

## 👥 Contributors

[Your Team Here]
