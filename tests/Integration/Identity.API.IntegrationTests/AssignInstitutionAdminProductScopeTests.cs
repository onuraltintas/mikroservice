using Identity.Application.Commands.ManageInstitutions;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class AssignInstitutionAdminProductScopeTests
{
    [Theory]
    [InlineData(PlatformProduct.Coaching)]
    [InlineData(PlatformProduct.SpeedReading)]
    public async Task Validator_AcceptsDefinedProduct(PlatformProduct product)
    {
        var command = new AssignInstitutionAdminCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InstitutionAdminRole.Admin,
            product);

        var result = await new AssignInstitutionAdminCommandValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_RejectsMissingOrUndefinedProduct()
    {
        var validator = new AssignInstitutionAdminCommandValidator();
        var missingProduct = new AssignInstitutionAdminCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InstitutionAdminRole.Admin);
        var undefinedProduct = missingProduct with { Product = (PlatformProduct)999 };

        Assert.False((await validator.ValidateAsync(missingProduct)).IsValid);
        Assert.False((await validator.ValidateAsync(undefinedProduct)).IsValid);
    }
}
