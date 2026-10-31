using Coaching.Application.Subscriptions;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap Y7: a linked parent sees the child's Coaching access and can send a bank-transfer notice for the child.
public sealed partial class CoachingCmsAndSubscriptionServiceTests
{
    [Fact]
    public async Task LinkedParentPaysForTheChildAndTheChildGetsTheAccess()
    {
        await using var db = CreateDbContext();
        var identity = new TestIdentityAuthorizationClient();
        var subscriptions = new CoachingSubscriptionService(db, identity);
        var actor = Guid.NewGuid(); var parent = Guid.NewGuid(); var child = Guid.NewGuid(); var stranger = Guid.NewGuid();
        identity.ParentLinks.Add((parent, child));
        var plan = (await subscriptions.CreatePlanAsync(Plan("parent-pays", "Bireysel yıllık", "Individual", 1200m, null), actor))!.Value;
        await Settings(subscriptions, actor, "settings-parent-pays-1");

        (await subscriptions.GetChildAccessAsync(stranger, child)).Should().BeNull();
        (await subscriptions.GetChildAccessAsync(parent, child))!.HasAccess.Should().BeFalse();
        var notice = new CoachingBankTransferRequestCreate(plan, "EFT-VELI-0001", null, "Annesi ödedi", true, AcceptSalesContract: true);
        (await subscriptions.CreateChildBankTransferRequestAsync(stranger, "Yabancı", "x@example.test", child, "Ada Öğrenci", "ada@example.test",
            notice, "create-parent-pays-0001")).Should().BeNull();
        var sent = await subscriptions.CreateChildBankTransferRequestAsync(parent, "Ayşe Veli", "veli@example.test", child, "Ada Öğrenci",
            "ada@example.test", notice, "create-parent-pays-0002");

        sent!.UserId.Should().Be(child);
        (sent.PayerName, sent.SubmittedByUserId).Should().Be(("Ayşe Veli", parent));
        (await subscriptions.GetMyBankTransferRequestsAsync(child)).Should().ContainSingle(x => x.Id == sent.Id);
        (await subscriptions.GetChildBankTransferRequestsAsync(parent, child))!.Should().ContainSingle(x => x.Id == sent.Id);
        (await subscriptions.GetChildBankTransferRequestsAsync(stranger, child)).Should().BeNull();

        // The parent who sent it may correct or withdraw it like the child can.
        (await subscriptions.UpdateMyBankTransferRequestAsync(parent, sent.Id, new("EFT-VELI-0002", "Ayşe Veli", null)))!
            .PaymentReference.Should().Be("EFT-VELI-0002");

        await subscriptions.ReviewBankTransferRequestAsync(sent.Id, new CoachingBankTransferReviewRequest("Approved", null), actor, "review-parent-pays-0001");
        (await subscriptions.GetMyAccessAsync(child)).HasAccess.Should().BeTrue();
        (await subscriptions.GetChildAccessAsync(parent, child))!.HasAccess.Should().BeTrue();
        (await db.CoachingSubscriptions.SingleAsync()).UserId.Should().Be(child);
    }
}
