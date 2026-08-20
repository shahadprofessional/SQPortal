using Microsoft.Extensions.Options;

namespace SQPortal.Services.Ad;

/// <summary>
/// Re-reads the branch-manager groups on a timer, so a move or a leaver in
/// Active Directory shows up in the portal without anyone touching it. Also
/// runs once shortly after startup.
/// </summary>
public class AdSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AdSettings _settings;
    private readonly AdSyncState _state;
    private readonly ILogger<AdSyncHostedService> _logger;

    public AdSyncHostedService(
        IServiceScopeFactory scopes,
        IOptions<AdSettings> settings,
        AdSyncState state,
        ILogger<AdSyncHostedService> logger)
    {
        _scopes = scopes;
        _settings = settings.Value;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            // Nothing to poll for. Settings still shows why, from the result
            // recorded here.
            _state.Last = AdSyncResult.NotLinked(
                "Active Directory is not linked yet — branch managers are whatever was last recorded. " +
                "Set the \"Ad\" section in appsettings.json once IT Security has given you the group names.");
            _logger.LogInformation("AD manager sync is off (Ad:Enabled is false).");
            return;
        }

        // Let the app finish starting before the first directory call.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(_settings.SyncInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var sync = scope.ServiceProvider.GetRequiredService<AdManagerSyncService>();
            await sync.SyncAsync("AD sync (scheduled)", stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            // A failed sync leaves the last known roster in place; it must
            // never take the app down.
            _logger.LogError(ex, "Scheduled AD manager sync failed.");
            _state.Last = AdSyncResult.Failed($"Nothing was changed — the scheduled sync failed: {ex.Message}");
        }
    }
}
