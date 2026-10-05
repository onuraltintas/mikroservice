using System.ComponentModel.DataAnnotations;
using Coaching.Application.Subscriptions;
using SpeedReading.Application.Subscription;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class AdultPayerDeclarationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothPaymentContractsRequireExplicitAdultDeclaration(bool accepted)
    {
        foreach (var type in new[] { typeof(CoachingBankTransferRequestCreate), typeof(CreateBankTransferPaymentRequest) })
        {
            var property = type.GetProperty("AdultPayerDeclaration");
            property.Should().NotBeNull("payment requests must explicitly carry the payer declaration");
            var request = Activator.CreateInstance(type, Guid.NewGuid(), "REF", "Payer", null, accepted)!;
            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(request, new ValidationContext(request), errors, true).Should().Be(accepted);
        }
    }
}
