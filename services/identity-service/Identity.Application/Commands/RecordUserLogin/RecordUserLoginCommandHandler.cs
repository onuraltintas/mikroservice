using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.RecordUserLogin;

public class RecordUserLoginCommandHandler : IRequestHandler<RecordUserLoginCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordUserLoginCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RecordUserLoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            return Result.Failure(new Error("User.NotFound", "User not found"));
        }

        user.RecordLogin();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
