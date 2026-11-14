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

    /// <summary>
    /// The suite's plans expect the one-week spaced review. Paket I5 made reviews the student's choice (none by default); the suite
    /// pins the default to one week and tests the choice on its own (WeeklyProposalEngineTests).
    /// </summary>
    [ModuleInitializer]
    internal static void ReviewAfterAWeek() => WeeklyPlanEngine.DefaultSpacedDays = [7];

    /// <summary>Paket I5: the suite's plans expect one task per outcome; the sessions of a topic are tested on their own.</summary>
    [ModuleInitializer]
    internal static void OneTaskPerOutcome() => WeeklyPlanEngine.TopicSessionMinutes = 0;
}
