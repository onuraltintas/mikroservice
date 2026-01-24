# EduPlatform Testing Strategy

## 📋 Overview

This document outlines the comprehensive testing strategy for the EduPlatform microservices architecture. We follow the **Test Pyramid** approach:

```
           /\
          /  \         E2E Tests (10%)
         /____\        - Full system integration
        /      \       - User scenarios
       /________\      Integration Tests (30%)
      /          \     - API endpoints
     /____________\    - Database operations
    /              \   - Message bus
   /________________\  Unit Tests (60%)
                       - Business logic
                       - Domain entities
                       - Validators
```

---

## 🎯 Test Categories

### 1. **Unit Tests (60%)**
**Goal:** Test individual components in isolation

**Coverage:**
- ✅ Domain Entities & Value Objects
- ✅ Command/Query Handlers (MediatR)
- ✅ Validators (FluentValidation)
- ✅ Domain Services
- ✅ Utility Classes

**Tools:**
- `xUnit` - Test framework
- `FluentAssertions` - Assertion library
- `Moq` - Mocking framework
- `AutoFixture` - Test data generation

**Example:**
```csharp
[Fact]
public async Task RegisterStudent_WithValidData_ShouldCreateUser()
{
    // Arrange
    var mockRepo = new Mock<IUserRepository>();
    var handler = new RegisterStudentCommandHandler(mockRepo.Object);
    
    // Act
    var result = await handler.Handle(command, CancellationToken.None);
    
    // Assert
    result.IsSuccess.Should().BeTrue();
}
```

---

### 2. **Integration Tests (30%)**
**Goal:** Test component interactions with real infrastructure

**Coverage:**
- ✅ API Endpoints (Controllers)
- ✅ Database Operations (EF Core)
- ✅ Message Bus (MassTransit/RabbitMQ)
- ✅ Authentication (Keycloak JWT)
- ✅ Caching (Redis)
- ✅ Email Service (SMTP)

**Tools:**
- `WebApplicationFactory` - In-memory API testing
- `Testcontainers` - Docker containers for dependencies
- `Respawn` - Database cleanup
- `WireMock.Net` - HTTP mocking

**Test Containers:**
```csharp
- PostgreSQL (Database)
- RabbitMQ (Message Bus)
- Redis (Cache)
- Keycloak (Auth)
- MailCatcher (Email)
```

---

### 3. **End-to-End Tests (10%)**
**Goal:** Test complete user scenarios across all services

**Coverage:**
- ✅ User Registration Flow
- ✅ Authentication & Authorization
- ✅ Event-Driven Workflows
- ✅ API Gateway Routing
- ✅ Cross-Service Communication

**Tools:**
- `SpecFlow` - BDD framework (Gherkin syntax)
- `Testcontainers` - Full stack orchestration
- `RestSharp` - HTTP client for API calls

**Example Scenario:**
```gherkin
Feature: Student Registration
  Scenario: New student registers successfully
    Given the system is running
    When a student registers with valid credentials
    Then a user account is created in Identity Service
    And a welcome goal is created in Coaching Service
    And a welcome email is sent
```

---

## 🏗️ Project Structure

```
tests/
├── Unit/
│   ├── Identity.Domain.Tests/
│   ├── Identity.Application.Tests/
│   ├── Coaching.Domain.Tests/
│   └── Coaching.Application.Tests/
├── Integration/
│   ├── Identity.API.IntegrationTests/
│   ├── Coaching.API.IntegrationTests/
│   ├── Gateway.IntegrationTests/
│   └── Shared.IntegrationTests/
│       ├── Fixtures/
│       │   ├── PostgresFixture.cs
│       │   ├── RabbitMqFixture.cs
│       │   ├── RedisFixture.cs
│       │   └── KeycloakFixture.cs
│       └── Helpers/
└── E2E/
    ├── EduPlatform.E2E.Tests/
    └── Features/
        ├── StudentRegistration.feature
        ├── Authentication.feature
        └── CoachingWorkflow.feature
```

---

## 🧩 Test Components

### **A. Database Tests**
```csharp
[Collection("Database")]
public class UserRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private readonly IdentityDbContext _context;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().Build();
        await _postgres.StartAsync();
        // Apply migrations
    }

    [Fact]
    public async Task AddUser_ShouldPersistToDatabase()
    {
        // Test implementation
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

### **B. Message Bus Tests**
```csharp
[Collection("RabbitMQ")]
public class UserCreatedEventTests
{
    private readonly RabbitMqContainer _rabbitMq;
    private readonly ITestHarness _harness;

    [Fact]
    public async Task PublishUserCreatedEvent_ShouldBeConsumedByCoachingService()
    {
        // Arrange
        var userEvent = new UserCreatedEvent { UserId = Guid.NewGuid() };

        // Act
        await _harness.Bus.Publish(userEvent);

        // Assert
        (await _harness.Published.Any<UserCreatedEvent>()).Should().BeTrue();
        (await _harness.Consumed.Any<UserCreatedEvent>()).Should().BeTrue();
    }
}
```

### **C. Authentication Tests**
```csharp
[Collection("Keycloak")]
public class AuthenticationTests
{
    private readonly KeycloakContainer _keycloak;
    private readonly HttpClient _client;

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnJwtToken()
    {
        // Arrange
        var loginRequest = new { username = "test", password = "test123" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        token.AccessToken.Should().NotBeNullOrEmpty();
    }
}
```

### **D. Email Tests**
```csharp
[Collection("Email")]
public class EmailServiceTests
{
    private readonly MailCatcherContainer _mailCatcher;

