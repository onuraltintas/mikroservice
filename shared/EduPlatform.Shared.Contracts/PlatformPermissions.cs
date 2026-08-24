namespace EduPlatform.Shared.Contracts.Authorization;

/// <summary>
/// Permission keys used across bounded contexts. Identity owns persistence and
/// role assignment; other services consume these stable contract values.
/// </summary>
public static class PlatformPermissions
{
    public static class Institutions
    {
        public const string View = "Permissions.Institutions.View";
        public const string Manage = "Permissions.Institutions.Manage";
    }

    public static class Coaching
    {
        public const string View = "Permissions.Coaching.View";
        public const string Manage = "Permissions.Coaching.Manage";
    }

    public static class SpeedReading
    {
        public const string View = "Permissions.SpeedReading.View";
        public const string ContentManage = "Permissions.SpeedReading.ContentManage";
        public const string ProgramManage = "Permissions.SpeedReading.ProgramManage";
        public const string ProgressView = "Permissions.SpeedReading.ProgressView";
        public const string ReportView = "Permissions.SpeedReading.ReportView";
        public const string PlatformAnalyticsView = "Permissions.SpeedReading.PlatformAnalyticsView";
        public const string ReportManage = "Permissions.SpeedReading.ReportManage";
        public const string LeaderboardView = "Permissions.SpeedReading.LeaderboardView";
        public const string GamificationManage = "Permissions.SpeedReading.GamificationManage";
        public const string SettingsManage = "Permissions.SpeedReading.SettingsManage";
    }

    public static class Support
    {
        public const string View = "Permissions.Support.View";
        public const string Reply = "Permissions.Support.Reply";
    }

    public static class Notifications
    {
        public const string Templates = "Permissions.Notifications.Templates";
    }

    public static class Operations
    {
        public const string View = "Permissions.Operations.View";
    }

    public static IReadOnlyList<string> GetAll() =>
    [
        Institutions.View,
        Institutions.Manage,
        Coaching.View,
        Coaching.Manage,
        SpeedReading.View,
        SpeedReading.ContentManage,
        SpeedReading.ProgramManage,
        SpeedReading.ProgressView,
        SpeedReading.ReportView,
        SpeedReading.PlatformAnalyticsView,
        SpeedReading.ReportManage,
        SpeedReading.LeaderboardView,
        SpeedReading.GamificationManage,
        SpeedReading.SettingsManage,
        Support.View,
        Support.Reply,
        Notifications.Templates,
        Operations.View
    ];
}
