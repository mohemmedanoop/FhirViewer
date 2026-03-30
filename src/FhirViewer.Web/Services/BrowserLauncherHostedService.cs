using System.Diagnostics;
using FhirViewer.Web.Options;
using Microsoft.Extensions.Options;

namespace FhirViewer.Web.Services;

public sealed class BrowserLauncherHostedService(
    IHostApplicationLifetime applicationLifetime,
    IOptions<BrowserLaunchOptions> options,
    ILogger<BrowserLauncherHostedService> logger) : IHostedService
{
    private int _started;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        applicationLifetime.ApplicationStarted.Register(OnStarted);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void OnStarted()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1 || !options.Value.Enabled)
        {
            return;
        }

        try
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using var delay = new ManualResetEventSlim(false);
                    delay.Wait(TimeSpan.FromMilliseconds(750));

                    var addresses = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
                    var firstAddress = addresses?
                        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(firstAddress))
                    {
                        firstAddress = "http://localhost:5050";
                    }

                    var url = $"{firstAddress.TrimEnd('/')}{options.Value.RelativeUrl}";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Unable to launch the browser automatically.");
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to register automatic browser launch.");
        }
    }
}
