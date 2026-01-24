namespace Identity.Domain.Enums;

/// <summary>
/// Kurum türleri
/// </summary>
public enum InstitutionType
{
    School = 1,           // Okul
    PrivateCourse = 2,    // Dershane
    StudyCenter = 3,      // Etüt Merkezi
    OnlinePlatform = 4    // Online Platform
}

/// <summary>
/// Kurum lisans türleri
/// </summary>
public enum LicenseType
{
    Trial = 1,        // 14 gün deneme
    Basic = 2,        // Temel paket
    Premium = 3,      // Gelişmiş paket
    Enterprise = 4    // Kurumsal paket
}

/// <summary>
/// Kullanıcı rolleri
/// </summary>
public enum UserRole
{
    Student = 1,
    Teacher = 2,
    Parent = 3,
    InstitutionAdmin = 4,
    InstitutionOwner = 5,
    SystemAdmin = 6
}

/// <summary>
/// Kurum yöneticisi rolü
/// </summary>
public enum InstitutionAdminRole
{
    Owner = 1,      // Kurum sahibi
    Admin = 2,      // Yönetici
    Manager = 3     // Müdür/Koordinatör
}

/// <summary>
/// Cinsiyet
/// </summary>
public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3,
    PreferNotToSay = 4
}

/// <summary>
/// Öğrenme stili
/// </summary>
public enum LearningStyle
{
    Visual = 1,          // Görsel
    Auditory = 2,        // İşitsel
    Kinesthetic = 3,     // Kinestetik
    ReadingWriting = 4   // Okuma/Yazma
}

/// <summary>
/// Veli ilişkisi
/// </summary>
public enum ParentRelationship
{
    Mother = 1,     // Anne
    Father = 2,     // Baba
    Guardian = 3,   // Vasi
    Other = 4       // Diğer
}
