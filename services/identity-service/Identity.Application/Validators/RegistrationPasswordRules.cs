using FluentValidation;

namespace Identity.Application.Validators;

public static class RegistrationPasswordRules
{
    public static bool IsValid(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length is < 8 or > 128)
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
            .WithMessage("Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.");
    }
}
