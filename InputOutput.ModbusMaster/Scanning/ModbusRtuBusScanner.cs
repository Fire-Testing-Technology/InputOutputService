using System.Diagnostics;
using System.Runtime.CompilerServices;
using InputOutput.ModbusMaster.Devices.Sequent;
using InputOutput.ModbusMaster.Devices.Waveshare;
using InputOutput.ModbusMaster.Hosting;
using FluentModbus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InputOutput.ModbusMaster.Scanning;

/// <summary>
/// Probes consecutive Modbus unit ids on the RTU master's open serial port.
/// Classification: Waveshare (holding 0x8000), Sequent (16 holdings + optional coils), else Unknown.
/// </summary>
public sealed class ModbusRtuBusScanner(
    IModbusMaster master,
    IOptions<ModbusRtuHostOptions> options,
    ILogger<ModbusRtuBusScanner> logger) : IModbusBusScanner
{
    private readonly SemaphoreSlim _scanGate = new(1, 1);

    public bool IsScanning { get; private set; }

    public async Task<ModbusScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        ModbusScanCompletedEvent? completed = null;
        await foreach (var evt in ScanStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            switch (evt)
            {
                case ModbusScanCompletedEvent c:
                    completed = c;
                    break;
                case ModbusScanFailedEvent f:
                    throw new InvalidOperationException(f.Message);
            }
        }

        return completed?.Result
               ?? throw new InvalidOperationException("Scan ended without a completed result.");
    }

    public async IAsyncEnumerable<ModbusScanEvent> ScanStreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!await _scanGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            yield return new ModbusScanFailedEvent("An RS-485 scan is already in progress.");
            yield break;
        }

        IsScanning = true;
        var hostOptions = options.Value;
        var from = hostOptions.ScanUnitIdFrom;
        var to = hostOptions.ScanUnitIdTo;
        var wasPolling = master.IsPolling;
        var stopwatch = Stopwatch.StartNew();
        var discovered = new List<ModbusDiscoveredUnit>();

        try
        {
            if (from is < 1 or > 247 || to is < 1 or > 247 || from > to)
            {
                yield return new ModbusScanFailedEvent("ScanUnitIdFrom/To must satisfy 1 ≤ from ≤ to ≤ 247.");
                yield break;
            }

            Exception? connectError = null;
            try
            {
                EnsureConnected(hostOptions);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect for RS-485 scan on {Port}.", hostOptions.PortName);
                connectError = ex;
            }

            if (connectError is not null)
            {
                yield return new ModbusScanFailedEvent($"Unable to connect on {hostOptions.PortName}: {connectError.Message}");
                yield break;
            }

            if (wasPolling)
            {
                await master.StopPollingAsync().ConfigureAwait(false);
            }

            logger.LogInformation("RS-485 scan starting on {Port}, units {From}–{To}.", hostOptions.PortName, from, to);
            yield return new ModbusScanStartedEvent(from, to, hostOptions.PortName);

            var total = to - from + 1;
            var index = 0;
            for (int unitId = from; unitId <= to; unitId++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;
                yield return new ModbusScanProbingEvent((byte)unitId, index, total);

                var unit = await ProbeUnitAsync((byte)unitId, hostOptions, cancellationToken).ConfigureAwait(false);
                if (unit is not null)
                {
                    discovered.Add(unit);
                    logger.LogInformation(
                        "Discovered unit {UnitId} as {Type} ({Detail}).",
                        unit.UnitId,
                        unit.DetectedType,
                        unit.Detail ?? "no detail");
                    yield return new ModbusScanDiscoveredEvent(unit);
                }

                if (hostOptions.ScanInterProbeDelay > TimeSpan.Zero && unitId < to)
                {
                    await Task.Delay(hostOptions.ScanInterProbeDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            stopwatch.Stop();
            logger.LogInformation(
                "RS-485 scan finished in {DurationMs}ms; {Count} unit(s) found.",
                stopwatch.ElapsedMilliseconds,
                discovered.Count);

            yield return new ModbusScanCompletedEvent(new ModbusScanResult
            {
                Units = discovered,
                Duration = stopwatch.Elapsed,
                UnitIdFrom = from,
                UnitIdTo = to
            });
        }
        finally
        {
            try
            {
                if (wasPolling && master.IsConnected)
                {
                    await master.StartPollingAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resume polling after RS-485 scan.");
            }

            IsScanning = false;
            _scanGate.Release();
        }
    }

    private void EnsureConnected(ModbusRtuHostOptions hostOptions)
    {
        if (master.IsConnected)
        {
            return;
        }

        master.Connect();
        logger.LogInformation("Connected Modbus RTU master on {Port} for scan.", hostOptions.PortName);
    }

    private async Task<ModbusDiscoveredUnit?> ProbeUnitAsync(
        byte unitId,
        ModbusRtuHostOptions hostOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            string? detectedType = null;
            string? detail = null;

            await master.ExecuteAsync(unitId, (channel, ct) =>
            {
                _ = channel.ReadHoldingRegisters<ushort>(0, 1);
                detectedType = Classify(channel, out detail);
                return ValueTask.CompletedTask;
            }, cancellationToken).ConfigureAwait(false);

            if (detectedType is null)
            {
                return null;
            }

            var registered = false;
            if (hostOptions.RegisterDiscoveredDevices &&
                detectedType is not ModbusDetectedDeviceTypes.Unknown &&
                !master.RegisteredUnitIds.Contains(unitId))
            {
                var device = ModbusDeviceFactory.Create(new ModbusDeviceRegistrationOptions
                {
                    Type = detectedType,
                    UnitId = unitId,
                    Name = $"{detectedType} @{unitId}"
                });
                master.Register(device);
                registered = true;
            }

            return new ModbusDiscoveredUnit
            {
                UnitId = unitId,
                DetectedType = detectedType,
                Detail = detail,
                Registered = registered
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (ModbusException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Probe of unit {UnitId} failed; treating as absent.", unitId);
            return null;
        }
    }

    private static string Classify(IModbusDeviceChannel channel, out string? detail)
    {
        detail = null;

        try
        {
            var version = channel.ReadHoldingRegisters<ushort>(WaveshareAnalogOutputRegisters.SoftwareVersion, 1);
            detail = $"SW {version[0] / 100.0:0.00}";
            return nameof(WaveshareAnalogOutput8Ch);
        }
        catch (ModbusException)
        {
        }

        try
        {
            _ = channel.ReadHoldingRegisters<ushort>(Sequent16UOutRegisters.VoltageOutput1, Sequent16UOutRegisters.ChannelCount);
            try
            {
                _ = channel.ReadCoils(Sequent16UOutRegisters.Led1, Sequent16UOutRegisters.LedCount);
                detail = "16 AO + 16 LED";
            }
            catch (ModbusException)
            {
                detail = "16 AO";
            }

            return nameof(Sequent16UOut);
        }
        catch (ModbusException)
        {
        }

        try
        {
            _ = channel.ReadHoldingRegisters<ushort>(WaveshareAnalogOutputRegisters.Channel1, WaveshareAnalogOutputRegisters.ChannelCount);
            detail = "8 AO";
            return nameof(WaveshareAnalogOutput8Ch);
        }
        catch (ModbusException)
        {
        }

        return ModbusDetectedDeviceTypes.Unknown;
    }
}

public static class ModbusDetectedDeviceTypes
{
    public const string Unknown = "Unknown";
}
