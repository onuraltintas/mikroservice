using System.Runtime.CompilerServices;
using Coaching.Application.StudyPlanning;

namespace Coaching.Curriculum.IntegrationTests;

internal static class TestAssemblySetup
{
    /// <summary>
    /// The suite's plans are written against the catalog minutes. Paket I2 plans a share of them (WeeklyPlanEngine.ContentFactor);
    /// the suite pins the share to 1 and tests the factor on its own (WeeklyPlanEngineTests).
    /// </summary>
    [ModuleInitializer]
    internal static void PlanTheCatalogMinutes() => WeeklyPlanEngine.ContentFactor = 1m;
}
