using System.ComponentModel.DataAnnotations;
using Coaching.Application.Subscriptions;
using SpeedReading.Application.Subscription;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class AdultPayerDeclarationTests
{
    [Fact]
    public void BothPaymentSummariesExposeDeclarationEvidenceToReviewers()
    {
        foreach (var type in new[] { typeof(CoachingBankTransferRequestSummary), typeof(BankTransferPaymentRequestSummary) })
        {
            type.GetProperty("AdultPayerDeclarationVersion").Should().NotBeNull();
            type.GetProperty("AdultPayerDeclaredAt").Should().NotBeNull();
        }
    }
    [Fact]
    public void BothPaymentEntitiesKeepNullableDeclarationEvidenceForHistoricalRecords()
    {
        var speedType = typeof(SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Legacy.LegacyBankTransferPaymentRequest")!;
        foreach (var type in new[] { typeof(Coaching.Domain.Entities.CoachingBankTransferRequest), speedType })
        {
            type.GetProperty("AdultPayerDeclarationVersion").Should().NotBeNull();
            type.GetProperty("AdultPayerDeclarationVersion")!.PropertyType.Should().Be(typeof(int?));
            type.GetProperty("AdultPayerDeclaredAt")!.PropertyType.Should().Be(typeof(DateTime?));
            var historical = Activator.CreateInstance(type)!;
            type.GetProperty("AdultPayerDeclarationVersion")!.GetValue(historical).Should().BeNull();
            type.GetProperty("AdultPayerDeclaredAt")!.GetValue(historical).Should().BeNull();
        }
    }
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
