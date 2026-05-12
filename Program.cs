using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<SQPortalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<SlaService>();
builder.Services.AddScoped<PartnerAssignmentService>();
builder.Services.AddScoped<DuplicateDetectionService>();
builder.Services.AddScoped<CsvExportService>();
builder.Services.AddScoped<RepeatCustomerService>();
builder.Services.AddScoped<DashboardService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SQPortalDbContext>();
    db.Database.EnsureCreated();
    db.SeedLookups();
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
