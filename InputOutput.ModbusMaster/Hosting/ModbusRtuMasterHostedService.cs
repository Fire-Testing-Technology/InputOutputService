using InputOutput.ModbusMaster.Scanning;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InputOutput.ModbusMaster.Hosting;

/// <summary>
/// Registers configured devices with <see cref="IModbusMaster"/> and optionally connects,
/// runs an RS-485 scan, and starts polling.
/// </summary>
public sealed class ModbusRtuMasterHostedService(
    IModbusMaster master,
    IModbusBusScanner scanner,
    IOptions<ModbusRtuHostOptions> options,
    ILoggerFactory loggerFactory,
    ILogger<ModbusRtuMasterHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var hostOptions = options.Value;
        foreach (var registration in hostOptions.Devices)
        {
            var device = ModbusDeviceFactory.Create(registration, loggerFactory);
            master.Register(device);
            logger.LogInformation(
                "Registered {Type} at unit {UnitId} ({Name}).",
                registration.Type,
                registration.UnitId,
                device.Name ?? "unnamed");
        }

        if (!hostOptions.AutoConnect)
        {
            logger.LogInformation("Modbus AutoConnect is disabled; skipping Connect/Scan/StartPolling.");
            return;
        }

        var availablePorts = SerialPortAvailability.GetAvailablePorts();
        if (!SerialPortAvailability.IsAvailable(hostOptions.PortName, availablePorts))
        {
            logger.LogError(
                "Configured serial port {Port} is not available on this machine (available: {Available}). "
                + "Skipping Connect/Scan/StartPolling; units remain listed. Check the cabling/adapter or change the port on the Configure page.",
                hostOptions.PortName,
                availablePorts.Count == 0 ? "none" : string.Join(", ", availablePorts));
            return;
        }

        try
        {
            master.Connect();
            logger.LogInformation("Modbus RTU master connected on {Port}.", hostOptions.PortName);

            if (hostOptions.AutoScan)
            {
                var scan = await scanner.ScanAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Startup RS-485 scan found {Count} unit(s) in {DurationMs}ms.",
                    scan.Units.Count,
                    scan.Duration.TotalMilliseconds);
            }

            await master.StartPollingAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Modbus RTU polling started.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Modbus RTU master on {Port}. Units remain listed.", hostOptions.PortName);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await master.StopPollingAsync().ConfigureAwait(false);
            master.Disconnect();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while stopping Modbus RTU master.");
        }
    }
}
