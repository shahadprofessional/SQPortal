using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Helpers;
using SQPortal.Services;

var builder = WebApplication.CreateBuilder(args);

// On a server there is no console to read; everything also lands in
// Logs/sqportal-<date>.log so failures stay diagnosable.
if (builder.Configuration.GetValue("Logging:File:Enabled", true))
{
    var logFolder = builder.Configuration["Logging:File:Folder"];
    if (string.IsNullOrWhiteSpace(logFolder))
    {
        logFolder = "Logs";
    }
    if (!Path.IsPathRooted(logFolder))
    {
        logFolder = Path.Combine(builder.Environment.ContentRootPath, logFolder);
    }
    var fileLogLevel = Enum.TryParse<LogLevel>(builder.Configuration["Logging:File:MinimumLevel"], true, out var parsedLevel)
        ? parsedLevel
        : LogLevel.Information;
    var retainDays = builder.Configuration.GetValue("Logging:File:RetainDays", 90);
    builder.Logging.AddProvider(new FileLoggerProvider(logFolder, fileLogLevel, retainDays));
}

// Cookies and antiforgery tokens are encrypted with data-protection keys.
// Persisting the keys to a fixed folder keeps every session and open form
// valid across app restarts, recycles and deployments.
var keysFolder = builder.Configuration["Security:DataProtectionKeysFolder"];
if (string.IsNullOrWhiteSpace(keysFolder))
{
    keysFolder = Path.Combine(builder.Environment.ContentRootPath, "keys");
}

// An unusable folder must not stop the app from starting: it falls back to the
// framework default, which costs sessions on restart rather than all service.
var keysFolderReady = true;
string? keysFolderError = null;
try
{
    Directory.CreateDirectory(keysFolder);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
{
    keysFolderReady = false;
    keysFolderError = ex.Message;
}

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("SQPortal");
if (keysFolderReady)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysFolder));
    if (OperatingSystem.IsWindows())
    {
        // Encrypts the key files at rest under the service account's identity.
        dataProtection.ProtectKeysWithDpapi();
    }
}

builder.Services.AddControllersWithViews(options =>
{
    // Antiforgery is enforced on every unsafe HTTP method, not only on
    // actions carrying the explicit attribute.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddDbContext<SQPortalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Secure-only cookies outside Development. Security:RequireHttps=false is the
// deliberate opt-out for plain-http intranet hosting with no certificate —
// without it the browser would never send the auth cookie over http and
// sign-in would silently loop.
var requireHttps = builder.Configuration.GetValue("Security:RequireHttps", true);
var cookieSecurePolicy = builder.Environment.IsDevelopment() || !requireHttps
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

/////////remove when you want to link AD\\\\\\\\\\
// Windows SSO against the work Active Directory. To link AD:
//   1. Uncomment the Microsoft.AspNetCore.Authentication.Negotiate package
//      reference in SQPortal.csproj (same marker) and restore.
//   2. Uncomment this whole block, and move the using directive to the top of
//      this file.
//   3. Delete both "test-only" blocks below — the cookie sign-in registration
//      and the auto sign-in middleware — plus Data/TestUsers.cs. Windows SSO
//      authenticates the domain account instead.
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
// Test-environment sign-in: no login page and no password. The middleware
// further down signs every request in as Data/TestUsers.cs's single identity;
// this cookie only carries it between requests.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "SQPortal.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
    });

builder.Services.AddAuthorization(options =>
{
    // Deny by default; only [AllowAnonymous] endpoints are public.
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

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// Outbound mail; sender address and SMTP settings come from the "Mail" section.
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection(MailSettings.SectionName));
builder.Services.AddScoped<EmailService>();

// SLA clock: business time zone and weekend days come from the "Sla" section.
builder.Services.Configure<SlaSettings>(builder.Configuration.GetSection(SlaSettings.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditService>();

builder.Services.AddScoped<SlaService>();
builder.Services.AddScoped<PartnerAssignmentService>();
builder.Services.AddScoped<CsvExportService>();
builder.Services.AddScoped<ManagerAssignmentService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<WeeklyReportService>();

var app = builder.Build();

if (!keysFolderReady)
{
    app.Logger.LogCritical(
        "Data-protection keys folder \"{Folder}\" is not usable ({Error}). Sessions and open forms " +
        "will be invalidated whenever the app restarts. Grant the service account write access, or " +
        "set Security:DataProtectionKeysFolder to a writable path.",
        keysFolder, keysFolderError);
}

// Schema is created manually via Scripts/database.sql; SeedLookups() only inserts
// default rows into empty lookup tables. The SchemaVersions check catches a
// database that has not had the latest script applied.
const string requiredSchemaVersion = "006";
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SQPortalDbContext>();

    var schemaCurrent = false;
    try
    {
        schemaCurrent = db.SchemaVersions.Any(v => v.Version == requiredSchemaVersion);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "SchemaVersions table not readable.");
    }

    if (schemaCurrent)
    {
        // The demo rosters are development-only; UAT/production start empty
        // and real branches/staff are entered in Settings.
        if (app.Environment.IsDevelopment())
        {
            db.SeedLookups();
        }
        db.BackfillCaseNumbers();
    }
    else
    {
        app.Logger.LogCritical(
            "Database schema is not at version {Version}. Run Scripts/database.sql — Part 1 on a " +
            "new database or Part 2 on an existing one, then Part 3. Instructions are in its header.",
            requiredSchemaVersion);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    if (requireHttps)
    {
        app.UseHsts();
    }
}

// Meaningless without an https binding; skipped when RequireHttps is off.
if (requireHttps)
{
    app.UseHttpsRedirection();
}

// Security headers plus the per-request CSP nonce used by inline page scripts.
app.Use(async (context, next) =>
{
    var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    context.Items[Csp.NonceKey] = nonce;

    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    // Fully self-hosted: no external hosts appear in the policy.
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        $"script-src 'self' 'nonce-{nonce}'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "font-src 'self'; " +
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
app.UseAuthentication();

///////// test-only: delete this block when AD is linked \\\\\\\\\\
// No login page: any request without a session is signed in as the single
// test identity, so the portal opens straight onto the dashboard. Windows SSO
// replaces this by authenticating the domain account instead.
app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated != true)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUser.Username),
                new Claim(ClaimTypes.Name, TestUser.DisplayName)
            },
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        // Also applied to the request in flight, so this first visit is
        // authorized without a redirect.
        context.User = principal;
    }

    await next();
});
///////// end test-only \\\\\\\\\\

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
