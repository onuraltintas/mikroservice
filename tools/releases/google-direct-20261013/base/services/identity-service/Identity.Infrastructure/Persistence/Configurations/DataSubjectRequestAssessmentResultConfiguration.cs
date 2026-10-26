using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class DataSubjectRequestAssessmentResultConfiguration : IEntityTypeConfiguration<DataSubjectRequestAssessmentResult>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequestAssessmentResult> builder)
    {
        builder.ToTable("DataSubjectRequestAssessmentResults");
        builder.HasKey(result => result.Id);
        builder.Property(result => result.ServiceName).HasMaxLength(100).IsRequired();
        var converter = new ValueConverter<Dictionary<string, int>, string>(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<Dictionary<string, int>>(value, (JsonSerializerOptions?)null) ?? new());
        var comparer = new ValueComparer<Dictionary<string, int>>(
            (left, right) => DictionariesEqual(left, right),
            value => DictionaryHashCode(value),
            value => new Dictionary<string, int>(value));

        builder.Property(result => result.RecordCounts)
            .HasConversion(converter)
            .Metadata.SetValueComparer(comparer);
        builder.Property(result => result.RecordCounts).HasColumnType("jsonb");
        builder.Ignore(result => result.TotalRecordCount);
        builder.HasIndex(result => new { result.RequestId, result.ServiceName }).IsUnique();
        builder.HasIndex(result => new { result.SubjectUserId, result.AssessedAt });
    }

    private static bool DictionariesEqual(
        IReadOnlyDictionary<string, int>? left,
        IReadOnlyDictionary<string, int>? right) =>
        ReferenceEquals(left, right) ||
        left is not null && right is not null && left.Count == right.Count &&
        left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static int DictionaryHashCode(IReadOnlyDictionary<string, int> value)
    {
        var hash = new HashCode();
        foreach (var pair in value.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }
}
