using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public class SystemConfiguration : Entity
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ConfigurationDataType DataType { get; private set; }
    public bool IsPublic { get; private set; } // Can be exposed to frontend
    public string Group { get; private set; } = "General"; // e.g. "System", "Auth", "Mail"

    private SystemConfiguration() { }

    public static SystemConfiguration Create(string key, string value, string description, ConfigurationDataType dataType, string group = "General", bool isPublic = false)
    {
        return new SystemConfiguration
        {
            Id = Guid.NewGuid(),
            Key = key,
            Value = value,
            Description = description,
            DataType = dataType,
            Group = group,
            IsPublic = isPublic,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateValue(string value, bool? isPublic = null)
    {
        Value = value;
        if (isPublic.HasValue) IsPublic = isPublic.Value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateMetadata(string description, string group, ConfigurationDataType dataType)
    {
        Description = description;
        Group = group;
        DataType = dataType;
        UpdatedAt = DateTime.UtcNow;
    }
}
