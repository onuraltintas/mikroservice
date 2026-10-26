using EduPlatform.Shared.Kernel.Results;
using Identity.Application.DTOs.Institutions;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetAllUsers;
using Identity.Application.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using FluentValidation;
using MediatR;

namespace Identity.Application.Queries.GetInstitutions;

public sealed record GetInstitutionsQuery(
    int PageNumber = 1,
    int PageSize = 25,
    string? SearchTerm = null,
    bool? IsActive = null) : IRequest<Result<PagedList<InstitutionDto>>>;

public sealed class GetInstitutionsQueryValidator : AbstractValidator<GetInstitutionsQuery>
{
    public GetInstitutionsQueryValidator()
    {
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.SearchTerm).MaximumLength(200).When(query => query.SearchTerm is not null);
    }
}

public sealed class GetInstitutionsQueryHandler
    : IRequestHandler<GetInstitutionsQuery, Result<PagedList<InstitutionDto>>>
{
    private readonly IInstitutionRepository _repository;
    private readonly InstitutionManagementAuthorization _authorization;

    public GetInstitutionsQueryHandler(
        IInstitutionRepository repository,
        InstitutionManagementAuthorization authorization)
    {
        _repository = repository;
        _authorization = authorization;
    }

    public async Task<Result<PagedList<InstitutionDto>>> Handle(
        GetInstitutionsQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _authorization.ResolveScopeAsync(cancellationToken);
        if (scope.IsFailure)
        {
            return Result.Failure<PagedList<InstitutionDto>>(scope.Error);
        }

        var result = await _repository.GetAllAsync(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.IsActive,
            scope.Value.InstitutionId,
            cancellationToken);

        return Result.Success(result);
    }
}

public sealed record GetInstitutionByIdQuery(Guid Id) : IRequest<Result<InstitutionDto>>;

public sealed class GetInstitutionByIdQueryHandler
    : IRequestHandler<GetInstitutionByIdQuery, Result<InstitutionDto>>
{
    private readonly IInstitutionRepository _repository;
    private readonly InstitutionManagementAuthorization _authorization;

    public GetInstitutionByIdQueryHandler(
        IInstitutionRepository repository,
        InstitutionManagementAuthorization authorization)
    {
        _repository = repository;
        _authorization = authorization;
    }

    public async Task<Result<InstitutionDto>> Handle(
        GetInstitutionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var access = await _authorization.EnsureInstitutionAccessAsync(request.Id, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<InstitutionDto>(access.Error);
        }

        var institution = await _repository.GetDtoByIdAsync(request.Id, cancellationToken);
        return institution is null
            ? Result.Failure<InstitutionDto>(new Error("Institution.NotFound", "Kurum bulunamadı."))
            : Result.Success(institution);
    }
}
