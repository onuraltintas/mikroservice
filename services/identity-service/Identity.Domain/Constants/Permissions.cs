using EduPlatform.Shared.Contracts.Authorization;

namespace Identity.Domain.Constants;

public static class Permissions
{
    public static class Users
    {
        public const string View = "Permissions.Users.View";
        public const string Create = "Permissions.Users.Create";
        public const string Edit = "Permissions.Users.Edit";
        public const string Delete = "Permissions.Users.Delete";
        public const string ChangePassword = "Permissions.Users.ChangePassword";
        public const string Activate = "Permissions.Users.Activate";
        public const string ConfirmEmail = "Permissions.Users.ConfirmEmail";
    }

    public static class Roles
    {
        public const string View = "Permissions.Roles.View";
        public const string Create = "Permissions.Roles.Create";
        public const string Edit = "Permissions.Roles.Edit";
        public const string Delete = "Permissions.Roles.Delete";
        public const string ManagePermissions = "Permissions.Roles.ManagePermissions";
    }

    public static class PermissionManagement
    {
        public const string View = "Permissions.Permissions.View";
        public const string Create = "Permissions.Permissions.Create";
        public const string Edit = "Permissions.Permissions.Edit";
        public const string Delete = "Permissions.Permissions.Delete";
    }

    public static class Institutions
    {
        public const string View = PlatformPermissions.Institutions.View;
        public const string Manage = PlatformPermissions.Institutions.Manage;
    }

    public static class Coaching
    {
        public const string View = PlatformPermissions.Coaching.View;
        public const string Manage = PlatformPermissions.Coaching.Manage;
    }

    public static class SpeedReading
    {
        public const string View = PlatformPermissions.SpeedReading.View;
        public const string ContentManage = PlatformPermissions.SpeedReading.ContentManage;
        public const string ProgramManage = PlatformPermissions.SpeedReading.ProgramManage;
        public const string ProgressView = PlatformPermissions.SpeedReading.ProgressView;
        public const string ReportView = PlatformPermissions.SpeedReading.ReportView;
        public const string PlatformAnalyticsView = PlatformPermissions.SpeedReading.PlatformAnalyticsView;
        public const string ReportManage = PlatformPermissions.SpeedReading.ReportManage;
        public const string LeaderboardView = PlatformPermissions.SpeedReading.LeaderboardView;
        public const string GamificationManage = PlatformPermissions.SpeedReading.GamificationManage;
        public const string SettingsManage = PlatformPermissions.SpeedReading.SettingsManage;
    }

    public static class Support
    {
        public const string View = PlatformPermissions.Support.View;
        public const string Reply = PlatformPermissions.Support.Reply;
    }

    public static class Notifications
    {
        public const string Templates = PlatformPermissions.Notifications.Templates;
    }

    public static class Operations
    {
        public const string View = PlatformPermissions.Operations.View;
    }
    
    // Helper to get all permissions
    public static List<string> GetAll()
    {
        var permissions = new List<string>();
        
        // Users
        permissions.Add(Users.View);
        permissions.Add(Users.Create);
        permissions.Add(Users.Edit);
        permissions.Add(Users.Delete);
        permissions.Add(Users.ChangePassword);
        permissions.Add(Users.Activate);
        permissions.Add(Users.ConfirmEmail);

        // Roles
        permissions.Add(Roles.View);
        permissions.Add(Roles.Create);
        permissions.Add(Roles.Edit);
        permissions.Add(Roles.Delete);
        permissions.Add(Roles.ManagePermissions);

        // Permissions
        permissions.Add(PermissionManagement.View);
        permissions.Add(PermissionManagement.Create);
        permissions.Add(PermissionManagement.Edit);
        permissions.Add(PermissionManagement.Delete);

        permissions.AddRange(PlatformPermissions.GetAll());

        return permissions;
    }
}
