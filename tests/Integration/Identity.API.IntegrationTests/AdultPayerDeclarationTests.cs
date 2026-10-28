using Coaching.Application.Subscriptions;
using SpeedReading.Application.Subscription;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;

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
            // O10 added optional parameters (discount code, contract acceptance); fill them so only the declaration varies.
            var parameters = type.GetConstructors().Single().GetParameters();
            var arguments = parameters.Select((parameter, index) => index switch
            {
                0 => Guid.NewGuid(), 1 => "REF", 2 => "Payer", 3 => null, 4 => accepted,
                _ => parameter.Name == "AcceptSalesContract" ? true : parameter.HasDefaultValue ? parameter.DefaultValue : null
            }).ToArray();
            var request = Activator.CreateInstance(type, arguments)!;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddControllers();
            using var provider = services.BuildServiceProvider();
            var context = new ActionContext { HttpContext = new DefaultHttpContext { RequestServices = provider } };
            provider.GetRequiredService<IObjectModelValidator>().Validate(context, null, string.Empty, request);
            context.ModelState.IsValid.Should().Be(accepted);
        }
    }
}
