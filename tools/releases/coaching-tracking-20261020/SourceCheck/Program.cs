using System.Reflection.Metadata;
using System.Security.Cryptography;
using var input = File.OpenRead(args[0]);
using var pdb = MetadataReaderProvider.FromPortablePdbStream(input);
var reader = pdb.GetMetadataReader(); var count = 0; var mismatches = 0;
foreach (var handle in reader.Documents)
{
    var document = reader.GetDocument(handle); var name = reader.GetString(document.Name);
    if (name.Contains("/obj/") || name.Contains("\\obj\\") || !name.EndsWith(".cs")) continue;
    count++;
    if (!File.Exists(name) || !SHA256.HashData(File.ReadAllBytes(name)).SequenceEqual(reader.GetBlobBytes(document.Hash)))
    { Console.WriteLine("Source differs: " + name); mismatches++; }
}
Console.WriteLine($"Compared {count} baseline API source files; mismatches: {mismatches}.");
return mismatches == 0 ? 0 : 1;
