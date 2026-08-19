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
        Support.View,
        Support.Reply,
        Notifications.Templates,
        Operations.View
    ];
}
