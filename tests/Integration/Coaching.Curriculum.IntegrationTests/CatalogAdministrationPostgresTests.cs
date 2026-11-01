using System.Reflection;
using Coaching.API.Controllers;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using static Coaching.Curriculum.IntegrationTests.CurriculumPostgresTests;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>KM7 (01.11.2026): catalog administration API on PostgreSQL.</summary>
public sealed class CatalogAdministrationPostgresTests
{
    private static CoachingCatalogAdminToolsService Tools(Pg f) => new(f.Db, f.Data.Scope, f.Data.User);

    [LocalPostgresFact]
    public async Task MigrationRoundTripKeepsLinksAndCopiesScoreModelsIntoHistory_AndDownRefusesRejectedLinks()
    {
        await using var f = await Pg.Create("20261109090000_ExamSessionsAndNewPurposes");
        var admin = Guid.NewGuid(); var ranks = "[{\"Score\":500,\"Rank\":1},{\"Score\":400,\"Rank\":80000}]";
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.score_models ("Id","Year","ScoreType","BaseScore","DiplomaWeight","CoefficientsJson","RanksJson","ImportedByUserId","ImportedAt","CreatedAt")
            VALUES ({Guid.NewGuid()},2025,'LGS',200,0,'[]'::jsonb,{ranks}::jsonb,{admin},{DateTime.UtcNow},{DateTime.UtcNow})
            """);
        await f.Db.Database.MigrateAsync();
        var version = await f.Db.Set<CoachingScoreModelVersion>().SingleAsync();
        Assert.Equal(("Upload", 2025, "LGS", 2, admin), (version.Action, version.Year, version.ScoreType, version.RankRows, version.ActorUserId));
        var topic = await f.Data.Seed();
        var link = await f.Db.Set<StudyCatalogTopicOutcome>().AsNoTracking().SingleAsync();
        Assert.False(link.IsRejected);
        await f.Db.GetService<IMigrator>().MigrateAsync("20261109090000_ExamSessionsAndNewPurposes");
        await f.Db.Database.MigrateAsync(); f.Db.ChangeTracker.Clear();
        Assert.True((await f.Db.Set<StudyCatalogTopicOutcome>().SingleAsync()).IsApproved);
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
        await f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.study_catalog_topic_outcomes SET "IsApproved" = false, "IsRejected" = true""");
        await Assert.ThrowsAnyAsync<Exception>(() => f.Db.GetService<IMigrator>().MigrateAsync("20261109090000_ExamSessionsAndNewPurposes"));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.study_catalog_topic_outcomes SET "IsApproved" = true"""));
        Assert.Equal(topic.Id, (await f.Db.Set<StudyCatalogTopicOutcome>().AsNoTracking().SingleAsync()).TopicId);
    }

    [LocalPostgresFact]
    public async Task ChildrenSearchIsTurkishCaseInsensitive_AndRowsCarryFiltersAndCounts()
    {
        await using var f = await Pg.Create(); var topic = await f.Data.Seed();
        foreach (var name in new[] { "Türev", "TÜREV ALMA", "türev uygulamaları", "IŞIK", "İntegral", "Limit" })
        { var t = StudyCatalogTopic.Create("test", name, topic.LessonId, topic.UnitId, name, null, 2); f.Db.Add(t); }
        await f.Db.SaveChangesAsync();
        var found = await f.Service.ChildrenAsync("topics", null, "türev", 1, new CurriculumChildrenFilter(), default, all: true);
        Assert.Equal(3, found.TotalCount);
        Assert.Single((await f.Service.ChildrenAsync("topics", null, "ışık", 1, new CurriculumChildrenFilter(), default, all: true)).Items);
        Assert.Single((await f.Service.ChildrenAsync("topics", null, "integral", 1, new CurriculumChildrenFilter(), default, all: true)).Items);
        Assert.Single((await f.Service.ChildrenAsync("topics", null, "İNTEGRAL", 1, new CurriculumChildrenFilter(), default, all: true)).Items);
        Assert.Equal(7, (await f.Service.ChildrenAsync("topics", null, null, 1, new CurriculumChildrenFilter(ExamCode: "lgs"), default, all: true)).TotalCount);
        Assert.Equal(0, (await f.Service.ChildrenAsync("topics", null, null, 1, new CurriculumChildrenFilter(ExamCode: "TYT"), default, all: true)).TotalCount);
        var active = await f.Service.ChildrenAsync("topics", null, null, 1, new CurriculumChildrenFilter(LessonId: topic.LessonId, Active: true, PageSize: 100), default, all: true);
        var row = Assert.Single(active.Items);
        Assert.Equal((60, 3, true, 1, 0), (row.EstimatedMinutes, row.YieldWeight, row.IsActive, row.OutcomeCount, row.PendingLinkCount));
        Assert.Equal(16, row.Version!.Length);
        Assert.Equal(100, active.PageSize);
        var outcomes = await f.Service.ChildrenAsync("outcomes", null, "ilk KAZANIM", 1, new CurriculumChildrenFilter(ExamCode: "LGS", LessonId: topic.LessonId, Active: true), default, all: true);
        Assert.Single(outcomes.Items);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.ChildrenAsync("topics", null, null, 1, new CurriculumChildrenFilter(PageSize: 201), default));
    }

    [LocalPostgresFact]
    public async Task BulkStatusActivatesParentsThenChildren_SkipsWithTurkishReasons_AndAuditsEveryChange()
    {
        await using var f = await Pg.Create();
        var lesson = StudyCatalogLesson.Create("km3", "l", "Tarih (AGS)", null, "LGS");
        var unit = StudyCatalogUnit.Create("km3", "u", lesson.Id, "Ünite", 1);
        var root = StudyCatalogTopic.Create("km3", "t", lesson.Id, unit.Id, "Konu", null, 1);
        var sub = StudyCatalogTopic.Create("km3", "s", lesson.Id, unit.Id, "Alt konu", root.Id, 1);
        var edition = OfficialCurriculumEdition.Create("km7", "LGS", null, "Tarih (AGS)", 2026, 2026, "https://meb.gov.tr/p", new string('b', 64), "https://meb.gov.tr/s");
        var group = OfficialCurriculumTopic.Create(edition.Id, "T.1", "Grup", 1, null);
        var outcome = StudyCatalogOutcome.Create(edition.Id, group.Id, "T.1.1", "Kazanım", "", 1, 1); outcome.Archive(true);
        f.Db.AddRange(lesson, unit, root, sub, edition, group, outcome, StudyCatalogTopicOutcome.Create(sub.Id, outcome.Id, "Kanıt metni"));
        var orphanUnit = StudyCatalogUnit.Create("km3", "u2", lesson.Id, "Diğer ünite", 2);
        var otherLesson = StudyCatalogLesson.Create("km3", "l2", "Pasif ders", null, null);
        var lonely = StudyCatalogUnit.Create("km3", "u3", otherLesson.Id, "Pasif dersin ünitesi", 1);
        f.Db.AddRange(orphanUnit, otherLesson, lonely); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var skipped = await Tools(f).BulkStatusAsync(new("units", [lonely.Id], true, false, "Üniteyi etkinleştir"), default);
        Assert.Equal(0, skipped.Changed); Assert.Contains("Üst kayıt pasif", Assert.Single(skipped.Skipped).Reason);

        var result = await Tools(f).BulkStatusAsync(new("lessons", [lesson.Id, Guid.NewGuid()], true, true, "KM3 dersini yayına al"), default);
        Assert.Equal(6, result.Changed); // lesson, 2 units, 2 topics, 1 outcome
        Assert.Equal("Kayıt bulunamadı.", Assert.Single(result.Skipped).Reason);
        Assert.True((await f.Db.StudyCatalogTopics.SingleAsync(x => x.Id == sub.Id)).IsActive);
        Assert.False((await f.Db.Set<StudyCatalogOutcome>().SingleAsync()).IsArchived);
        Assert.Equal(6, await f.Db.AdminAuditRecords.CountAsync(x => x.Action == "CurriculumBulkStatus"));

        var refused = await Tools(f).BulkStatusAsync(new("units", [unit.Id], false, false, "Üniteyi kapat"), default);
        Assert.Contains("Etkin alt kayıtları var", Assert.Single(refused.Skipped).Reason);
        var off = await Tools(f).BulkStatusAsync(new("units", [unit.Id], false, true, "Üniteyi alt kayıtlarıyla kapat"), default);
        Assert.Equal(4, off.Changed); // sub, root, unit, then the outcome no active topic uses
        Assert.True((await f.Db.Set<StudyCatalogOutcome>().SingleAsync()).IsArchived);
        await Assert.ThrowsAsync<ArgumentException>(() => Tools(f).BulkStatusAsync(new("units", [unit.Id], true, false, "kısa"), default));
        await Assert.ThrowsAsync<ArgumentException>(() => Tools(f).BulkStatusAsync(new("units", Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToArray(), true, false, "Çok fazla kayıt"), default));
        f.Data.Scope.Global = false;
        await Assert.ThrowsAsync<BusinessRuleException>(() => Tools(f).BulkStatusAsync(new("units", [unit.Id], true, false, "Yetkisiz deneme"), default));
    }

    [LocalPostgresFact]
    public async Task ReviewQueueListsFiltersAndBulkDecides_RejectedLinkDoesNotBlockTopicDeletion_UsedLinkCannotBeDeleted()
    {
        await using var f = await Pg.Create(); var topic = await f.Data.Seed();
        var outcome = await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var spare = StudyCatalogTopic.Create("approved-catalog", "spare", topic.LessonId, topic.UnitId, "TÜREV boş konu", null, 3); spare.SetActive(true);
        f.Db.Add(spare); f.Db.Add(StudyCatalogTopicOutcome.Create(spare.Id, outcome.Id, "Önerilen eşleştirme")); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var pending = await Tools(f).MappingsAsync("pending", "LGS", topic.LessonId, "approved-catalog", "türev", 1, 50, default);
        var row = Assert.Single(pending.Items);
        Assert.Equal("Matematik (LGS) › Sayılar › TÜREV boş konu", row.TopicPath);
        Assert.Equal(("pending", "M.8.1.1.1"), (row.Status, row.OutcomeCode));
        Assert.Equal(2, (await Tools(f).MappingsAsync("all", null, null, null, null, 1, 50, default)).TotalCount);
        Assert.Equal(["approved-catalog"], await Tools(f).MappingSourcesAsync(default));

        var decided = await Tools(f).BulkReviewAsync(new([new(spare.Id, outcome.Id), new(topic.Id, outcome.Id)], "approve", "Kapsam incelendi"), default);
        Assert.Equal(1, decided.Changed); Assert.Equal("Bağlantı zaten onaylı.", Assert.Single(decided.Skipped).Reason);
        var rejected = await Tools(f).BulkReviewAsync(new([new(spare.Id, outcome.Id)], "reject", "Konu kapsamı dışında"), default);
        Assert.Equal(1, rejected.Changed);
        Assert.Equal("rejected", Assert.Single((await Tools(f).MappingsAsync("rejected", null, null, null, null, 1, 50, default)).Items).Status);
        Assert.Equal("Konu kapsamı dışında", System.Text.Json.JsonDocument.Parse((await f.Db.AdminAuditRecords.OrderBy(x => x.OccurredAt).LastAsync()).ChangedFieldsJson!).RootElement.GetProperty("reason").GetString());

        var doc = await f.Service.GetAsync("topics", spare.Id, default);
        Assert.True(doc.CanDelete); Assert.Equal(0, doc.OutcomeCount);
        await f.Service.DeleteAsync("topics", spare.Id, new(doc.Fingerprint, "Boş konuyu sil", spare.Id), default);
        Assert.Single(await f.Db.Set<StudyCatalogTopicOutcome>().ToArrayAsync());

        var used = await Assert.ThrowsAsync<BusinessRuleException>(() => Tools(f).DeleteMappingAsync(topic.Id, outcome.Id, new("Bağlantıyı kaldır"), default));
        Assert.Contains("öğrenci çalışmasında", used.Message);
        var topics = await Tools(f).OutcomeTopicsAsync(outcome.Id, default);
        Assert.Equal(topic.Id, Assert.Single(topics).TopicId);
        await Assert.ThrowsAsync<ArgumentException>(() => Tools(f).BulkReviewAsync(new([new(topic.Id, outcome.Id)], "sil", "Geçersiz karar"), default));
    }

    [LocalPostgresFact]
    public async Task UnusedLinkCanBeDeleted_AndYksScopesAndOutcomeSourcesAreReplacedWithAudit()
    {
        await using var f = await Pg.Create(); var topic = await f.Data.Seed();
        var outcome = await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var other = StudyCatalogTopic.Create("test", "other", topic.LessonId, topic.UnitId, "Diğer", null, 2);
        f.Db.Add(other); f.Db.Add(StudyCatalogTopicOutcome.Create(other.Id, outcome.Id, "Taslak")); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        await Tools(f).DeleteMappingAsync(other.Id, outcome.Id, new("Yanlış eşleştirme"), default);
        Assert.Single(await f.Db.Set<StudyCatalogTopicOutcome>().ToArrayAsync());
        Assert.Contains(await f.Db.AdminAuditRecords.Select(x => x.Action).ToArrayAsync(), x => x == "OutcomeMappingDelete");

        Assert.Contains(await Tools(f).TestCodesAsync(default), x => x.Code == "AYT.EDESOS1" && x.Label.StartsWith("AYT"));
        var saved = await Tools(f).SaveYksScopesAsync(topic.Id, new([new("SÖZ", "AYT.SOS2", "AYT", null, [outcome.Id]), new("TYT", "TYT.MAT", "TYT", null, null)], "YKS kapsamını tanımla"), default);
        Assert.Equal(2, saved.Count); Assert.Contains(saved, x => x.ScoreType == "SOZ" && x.ExcludedOutcomeIds!.Single() == outcome.Id);
        saved = await Tools(f).SaveYksScopesAsync(topic.Id, new([new("TYT", "TYT.MAT", "TYT", null, [])], "Sözel kapsamı kaldır"), default);
        Assert.Equal("TYT.MAT", Assert.Single(await Tools(f).YksScopesAsync(topic.Id, default)).TestCode);
        await Assert.ThrowsAsync<ArgumentException>(() => Tools(f).SaveYksScopesAsync(topic.Id, new([new("XYZ", "TYT.MAT", "TYT", null, null)], "Hatalı puan türü"), default));
        await Assert.ThrowsAsync<ArgumentException>(() => Tools(f).SaveYksScopesAsync(topic.Id, new([new("SAY", "TYT.UYDURMA", "TYT", null, null)], "Bilinmeyen test"), default));
        await Assert.ThrowsAsync<ArgumentException>(() => Tools(f).SaveYksScopesAsync(topic.Id, new([new("TYT", "AYT.MAT", "AYT", null, null)], "Uyumsuz oturum"), default));

        var source = await Tools(f).SaveOutcomeSourceAsync(outcome.Id, new("https://mufredat.meb.gov.tr/matematik.pdf", [new("meb-2024", "M.8.1.1.1")], "Kaynak adresi ekle"), default);
        Assert.Equal("https://mufredat.meb.gov.tr/matematik.pdf", source.SourceUrl);
        var node = await f.Service.GetAsync("outcomes", outcome.Id, default);
        Assert.Equal(("https://mufredat.meb.gov.tr/matematik.pdf", "meb-2024"), (node.SourceUrl, node.SourceIds!.Single().Source));
        await Assert.ThrowsAsync<ArgumentException>(() => Tools(f).SaveOutcomeSourceAsync(outcome.Id, new("http://güvensiz", [], "Güvensiz adres"), default));
        var edition = await f.Db.Set<OfficialCurriculumEdition>().SingleAsync(); var group = await f.Db.Set<OfficialCurriculumTopic>().SingleAsync();
        var second = StudyCatalogOutcome.Create(edition.Id, group.Id, "M.8.1.1.9", "İkinci", "", 1, 9); f.Db.Add(second); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Tools(f).SaveOutcomeSourceAsync(second.Id, new(null, [new("meb-2024", "M.8.1.1.1")], "Çakışan kimlik"), default));
        source = await Tools(f).SaveOutcomeSourceAsync(outcome.Id, new(null, [], "Kaynağı temizle"), default);
        Assert.Null(source.SourceUrl); Assert.Empty(source.SourceIds);
    }

    [LocalPostgresFact]
    public async Task QualityReportCountsByLessonAndExports_AndInlinePatchDetectsConflicts()
    {
        await using var f = await Pg.Create(); var topic = await f.Data.Seed();
        var lesson = await f.Db.StudyCatalogLessons.SingleAsync();
        var bare = StudyCatalogTopic.Create("test", "bare", lesson.Id, topic.UnitId, "Süresiz konu", null, 2); bare.SetActive(true);
        f.Db.Add(bare); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var report = await Tools(f).QualityAsync("LGS", null, null, 1, 50, default);
        Assert.Equal(1, report.Counts["noMinutes"]); Assert.Equal(1, report.Counts["noOutcomes"]); Assert.Equal(0, report.Counts["noYksScope"]);
        Assert.Equal(1, Assert.Single(report.ByLesson).Counts["noMinutes"]);
        Assert.All(report.Items, x => Assert.Equal(bare.Id, x.TopicId));
        var csv = System.Text.Encoding.UTF8.GetString(await Tools(f).QualityCsvAsync(null, lesson.Id, "noMinutes", default));
        Assert.Contains("Süresi yok;Matematik (LGS) › Sayılar › Süresiz konu", csv);
        await Assert.ThrowsAsync<ArgumentException>(() => Tools(f).QualityAsync(null, null, "bilinmeyen", 1, 50, default));

        var row = Assert.Single((await f.Service.ChildrenAsync("topics", null, "Süresiz", 1, new CurriculumChildrenFilter(), default, all: true)).Items);
        var patched = await Tools(f).PatchTopicsAsync(new([new(bare.Id, row.Version, 40, 5), new(topic.Id, "eski-surum", 30, null)], "Süreleri gir"), default);
        Assert.Equal(1, patched.Changed); Assert.Equal(topic.Id, Assert.Single(patched.Conflicts).Id);
        var saved = await f.Db.StudyCatalogTopics.AsNoTracking().SingleAsync(x => x.Id == bare.Id);
        Assert.Equal((40, 5), (saved.EstimatedMinutes, saved.YieldWeight));
        var stale = await Tools(f).PatchTopicsAsync(new([new(bare.Id, row.Version, 50, null)], "Eski satırla kaydet"), default);
        Assert.Equal(0, stale.Changed); Assert.Single(stale.Conflicts);
        await Assert.ThrowsAsync<ArgumentException>(() => Tools(f).PatchTopicsAsync(new([new(bare.Id, row.Version, null, 9)], "Geçersiz ağırlık"), default));
    }

    [LocalPostgresFact]
    public async Task UnusedTopicAndUnitMoveInPlace_UsedTopicStillCopies()
    {
        await using var f = await Pg.Create(); var topic = await f.Data.Seed();
        var free = StudyCatalogTopic.Create("test", "free", topic.LessonId, topic.UnitId, "Serbest konu", null, 2); free.SetActive(true);
        var child = StudyCatalogTopic.Create("test", "free-child", topic.LessonId, topic.UnitId, "Serbest alt konu", free.Id, 1);
        var target = StudyCatalogUnit.Create("test", "target", topic.LessonId, "Hedef ünite", 2); target.SetActive(true);
        f.Db.AddRange(free, child, target); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var doc = await f.Service.GetAsync("topics", free.Id, default);
        var moved = await f.Service.MoveAsync("topics", free.Id, new(target.Id, doc.Fingerprint, "Konuyu doğru üniteye taşı"), default);
        Assert.Equal(free.Id, moved.Id);
        Assert.Equal(target.Id, (await f.Db.StudyCatalogTopics.AsNoTracking().SingleAsync(x => x.Id == child.Id)).UnitId);
        Assert.Equal(3, await f.Db.StudyCatalogTopics.CountAsync());
        var used = await f.Service.GetAsync("topics", topic.Id, default);
        var copied = await f.Service.MoveAsync("topics", topic.Id, new(target.Id, used.Fingerprint, "Kullanılan konuyu taşı"), default);
        Assert.NotEqual(topic.Id, copied.Id);
        var spareLesson = StudyCatalogLesson.Create("test", "same", "Matematik", 8, "LGS"); spareLesson.SetActive(true); f.Db.Add(spareLesson); await f.Db.SaveChangesAsync();
        var emptyUnit = StudyCatalogUnit.Create("test", "empty", topic.LessonId, "Boş ünite", 9); f.Db.Add(emptyUnit); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var unitDoc = await f.Service.GetAsync("units", emptyUnit.Id, default);
        Assert.Equal(emptyUnit.Id, (await f.Service.MoveAsync("units", emptyUnit.Id, new(spareLesson.Id, unitDoc.Fingerprint, "Üniteyi taşı"), default)).Id);
    }

    [LocalPostgresFact]
    public async Task UniversityProgramsAreSearchedEditedAndDeletedOnlyWhenUnused()
    {
        await using var f = await Pg.Create();
        var service = new CoachingUniversityProgramAdminService(f.Db, f.Data.Scope, f.Data.User);
        var official = TargetUniversityProgram.Create("101110001", "İSTANBUL ÜNİVERSİTESİ", "Tıp", "TIP FAKÜLTESİ", "Bachelor", "SAY"); official.SetActive(true);
        f.Db.Add(official); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        Assert.Single((await service.ListAsync("istanbul", "SAY", null, null, true, 1, 25, default)).Items);
        Assert.Single((await service.ListAsync("101110001", null, null, null, null, 1, 25, default)).Items);
        Assert.Empty((await service.ListAsync(null, "EA", null, null, null, 1, 25, default)).Items);
        var created = await service.CreateAsync(new("Elle eklenen program", Name: "Hukuk", UniversityName: "Örnek Üniversitesi", ScoreType: "EA", MinimumScore: 400.5m, Rank: 12000, Year: 2025, IsActive: true), default);
        Assert.Equal(("admin-manual", "EA", 2025), (created.Data.Source, created.Data.ScoreType, created.Data.Year));
        var doc = await service.GetAsync(official.Id, default);
        var updated = await service.UpdateAsync(official.Id, new("Taban puanı düzelt", doc.Fingerprint, "Tıp", "TIP FAKÜLTESİ", 480.12345m, 500m, 1500, 300, 300, 2025), default);
        Assert.Equal((480.12345m, 1500), (updated.Data.MinimumScore, updated.Data.Rank));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(official.Id, new("Eski formu kaydet", doc.Fingerprint, "Tıp"), default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(official.Id, new("Ters puan aralığı", updated.Fingerprint, "Tıp", MinimumScore: 500, MaximumScore: 400), default));
        var off = await service.SetActiveAsync(official.Id, new(updated.Fingerprint, false, "Program kapandı"), default);
        Assert.False(off.Data.IsActive);
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Hedef", GoalCategory.ExamPreparation); goal.SetCatalogTarget(official.Id, null); f.Db.Add(goal); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        off = await service.GetAsync(official.Id, default);
        Assert.Equal(1, off.GoalCount);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(official.Id, new(off.Fingerprint, "Kullanılan programı sil"), default));
        await service.DeleteAsync(created.Data.Id, new(created.Fingerprint, "Yanlış eklenen programı sil"), default);
        Assert.Equal(4, await f.Db.AdminAuditRecords.CountAsync(x => x.ResourceType == "UniversityProgram"));
    }

    [LocalPostgresFact]
    public async Task ScoreModelHistoryKeepsEveryUploadAndRestoresOrDeletesWithAudit()
    {
        await using var f = await Pg.Create();
        var admin = Guid.NewGuid(); var service = new CoachingScoreEstimateService(f.Db, null!, TimeProvider.System);
        string Csv(int baseScore) => CoachingScoreEstimateService.Header + $"\ntaban;LGS;;;{baseScore};;\n" +
            string.Join("\n", new[] { "TUR", "MAT", "FEN", "INK", "DIN", "ENG" }.Select(x => $"katsayi;LGS;LGS;{x};3;;")) + "\nsiralama;LGS;;;500;1;\nsiralama;LGS;;;400;80000;";
        foreach (var score in new[] { 200, 210 })
        {
            var preview = await service.PreviewAsync(admin, new(2025, Csv(score), null), default);
            await service.ApplyAsync(admin, new(2025, Csv(score), preview.Fingerprint, $"{score} tabanlı dosya"), default);
        }
        var history = await service.HistoryAsync(2025, default);
        Assert.Equal(2, history.Count);
        Assert.Equal((210m, true), (history[0].BaseScore, history[0].IsCurrent));
        Assert.Equal(64, history[0].FileSha256!.Length);
        var restored = await service.RestoreAsync(admin, history[1].Id, "Eski katsayılar doğruydu", default);
        Assert.Equal(200m, restored.BaseScore);
        Assert.True((await service.HistoryAsync(2025, default)).First().IsCurrent);
        await service.DeleteAsync(admin, 2025, "LGS", "Yanlış yıl yüklendi", default);
        Assert.Empty(await f.Db.Set<CoachingScoreModel>().ToArrayAsync());
        Assert.Equal(4, (await service.HistoryAsync(null, default)).Count);
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteAsync(admin, 2025, "LGS", "kısa", default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(admin, 2025, "LGS", "Olmayan veriyi sil", default));
        Assert.Equal(new[] { "ScoreModelApply", "ScoreModelApply", "ScoreModelDelete", "ScoreModelRestore" },
            (await f.Db.AdminAuditRecords.Select(x => x.Action!).ToArrayAsync()).Order().ToArray());
    }

    [Fact]
    public void NewEndpointsKeepMfaPermissionAndGlobalAdminChecks()
    {
        foreach (var type in new[] { typeof(CoachingCurriculumToolsAdminController), typeof(CoachingOutcomeLinksAdminController),
                     typeof(CoachingYksScopeAdminController), typeof(CoachingUniversityProgramsAdminController), typeof(CoachingScoreModelsAdminController) })
        {
            Assert.NotEmpty(type.GetCustomAttributes<AuthorizeAttribute>());
            Assert.NotNull(type.GetCustomAttribute<MfaCategoryAttribute>());
            Assert.NotNull(type.GetCustomAttribute<HasPermissionAttribute>());
            foreach (var method in type.GetMethods().Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any(a => !a.HttpMethods.Contains("GET"))))
                Assert.Equal(PlatformPermissions.Coaching.ContentManage,
                    (method.GetCustomAttribute<HasPermissionAttribute>() ?? type.GetCustomAttribute<HasPermissionAttribute>())!.Permission);
        }
        Assert.Contains(typeof(CoachingScoreModelsAdminController).GetCustomAttributes<AuthorizeAttribute>(), x => x.Roles == "SystemAdmin");
    }

    [Fact]
    public async Task ControllersMapErrorsToTurkishResponses()
    {
        await using var f = new CurriculumManagementTests.Fixture(); await f.Seed();
        var controller = new CoachingOutcomeLinksAdminController(new CoachingCatalogAdminToolsService(f.Db, f.Scope, f.User));
        Assert.IsType<OkObjectResult>(await controller.Mappings(ct: default));
        Assert.IsType<BadRequestObjectResult>(await controller.Mappings("bilinmeyen", ct: default));
        Assert.IsType<NotFoundObjectResult>(await controller.Topics(Guid.NewGuid(), default));
        f.Scope.Global = false;
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.Mappings(ct: default)).StatusCode);
    }
}
