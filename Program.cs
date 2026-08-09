using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Helpers;
using SQPortal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Every unsafe-method request must carry a valid antiforgery token, even
    // if an action forgets its explicit [ValidateAntiForgeryToken].
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddDbContext<SQPortalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Outside Development cookies only ever travel over HTTPS; the dev "http"
// launch profile has no TLS port, so SameAsRequest keeps local runs working.
var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

/////////remove when you want to link AD\\\\\\\\\\
// Windows SSO against the work Active Directory. To link AD:
//   1. Uncomment the Microsoft.AspNetCore.Authentication.Negotiate package
//      reference in SQPortal.csproj (same marker) and restore.
//   2. Uncomment this whole block, and move the using directive to the top of
//      this file.
//   3. Delete everything between the "test-only" markers below (the cookie
//      sign-in), plus Controllers/AccountController.cs,
//      Views/Account/Login.cshtml, Data/TestUsers.cs and the sign-out form in
//      Views/Shared/_Layout.cshtml — Windows SSO has no login page and no
//      sign-out.
//   4. Set Auth:AllowedAdGroup in appsettings.json to the AD group whose
//      members may use the portal, e.g. "CONTOSO\\SQ Portal Users".
// Domain-joined browsers then sign in automatically via Kerberos/NTLM, and
// only members of the configured group are allowed in (their AD groups arrive
// as role claims). Hosting note: works on Kestrel (Windows) and IIS; on IIS
// also enable Windows Authentication for the site.
//
// using Microsoft.AspNetCore.Authentication.Negotiate;
//
// builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
//     .AddNegotiate();
//
// builder.Services.AddAuthorization(options =>
// {
//     var adGroup = builder.Configuration["Auth:AllowedAdGroup"];
//     if (string.IsNullOrWhiteSpace(adGroup))
//     {
//         // Fail closed: without a configured group the portal must not fall
//         // back to "any domain user".
//         throw new InvalidOperationException(
//             "Auth:AllowedAdGroup must be set before linking Active Directory.");
//     }
//
//     options.FallbackPolicy = new AuthorizationPolicyBuilder()
//         .RequireAuthenticatedUser()
//         .RequireRole(adGroup)
//         .Build();
// });
/////////end of AD code\\\\\\\\\\

///////// test-only: delete this block when AD is linked \\\\\\\\\\
// Test-environment sign-in: the login page lists the hardcoded users in
// Data/TestUsers.cs and pressing one signs in through this cookie.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "SQPortal.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.Events = new CookieAuthenticationEvents
        {
            // Background fetches (the case-details dialog) should see a 401,
            // not the login page's HTML injected into the dialog body.
            OnRedirectToLogin = context =>
            {
                if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                }
                else
                {
                    context.Response.Redirect(context.RedirectUri);
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Deny by default: every endpoint requires a signed-in user unless it
    // opts out with [AllowAnonymous] (the login page and the error page).
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
///////// end test-only \\\\\\\\\\

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "SQPortal.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});

// Throttle sign-in attempts per client address to slow credential guessing.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

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

// Browser hardening headers plus a per-request nonce for the inline page scripts.
app.Use(async (context, next) =>
{
    var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    context.Items[Csp.NonceKey] = nonce;

    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        $"script-src 'self' 'nonce-{nonce}' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "font-src https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
