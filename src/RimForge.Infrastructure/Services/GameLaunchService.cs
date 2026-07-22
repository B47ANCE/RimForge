using System.Diagnostics;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class GameLaunchService(
    IGameLogService gameLogService,
    IPlatformDiscoveryService platformDiscoveryService) : IGameLaunchService
{
    public async Task<GameLaunchResult> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default)
    {
        var playerLog = string.IsNullOrWhiteSpace(request.PlayerLogPath) ? GetDefaultPlayerLogPath() : request.PlayerLogPath;
        await gameLogService.StartAsync(playerLog!, startAtEnd: true, cancellationToken);

        try
        {
            Process? process;
            string target;
            if (!string.IsNullOrWhiteSpace(request.SteamExecutable) && File.Exists(request.SteamExecutable))
            {
                target = request.SteamExecutable;
                process = Process.Start(new ProcessStartInfo(target, "-applaunch 294100")
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(target)
                });
            }
            else if (!string.IsNullOrWhiteSpace(request.GameExecutable) && File.Exists(request.GameExecutable))
            {
                target = request.GameExecutable;
                process = Process.Start(new ProcessStartInfo(target)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(target)
                });
            }
            else
            {
                target = "steam://run/294100";
                process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }

            return new GameLaunchResult(process is not null, process is null
                ? "RimWorld launch did not return a process."
                : $"Launched '{request.Profile.Name}' and started watching Player.log.",
                process?.Id, target, playerLog);
        }
        catch (Exception ex)
        {
            await gameLogService.StopAsync();
            return new GameLaunchResult(false, $"RimWorld could not be launched: {ex.Message}", PlayerLogPath: playerLog);
        }
    }

    public string GetDefaultPlayerLogPath() => platformDiscoveryService.Discover().UserPaths.PlayerLogPath;
}
