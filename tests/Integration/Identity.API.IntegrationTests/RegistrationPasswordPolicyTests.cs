using FluentAssertions;
using Identity.Application.Commands.RegisterParent;
using Identity.Application.Commands.RegisterStudent;
using Identity.Application.Commands.RegisterTeacher;
using Identity.Application.Commands.RegisterInstitution;
using Identity.Application.Validators;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public class RegistrationPasswordPolicyTests
{
    [Fact]
    public void StudentRegistration_ShouldRejectPasswordWithoutRequiredCharacterClasses()
    {
        var command = new RegisterStudentCommand(
            "student@example.com",
            "password",
            "Test",
            "Student",
            null);

        new RegisterStudentCommandValidator().Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void TeacherRegistration_ShouldRejectPasswordWithoutRequiredCharacterClasses()
    {
        var command = new RegisterTeacherCommand(
            "teacher@example.com",
            "password",
            "Test",
            "Teacher",
            null);

        new RegisterTeacherCommandValidator().Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ParentRegistration_ShouldRejectPasswordWithoutRequiredCharacterClasses()
    {
        var command = new RegisterParentCommand(
            "parent@example.com",
            "password",
            "Test",
            "Parent",
            null);

        new RegisterParentCommandValidator().Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void InstitutionRegistration_ShouldRejectPasswordWithoutRequiredCharacterClasses()
    {
        var command = new RegisterInstitutionCommand(
            "institution@example.com",
            "password",
            "Test",
            "Owner",
            "Example School",
            (InstitutionType)0,
            null,
            "province",
            "district");

        new RegisterInstitutionCommandValidator().Validate(command).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Password123!")]
    [InlineData("LongerPassphrase2#")]
    public void AllRegistrationRoles_ShouldAcceptCurrentStrongPasswordBaseline(string password)
    {
        new RegisterStudentCommandValidator()
            .Validate(new RegisterStudentCommand("student@example.com", password, "Test", "Student", null))
            .IsValid.Should().BeTrue();

        new RegisterTeacherCommandValidator()
            .Validate(new RegisterTeacherCommand("teacher@example.com", password, "Test", "Teacher", null))
            .IsValid.Should().BeTrue();

        new RegisterParentCommandValidator()
            .Validate(new RegisterParentCommand("parent@example.com", password, "Test", "Parent", null))
            .IsValid.Should().BeTrue();

        new RegisterInstitutionCommandValidator()
            .Validate(new RegisterInstitutionCommand(
                "institution@example.com",
                password,
                "Test",
                "Owner",
                "Example School",
                (InstitutionType)0,
                null,
                "province",
                "district"))
            .IsValid.Should().BeTrue();
    }
}
