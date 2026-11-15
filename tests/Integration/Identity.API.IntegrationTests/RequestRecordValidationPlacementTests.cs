using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Rehearsal 15.11.2026: the account erasure request answered 500 because a request record carried its validation attributes
/// on the property ([property: Required]) instead of the constructor parameter; ASP.NET Core refuses such a record when it
/// validates the request. No record a controller action takes may do that.
/// </summary>
public sealed class RequestRecordValidationPlacementTests
{
    private static readonly Assembly[] Apis =
    [
        typeof(Identity.API.Controllers.DataSubjectRequestsController).Assembly,
        typeof(Coaching.API.Controllers.CoachingStaffStudyController).Assembly,
        typeof(SpeedReading.API.Controllers.SpeedReadingInvitationsController).Assembly,
        typeof(Notification.API.Controllers.CourseManagementNotificationsController).Assembly
    ];

    private static bool IsRecord(Type type) => type.GetMethod("<Clone>$") is not null;

    [Fact]
    public void NoRequestRecordPutsValidationOnAPropertyThatIsAConstructorParameter()
    {
        var requests = Apis.SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetParameters()).Select(parameter => parameter.ParameterType)
            .Where(type => type.IsClass && type != typeof(string) && IsRecord(type)).Distinct().ToArray();
        Assert.Contains(requests, type => type.Name == "SubmitErasureRequest");

        var wrong = (from type in requests
            let constructor = type.GetConstructors().OrderByDescending(x => x.GetParameters().Length).FirstOrDefault()
            where constructor is not null
            from parameter in constructor.GetParameters()
            let property = type.GetProperty(parameter.Name!, BindingFlags.Public | BindingFlags.Instance)
            where property is not null && property.GetCustomAttributes<ValidationAttribute>(true).Any()
            select $"{type.FullName}.{property.Name}").ToArray();

        Assert.True(wrong.Length == 0, "Move these validation attributes to the constructor parameter: " + string.Join(", ", wrong));
    }

    [Fact]
    public void TheErasureAndInvitationRequestsStillValidateTheirFields()
    {
        static ValidationAttribute[] On(Type type, string parameter) =>
            type.GetConstructors().Single().GetParameters().Single(x => x.Name == parameter).GetCustomAttributes<ValidationAttribute>().ToArray();

        Assert.Contains(On(typeof(Identity.API.Controllers.SubmitErasureRequest), "Reason"), x => x is RequiredAttribute);
        Assert.Contains(On(typeof(Identity.API.Controllers.DataSubjectRequestDecisionRequest), "Reason"), x => x is MaxLengthAttribute { Length: 2_000 });
        Assert.Contains(On(typeof(SpeedReading.API.Controllers.InviteSpeedReadingStudentRequest), "Email"), x => x is EmailAddressAttribute);
    }
}
