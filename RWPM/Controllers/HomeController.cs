using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using RWPM.Common;
using RWPM.Infrastructure.Data;
using RWPM.Infrastructure.Seeder;
using RWPM.Models;

namespace RWPM.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly DefaultDatabaseContext _defaultDatabaseContext;

        public HomeController(ILogger<HomeController> logger,
            DefaultDatabaseContext databaseContext)
        {
            _logger = logger;
            _defaultDatabaseContext = databaseContext;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Index", "Acc");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var exceptionHandlerFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();
            var exception = exceptionHandlerFeature?.Error;

            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Message = exception?.Message
            });
        }


        [AllowAnonymous]
        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            return LocalRedirect(returnUrl);
        }

        [Authorize(Roles = "SuperAdmin,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitData()
        {
            try
            {
                await new _Seeder(_defaultDatabaseContext).SeedAsync();
                AlertHelper.AddSuccessMessage(TempData, Resources.Shared.SharedResource.InitDataSuccess);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
            }

            return View(nameof(Index));
        }
    }
}
