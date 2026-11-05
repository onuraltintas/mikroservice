using FluentAssertions;
using FluentValidation;
using Identity.Application.Commands.AdminChangePassword;
using Identity.Application.Commands.ChangePassword;
using Identity.Application.Commands.RegisterInstitution;
using Identity.Application.Commands.RegisterParent;
using Identity.Application.Commands.RegisterTeacher;
using Identity.Application.Validators;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Every server path that sets a password (registration, admin change, self change, reset)
/// applies the one RegistrationPasswordRules policy and answers in Turkish.
/// </summary>
public sealed class PasswordPolicyConsistencyTests
{
    private const string PolicyMessage =
        "Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.";

    public static TheoryData<string, bool> Passwords => new()
    {
        { "Sifre-2026", true },
        { "Sifre_2026", true },
        { "Sifre 2026", true },
        { "Şifre+2026A", true },
        { "Sifre2026", false },
        { "sifre-2026", false },
        { "SIFRE-2026", false },
        { "Sifre-Yeni", false },
        { "Ab-1", false },
        { "Sifre-2026" + new string('x', 119), false },
    };

    [Theory]
    [MemberData(nameof(Passwords))]
    public void AdminChange_UsesTheRegistrationPolicy(string password, bool valid)
    {
        var result = new AdminChangePasswordCommandValidator().Validate(new AdminChangePasswordCommand(Guid.NewGuid(), password));

        result.IsValid.Should().Be(valid);
        result.IsValid.Should().Be(RegistrationPasswordRules.IsValid(password));
        if (!valid) result.Errors.Select(e => e.ErrorMessage).Should().Equal(PolicyMessage);
    }

    [Theory]
    [MemberData(nameof(Passwords))]
    public void SelfChange_UsesTheRegistrationPolicy(string password, bool valid)
    {
        var result = new ChangePasswordCommandValidator().Validate(new ChangePasswordCommand("eski", password));

        result.IsValid.Should().Be(valid);
        if (!valid) result.Errors.Select(e => e.ErrorMessage).Should().Equal(PolicyMessage);
    }

    [Fact]
    public void SelfChange_AcceptsALegacyShortCurrentPassword_AndExplainsMissingFieldsInTurkish()
    {
        new ChangePasswordCommandValidator().Validate(new ChangePasswordCommand("abc12", "Sifre-2026"))
            .IsValid.Should().BeTrue();

        var empty = new ChangePasswordCommandValidator().Validate(new ChangePasswordCommand("", ""));
        empty.Errors.Select(e => e.ErrorMessage).Should().Contain(["Mevcut şifre boş olamaz.", "Yeni şifre boş olamaz."]);
    }

    [Fact]
    public void AdminChange_ReportsAnEmptyPasswordOnce()
    {
        var result = new AdminChangePasswordCommandValidator().Validate(new AdminChangePasswordCommand(Guid.NewGuid(), ""));

        result.Errors.Select(e => e.ErrorMessage).Should().Equal("Şifre boş olamaz.");
    }

    [Fact]
    public void RegistrationValidators_AnswerInTurkish()
    {
        AssertTurkish(new RegisterParentCommandValidator().Validate(
            new RegisterParentCommand("", "", "", "", "12", PlatformProduct.Coaching)));
        AssertTurkish(new RegisterTeacherCommandValidator().Validate(
            new RegisterTeacherCommand("", "", "", "", "12")));
        AssertTurkish(new RegisterInstitutionCommandValidator().Validate(new RegisterInstitutionCommand(
            "", "", "", "", "", InstitutionType.School, null, "", "")));
    }

    private static void AssertTurkish(FluentValidation.Results.ValidationResult result)
    {
        result.IsValid.Should().BeFalse();
        foreach (var message in result.Errors.Select(error => error.ErrorMessage))
        {
            message.Should().NotMatchRegex("(?i)\\b(must|required|valid|cannot|not be empty|is not)\\b", message);
        }
    }
}
