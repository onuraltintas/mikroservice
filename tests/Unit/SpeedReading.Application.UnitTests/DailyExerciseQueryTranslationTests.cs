using Microsoft.EntityFrameworkCore;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class DailyExerciseQueryTranslationTests
{
    [Fact]
    public void Scheduled_exercise_and_type_id_lists_translate_to_sql()
    {
        using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);
        var exerciseIds = new List<Guid> { Guid.NewGuid() };
        var typeIds = new List<Guid> { Guid.NewGuid() };
        var nodeIds = new List<Guid> { Guid.NewGuid() };

        var exerciseSql = db.Exercises.Where(item => exerciseIds.Contains(item.Id)).ToQueryString();
        var typeSql = db.ExerciseTypes.Where(item => typeIds.Contains(item.Id)).ToQueryString();
        var nodeSql = db.LearningPathNodeContents.Where(item => nodeIds.Contains(item.NodeId)).ToQueryString();

        Assert.Contains("exercises", exerciseSql);
        Assert.Contains("exercise_types", typeSql);
        Assert.Contains("learning_path_node_contents", nodeSql);
    }
}
