using Identity.Domain.Enums;

namespace Identity.Application.DTOs.Settings;

public class ConfigurationDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ConfigurationDataType DataType { get; set; }
    public bool IsPublic { get; set; }
    public string Group { get; set; } = "General";
}

public class CreateConfigurationRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ConfigurationDataType DataType { get; set; }
    public string Group { get; set; } = "General";
    public bool IsPublic { get; set; }
}

public class UpdateConfigurationRequest
{
    public string Value { get; set; } = string.Empty;
}
