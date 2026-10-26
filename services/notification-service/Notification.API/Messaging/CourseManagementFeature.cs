using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Notification.API.Controllers;

namespace Notification.API.Messaging;

/// <summary>
/// Course Management notifications are switched on with <c>CourseManagement__Enabled=true</c>.
/// Off (the default) means no Course Management queues are declared or consumed and its
/// notification API is not mapped, so the service can ship before Course Management does.
/// </summary>
public static class CourseManagementFeature
{
    public const string ConfigurationKey = "CourseManagement:Enabled";

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(ConfigurationKey);

    /// <summary>Removes the Course Management controller while the feature is off.</summary>
    public sealed class ControllerConvention(bool enabled) : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            if (enabled) return;
            foreach (var controller in application.Controllers
                         .Where(controller => controller.ControllerType == typeof(CourseManagementNotificationsController))
                         .ToList())
            {
                application.Controllers.Remove(controller);
            }
        }
    }
}
