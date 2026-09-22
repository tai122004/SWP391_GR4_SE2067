using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RWPM.Common;
using RWPM.Common.Attributes;
using RWPM.Common.Exceptions;
using RWPM.Common.Helper;
using RWPM.Models.ViewModels.Store;
using RWPM.Services.Abstraction;

namespace RWPM.Controllers
{
    [Authorize(Roles = "Admin,HR,AreaManager")]
    public class StoreController : Controller
    {
        private readonly IStringLocalizer _localizer;
        private readonly IStoreService _storeService;

        public StoreController(
            IStringLocalizer<ErrorServerDefinition> localizer,
            IStoreService storeService)
        {
            _localizer = localizer;
            _storeService = storeService;
        }

        // GET: Store
        [RemoveEmptyQueryString]
        public async Task<IActionResult> Index(StoreSearch searchObject)
        {
            var result = await _storeService.SearchAsync(searchObject);
            return View(new StoreListVM(result, searchObject));
        }

        // GET: Store/Create
        public IActionResult Create()
        {
            return View(new StoreCreateVM());
        }

        // POST: Store/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StoreCreateVM viewModel)
        {
            if (!ModelState.IsValid)
            {
                var errorMsg = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                AlertHelper.AddErrorMessage(TempData, "Vui lòng kiểm tra lại thông tin nhập: " + errorMsg);
                return View(viewModel);
            }

            try
            {
                await _storeService.CreateAsync(viewModel.ToEntity());
                AlertHelper.CreateSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return View(viewModel);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                return View(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Store/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var store = await _storeService.GetByIdAsync(id);
            if (store == null)
                return NotFound();

            return View(new StoreEditVM(store));
        }

        // POST: Store/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StoreEditVM viewModel)
        {
            if (id != viewModel.StoreId)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                var errorMsg = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                AlertHelper.AddErrorMessage(TempData, "Vui lòng kiểm tra lại thông tin nhập: " + errorMsg);
                return View(viewModel);
            }

            try
            {
                var store = await _storeService.GetRequiredByIdAsync(id);
                viewModel.ApplyToEntity(store);
                await _storeService.UpdateAsync(store);
                AlertHelper.EditSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return View(viewModel);
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                return View(viewModel);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Store/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var store = await _storeService.GetByIdAsync(id);
            if (store == null)
                return NotFound();

            return View(store);
        }

        // POST: Store/DeleteConfirmed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int storeId)
        {
            try
            {
                var store = await _storeService.GetRequiredByIdAsync(storeId);
                await _storeService.DeleteAsync(store);
                AlertHelper.DeleteSuccess(TempData);
            }
            catch (ModelValidationException ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.GetErrorString(_localizer));
                return RedirectToAction(nameof(Delete), new { id = storeId });
            }
            catch (Exception ex)
            {
                AlertHelper.AddErrorMessage(TempData, ex.Message);
                return RedirectToAction(nameof(Delete), new { id = storeId });
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Store/UpdateActiveStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateActiveStatus(int id, [FromBody] StoreUpdateActiveStatusVM viewModel)
        {
            if (id != viewModel.StoreId)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values
                                    .SelectMany(v => v.Errors)
                                    .Select(e => e.ErrorMessage));
                return BadRequest(errors);
            }

            try
            {
                await _storeService.UpdateActiveStatusAsync(viewModel.StoreId!.Value, viewModel.IsActive!.Value);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: Store/SearchAddress?query=...
        [HttpGet]
        public async Task<IActionResult> SearchAddress(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(new object[] { });

            var cleanedQuery = query.Trim();

            // 1. Try Esri World Geocoding API (Fast, reliable in VN, no CORS/User-Agent blocking)
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                
                var searchQ = cleanedQuery;
                if (!searchQ.ToLower().Contains("vietnam") && !searchQ.ToLower().Contains("việt nam"))
                {
                    searchQ += ", Việt Nam";
                }

                var esriUrl = $"https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/findAddressCandidates?f=json&singleLine={Uri.EscapeDataString(searchQ)}&outFields=Match_addr&maxLocations=5";
                var jsonStr = await client.GetStringAsync(esriUrl);

                if (!string.IsNullOrWhiteSpace(jsonStr))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var results = new List<object>();
                        foreach (var item in candidates.EnumerateArray())
                        {
                            var addr = item.GetProperty("address").GetString();
                            var loc = item.GetProperty("location");
                            double x = loc.GetProperty("x").GetDouble();
                            double y = loc.GetProperty("y").GetDouble();

                            results.Add(new
                            {
                                lat = y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                lon = x.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                display_name = addr
                            });
                        }
                        return Json(results);
                    }
                }
            }
            catch
            {
                // Fallback below
            }

            // 2. Try Nominatim Geocoding API
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(4);
                client.DefaultRequestHeaders.Add("User-Agent", "RWPM-StoreApp/1.0 (contact@rwpm.internal)");

                var fullQuery = cleanedQuery;
                if (!fullQuery.ToLower().Contains("vietnam") && !fullQuery.ToLower().Contains("việt nam"))
                {
                    fullQuery += ", Việt Nam";
                }

                var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(fullQuery)}&limit=5";
                var response = await client.GetStringAsync(url);

