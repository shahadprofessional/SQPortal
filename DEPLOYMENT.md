# SQPortal — deployment checklist (UAT / production)

The servers are internal with no internet access; the site is fully
self-contained (all styles, scripts and fonts are served from the app itself),
so no outbound connectivity is needed. Work through every item below for each
environment.

## 1. Database

- [ ] Create an empty `SQPortal` database on the internal SQL Server.
- [ ] Run `Scripts/database.sql` against it: Part 1 for a new empty database,
      Part 2 for a database created from an earlier schema, then Part 3 in both
      cases. Full instructions are in the file's header.
- [ ] Set the real connection string in the environment's configuration
      (`ConnectionStrings:DefaultConnection`) — the checked-in value points at
      LocalDB and is for development only. The app's service account needs
      read/write on all tables (no DDL rights required).
- [ ] Schedule SQL Server backups (full + transaction log) and test a restore
      once. Soft delete protects against stray clicks, not against disk loss.

## 2. HTTPS

- [ ] Preferred: bind an HTTPS certificate from the internal CA to the site and
      keep `Security:RequireHttps` = `true`.
- [ ] Plain-http intranet hosting only: set `Security:RequireHttps` = `false`.
      Without this, browsers never send the sign-in cookie over http and login
      loops silently.

## 3. Configuration (per environment)

- [ ] `AllowedHosts` — the site's real internal hostname, e.g.
      `sqportal.company.local`.
- [ ] `Sla:TimeZone` — the business time zone (e.g. `Arabian Standard Time`);
      required on servers running UTC, or SLA dates shift.
- [ ] `Sla:WeekendDays` — confirm the non-working days.
- [ ] `Mail` — real `FromAddress` (replacing the placeholder), `SmtpHost`,
      port/TLS, and credentials via environment variables
      (`Mail__SmtpUsername`, `Mail__SmtpPassword`), not in the JSON file.
- [ ] `Security:DataProtectionKeysFolder` — leave empty to use `<app>/keys`,
      or point at a fixed folder. Either way it must survive deployments and
      the service account needs write access; losing it signs everyone out.
- [ ] `Logging:File:Folder` — where daily log files go (`Logs` under the app
      by default); service account needs write access. Check this folder first
      when anything misbehaves. `Logging:File:RetainDays` (default 90) controls
      how long files are kept before the app deletes them; raise it if an audit
      policy requires longer, or set 0 to keep everything and manage the folder
      externally.

## 4. Sign-in

- [ ] UAT runs without sign-in: there is no login page, and every visitor is
      admitted as the single test identity in `Data/TestUsers.cs`. Anyone who
      can reach the URL has full access, so this is acceptable only on a
      restricted network.
- [ ] Before production: link Active Directory. Follow the markers
      `/////////remove when you want to link AD\\\\\\\\\\` in `Program.cs` and
      `SQPortal.csproj`, set `Auth:AllowedAdGroup`, and delete the test-only
      blocks they list. On IIS also enable Windows Authentication for the site.

## 5. Hosting


- IIS: dedicated app pool ("No Managed Code"), app-pool identity granted write
  access to the keys and Logs folders; note the default app-pool recycle
  schedule is fine once keys are persisted.
- Windows service / Kestrel: install with the environment set via
  `ASPNETCORE_ENVIRONMENT` and the URLs via `ASPNETCORE_URLS`.
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production` (UAT can use `Production` with
      its own config values; `Development` enables the demo seed data and
      developer error pages, and belongs on workstations only).

## 6. First start

- [ ] Start the app and read `Logs/sqportal-<date>.log`: a critical line about
      the schema version means a database script was missed.
- [ ] The portal starts with no branches or staff — enter the real rosters in
      Settings before the first case is logged.
- [ ] Sign in, create a test case, edit it, check its history section, export
      CSV, then delete the test case.

## Known items deliberately deferred

- .NET 9 left in place by decision; it is past Microsoft's support window —
  plan the move to .NET 10 LTS.
- Email sends over classic SMTP authentication; if corporate mail later
  requires modern auth (OAuth), `Services/EmailService.cs` is the single place
  to change.
- No automated retention/purge of customer data yet.
