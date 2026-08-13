using EduPlatform.Shared.Kernel.Results;
using FluentAssertions;
using Identity.Application.Authorization;
using Identity.Application.Queries.GetAllUsers;

namespace Identity.API.IntegrationTests;

public class InstitutionAccessScopeTests
{
    [Fact]
    public void SystemAdmin_ShouldReceiveGlobalScope()
    {
        var result = InstitutionAccessScopeResolver.Resolve(
            Guid.NewGuid(),
            new[] { "SystemAdmin" },
            Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        result.Value.IsGlobal.Should().BeTrue();
        result.Value.InstitutionId.Should().BeNull();
    }

    [Fact]
    public void InstitutionUser_ShouldReceiveInstitutionScope()
    {
        var institutionId = Guid.NewGuid();
        var result = InstitutionAccessScopeResolver.Resolve(
            Guid.NewGuid(),
            new[] { "InstitutionAdmin" },
            institutionId);

        result.IsSuccess.Should().BeTrue();
        result.Value.InstitutionId.Should().Be(institutionId);
    }

    [Fact]
    public void InstitutionUserWithoutMembership_ShouldBeForbidden()
    {
        var result = InstitutionAccessScopeResolver.Resolve(
            Guid.NewGuid(),
            new[] { "InstitutionAdmin" },
            null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.Forbidden("Kullanıcının erişebileceği aktif bir kurum bulunamadı."));
    }

    [Fact]
    public void GetAllUsersQuery_ShouldCapPageSize()
    {
        var validator = new GetAllUsersQueryValidator();
        var result = validator.Validate(new GetAllUsersQuery(PageNumber: 1, PageSize: 101));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetAllUsersQuery.PageSize));
    }

    [Fact]
    public void GetAllUsersQuery_ShouldCapPageNumber()
    {
        var validator = new GetAllUsersQueryValidator();
        var result = validator.Validate(new GetAllUsersQuery(PageNumber: GetAllUsersQuery.MaxPageNumber + 1, PageSize: 10));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetAllUsersQuery.PageNumber));
    }
}
