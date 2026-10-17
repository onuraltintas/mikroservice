using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Infrastructure.Data.Migrations;

[DbContext(typeof(CoachingDbContext))]
[Migration("20261017140000_ManualTimedStudyTasks")]
public sealed class ManualTimedStudyTasks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("ck_study_task_weekly_time","study_plan_tasks","coaching");
        migrationBuilder.AddCheckConstraint("ck_study_task_weekly_time","study_plan_tasks",
            "(\"PlannedStartMinute\" IS NULL AND \"WeeklyLessonId\" IS NULL AND \"LessonNameSnapshot\" IS NULL) OR (\"PlannedStartMinute\" IS NOT NULL AND \"PlannedStartMinute\" BETWEEN 0 AND 1439 AND \"PlannedStartMinute\"+\"PlannedMinutes\"<=1440 AND ((\"WeeklyLessonId\" IS NULL AND \"LessonNameSnapshot\" IS NULL) OR (\"WeeklyLessonId\" IS NOT NULL AND \"LessonNameSnapshot\" IS NOT NULL)))","coaching");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("ck_study_task_weekly_time","study_plan_tasks","coaching");
        migrationBuilder.AddCheckConstraint("ck_study_task_weekly_time","study_plan_tasks",
            "(\"PlannedStartMinute\" IS NULL AND \"WeeklyLessonId\" IS NULL AND \"LessonNameSnapshot\" IS NULL) OR (\"PlannedStartMinute\" IS NOT NULL AND \"PlannedStartMinute\" BETWEEN 0 AND 1439 AND \"PlannedStartMinute\"+\"PlannedMinutes\"<=1440 AND \"WeeklyLessonId\" IS NOT NULL AND \"LessonNameSnapshot\" IS NOT NULL)","coaching");
    }
}
