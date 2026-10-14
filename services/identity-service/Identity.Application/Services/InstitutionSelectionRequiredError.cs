using EduPlatform.Shared.Kernel.Results;

namespace Identity.Application.Services;

public sealed record InstitutionEnrollmentChoice(Guid Id, string Name, string Role);

public sealed record InstitutionSelectionRequiredError(IReadOnlyList<InstitutionEnrollmentChoice> Institutions)
    : Error("Auth.InstitutionSelectionRequired", "Katılacağınız servise bağlanacak kurumu seçin. Diğer kurum bağlantılarınız korunur.");
