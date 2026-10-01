using Coaching.Application.Content;
using Coaching.Application.Interfaces;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingCmsAndSubscriptionServiceTests
{
    [Theory]
    [InlineData("Teacher")]
    [InlineData("Institution")]
    public async Task ContactPlan_WithoutAgreedCapacityCannotGrantAccess(string audience)
    {
        await using var db = CreateDbContext();
        var service = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient());
        var plan = new CoachingSubscriptionPlan { Slug = "quote", Name = "Quote", Audience = audience,
            Price = 0, IsContactOnly = true, DurationDays = 365, IncludedStudentSeats = null };
        db.CoachingSubscriptionPlans.Add(plan);
        await db.SaveChangesAsync();

        var result = await service.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            plan.Id, audience == "Teacher" ? Guid.NewGuid() : null, null, null,
            audience == "Institution" ? Guid.NewGuid() : null, [], DateTime.UtcNow, null), Guid.NewGuid());

        result.Should().BeNull();
        (await db.CoachingSubscriptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CmsEntries_SupportPublicationSchedulingAndRevisionRestore()
    {
        await using var db = CreateDbContext();
        var cms = CreateCmsService(db);
        var actorId = Guid.NewGuid();
        var request = new CoachingCmsEntryRequest(
            "Page", null, "İlk sayfa", "ilk-sayfa", null, "İlk metin", null, null, [],
            IsPublished: true, DateTime.UtcNow.AddHours(1), 0);

        var id = await cms.CreateEntryAsync(request, actorId);
        id.Should().NotBeNull();
        (await cms.GetPublishedPageAsync("ilk-sayfa")).Should().BeNull();

        var updated = request with { Title = "Güncel sayfa", Content = "Güncel metin", ScheduledPublishAt = null };
        (await cms.UpdateEntryAsync(id!.Value, updated, actorId)).Should().BeTrue();
        (await cms.GetPublishedPageAsync("ilk-sayfa"))!.Title.Should().Be("Güncel sayfa");

        var revisions = await cms.GetRevisionsAsync(id.Value);
        revisions.Should().ContainSingle();
        (await cms.RestoreRevisionAsync(id.Value, revisions[0].Id, actorId)).Should().BeTrue();
        (await cms.GetEntryAsync(id.Value))!.Title.Should().Be("İlk sayfa");
    }

    [Fact]
    public async Task CmsEntries_RejectDuplicateKindAndSlug()
    {
        await using var db = CreateDbContext();
        var cms = CreateCmsService(db);
        var request = new CoachingCmsEntryRequest(
            "Page", null, "Hakkımızda", "hakkimizda", null, "İçerik", null, null, [], false, null, 0);

        (await cms.CreateEntryAsync(request, Guid.NewGuid())).Should().NotBeNull();
        (await cms.CreateEntryAsync(request with { Title = "Başka sayfa" }, Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task CmsBlogEntries_PreserveEditorialMetadataThroughRevisionRestore()
    {
        await using var db = CreateDbContext();
        var cms = CreateCmsService(db);
        var actorId = Guid.NewGuid();
        var publishedAt = new DateTime(2026, 9, 20, 9, 30, 0, DateTimeKind.Utc);
        var coverImageUrl = $"/api/coaching/cms/media/{Guid.NewGuid():D}";
        var request = new CoachingCmsEntryRequest(
            "Blog", null, "Çalışma planı", "calisma-plani", "Kısa özet", "Yazı içeriği", null, null, ["plan"],
            IsPublished: true, ScheduledPublishAt: null, SortOrder: 0,
            Author: "  Deniz Öğretmen  ", PublishedAt: publishedAt, CoverImageUrl: coverImageUrl);

        var id = await cms.CreateEntryAsync(request, actorId);
        id.Should().NotBeNull();
        var saved = (await cms.GetPublishedBlogAsync(1, 10)).Items.Should().ContainSingle().Which;
        saved.Author.Should().Be("Deniz Öğretmen");
        saved.PublishedAt.Should().Be(publishedAt);
        saved.CoverImageUrl.Should().Be(coverImageUrl);

        (await cms.GetRevisionsAsync(id!.Value)).Should().BeEmpty();
        (await cms.UpdateEntryAsync(id.Value, request with { Author = "Başka yazar", CoverImageUrl = null }, actorId)).Should().BeTrue();
        var previous = (await cms.GetRevisionsAsync(id.Value)).Should().ContainSingle().Which;
        (await cms.RestoreRevisionAsync(id.Value, previous.Id, actorId)).Should().BeTrue();

        var restored = (await cms.GetEntryAsync(id.Value))!;
        restored.Author.Should().Be("Deniz Öğretmen");
        restored.PublishedAt.Should().Be(publishedAt);
        restored.CoverImageUrl.Should().Be(coverImageUrl);
    }

    [Fact]
    public async Task CmsBlogEntries_RejectExternalCoverImageUrls()
    {
        await using var db = CreateDbContext();
        var cms = CreateCmsService(db);
        var request = new CoachingCmsEntryRequest(
            "Blog", null, "Çalışma planı", "calisma-plani", null, "İçerik", null, null, [],
            IsPublished: false, ScheduledPublishAt: null, SortOrder: 0,
            Author: null, PublishedAt: null, CoverImageUrl: "https://images.example.test/cover.jpg");

        var action = () => cms.CreateEntryAsync(request, Guid.NewGuid());

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CmsBlocks_CanBeFilteredByDedicatedHomepageGroup()
    {
        await using var db = CreateDbContext();
        var cms = CreateCmsService(db);
        var actorId = Guid.NewGuid();
        var faq = new CoachingCmsEntryRequest(
            "Block", "HomeFaq", "Öğrenci nasıl katılır?", "faq-student", null, "Davet bağlantısıyla.", null, null, [],
            false, null, 0);
        var testimonial = faq with { Group = "HomeTestimonials", Title = "Öğrenci", Slug = "testimonial-student" };
        await cms.CreateEntryAsync(faq, actorId);
        await cms.CreateEntryAsync(testimonial, actorId);

        var result = await cms.GetEntriesAsync("Block", 1, 25, null, "HomeFaq");

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Group.Should().Be("HomeFaq");
    }

    [Fact]
    public async Task CmsNavigation_OnlyAllowsMenusRenderedByTheCoachingSite()
    {
        await using var db = CreateDbContext();
        var cms = CreateCmsService(db);
        var actorId = Guid.NewGuid();
        var footer = new CoachingCmsNavigationItemRequest("Footer", "SSS", "/coaching/faq", null, 0, true, false);
        var unsupported = footer with { Menu = "UnusedMenu" };

        (await cms.CreateNavigationItemAsync(footer, actorId)).Should().NotBeNull();
        var action = () => cms.CreateNavigationItemAsync(unsupported, actorId);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CmsHomepageSettings_AllowOnlyOneEntryPerManagedSection()
    {
        await using var db = CreateDbContext();
        var cms = CreateCmsService(db);
        var actorId = Guid.NewGuid();
        var first = new CoachingCmsEntryRequest(
            "Block", "HomeFaqHeading", "SSS", "home-faq-heading", null, "", null, null, [], false, null, 0);
        var second = first with { Title = "Sorular", Slug = "home-faq-title-2" };

        (await cms.CreateEntryAsync(first, actorId)).Should().NotBeNull();
        (await cms.CreateEntryAsync(second, actorId)).Should().BeNull();
    }

    [Fact]
    public async Task CmsTestimonials_RequireRecordedPublicationConsentBeforePublishing()
    {
        await using var db = CreateDbContext();
        var cms = CreateCmsService(db);
        var request = new CoachingCmsEntryRequest(
            "Block", "HomeTestimonials", "Ada", "ada-ogrenci", "Öğrenci", "Gerçek geri bildirim.", null, null, [],
            IsPublished: true, ScheduledPublishAt: null, SortOrder: 0);

        var action = () => cms.CreateEntryAsync(request, Guid.NewGuid());
        await action.Should().ThrowAsync<ArgumentException>();

        var approvedId = await cms.CreateEntryAsync(request with { TestimonialConsentConfirmed = true }, Guid.NewGuid());
        approvedId.Should().NotBeNull();
        (await cms.GetPublishedBlocksAsync("HomeTestimonials")).Should().ContainSingle();
    }

    [Fact]
    public async Task ApprovedBankTransfer_GrantsCoachingAccessAndCreatesOnePayment()
    {
        await using var db = CreateDbContext();
        var subscriptions = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient());
        var actorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var planId = await subscriptions.CreatePlanAsync(
            Plan("individual-annual", "Bireysel yıllık", "Individual", 1200m, null), actorId);
        planId.Should().NotBeNull();

        await subscriptions.UpdateSettingsAsync(new CoachingSubscriptionSettingsRequest(
            RequireActiveSubscription: true,
            Currency: "TRY",
            AccountHolder: "Edu İvme",
            BankName: "Örnek Banka",
            Iban: "TR33 0006 1005 1978 6457 8413 26",
            PaymentInstructions: "Açıklamaya kullanıcı e-postasını yazın.",
            BankTransferEnabled: true), actorId, "settings-approved-test-key-0001");

        (await subscriptions.GetMyAccessAsync(userId)).HasAccess.Should().BeFalse();
        var transfer = await subscriptions.CreateBankTransferRequestAsync(
            userId,
            "Öğrenci Adı",
            "student@example.com",
            new CoachingBankTransferRequestCreate(planId!.Value, "EFT-2026-001", "Öğrenci Adı", null),
            "create-approved-test-key-0001");
        transfer.Should().NotBeNull();

        var reviewed = await subscriptions.ReviewBankTransferRequestAsync(
            transfer!.Id, new CoachingBankTransferReviewRequest("Approved", "Banka hareketi doğrulandı."), actorId,
            "review-approved-test-key-0001");
        reviewed!.Status.Should().Be("Approved");
        (await subscriptions.GetMyAccessAsync(userId)).HasAccess.Should().BeTrue();
        (await subscriptions.GetPaymentsAsync(null, null, 1, 25)).TotalCount.Should().Be(1);

        await subscriptions.ReviewBankTransferRequestAsync(
            transfer.Id, new CoachingBankTransferReviewRequest("Approved", "Tekrar istek"), actorId,
            "review-approved-repeat-key-01");
        (await subscriptions.GetPaymentsAsync(null, null, 1, 25)).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task BankTransferPaymentReferences_AreUniquePerUserAtTheDatabaseBoundary()
    {
        await using var db = CreateDbContext();
        var requestEntity = db.Model.FindEntityType(typeof(CoachingBankTransferRequest));
        var index = requestEntity!.GetIndexes().SingleOrDefault(candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(CoachingBankTransferRequest.UserId), nameof(CoachingBankTransferRequest.PaymentReference)]));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public async Task BankTransferWrites_AreIdempotentAndPaymentReferencesCannotBeReused()
    {
        await using var db = CreateDbContext();
        var subscriptions = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient());
        var actorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var planId = await subscriptions.CreatePlanAsync(
            Plan("idempotent-student", "Bireysel EFT", "Individual", 1200m, null), actorId);
        var settings = new CoachingSubscriptionSettingsRequest(
            true, "TRY", "Edu İvme", "Örnek Banka", "TR33 0006 1005 1978 6457 8413 26", null, true);
        (await subscriptions.UpdateSettingsAsync(settings, actorId, "settings-idempotency-key-0001"))
            .BankTransferEnabled.Should().BeTrue();
        (await subscriptions.UpdateSettingsAsync(settings, actorId, "settings-idempotency-key-0001"))
            .BankTransferEnabled.Should().BeTrue();

        var createRequest = new CoachingBankTransferRequestCreate(planId!.Value, "eft 2026-01", "Ada Öğrenci", null);
        var first = await subscriptions.CreateBankTransferRequestAsync(
            userId, "Ada Öğrenci", "ada@example.test", createRequest, "create-idempotency-key-0001");
        var retry = await subscriptions.CreateBankTransferRequestAsync(
            userId, "Ada Öğrenci", "ada@example.test", createRequest, "create-idempotency-key-0001");
        first!.Id.Should().Be(retry!.Id);
        first.PaymentReference.Should().Be("EFT-2026-01");

        var sameReference = await subscriptions.CreateBankTransferRequestAsync(
            userId, "Ada Öğrenci", "ada@example.test",
            createRequest with { Note = "tekrar gönderim" }, "create-idempotency-key-0002");
        sameReference!.Id.Should().Be(first.Id);

        var reusedKey = () => subscriptions.CreateBankTransferRequestAsync(
            userId, "Ada Öğrenci", "ada@example.test",
            createRequest with { PaymentReference = "EFT-2026-02" }, "create-idempotency-key-0001");
        await reusedKey.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Idempotency.Conflict");
    }

    [Fact]
    public async Task BankTransferReview_RequiresReasonForRejectionAndCanDeleteRequests()
    {
        await using var db = CreateDbContext();
        var subscriptions = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient());
        var actorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var planId = await subscriptions.CreatePlanAsync(
            Plan("review-student", "Bireysel EFT", "Individual", 1200m, null), actorId);
        await subscriptions.UpdateSettingsAsync(new CoachingSubscriptionSettingsRequest(
            true, "TRY", "Edu İvme", "Örnek Banka", "TR33 0006 1005 1978 6457 8413 26", null, true),
            actorId, "settings-review-key-0001");
        var transfer = await subscriptions.CreateBankTransferRequestAsync(
            userId, "Ada Öğrenci", "ada@example.test",
            new CoachingBankTransferRequestCreate(planId!.Value, "EFT-2026-03", "Ada Öğrenci", null),
            "create-review-key-0001");

        var noReason = await subscriptions.ReviewBankTransferRequestAsync(
            transfer!.Id, new CoachingBankTransferReviewRequest("Rejected", null), actorId, "review-key-no-reason-001");
        noReason.Should().BeNull();
        var rejected = await subscriptions.ReviewBankTransferRequestAsync(
            transfer.Id, new CoachingBankTransferReviewRequest("Rejected", "Banka hareketi bulunamadı."), actorId,
            "review-key-with-reason-01");
        rejected!.ReviewNote.Should().Be("Banka hareketi bulunamadı.");

        (await subscriptions.DeleteBankTransferRequestAsync(transfer.Id)).Should().BeTrue();
        (await subscriptions.GetMyBankTransferRequestsAsync(userId)).Should().BeEmpty();
        (await db.IdempotencyRecords.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task InstitutionEftGrant_StoresAndDeduplicatesInstitutionPaymentReference()
    {
        await using var db = CreateDbContext();
        var subscriptions = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient());
        var actorId = Guid.NewGuid();
        var institutionId = Guid.NewGuid();
        var firstStudentId = Guid.NewGuid();
        var secondStudentId = Guid.NewGuid();
        var planId = await subscriptions.CreatePlanAsync(
            Plan("institution-eft", "Kurum EFT", "Institution", 10000m, 20), actorId);

        var request = new CoachingSubscriptionCreateRequest(
            planId!.Value, null, null, null, institutionId, [firstStudentId], DateTime.UtcNow, null, "eft-2026/kurum-01");
        var first = await subscriptions.CreateSubscriptionAsync(request, actorId);
        var replay = await subscriptions.CreateSubscriptionAsync(
            request with { StudentIds = [secondStudentId] }, actorId);

        first.Should().NotBeNull();
        first!.PaymentReference.Should().Be("EFT-2026-KURUM-01");
        replay!.Id.Should().Be(first.Id);
        replay.SeatCount.Should().Be(1);
        (await db.CoachingSubscriptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task InstitutionSubscription_GrantsSeatAccessAndSuspensionRevokesIt()
    {
        await using var db = CreateDbContext();
        var subscriptions = new CoachingSubscriptionService(db, new TestIdentityAuthorizationClient());
        var actorId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var planId = await subscriptions.CreatePlanAsync(
            Plan("institution-annual", "Kurum yıllık", "Institution", 10000m, 20), actorId);
        await subscriptions.UpdateSettingsAsync(new CoachingSubscriptionSettingsRequest(
            true, "TRY", null, null, null, null, false), actorId, "settings-institution-test-key-01");
        var license = await subscriptions.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            planId!.Value, null, null, null, Guid.NewGuid(), [studentId], DateTime.UtcNow, null), actorId);

        license.Should().NotBeNull();
        (await subscriptions.GetMyAccessAsync(studentId)).HasAccess.Should().BeTrue();
        (await subscriptions.ChangeStudentSeatAsync(
            license!.Id, studentId, new CoachingSubscriptionSeatChangeRequest(true, "Kayıt donduruldu"), actorId)).Should().BeTrue();
        (await subscriptions.GetMyAccessAsync(studentId)).HasAccess.Should().BeFalse();
    }

    [Fact]
    public async Task TeacherSubscription_OnlyGrantsSeatsToCurrentlyAssignedStudentsWithinCapacity()
    {
        await using var db = CreateDbContext();
        var identity = new TestIdentityAuthorizationClient();
        var subscriptions = new CoachingSubscriptionService(db, identity);
        var actorId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var unassignedStudentId = Guid.NewGuid();
        identity.ActiveTeachers.Add(teacherId);
        identity.TeacherAssignments.Add((teacherId, studentId));
        identity.TeacherAssignments.Add((teacherId, otherStudentId));

        var planId = await subscriptions.CreatePlanAsync(
            Plan("teacher-monthly", "Öğretmen aylık", "Teacher", 500m, 1), actorId);
        planId.Should().NotBeNull();
        await subscriptions.UpdateSettingsAsync(new CoachingSubscriptionSettingsRequest(
            true, "TRY", "Edu İvme", "Örnek Banka", "TR33 0006 1005 1978 6457 8413 26", null, true),
            actorId, "settings-teacher-test-key-0001");

        var transfer = await subscriptions.CreateTeacherBankTransferRequestAsync(
            teacherId, "Öğretmen", "teacher@example.test",
            new CoachingBankTransferRequestCreate(planId!.Value, "TEACHER-EFT-1", "Öğretmen", null),
            "create-teacher-test-key-0001");
        transfer.Should().NotBeNull();
        var reviewed = await subscriptions.ReviewBankTransferRequestAsync(
            transfer!.Id, new CoachingBankTransferReviewRequest("Approved", null), actorId,
            "review-teacher-test-key-0001");
        reviewed!.Status.Should().Be("Approved");

        var summary = await subscriptions.GetMyTeacherSubscriptionAsync(teacherId);
        summary.Should().NotBeNull();
        summary!.IncludedStudentSeats.Should().Be(1);
        summary.UsedStudentSeats.Should().Be(0);
        (await subscriptions.GetMyAccessAsync(teacherId)).HasAccess.Should().BeFalse();

        (await subscriptions.AssignMyTeacherStudentSeatAsync(teacherId, unassignedStudentId)).Should().BeFalse();
        (await subscriptions.AssignMyTeacherStudentSeatAsync(teacherId, studentId)).Should().BeTrue();
        (await subscriptions.AssignMyTeacherStudentSeatAsync(teacherId, otherStudentId)).Should().BeFalse();
        (await subscriptions.GetMyAccessAsync(studentId)).HasAccess.Should().BeTrue();

        identity.TeacherAssignments.Remove((teacherId, studentId));
        (await subscriptions.GetMyAccessAsync(studentId)).HasAccess.Should().BeFalse();
        (await subscriptions.RemoveMyTeacherStudentSeatAsync(teacherId, studentId)).Should().BeTrue();
        (await subscriptions.GetMyTeacherSubscriptionAsync(teacherId))!.UsedStudentSeats.Should().Be(0);
    }

    [Fact]
    public async Task TeacherSubscription_RenewalExtendsExistingSubscriptionAndPreservesAssignedSeats()
    {
        await using var db = CreateDbContext();
        var identity = new TestIdentityAuthorizationClient();
        var subscriptions = new CoachingSubscriptionService(db, identity);
        var actorId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        identity.ActiveTeachers.Add(teacherId);
        identity.TeacherAssignments.Add((teacherId, studentId));

        var planId = await subscriptions.CreatePlanAsync(
            Plan("teacher-renewal", "Öğretmen yenileme", "Teacher", 500m, 2), actorId);
        await subscriptions.UpdateSettingsAsync(new CoachingSubscriptionSettingsRequest(
            true, "TRY", "Edu İvme", "Örnek Banka", "TR33 0006 1005 1978 6457 8413 26", null, true),
            actorId, "settings-renewal-test-key-01");
        var original = await subscriptions.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            planId!.Value, teacherId, "Öğretmen", "teacher@example.test", null, [], DateTime.UtcNow, null), actorId);
        original.Should().NotBeNull();
        (await subscriptions.AssignMyTeacherStudentSeatAsync(teacherId, studentId)).Should().BeTrue();
        var originalEndDate = original!.EndDate;

        var transfer = await subscriptions.CreateTeacherBankTransferRequestAsync(
            teacherId, "Öğretmen", "teacher@example.test",
            new CoachingBankTransferRequestCreate(planId.Value, "TEACHER-EFT-RENEW", "Öğretmen", null),
            "create-teacher-renewal-key-01");
        transfer.Should().NotBeNull();
        (await subscriptions.ReviewBankTransferRequestAsync(
            transfer!.Id, new CoachingBankTransferReviewRequest("Approved", null), actorId,
            "review-teacher-renewal-key-01"))!
            .Status.Should().Be("Approved");

        (await db.CoachingSubscriptions.CountAsync()).Should().Be(1);
        (await db.CoachingSubscriptions.SingleAsync()).EndDate.Should().BeAfter(originalEndDate);
        var renewed = await subscriptions.GetMyTeacherSubscriptionAsync(teacherId);
        renewed!.UsedStudentSeats.Should().Be(1);
        renewed.Students.Should().ContainSingle().Which.StudentId.Should().Be(studentId);
        (await subscriptions.GetPaymentsAsync(null, null, 1, 25)).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task TeacherPlan_AdminGrantRejectsInstitutionLinkedTeacher()
    {
        await using var db = CreateDbContext();
        var identity = new TestIdentityAuthorizationClient();
        var subscriptions = new CoachingSubscriptionService(db, identity);
        var actorId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        identity.ActiveTeachers.Add(teacherId);
        identity.TeacherInstitutions.Add(teacherId, Guid.NewGuid());
        var planId = await subscriptions.CreatePlanAsync(
            Plan("independent-teacher", "Bağımsız öğretmen", "Teacher", 500m, 5), actorId);

        var result = await subscriptions.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            planId!.Value, teacherId, "Öğretmen", "teacher@example.test", null, [], DateTime.UtcNow, null), actorId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TeacherPlan_RejectsOverlappingPaidPlansForTheSameIndependentTeacher()
    {
        await using var db = CreateDbContext();
        var identity = new TestIdentityAuthorizationClient();
        var subscriptions = new CoachingSubscriptionService(db, identity);
        var actorId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        identity.ActiveTeachers.Add(teacherId);
        var firstPlanId = await subscriptions.CreatePlanAsync(
            Plan("teacher-first", "Öğretmen ilk plan", "Teacher", 500m, 5), actorId);
        var secondPlanId = await subscriptions.CreatePlanAsync(
            Plan("teacher-second", "Öğretmen ikinci plan", "Teacher", 700m, 10), actorId);

        var first = await subscriptions.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            firstPlanId!.Value, teacherId, "Öğretmen", "teacher@example.test", null, [], DateTime.UtcNow, null), actorId);
        var overlapping = await subscriptions.CreateSubscriptionAsync(new CoachingSubscriptionCreateRequest(
            secondPlanId!.Value, teacherId, "Öğretmen", "teacher@example.test", null, [], DateTime.UtcNow.AddDays(1), null), actorId);

        first.Should().NotBeNull();
        overlapping.Should().BeNull();
    }

    [Fact]
    public async Task CmsMedia_UploadsListsServesAndDeletesOnlyCoachingAssets()
    {
        await using var db = CreateDbContext();
        var storage = new MemoryCmsMediaStorage();
        var cms = new CoachingCmsService(db, storage);
        var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1 };

        var uploaded = await cms.UploadMediaAsync(
            new CoachingCmsMediaUpload("cover.png", "image/png", bytes.LongLength, new MemoryStream(bytes), "Koçluk kapak görseli"),
            Guid.NewGuid());

        uploaded.FileName.Should().Be("cover.png");
        uploaded.SizeBytes.Should().Be(bytes.LongLength);
        (await cms.GetMediaAssetsAsync(1, 25)).TotalCount.Should().Be(1);
        var download = await cms.GetMediaDownloadAsync(uploaded.Id);
        download.Should().NotBeNull();
        using var output = new MemoryStream();
        await download!.Content.CopyToAsync(output);
        output.ToArray().Should().Equal(bytes);
        (await cms.DeleteMediaAsync(uploaded.Id, Guid.NewGuid())).Should().BeTrue();
        (await cms.GetMediaDownloadAsync(uploaded.Id)).Should().BeNull();
        (await cms.GetMediaAssetsAsync(1, 25)).TotalCount.Should().Be(0);
    }

    private static CoachingSubscriptionPlanRequest Plan(
        string slug,
        string name,
        string audience,
        decimal price,
        int? seats) => new(
            slug, name, "Koçluk abonelik planı", audience, price, false, "Annual", 365, seats,
            ["Öğrenci raporları", "Ödev ve seans yönetimi"], true, true, 0);

    private static CoachingDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class TestIdentityAuthorizationClient : ICoachingIdentityAuthorizationClient
    {
        public HashSet<Guid> ActiveTeachers { get; } = [];
        public HashSet<(Guid TeacherId, Guid StudentId)> TeacherAssignments { get; } = [];
        public Dictionary<Guid, Guid> TeacherInstitutions { get; } = [];

        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken cancellationToken) =>
            Task.FromResult<CoachingAdminAccessScope?>(null);

        public Task<Guid?> AuthorizeTeacherTargetsAsync(
            Guid teacherId,
            IReadOnlyCollection<Guid> studentIds,
            Guid? requestedInstitutionId,
            bool isSystemAdministrator,
            CancellationToken cancellationToken)
        {
            if (!ActiveTeachers.Contains(teacherId))
            {
                throw new BusinessRuleException("Authorization.Forbidden", "Teacher is not active.");
            }

            Guid? institutionId = TeacherInstitutions.TryGetValue(teacherId, out var value) ? value : null;
            if (requestedInstitutionId.HasValue && requestedInstitutionId != institutionId
                || studentIds.Any(studentId => !TeacherAssignments.Contains((teacherId, studentId))))
            {
                throw new BusinessRuleException("Authorization.Forbidden", "Teacher target is not assigned.");
            }

            return Task.FromResult(institutionId);
        }

        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(
            Guid viewerUserId,
            IReadOnlyCollection<Guid> studentIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());
    }

    private static CoachingCmsService CreateCmsService(CoachingDbContext db) => new(db, new MemoryCmsMediaStorage());

    private sealed class MemoryCmsMediaStorage : ICoachingCmsMediaStorage
    {
        private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

        public async Task<CoachingCmsStoredMedia> SaveAsync(Guid mediaId, CoachingCmsMediaUpload upload, string extension, CancellationToken cancellationToken = default)
        {
            using var contents = new MemoryStream();
            await upload.Content.CopyToAsync(contents, cancellationToken);
            var bytes = contents.ToArray();
            var key = $"cms/{mediaId:N}{extension}";
            files[key] = bytes;
            return new CoachingCmsStoredMedia(key, bytes.LongLength, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(files.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes, writable: false) as Stream : null);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }
}
