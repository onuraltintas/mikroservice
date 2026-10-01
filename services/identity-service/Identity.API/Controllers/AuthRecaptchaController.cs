using Identity.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/auth/captcha-config")]
[AllowAnonymous]
public sealed class AuthRecaptchaController(AuthRecaptchaOptions options) : ControllerBase
{
    [HttpGet]
    public ActionResult<AuthRecaptchaPublicConfiguration> GetConfiguration() =>
        Ok(options.ToPublicConfiguration());
}
