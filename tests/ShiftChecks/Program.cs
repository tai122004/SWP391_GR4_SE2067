using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RWPM.Common.Helper;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Shift;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    checks++; Console.WriteLine("PASS: " + name);
}
var date = new DateTime(2026,10,1);
var shift = new Shift { ShiftName="Ca ngày", StartTime=new(8,0,0), EndTime=new(17,0,0), EffectiveFrom=date,
    BreakStartTime=new(12,0,0), BreakEndTime=new(13,0,0), BreakStartDayOffset=0, BreakEndDayOffset=0 };
Check(!ShiftRules.Validate(shift).Any(), "Valid day shift and break");
Check(ShiftTimeHelper.GetWorkedHours(date,shift,date.AddHours(8),date.AddHours(17)) == 8, "Full day deducts unpaid break");
Check(ShiftTimeHelper.GetWorkedHours(date,shift,date.AddHours(8),date.AddHours(11)) == 3, "Leave before break deducts no break");
Check(ShiftTimeHelper.GetWorkedHours(date,shift,date.AddHours(8),date.AddHours(12.5)) == 4, "Partial break deducts overlap only");
Check(ShiftTimeHelper.GetWorkedHours(date,shift,date.AddHours(7),date.AddHours(18)) == 8, "Early arrival and late departure are capped to shift");
shift.EndDayOffset=1;
Check(ShiftRules.Validate(shift).Any(), "Invalid 33-hour day shift rejected");
shift.StartTime=new(22,0,0); shift.EndTime=new(6,0,0);
shift.BreakStartTime=new(2,0,0); shift.BreakEndTime=new(2,30,0);
shift.BreakStartDayOffset=1; shift.BreakEndDayOffset=1;
Check(!ShiftRules.Validate(shift).Any(), "Valid overnight shift and next-day break");
Check(ShiftTimeHelper.GetDateTimeRange(date,shift).EndAt == date.AddDays(1).AddHours(6), "Overnight end uses explicit next-day offset");
Check(ShiftTimeHelper.GetWorkedHours(date,shift,date.AddHours(22),date.AddDays(1).AddHours(6)) == 7.5, "Overnight break calculation");
var record=new AttendanceRecord { Date=date, Shift=shift, CheckInTime=new(22,0,0), CheckOutTime=new(6,0,0) };
Check(record.WorkHours == 7.5, "Attendance calculates overnight paid hours");
shift.BreakStartDayOffset=0;
Check(ShiftRules.Validate(shift).Any(), "Break outside shift rejected");
shift.BreakStartDayOffset=1; shift.BreakEndTime=null;
Check(ShiftRules.Validate(shift).Any(), "Incomplete break rejected");
var vm=new ShiftCreateVM { ShiftName="Ca", StartTime=new(8,0,0), EndTime=new(12,0,0), StoreIds=new(){1,1,2} };
Check(vm.ToEntity().StoreShifts.Count==2, "Repeated store selection deduplicated");
Check(vm.GracePeriodMinutes == 15 && vm.EarlyCheckOutMinutes == 15, "New shift prepopulates explicit tolerances");
vm.GracePeriodMinutes = null;
Check(ShiftRules.Validate(vm.ToEntity()).Any(), "Missing tolerance rejected by service validation");
vm.GracePeriodMinutes = 0; vm.EarlyCheckOutMinutes = 0;
Check(!ShiftRules.Validate(vm.ToEntity()).Any(), "Zero tolerance remains valid and explicit");
vm.StoreIds.Clear();
Check(vm.Validate(new(vm)).Any(), "At least one store required");
using var ctx=new DefaultDatabaseContext(new DbContextOptionsBuilder<DefaultDatabaseContext>()
    .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ShiftChecks;Trusted_Connection=True;").Options);
var entity=ctx.Model.FindEntityType(typeof(Shift))!;
Check(!entity.FindProperty("GracePeriodMinutes")!.IsNullable && !entity.FindProperty("EarlyCheckOutMinutes")!.IsNullable, "Database requires explicit shift tolerances");
Check(entity.GetProperties().Count()==19 && entity.FindProperty("ShiftCode")==null && entity.FindProperty("StoreId")==null,
    "Shift maps only the new 19-column schema");
var link=ctx.Model.FindEntityType(typeof(StoreShift))!;
Check(link.FindPrimaryKey()!.Properties.Select(p=>p.Name).SequenceEqual(new[]{"StoreId","ShiftId"}), "StoreShift composite key");
Check(link.GetForeignKeys().All(f=>f.DeleteBehavior==DeleteBehavior.Restrict), "Assignments prevent cascade deletion");
var script=ctx.GetService<IMigrator>().GenerateScript("20260927193140_AddAttendanceAdjustment","20261001110125_ReplaceShiftWithStoreAssignments");
Check(script.IndexOf("ShiftLegacyArchive",StringComparison.Ordinal)<script.IndexOf("DROP COLUMN [ShiftCode]",StringComparison.Ordinal), "Legacy archive precedes removal of columns");
Check(script.Contains("ShiftBreakConversionMapping") && script.Contains("THROW 51000"), "Migration requires reviewed legacy break mapping");
Check(script.Contains("CREATE TABLE [StoreShift]") && !script.Contains("DROP TABLE [Shift]"), "Migration preserves Shift table and IDs");
var webBuilder = WebApplication.CreateBuilder(new WebApplicationOptions {
    ApplicationName=typeof(RWPM.Controllers.ShiftController).Assembly.GetName().Name,
    ContentRootPath=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../RWPM")) });
webBuilder.Services.AddControllersWithViews();
webBuilder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
webBuilder.Services.AddSingleton<IUrlHelperFactory, CheckUrlHelperFactory>();
await using var web=webBuilder.Build();
using var scope=web.Services.CreateScope();
var http=new DefaultHttpContext { RequestServices=scope.ServiceProvider };
http.Request.Scheme="http"; http.Request.Host=new HostString("localhost");
var action=new ActionContext(http,new RouteData(),new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
var engine=scope.ServiceProvider.GetRequiredService<ICompositeViewEngine>();
var view=engine.GetView(null,"/Views/Shift/_Form.cshtml",false);
Check(view.Success,"Shift form compiled view found");
var renderView=view.View ?? throw new Exception("Compiled shift form missing");
var viewData=new ViewDataDictionary<ShiftCreateVM>(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),new()) { Model=new ShiftCreateVM { StoreIds=new(){1} } };
viewData["StoreList"]=new SelectList(new[]{ new { Id=0,Name="Placeholder" },new { Id=1,Name="Vincom" },new { Id=2,Name="Vùng xa" } },"Id","Name");
using var writer=new StringWriter();
var viewContext=new ViewContext(action,renderView,viewData,new TempDataDictionary(http,scope.ServiceProvider.GetRequiredService<ITempDataProvider>()),writer,new HtmlHelperOptions());
await renderView.RenderAsync(viewContext);
var html=writer.ToString();
Check(html.Contains("name=\"StoreIds\"") && html.Contains("store-1") && !html.Contains("store-0"),"Form renders valid store checkboxes without placeholder");
Check(!html.Contains("name=\"ShiftCode\"") && !html.Contains("name=\"IsTemplate\""),"Old fields absent from rendered form");
var indexView=engine.GetView(null,"/Views/Shift/Index.cshtml",false).View ?? throw new Exception("Compiled index missing");
var displayShift=new Shift { ShiftId=1, ShiftName="Ca tối", StartTime=new(22,0,0), EndTime=new(6,0,0), EndDayOffset=1, EffectiveFrom=date,
    StoreShifts=new List<StoreShift> { new() { StoreId=1, Store=new Store { StoreName="Vincom" } } } };
var listModel=new ShiftListVM(new RWPM.Common.Models.PaginationRes<Shift>(new[]{displayShift},1,10,1),new ShiftSearch(),
    new SelectList(new[]{ new {Id=1,Name="Vincom"} },"Id","Name"));
var indexData=new ViewDataDictionary<ShiftListVM>(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),new()) { Model=listModel };
using var indexWriter=new StringWriter();
await indexView.RenderAsync(new ViewContext(action,indexView,indexData,new TempDataDictionary(http,scope.ServiceProvider.GetRequiredService<ITempDataProvider>()),indexWriter,new HtmlHelperOptions()));
Check(indexWriter.ToString().Contains("22:00") && !indexWriter.ToString().Contains("(+1)") && indexWriter.ToString().Contains(RWPM.Resources.Shared.Shift.ResourceManager.GetString("NextDay")!),"List renders localized overnight indicator");
Check(!indexWriter.ToString().Contains("<th>" + RWPM.Resources.Shared.Shift.ResourceManager.GetString("UnpaidBreak") + "</th>"), "List omits break column while keeping details");
Check(indexWriter.ToString().Contains("Vincom") && !indexWriter.ToString().Contains("Clone"),"List renders store applicability without cloning");
Console.WriteLine($"All {checks} checks passed. No database connection was used.");

sealed class CheckUrlHelperFactory : IUrlHelperFactory
{
    public IUrlHelper GetUrlHelper(ActionContext context)=>new CheckUrlHelper(context);
}
sealed class CheckUrlHelper(ActionContext context) : IUrlHelper
{
    public ActionContext ActionContext=>context;
    public string? Action(UrlActionContext context)=>"/Shift/"+context.Action;
    public string? Content(string? path)=>path?.Replace("~/","/");
    public bool IsLocalUrl(string? url)=>true;
    public string? Link(string? routeName, object? values)=>"/Shift";
    public string? RouteUrl(UrlRouteContext context)=>"/Shift";
}
