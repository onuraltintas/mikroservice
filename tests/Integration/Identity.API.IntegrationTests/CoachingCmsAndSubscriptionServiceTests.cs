using EduPlatform.Shared.Contracts.Events.Coaching;
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

public sealed partial class CoachingCmsAndSubscriptionServiceTests
{
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

    private sealed class CapturingPublisher : ICoachingEventPublisher
    {
        private readonly List<object> messages = [];
        public IReadOnlyList<T> Of<T>() => messages.OfType<T>().ToArray();
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private static CoachingDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class TestIdentityAuthorizationClient : ICoachingIdentityAuthorizationClient
    {
        public HashSet<Guid> ActiveTeachers { get; } = [];
        public HashSet<(Guid TeacherId, Guid StudentId)> TeacherAssignments { get; } = [];
        public Dictionary<Guid, Guid> TeacherInstitutions { get; } = [];
        public HashSet<(Guid InstitutionId, Guid StudentId)> InstitutionStudents { get; } = [];
        public Task<bool> IsActiveInstitutionStudentAsync(Guid institutionId, Guid studentId, CancellationToken cancellationToken) =>
            Task.FromResult(InstitutionStudents.Contains((institutionId, studentId)));

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
            Task.FromResult<IReadOnlyCollection<Guid>>(studentIds.Where(student => ParentLinks.Contains((viewerUserId, student))).ToArray());

        public HashSet<(Guid ParentId, Guid StudentId)> ParentLinks { get; } = [];
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
