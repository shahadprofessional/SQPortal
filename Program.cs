using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<SQPortalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<SlaService>();
builder.Services.AddScoped<PartnerAssignmentService>();
builder.Services.AddScoped<CsvExportService>();
builder.Services.AddScoped<ManagerAssignmentService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<WeeklyReportService>();

var app = builder.Build();

// Schema is created manually via Scripts/init.sql (run in SSMS) — no DDL in code.
// On first run after the schema exists, SeedLookups() populates default partners/branches/assignments.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SQPortalDbContext>();
    db.SeedLookups();
    db.BackfillCaseNumbers();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
