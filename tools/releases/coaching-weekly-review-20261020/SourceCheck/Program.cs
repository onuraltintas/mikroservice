using System.Reflection.Metadata;
using System.Security.Cryptography;
var root = Path.GetFullPath(args[0]); var baseline = Path.GetFullPath(args[1]);
var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
 "Coaching.Application/Queries/ExportCoachingData/ExportCoachingDataQuery.cs",
 "Coaching.Infrastructure/DependencyInjection.cs",
 "Coaching.Infrastructure/Data/Migrations/CoachingDbContextModelSnapshot.cs",
 "Coaching.Infrastructure/Privacy/CoachingErasureAssessmentService.cs",
 "Coaching.Infrastructure/Privacy/CoachingErasureExecutionService.cs",
 "Coaching.Infrastructure/Repositories/CoachingDataExportRepository.cs"
};
var failures = 0;
var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
 "Coaching.API/Controllers/WeeklyReviewController.cs", "Coaching.Application/StudyPlanning/WeeklyReviewModels.cs",
 "Coaching.Domain/Entities/WeeklyReview.cs", "Coaching.Infrastructure/StudyPlanning/CoachingWeeklyReviewService.cs",
 "Coaching.Infrastructure/Data/Configurations/WeeklyReviewConfigurations.cs",
 "Coaching.Infrastructure/Data/Migrations/20261020130142_WeeklyReviewsAndDecisions.cs",
 "Coaching.Infrastructure/Data/Migrations/20261020130142_WeeklyReviewsAndDecisions.Designer.cs"
};
foreach (var project in new[] { "Coaching.API", "Coaching.Application", "Coaching.Infrastructure", "Coaching.Domain" }) {
 using var input = File.OpenRead(Path.Combine(baseline, project + ".pdb"));
 using var pdb = MetadataReaderProvider.FromPortablePdbStream(input); var reader = pdb.GetMetadataReader(); var count = 0; var originals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 foreach (var handle in reader.Documents) {
  var doc = reader.GetDocument(handle); var name = reader.GetString(doc.Name).Replace('\\','/');
  if (name.Contains("/obj/") || !name.EndsWith(".cs")) continue;
  var index = name.IndexOf(project + "/", StringComparison.Ordinal); if (index < 0) throw new Exception("Unmapped source: " + name);
  var relative = name[index..]; originals.Add(relative); var current = Path.Combine(root, "services/coaching-service", relative); count++;
  var equal = File.Exists(current) && SHA256.HashData(File.ReadAllBytes(current)).SequenceEqual(reader.GetBlobBytes(doc.Hash));
  if (!equal) { Console.WriteLine((allowed.Contains(relative) ? "Approved change: " : "Unexpected difference: ") + relative); if (!allowed.Contains(relative)) failures++; }
 }
 Console.WriteLine($"{project}: checked {count} source files.");
 if (args.Length > 2) {
  using var nextFile = File.OpenRead(Path.Combine(args[2], project + ".pdb")); using var next = MetadataReaderProvider.FromPortablePdbStream(nextFile); var nextReader = next.GetMetadataReader();
  foreach (var handle in nextReader.Documents) {
   var doc = nextReader.GetDocument(handle); var name = nextReader.GetString(doc.Name).Replace('\\','/');
   if (name.Contains("/obj/") || !name.EndsWith(".cs")) continue;
   var index = name.IndexOf(project + "/", StringComparison.Ordinal); if (index < 0) throw new Exception("Unmapped new source"); var relative = name[index..];
   if (!originals.Remove(relative)) { Console.WriteLine((added.Contains(relative) ? "Approved addition: " : "Unexpected addition: ") + relative); if (!added.Contains(relative)) failures++; }
  }
  if (originals.Count > 0) { failures += originals.Count; Console.WriteLine("Unexpected removed sources: " + string.Join(", ", originals)); }
 }
}
Console.WriteLine($"Unexpected differences: {failures}"); return failures == 0 ? 0 : 1;
