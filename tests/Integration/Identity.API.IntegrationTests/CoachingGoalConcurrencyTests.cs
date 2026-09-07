using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingGoalConcurrencyTests
{
    [Fact]
    public async Task Stale_teacher_edit_cannot_overwrite_student_progress()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var setup = new CoachingDbContext(options);
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Original", GoalCategory.StudyHabits);
        goal.UpdateProgress(20);
        setup.AcademicGoals.Add(goal);
        await setup.SaveChangesAsync();

        await using var teacher = new CoachingDbContext(options);
        await using var student = new CoachingDbContext(options);
        var teacherRepository = new AcademicGoalRepository(teacher);
        var studentRepository = new AcademicGoalRepository(student);
        var teacherGoal = (await teacherRepository.GetByIdAsync(goal.Id))!;
        var studentGoal = (await studentRepository.GetByIdAsync(goal.Id))!;
        studentGoal.UpdateProgress(80);
        await studentRepository.UpdateAsync(studentGoal);
        await student.SaveChangesAsync();

        teacherGoal.UpdateDetails(title: "Teacher edit");
        await teacherRepository.UpdateAsync(teacherGoal);
        var save = () => teacher.SaveChangesAsync();
        await save.Should().ThrowAsync<ConcurrencyException>();

        await using var verification = new CoachingDbContext(options);
        var persisted = await verification.AcademicGoals.SingleAsync();
        persisted.CurrentProgress.Should().Be(80);
        persisted.Title.Should().Be("Original");
    }

    [Fact]
    public async Task Updating_a_tracked_goal_only_marks_changed_fields()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new CoachingDbContext(options);
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Original", GoalCategory.StudyHabits);
        context.AcademicGoals.Add(goal);
        await context.SaveChangesAsync();

        goal.UpdateProgress(80);
        await new AcademicGoalRepository(context).UpdateAsync(goal);
        context.ChangeTracker.DetectChanges();

        context.Entry(goal).Property(item => item.Title).IsModified.Should().BeFalse();
        context.Entry(goal).Property(item => item.CurrentProgress).IsModified.Should().BeTrue();
    }
}
