using FluentValidation;

namespace Identity.Application.Validators;

/// <summary>
/// The one password policy of the platform: registration, admin password change, self password
/// change and password reset all use it, and the clients mirror it (8–128 characters, an ASCII
/// upper-case letter, an ASCII lower-case letter, a digit and any character that is not an ASCII
/// letter or digit).
/// </summary>
public static class RegistrationPasswordRules
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    public const string Message =
        "Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.";

    public static bool IsValid(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length is < MinimumLength or > MaximumLength)
        {
            return false;
        }

        return password.Any(character => character is >= 'A' and <= 'Z')
            && password.Any(character => character is >= 'a' and <= 'z')
            && password.Any(character => character is >= '0' and <= '9')
            && password.Any(character => !(character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'));
    }

    public static IRuleBuilderOptions<T, string> ApplyRegistrationPasswordPolicy<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Must(password => IsValid(password))
            .WithMessage(Message);
    }
}
