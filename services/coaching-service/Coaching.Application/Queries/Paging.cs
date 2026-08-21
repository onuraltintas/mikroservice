using FluentValidation;

namespace Coaching.Application.Queries;

public static class CoachingPaging
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageNumber = 1_000;
    public const int MaxPageSize = 100;

    public static int GetSkip(int pageNumber, int pageSize) =>
        checked((pageNumber - 1) * pageSize);
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public abstract class PagedQueryValidator<TQuery> : AbstractValidator<TQuery>
{
    protected void AddPagingRules(
        Func<TQuery, int> pageNumber,
        Func<TQuery, int> pageSize)
    {
        RuleFor(query => pageNumber(query))
            .InclusiveBetween(CoachingPaging.DefaultPageNumber, CoachingPaging.MaxPageNumber)
            .WithMessage($"Page number must be between {CoachingPaging.DefaultPageNumber} and {CoachingPaging.MaxPageNumber}.");
        RuleFor(query => pageSize(query))
            .InclusiveBetween(1, CoachingPaging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {CoachingPaging.MaxPageSize}.");
    }
}
