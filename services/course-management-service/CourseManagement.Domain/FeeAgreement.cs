namespace CourseManagement.Domain;

public enum FeeModel { Term = 1, MonthlyFixed = 2, PerLesson = 3, LessonPackage = 4 }
public enum LessonBillingUnit { Lesson = 1, Hour = 2 }

public sealed class FeeAgreement
{
    private FeeAgreement() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public FeeModel Model { get; private set; }
    public decimal Amount { get; private set; }
    public LessonBillingUnit? BillingUnit { get; private set; }
    public int? PackageSessions { get; private set; }
    public DateTimeOffset? PackageExpiresAt { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public static FeeAgreement Create(Guid id, Guid workspaceId, Guid studentId, Guid courseId, Guid createdByUserId,
        FeeModel model, decimal amount, LessonBillingUnit? billingUnit, int? packageSessions,
        DateTimeOffset? packageExpiresAt, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || studentId == Guid.Empty || courseId == Guid.Empty
            || createdByUserId == Guid.Empty || !Enum.IsDefined(model))
            throw new ArgumentException("Fee agreement identifiers and model are required.");
        if (amount is <= 0 or > 9_999_999_999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive and use at most two decimal places.");
        if (model == FeeModel.PerLesson && billingUnit is not (LessonBillingUnit.Lesson or LessonBillingUnit.Hour))
            throw new ArgumentException("Per-lesson agreements require a billing unit.", nameof(billingUnit));
        if (model != FeeModel.PerLesson && billingUnit is not null)
            throw new ArgumentException("A billing unit only applies to per-lesson agreements.", nameof(billingUnit));
        if (model == FeeModel.LessonPackage && packageSessions is not > 0)
            throw new ArgumentException("A package must include at least one lesson.", nameof(packageSessions));
        if (model != FeeModel.LessonPackage && (packageSessions is not null || packageExpiresAt is not null))
            throw new ArgumentException("Package settings only apply to lesson packages.", nameof(packageSessions));

        return new FeeAgreement
        {
            Id = id,
            WorkspaceId = workspaceId,
            StudentId = studentId,
            CourseId = courseId,
            CreatedByUserId = createdByUserId,
            Model = model,
            Amount = amount,
            BillingUnit = billingUnit,
            PackageSessions = packageSessions,
            PackageExpiresAt = packageExpiresAt?.ToUniversalTime(),
            IsActive = true,
            CreatedAt = now.ToUniversalTime()
        };
    }

    public void Close(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        ClosedAt = now.ToUniversalTime();
    }
}
