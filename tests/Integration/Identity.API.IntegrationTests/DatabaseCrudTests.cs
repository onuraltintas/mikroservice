using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Integration tests for database CRUD operations
/// Tests Entity Framework Core with PostgreSQL
/// </summary>
[Collection("Database")]
public class DatabaseCrudTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgresFixture;
    private readonly ITestOutputHelper _output;
    private IdentityDbContext? _dbContext;
    private NpgsqlDataSource? _dataSource;

    public DatabaseCrudTests(PostgresFixture postgresFixture, ITestOutputHelper output)
    {
        _postgresFixture = postgresFixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine($"PostgreSQL Connection String: {_postgresFixture.ConnectionString}");

        // Create DbContext with test database
        _dataSource = new NpgsqlDataSourceBuilder(_postgresFixture.ConnectionString)
            .EnableDynamicJson()
            .Build();

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_dataSource)
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext = new IdentityDbContext(options);

        // Ensure database is created and migrations are applied
        await _dbContext.Database.EnsureCreatedAsync();
        
        _output.WriteLine("Database created and migrations applied");
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.DisposeAsync();
        }

        if (_dataSource != null)
        {
            await _dataSource.DisposeAsync();
        }
    }

    [Fact]
    public async Task CreateInstitution_ShouldPersistToDatabase()
    {
        // Arrange
        var institution = Institution.Create(
            "Test University",
            InstitutionType.School,
            "Istanbul",
            "contact@test.edu"
        );

        // Act
        _dbContext!.Institutions.Add(institution);
        await _dbContext.SaveChangesAsync();

        // Clear tracking to simulate fresh query
        _dbContext.ChangeTracker.Clear();

        // Assert
        var retrieved = await _dbContext.Institutions
            .FirstOrDefaultAsync(i => i.Id == institution.Id);

        retrieved.Should().NotBeNull("institution should be persisted");
        retrieved!.Name.Should().Be("Test University");
        retrieved.Type.Should().Be(InstitutionType.School);
        retrieved.City.Should().Be("Istanbul");
        retrieved.Email.Should().Be("contact@test.edu");
    }

    [Fact]
    public async Task CreateUser_ShouldPersistToDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create(
            userId,
            "john.doe@school.edu",
            "John",
            "Doe"
        );

        // Act
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        // Assert
        var retrieved = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Email.Should().Be("john.doe@school.edu");
        retrieved.FirstName.Should().Be("John");
        retrieved.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task UpdateUser_ShouldPersistChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create(
            userId,
            "jane@update.edu",
            "Jane",
            "Smith"
        );

        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        // Act
        var userToUpdate = await _dbContext.Users.FindAsync(user.Id);
        userToUpdate!.UpdateName("Janet", "Smith-Johnson");
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        // Assert
        var updated = await _dbContext.Users.FindAsync(user.Id);
        updated!.FirstName.Should().Be("Janet");
        updated.LastName.Should().Be("Smith-Johnson");
    }

    [Fact]
    public async Task AddExternalLogin_AfterRegistrationAndEmailConfirmation_ShouldPersist()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "google-flow@concurrency.edu", "Google", "Flow");

        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Match the Google registration flow: load the saved aggregate, confirm
        // the trusted provider email, then attach the external login.
        var reloaded = await _dbContext.Users
            .Include(candidate => candidate.Roles)
            .ThenInclude(userRole => userRole.Role)
            .FirstAsync(candidate => candidate.Id == userId);
        reloaded.ConfirmEmail();
        var login = UserLogin.Create(
            userId,
            "Google",
            "google-subject",
            "Google");

        var repository = new Identity.Infrastructure.Repositories.UserRepository(_dbContext);
        var loginAdded = await repository.TryAddLoginAsync(reloaded, login, CancellationToken.None);
        loginAdded.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        var persistedLogin = await _dbContext.UserLogins
            .SingleOrDefaultAsync(login => login.UserId == userId);
        persistedLogin.Should().NotBeNull();
        persistedLogin!.ProviderKey.Should().Be("google-subject");
    }

    [Fact]
    public async Task AddExternalLogin_WhenProviderKeyAlreadyExists_ShouldReturnFalseAndLeaveNoDirtyUserState()
    {
        var firstUserId = Guid.NewGuid();
        var firstUser = User.Create(firstUserId, "google-first@concurrency.edu", "First", "User");
        _dbContext!.Users.Add(firstUser);
        await _dbContext.SaveChangesAsync();

        var repository = new Identity.Infrastructure.Repositories.UserRepository(_dbContext);
        var firstLogin = UserLogin.Create(firstUserId, "Google", "duplicate-subject", "Google");
        (await repository.TryAddLoginAsync(firstUser, firstLogin, CancellationToken.None)).Should().BeTrue();

        var secondUserId = Guid.NewGuid();
        var secondUser = User.Create(secondUserId, "google-second@concurrency.edu", "Second", "User");
        _dbContext.Users.Add(secondUser);
        await _dbContext.SaveChangesAsync();
        secondUser.UpdateName("Unsaved", "Change");

        var duplicateLogin = UserLogin.Create(secondUserId, "Google", "duplicate-subject", "Google");
        (await repository.TryAddLoginAsync(secondUser, duplicateLogin, CancellationToken.None)).Should().BeFalse();

        _dbContext.ChangeTracker.Clear();
        var persistedSecondUser = await _dbContext.Users.SingleAsync(user => user.Id == secondUserId);
        persistedSecondUser.FirstName.Should().Be("Second");
        persistedSecondUser.LastName.Should().Be("User");
        (await _dbContext.UserLogins.CountAsync(login =>
            login.LoginProvider == "Google" && login.ProviderKey == "duplicate-subject"))
            .Should().Be(1);
    }

    [Fact]
    public async Task QueryUsers_WithFilters_ShouldWork()
    {
        // Arrange
        var users = new[]
        {
            User.Create(Guid.NewGuid(), "alice@query.edu", "Alice", "Anderson"),
            User.Create(Guid.NewGuid(), "bob@query.edu", "Bob", "Brown"),
            User.Create(Guid.NewGuid(), "charlie@query.edu", "Charlie", "Clark"),
        };

        _dbContext!.Users.AddRange(users);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        // Act
        var allUsers = await _dbContext.Users
            .OrderBy(u => u.FirstName)
            .ToListAsync();

        var filteredByEmail = await _dbContext.Users
            .Where(u => u.Email.Contains("alice"))
            .ToListAsync();

        // Assert
        allUsers.Should().HaveCountGreaterOrEqualTo(3);
        allUsers.Select(u => u.FirstName).Should().Contain(new[] { "Alice", "Bob", "Charlie" });

        filteredByEmail.Should().HaveCount(1);
        filteredByEmail[0].FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task ConcurrentUpdates_ShouldHandleConcurrency()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create(
            userId,
            "concurrent@concurrency.edu",
            "Concurrent",
            "User"
        );

        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        // Create two separate contexts simulating concurrent access
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_dataSource!)
            .Options;

        await using var context1 = new IdentityDbContext(options);
        await using var context2 = new IdentityDbContext(options);

        var user1 = await context1.Users.FindAsync(user.Id);
        var user2 = await context2.Users.FindAsync(user.Id);

        user1!.UpdateName("Updated1", "User");
        user2!.UpdateName("Updated2", "User");

        await context1.SaveChangesAsync();
        
        // Second save should succeed (last write wins in this case)
        await context2.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();
        var final = await _dbContext.Users.FindAsync(user.Id);
        final!.FirstName.Should().Be("Updated2", "last write should win");
    }

    [Fact]
    public async Task CreateMultipleInstitutions_ShouldWork()
    {
        // Arrange & Act
        var institutions = new[]
        {
            Institution.Create("School 1", InstitutionType.School, "City1", "school1@test.com"),
            Institution.Create("Course 1", InstitutionType.PrivateCourse, "City2", "course1@test.com"),
            Institution.Create("Center 1", InstitutionType.StudyCenter, "City3", "center1@test.com"),
        };

        _dbContext!.Institutions.AddRange(institutions);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        // Assert
        var retrieved = await _dbContext.Institutions.ToListAsync();
        retrieved.Should().HaveCountGreaterOrEqualTo(3);
        
        var school = retrieved.FirstOrDefault(i => i.Type == InstitutionType.School);
        school.Should().NotBeNull();
        school!.Name.Should().Be("School 1");
    }

    [Fact]
    public async Task DatabaseConnection_ShouldBeHealthy()
    {
        // Act
        var canConnect = await _dbContext!.Database.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue("database connection should be healthy");
    }
}
