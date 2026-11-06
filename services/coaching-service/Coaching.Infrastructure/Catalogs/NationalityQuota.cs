using System.Linq.Expressions;
using Coaching.Domain.Entities;

namespace Coaching.Infrastructure.Catalogs;

/// <summary>
/// E2 (#17, live test 06.11.2026): ÖSYM lists places reserved for citizens of one country as programmes of their own, marked in
/// the name, e.g. "Ergoterapi (KKTC Uyruklu)" (only citizens of the TRNC may choose them). The 2026 catalog has 455 such rows,
/// all "(KKTC Uyruklu)"; the marker is the word "Uyruklu" (nationality), so a future "(… Uyruklu)" quota is covered too. Other
/// markers are not nationality quotas and stay: "(M.T.O.K.)" (vocational school graduates), "(Burslu)", "(%50 İndirimli)",
/// "(Kız)"/"(Erkek)", "(… Bakanlığı Adına)", "(UOLP-…)". The lists a student or teacher chooses a target from leave these rows
/// out; the catalog keeps them (administration, existing targets).
/// </summary>
public static class NationalityQuota
{
    public const string Marker = "UYRUKLU";

    /// <summary>The programme is open to every candidate (no nationality-restricted quota in its name).</summary>
    public static readonly Expression<Func<TargetUniversityProgram, bool>> OpenToEveryone = x => !x.Name.ToUpper().Contains(Marker);
}
