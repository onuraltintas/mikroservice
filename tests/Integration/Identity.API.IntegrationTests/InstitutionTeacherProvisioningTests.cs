using FluentAssertions;
using Identity.Application.Commands.CreateTeacher;
using Identity.Application.Validators;

namespace Identity.API.IntegrationTests;

public sealed class InstitutionTeacherProvisioningTests
{
    [Fact]
    public async Task Institution_admin_can_create_a_teacher_before_subjects_are_configured()
    {
        var result = await new CreateTeacherCommandValidator().ValidateAsync(
            new CreateTeacherCommand(
                "ayse@example.test",
                "Ayşe",
                "Öğretmen",
                null,
                []));

        result.IsValid.Should().BeTrue();
    }
}
