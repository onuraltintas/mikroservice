namespace EduPlatform.Gateway.Configuration;

public static class GatewayAuthorizationPolicies
{
    /// <summary>SystemAdmin or the Operations.View permission (same audience as the configuration screen).</summary>
    public const string OperationsView = "GatewayOperationsView";
}
