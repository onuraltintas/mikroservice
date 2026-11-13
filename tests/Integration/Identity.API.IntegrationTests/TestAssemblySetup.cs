using System.Runtime.CompilerServices;

namespace Identity.API.IntegrationTests;

internal static class TestAssemblySetup
{
    /// <summary>
    /// The suite runs thousands of tests in parallel, many with synchronous database and
    /// hashing work. With the default minimum the thread pool adds threads slowly, and the
    /// in-memory MassTransit consumers of harness tests could wait longer than their window.
    /// </summary>
    [ModuleInitializer]
    internal static void RaiseThreadPoolMinimum()
    {
        ThreadPool.GetMinThreads(out var worker, out var io);
        var wanted = Math.Max(64, Environment.ProcessorCount * 8);
        ThreadPool.SetMinThreads(Math.Max(worker, wanted), Math.Max(io, wanted));
    }

    /// <summary>The suite's study plans are written against the catalog minutes; Paket I2's planning share is pinned to 1.</summary>
    [ModuleInitializer]
    internal static void PlanTheCatalogMinutes() => Coaching.Application.StudyPlanning.WeeklyPlanEngine.ContentFactor = 1m;
}
