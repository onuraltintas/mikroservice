using System.Reflection.Metadata;
using System.Security.Cryptography;
var root = Path.GetFullPath(args[0]); var baseline = Path.GetFullPath(args[1]); var failures = 0;
var allowed = new HashSet<string>(StringComparer.Ordinal) {
"services/coaching-service/Coaching.API/Controllers/ManualStudyPlansController.cs",
"services/coaching-service/Coaching.API/Controllers/CoachingStaffStudyController.cs",
"services/coaching-service/Coaching.Application/StudyPlanning/IManualStudyPlanService.cs",
"services/coaching-service/Coaching.Infrastructure/StudyPlanning/CoachingManualStudyPlanService.cs"
};
foreach (var (folder, prefix, scope) in new[] { ("Coaching", "Coaching", "services/coaching-service"), ("Coaching", "EduPlatform.Shared", "shared") })
foreach (var project in prefix == "EduPlatform.Shared" ? new[] { "EduPlatform.Shared.Contracts" } : new[] { prefix + ".API", prefix + ".Application", prefix + ".Infrastructure", prefix + ".Domain" }) {
 using var input = File.OpenRead(Path.Combine(baseline, folder, project + ".pdb"));
 using var pdb = MetadataReaderProvider.FromPortablePdbStream(input); var reader = pdb.GetMetadataReader(); var count = 0; var originals = new HashSet<string>();
 foreach (var handle in reader.Documents) {
  var doc = reader.GetDocument(handle); var name = reader.GetString(doc.Name).Replace('\\','/');
  if (name.Contains("/obj/") || !name.EndsWith(".cs")) continue;
  var index = name.IndexOf(project + "/", StringComparison.Ordinal); if (index < 0) throw new Exception("Unmapped source: " + name);
  var relative = scope + "/" + name[index..]; originals.Add(relative); var current = Path.Combine(root, relative); count++;
  var hash = reader.GetBlobBytes(doc.Hash);
  var equal = File.Exists(current) && (SHA256.HashData(File.ReadAllBytes(current)).SequenceEqual(hash)
   || SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(current).Replace("\r\n", "\n"))).SequenceEqual(hash));
  if (!equal) { Console.WriteLine((allowed.Contains(relative) ? "Approved change: " : "Unexpected change: ") + relative); if (!allowed.Contains(relative)) failures++; }
 }
 Console.WriteLine($"{project}: checked {count} source files.");
 if (args.Length > 2) {
  using var nextFile = File.OpenRead(Path.Combine(args[2], folder == "Notification" ? "notification" : "coaching", project + ".pdb"));
  using var next = MetadataReaderProvider.FromPortablePdbStream(nextFile); var nextReader = next.GetMetadataReader();
  foreach (var handle in nextReader.Documents) {
   var doc = nextReader.GetDocument(handle); var name = nextReader.GetString(doc.Name).Replace('\\','/');
   if (name.Contains("/obj/") || !name.EndsWith(".cs")) continue;
   var index = name.IndexOf(project + "/", StringComparison.Ordinal); if (index < 0) throw new Exception("Unmapped published source");
   var relative = scope + "/" + name[index..];
   if (!originals.Remove(relative)) { var permitted = allowed.Contains(relative);
    Console.WriteLine((permitted ? "Approved addition: " : "Unexpected addition: ") + relative); if (!permitted) failures++; }
  }
  if (originals.Count > 0) { failures += originals.Count; Console.WriteLine("Unexpected removed sources: " + string.Join(", ", originals)); }
 }
}
Console.WriteLine($"Unexpected differences: {failures}"); return failures == 0 ? 0 : 1;
