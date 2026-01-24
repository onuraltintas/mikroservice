# EduPlatform Test Suite

This directory contains all automated tests for the EduPlatform microservices architecture.

## 📁 Structure

```
tests/
├── Shared.IntegrationTests/          # Shared test infrastructure
│   ├── Fixtures/                     # Testcontainer fixtures
│   │   ├── PostgresFixture.cs        # PostgreSQL container
│   │   ├── RabbitMqFixture.cs        # RabbitMQ container
│   │   ├── RedisFixture.cs           # Redis container
│   │   ├── KeycloakFixture.cs        # Keycloak container
│   │   └── MailCatcherFixture.cs     # MailCatcher container
│   └── Helpers/                      # Test helper utilities
│
├── Integration/                      # Integration tests
│   ├── Identity.API.IntegrationTests/
│   │   ├── HealthCheckTests.cs       # Health endpoint tests
│   │   ├── EventPublishingTests.cs   # RabbitMQ event tests
│   │   └── EmailTests.cs             # Email functionality tests
│   ├── Coaching.API.IntegrationTests/
│   └── Gateway.IntegrationTests/
│
├── Unit/                             # Unit tests (TODO)
│   ├── Identity.Domain.Tests/
│   ├── Identity.Application.Tests/
│   ├── Coaching.Domain.Tests/
│   └── Coaching.Application.Tests/
│
└── E2E/                              # End-to-end tests (TODO)
    └── EduPlatform.E2E.Tests/
```

## 🚀 Running Tests

### Prerequisites

- **.NET 9.0 SDK** installed
- **Docker** running (for Testcontainers)

### Run All Tests

```bash
# From project root
dotnet test

# With detailed output
dotnet test --logger "console;verbosity=detailed"

# With code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Run Specific Test Project

```bash
# Integration tests only
dotnet test tests/Integration/Identity.API.IntegrationTests

# Specific test class
dotnet test --filter "FullyQualifiedName~EmailTests"

# Specific test method
dotnet test --filter "FullyQualifiedName~EmailTests.SendEmail_ShouldAppearInMailCatcher"
```

### Run Tests by Category

```bash
# Run tests in a specific collection
dotnet test --filter "Category=Database"
dotnet test --filter "Category=MessageBus"
dotnet test --filter "Category=Email"
```

## 🧪 Test Categories

### 1. **Integration Tests** (Current Focus)

Integration tests use **Testcontainers** to spin up real infrastructure:

- ✅ **PostgreSQL** - Database operations
- ✅ **RabbitMQ** - Message bus events
- ✅ **Redis** - Caching (fixture ready)
- ✅ **Keycloak** - Authentication (fixture ready)
- ✅ **MailCatcher** - Email delivery

**Example:**
```csharp
[Collection("Email")]
public class EmailTests
{
    private readonly MailCatcherFixture _mailCatcherFixture;

    public EmailTests(MailCatcherFixture mailCatcherFixture)
    {
        _mailCatcherFixture = mailCatcherFixture;
    }

    [Fact]
    public async Task SendEmail_ShouldAppearInMailCatcher()
    {
        // Test implementation
    }
}
```

### 2. **Unit Tests** (TODO)

Fast, isolated tests for business logic:

- Domain entities
- Command/Query handlers
- Validators
- Domain services

### 3. **E2E Tests** (TODO)

Full system tests simulating real user scenarios:

- User registration flow
- Authentication & authorization
- Cross-service workflows

## 📊 Current Test Coverage

| Component | Integration Tests | Unit Tests | E2E Tests |
|-----------|-------------------|------------|-----------|
| **Identity Service** | ✅ 3 tests | 🔄 TODO | 🔄 TODO |
| **Coaching Service** | 🔄 TODO | 🔄 TODO | 🔄 TODO |
| **API Gateway** | 🔄 TODO | N/A | 🔄 TODO |
| **Shared Infrastructure** | ✅ Fixtures ready | N/A | N/A |

## 🛠️ Testcontainers

We use **Testcontainers** to provide real infrastructure for integration tests. Each container is automatically:

1. **Started** before tests run
2. **Shared** across tests in the same collection
3. **Cleaned up** after tests complete

### Available Fixtures

```csharp
[Collection("Database")]        // PostgreSQL
[Collection("MessageBus")]      // RabbitMQ
[Collection("Cache")]           // Redis
[Collection("Authentication")]  // Keycloak
[Collection("Email")]           // MailCatcher
```

### Container Lifecycle

```
Test Class Constructor
    ↓
IAsyncLifetime.InitializeAsync()  ← Container starts
    ↓
Test Method 1
Test Method 2
Test Method 3
    ↓
IAsyncLifetime.DisposeAsync()     ← Container stops
```

## 🎯 Writing New Tests

### Integration Test Template

```csharp
using FluentAssertions;
using Shared.IntegrationTests.Fixtures;
using Xunit;

namespace YourService.IntegrationTests;

[Collection("Database")] // or MessageBus, Email, etc.
public class YourFeatureTests
{
    private readonly PostgresFixture _postgresFixture;

    public YourFeatureTests(PostgresFixture postgresFixture)
    {
        _postgresFixture = postgresFixture;
    }

    [Fact]
    public async Task YourTest_WithCondition_ShouldExpectedResult()
    {
        // Arrange
        // ... setup test data

        // Act
        // ... execute the operation

        // Assert
        result.Should().Be(expected);
    }
}
```

### Best Practices

1. **Use AAA Pattern** (Arrange-Act-Assert)
2. **One assertion per test** (when possible)
3. **Descriptive test names**: `MethodName_Scenario_ExpectedResult`
4. **Clean up test data** between tests
5. **Use FluentAssertions** for readable assertions
6. **Avoid test interdependencies**

## 🔧 Troubleshooting

### Docker Issues

```bash
# Check if Docker is running
docker ps

# Clean up old containers
docker system prune -a

# Check Testcontainers logs
# Logs are automatically output when tests fail
```

### Port Conflicts

Testcontainers automatically assigns random ports. If you see port conflicts:

```bash
# Kill processes on specific ports
sudo lsof -ti:5432 | xargs kill -9  # PostgreSQL
sudo lsof -ti:5672 | xargs kill -9  # RabbitMQ
```

### Slow Tests

Integration tests are slower than unit tests due to container startup:

- **First run**: ~30-60 seconds (container download + startup)
- **Subsequent runs**: ~10-20 seconds (container startup only)
- **Tests within same collection**: Shared container (fast)

## 📈 CI/CD Integration

Tests are automatically run in GitHub Actions:

```yaml
- name: Run Integration Tests
  run: dotnet test tests/Integration --configuration Release
```

## 🎓 Learning Resources

- [xUnit Documentation](https://xunit.net/)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [FluentAssertions](https://fluentassertions.com/)
- [MassTransit Testing](https://masstransit.io/documentation/concepts/testing)

## 📝 Next Steps

1. ✅ **Phase 1 Complete**: Integration test infrastructure
2. 🔄 **Phase 2**: Add more integration tests for all endpoints
3. 🔄 **Phase 3**: Implement unit tests
4. 🔄 **Phase 4**: Add E2E tests with SpecFlow

---

**Last Updated:** 2026-01-22  
**Test Framework:** xUnit 2.6.6  
**Testcontainers:** 3.7.0
