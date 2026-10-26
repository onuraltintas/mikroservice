using System.Reflection;
using System.Runtime.Loader;

var directory = args[0];
AssemblyLoadContext.Default.Resolving += (_, name) =>
    Assembly.LoadFrom(Path.Combine(directory, name.Name + ".dll"));
var application = Assembly.LoadFrom(Path.Combine(directory, "Identity.Application.dll"));
var domain = Assembly.LoadFrom(Path.Combine(directory, "Identity.Domain.dll"));
var productType = domain.GetType("Identity.Domain.Enums.PlatformProduct", throwOnError: true)!;
var policy = application.GetType("Identity.Application.LegalPages.RegistrationLegalConsentPolicy", throwOnError: true)!;
var method = policy.GetMethod("GoogleRequiresAcceptance")!;
var speedReading = (bool)method.Invoke(null, [Enum.Parse(productType, "SpeedReading")])!;
var coaching = (bool)method.Invoke(null, [Enum.Parse(productType, "Coaching")])!;
Console.WriteLine($"Google acceptance gate: SpeedReading={speedReading}; Coaching={coaching}");
return !speedReading && coaching ? 0 : 1;
