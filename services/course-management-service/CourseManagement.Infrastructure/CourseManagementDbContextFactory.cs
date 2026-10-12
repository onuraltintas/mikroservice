using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CourseManagement.Infrastructure;

public sealed class CourseManagementDbContextFactory : IDesignTimeDbContextFactory<CourseManagementDbContext>
{
    public CourseManagementDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("COURSE_MANAGEMENT_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Set COURSE_MANAGEMENT_CONNECTION_STRING before running migrations.");
        return new CourseManagementDbContext(new DbContextOptionsBuilder<CourseManagementDbContext>()
            .UseNpgsql(connectionString).Options);
    }
}