    [Fact]
    public async Task SendWelcomeEmail_ShouldAppearInMailCatcher()
    {
        // Arrange
        var emailService = new EmailService(_mailCatcher.SmtpHost, _mailCatcher.SmtpPort);

        // Act
        await emailService.SendWelcomeEmailAsync("test@example.com");

        // Assert
        var messages = await _mailCatcher.GetMessagesAsync();
        messages.Should().ContainSingle(m => m.Recipients.Contains("test@example.com"));
    }
}
```

### **E. API Gateway Tests**
```csharp
[Collection("Gateway")]
public class GatewayRoutingTests
{
    [Fact]
    public async Task Request_ToIdentityRoute_ShouldForwardToIdentityService()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/auth/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RateLimiting_ShouldBlock_After100Requests()
    {
        // Test rate limiting
    }
}
```

### **F. Logging Tests**
```csharp
[Collection("Logging")]
public class SerilogTests
{
    [Fact]
    public void CommandHandler_ShouldLogExecutionTime()
    {
        // Arrange
        var logOutput = new StringWriter();
        Log.Logger = new LoggerConfiguration()
            .WriteTo.TextWriter(logOutput)
            .CreateLogger();

        // Act
        // Execute command

        // Assert
        logOutput.ToString().Should().Contain("Command executed in");
    }
}
```

---

## 🚀 Implementation Plan

### **Phase 1: Unit Tests (Week 1-2)**
- [ ] Identity.Domain.Tests
- [ ] Identity.Application.Tests
- [ ] Coaching.Domain.Tests
- [ ] Coaching.Application.Tests
- **Target Coverage:** 80%+

### **Phase 2: Integration Tests (Week 3-4)**
- [ ] Setup Testcontainers infrastructure
- [ ] Identity.API.IntegrationTests
- [ ] Coaching.API.IntegrationTests
- [ ] Gateway.IntegrationTests
- [ ] Message Bus integration tests
- **Target Coverage:** 70%+

### **Phase 3: E2E Tests (Week 5)**
- [ ] Setup SpecFlow
- [ ] Student registration flow
- [ ] Authentication flow
- [ ] Coaching workflow
- **Target Coverage:** Critical paths only

---

## 📊 CI/CD Integration

### **GitHub Actions Workflow**
```yaml
name: Test Pipeline

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run Unit Tests
        run: dotnet test tests/Unit --configuration Release

  integration-tests:
    runs-on: ubuntu-latest
    services:
      docker:
        image: docker:dind
    steps:
      - uses: actions/checkout@v3
      - name: Run Integration Tests
        run: dotnet test tests/Integration --configuration Release

  e2e-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Start Docker Compose
        run: docker-compose up -d
      - name: Run E2E Tests
        run: dotnet test tests/E2E --configuration Release
```

---

## 🎯 Success Metrics

| Metric | Target |
|--------|--------|
| **Unit Test Coverage** | ≥ 80% |
| **Integration Test Coverage** | ≥ 70% |
| **E2E Test Coverage** | Critical paths |
| **Build Time** | < 10 minutes |
| **Test Execution Time** | < 5 minutes |
| **Flaky Test Rate** | < 1% |

---

## 🛠️ Tools & Packages

### **Required NuGet Packages**
```xml
<!-- Unit Testing -->
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="AutoFixture" Version="4.18.1" />

<!-- Integration Testing -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
<PackageReference Include="Testcontainers" Version="3.7.0" />
<PackageReference Include="Testcontainers.PostgreSql" Version="3.7.0" />
<PackageReference Include="Testcontainers.RabbitMq" Version="3.7.0" />
<PackageReference Include="Testcontainers.Redis" Version="3.7.0" />
<PackageReference Include="Respawn" Version="6.2.1" />
<PackageReference Include="WireMock.Net" Version="1.5.54" />

<!-- E2E Testing -->
<PackageReference Include="SpecFlow" Version="3.9.74" />
<PackageReference Include="SpecFlow.xUnit" Version="3.9.74" />
<PackageReference Include="RestSharp" Version="110.2.0" />

<!-- MassTransit Testing -->
<PackageReference Include="MassTransit.TestFramework" Version="8.3.4" />
```

---

## 📝 Best Practices

1. **Arrange-Act-Assert (AAA) Pattern**
   - Clear separation of test phases
   - One assertion per test (when possible)

2. **Test Naming Convention**
   ```
   [MethodName]_[Scenario]_[ExpectedResult]
   Example: RegisterStudent_WithValidData_ShouldCreateUser
   ```

3. **Test Data Builders**
   - Use AutoFixture for complex object creation
   - Create reusable test data builders

4. **Avoid Test Interdependencies**
   - Each test should be independent
   - Use proper cleanup (IAsyncLifetime, IDisposable)

5. **Fast Feedback**
   - Unit tests should run in < 1 second
   - Integration tests should run in < 30 seconds

6. **Realistic Test Data**
   - Use production-like data
   - Test edge cases and boundary conditions

---

## 🔄 Continuous Improvement

- **Weekly:** Review test coverage reports
- **Monthly:** Analyze flaky tests and fix root causes
- **Quarterly:** Update testing strategy based on new features

---

**Last Updated:** 2026-01-22  
**Status:** 🟡 Planning Phase  
**Next Milestone:** Implement Unit Tests for Identity Service
