using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using RWPM.Common;
using RWPM.Infrastructure.Data;
using RWPM.Infrastructure.Seeder;
using RWPM.Models;
using RWPM.Services.Abstraction;

namespace RWPM.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly DefaultDatabaseContext _defaultDatabaseContext;
        private readonly IDashboardService _dashboardService;

        public HomeController(ILogger<HomeController> logger,
            DefaultDatabaseContext databaseContext,
            IDashboardService dashboardService)
        {
            _logger = logger;
            _defaultDatabaseContext = databaseContext;
            _dashboardService = dashboardService;
        }

        // GET: / and /Home/Index - "Tổng quan" dashboard, content depends on role
        public async Task<IActionResult> Index()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Challenge();

            bool isSuperOrAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
            bool isHR = User.IsInRole("HR");
            bool isStoreManager = User.IsInRole("StoreManager");
            // Same chain-wide role set as RequestController.Index
            bool isChainManager = isSuperOrAdmin || isHR || User.IsInRole("AreaManager");
            // Attendance/Index redirects a pure system Admin away, so don't offer check-in to them
            bool canUseAttendance = !(isSuperOrAdmin && !isHR && !isStoreManager);

            var vm = await _dashboardService.GetDashboardAsync(username, isChainManager, isStoreManager, canUseAttendance);
            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
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

        [Authorize(Roles = "Admin,HR,SuperAdmin")]
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

            // PRG: tránh gửi lại form khi F5 và lỗi 404/405 khi đổi ngôn ngữ tại URL POST /Home/InitData
            return RedirectToAction(nameof(Index));
        }
    }
}
