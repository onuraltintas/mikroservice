using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class StudentProgramQueryTranslationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Program_list_and_active_program_queries_translate_to_Postgres(bool activeOnly)
    {
        using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test;Password=test").Options);
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentProgram")!;
        var service = Activator.CreateInstance(type, db, null, null)!;
        var query = (IQueryable)type.GetMethod("GetRows", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, [Guid.NewGuid()])!;
        var row = Expression.Parameter(query.ElementType, "row");
        var progress = Expression.Property(row, "Progress");
        if (activeOnly)
        {
            query = query.Provider.CreateQuery(Expression.Call(typeof(Queryable), "Where", [query.ElementType],
                query.Expression, Expression.Quote(Expression.Lambda(Expression.Property(progress, "IsActive"), row))));
        }
        var key = Expression.Property(progress, activeOnly ? "CreatedAt" : "IsActive");
        query = query.Provider.CreateQuery(Expression.Call(typeof(Queryable), "OrderByDescending",
            [query.ElementType, key.Type], query.Expression, Expression.Quote(Expression.Lambda(key, row))));
        var sql = query.ToQueryString();
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("WHERE", sql);
    }
}
