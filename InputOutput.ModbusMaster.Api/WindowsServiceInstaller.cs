using CliWrap;

namespace InputOutput.ModbusMaster.Api;

/// <summary>
/// Installs the application as a Windows service (manual /Install and /Uninstall switches).
/// See https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service-with-installer
/// </summary>
public sealed class WindowsServiceInstaller(string[] args)
{
    /// <summary>SCM service name used by sc.exe create/delete/start/stop.</summary>
    public const string ServiceName = "FTTInputOutput";

    public const string DisplayName = "FTT Input Output";

    /// <summary>
    /// Install/uninstall based on cmd line args.
    /// Returns true when an install/uninstall switch was handled (caller should exit).
    /// </summary>
    public async Task<bool> InstallAsWindowsService()
    {
        if (args is not { Length: 1 })
        {
            return false;
        }

        try
        {
            var executablePath =
                Path.Combine(AppContext.BaseDirectory, $"{System.Reflection.Assembly.GetEntryAssembly()!.GetName().Name}.exe");

            if (args[0] is "/Install")
            {
                try
                {
                    await Cli.Wrap("sc")
                        .WithArguments(["stop", ServiceName])
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteAsync();
                }
                catch
                {
                    // Already stopped or missing
                }

                try
                {
                    await Cli.Wrap("sc")
                        .WithArguments(["delete", ServiceName])
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteAsync();
                }
                catch
                {
                    // Not installed
                }

                // Match Microsoft sample: binPath= and start= as single argv tokens.
                var result = await Cli.Wrap("sc")
                    .WithArguments([
                        "create", ServiceName,
                        $"binPath={executablePath}",
                        "start=auto",
                        $"DisplayName={DisplayName}"
                    ])
                    .ExecuteAsync();
                Console.WriteLine($"install result is {result.ExitCode},{result.IsSuccess}");

                var start = await Cli.Wrap("sc")
                    .WithArguments(["start", ServiceName])
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();
                Console.WriteLine($"start result is {start.ExitCode},{start.IsSuccess}");
            }
            else if (args[0] is "/Uninstall")
            {
                try
                {
                    await Cli.Wrap("sc")
                        .WithArguments(["stop", ServiceName])
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteAsync();
                }
                catch
                {
                    // Already stopped
                }

                await Cli.Wrap("sc")
                    .WithArguments(["delete", ServiceName])
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();
            }
            else
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error = {ex.Message}");
        }

        return true;
    }
}
