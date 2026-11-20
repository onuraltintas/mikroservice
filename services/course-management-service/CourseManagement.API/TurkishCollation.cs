namespace CourseManagement.API;

/// <summary>
/// Names are listed in Turkish alphabetical order (Ç after C, İ after I, Ü after U). PostgreSQL creates this ICU
/// collation itself; the default collation put "Üç" before "İki" (20.11.2026 live test).
/// </summary>
public static class TurkishCollation
{
    public const string Name = "tr-x-icu";
}
