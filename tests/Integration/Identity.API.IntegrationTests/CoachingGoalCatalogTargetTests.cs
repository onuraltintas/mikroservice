using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingGoalCatalogTargetTests
{
    [Fact]
    public void Goal_CatalogTargetCanBeReplacedAndClearedWithoutChangingOwnership()
    {
        var student = Guid.NewGuid();
        var goal = AcademicGoal.Create(student, "Target", GoalCategory.ExamPreparation);
        var program = Guid.NewGuid();
        goal.SetCatalogTarget(program, null);
        Assert.Equal(program, goal.TargetUniversityProgramId);
        var school = Guid.NewGuid();
        goal.SetCatalogTarget(null, school);
        Assert.Null(goal.TargetUniversityProgramId);
        Assert.Equal(school, goal.TargetSchoolId);
        goal.SetCatalogTarget(null, null);
        Assert.Null(goal.TargetSchoolId);
        Assert.Equal(student, goal.StudentId);
    }

    [Fact]
    public void Goal_RejectsAmbiguousAndEmptyCatalogTargetsWithoutMutating()
    {
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Target", GoalCategory.ExamPreparation);
        Assert.Throws<ArgumentException>(() => goal.SetCatalogTarget(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => goal.SetCatalogTarget(Guid.Empty, null));
        Assert.Throws<ArgumentException>(() => goal.SetCatalogTarget(null, Guid.Empty));
        Assert.Null(goal.TargetUniversityProgramId);
        Assert.Null(goal.TargetSchoolId);
    }

    [Fact]
    public void Goal_ModelRestrictsDeletionOfReferencedCatalogTargets()
    {
        using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        var entity = db.Model.FindEntityType(typeof(AcademicGoal))!;
        foreach (var name in new[] { nameof(AcademicGoal.TargetUniversityProgramId), nameof(AcademicGoal.TargetSchoolId) })
            Assert.Contains(entity.GetForeignKeys(), fk => fk.Properties.Any(p => p.Name == name)
                && fk.DeleteBehavior == DeleteBehavior.Restrict);
    }
}
