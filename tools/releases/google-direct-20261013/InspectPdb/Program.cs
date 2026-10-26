using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;

using var stream = File.OpenRead(args[0]);
using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
var reader = provider.GetMetadataReader();
const string prefix = "services/identity-service/Identity.Application/";
var checkedFiles = 0;
var mismatches = 0;
foreach (var handle in reader.Documents)
{
    var document = reader.GetDocument(handle);
    var name = reader.GetString(document.Name).Replace('\\', '/');
    var start = name.IndexOf(prefix, StringComparison.Ordinal);
    if (start < 0 || name.Contains("/obj/", StringComparison.Ordinal)) continue;
    checkedFiles++;
    var relative = name[start..];
    var file = Path.Combine(args[1], relative);
    var expected = reader.GetBlobBytes(document.Hash);
    var matches = false;
    if (File.Exists(file))
    {
        var bytes = File.ReadAllBytes(file);
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').Replace("\r\n", "\n");
        var candidates = new List<byte[]> { bytes, Encoding.UTF8.GetBytes(text), Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n")) };
        if (args.Length > 2)
        {
            var workingFile = Path.Combine(args[2], relative);
            if (File.Exists(workingFile))
            {
                var workingBytes = File.ReadAllBytes(workingFile);
                var workingText = Encoding.UTF8.GetString(workingBytes).TrimStart('\uFEFF').Replace("\r\n", "\n");
                if (workingText == text) candidates.Add(workingBytes);
            }
        }
        matches = candidates.Concat(candidates.Select(candidate => new byte[] { 0xef, 0xbb, 0xbf }.Concat(candidate).ToArray()))
            .Any(candidate => (expected.Length == 20 ? SHA1.HashData(candidate) : SHA256.HashData(candidate)).SequenceEqual(expected));
    }
    if (!matches) { Console.WriteLine($"MISMATCH {relative}"); mismatches++; }
}
Console.WriteLine($"Checked {checkedFiles} production Application source files; mismatches: {mismatches}");
return checkedFiles > 0 && mismatches == 0 ? 0 : 1;
