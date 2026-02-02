namespace Identity.Domain.Enums;

public enum ConfigurationDataType
{
    String = 0,
    Number = 1,
    Boolean = 2,
    Json = 3,
    Secret = 4 // For sensitive data like API Keys
}
