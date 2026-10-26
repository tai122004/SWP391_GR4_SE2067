using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RWPM.Common;
using RWPM.Common.Exceptions;
using RWPM.Models.ViewModels.Auth;
using RWPM.Services.Abstraction;

namespace RWPM.Controllers
{
    public class AuthController : Controller
    {
        private readonly IStringLocalizer _localizer;
        private readonly IAuthService _authService;
        public AuthController(
            IStringLocalizer<ErrorServerDefinition> localizer,
            IAuthService authService)
        {
            _localizer = localizer;
            _authService = authService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                // If already logged in, go to home (or returnUrl if provided)
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM viewModel, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
                return View(viewModel);

            try
            {
                await _authService.LoginAsync(viewModel);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return View(viewModel);
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied(string? returnUrl = null)
        {
            AlertHelper.AddErrorMessage(TempData, RWPM.Resources.Shared.Auth.Err_AccessDenied);
            return RedirectToAction("Index", "Home");
        }
    }
}
