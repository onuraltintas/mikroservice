namespace Identity.Domain.Enums;

public enum InvitationType
{
    /// <summary>
    /// Kurum, bağımsız öğretmeni kuruma davet eder
    /// </summary>
    TeacherToInstitution = 1,
    
    /// <summary>
    /// Kurum, bağımsız öğrenciyi kuruma davet eder
    /// </summary>
    StudentToInstitution = 2,
    
    /// <summary>
    /// Öğretmen, bağımsız öğrenciyi kendine öğrenci olarak davet eder
    /// </summary>
    StudentToTeacher = 3
}
