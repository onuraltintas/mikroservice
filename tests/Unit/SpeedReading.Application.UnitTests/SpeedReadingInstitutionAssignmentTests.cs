using FluentAssertions;
using SpeedReading.Domain.Assignments;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingInstitutionAssignmentTests
{
    [Fact]
    public void Institution_assignment_keeps_a_stable_tenant_scope()
    {
        var institutionId = Guid.NewGuid();
        var assignment = Assignment.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "Reading", null,
            DateTime.UtcNow.AddDays(7), institutionId: institutionId);

        assignment.InstitutionId.Should().Be(institutionId);
    }
}