                if (!string.IsNullOrWhiteSpace(response) && response != "[]")
                {
                    return Content(response, "application/json");
                }
            }
            catch
            {
                // Fallback below
            }

            // 3. Fallback match from preset dictionary if online APIs fail
            var fallbackDict = new Dictionary<string, (double lat, double lon)>
            {
                { "ha noi", (21.0285, 105.8542) },
                { "hà nội", (21.0285, 105.8542) },
                { "hoang quoc viet", (21.0465, 105.7942) },
                { "hoàng quốc việt", (21.0465, 105.7942) },
                { "ho chi minh", (10.7769, 106.7009) },
                { "hồ chí minh", (10.7769, 106.7009) },
                { "tphcm", (10.7769, 106.7009) },
                { "sai gon", (10.7769, 106.7009) },
                { "sài gòn", (10.7769, 106.7009) },
                { "da nang", (16.0544, 108.2022) },
                { "đà nẵng", (16.0544, 108.2022) },
                { "can tho", (10.0452, 105.7469) },
                { "cần thơ", (10.0452, 105.7469) },
                { "hai phong", (20.8449, 106.6881) },
                { "hải phòng", (20.8449, 106.6881) },
                { "hoa lac", (21.0130, 105.5268) },
                { "hòa lạc", (21.0130, 105.5268) },
                { "fpt", (21.0130, 105.5268) }
            };

            var lowerQ = cleanedQuery.ToLower();
            foreach (var kvp in fallbackDict)
            {
                if (lowerQ.Contains(kvp.Key))
                {
                    return Json(new[]
                    {
                        new {
                            lat = kvp.Value.lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            lon = kvp.Value.lon.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            display_name = $"{cleanedQuery} (Vị trí mặc định)"
                        }
                    });
                }
            }

            // Default fallback
            return Json(new[]
            {
                new {
                    lat = "21.0285",
                    lon = "105.8542",
                    display_name = "Hà Nội, Việt Nam"
                }
            });
        }

        // GET: Store/ReverseGeocode?lat=...&lng=...
        [HttpGet]
        public async Task<IActionResult> ReverseGeocode(double lat, double lng)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(4);
                var esriUrl = $"https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/reverseGeocode?f=json&location={lng.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                var jsonStr = await client.GetStringAsync(esriUrl);
                if (!string.IsNullOrWhiteSpace(jsonStr))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
                    if (doc.RootElement.TryGetProperty("address", out var addrProp))
                    {
                        var matchAddr = addrProp.GetProperty("Match_addr").GetString();
                        if (!string.IsNullOrWhiteSpace(matchAddr))
                        {
                            return Json(new { address = matchAddr });
                        }
                    }
                }
            }
            catch
            {
                // Fallback below
            }
            return Json(new { address = "" });
        }
    }
}
