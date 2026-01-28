using System.Threading.Tasks;

namespace Identity.Application.Interfaces;

public record GoogleUser(string Email, string FirstName, string LastName, string PictureUrl, string GoogleId);

public interface IGoogleAuthService
{
    Task<GoogleUser?> VerifyGoogleTokenAsync(string idToken);
}
