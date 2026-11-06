using System.Text.Json;
using System.Text.Json.Serialization;

namespace Identity.API;

/// <summary>The identity API's MVC JSON settings (Program.cs), shared with the contract tests.</summary>
public static class IdentityApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        options.PropertyNameCaseInsensitive = true;
    }
}
