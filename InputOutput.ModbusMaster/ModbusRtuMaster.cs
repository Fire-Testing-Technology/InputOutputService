using System.Collections.Concurrent;
using System.Diagnostics;
using FluentModbus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InputOutput.ModbusMaster;

/// <summary>
/// Modbus RTU master: one serial port, shared line settings, multiple devices registered by unit address.
/// </summary>
public sealed class ModbusRtuMaster : IModbusMaster
{
    private readonly ModbusRtuMasterOptions _options;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<byte, IModbusDevice> _devices = new();
    private readonly SemaphoreSlim _busLock = new(1, 1);
    private readonly object _lifecycleLock = new();

    private ModbusRtuClient? _client;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;
    private bool _disposed;

    public ModbusRtuMaster(ModbusRtuMasterOptions options, ILogger<ModbusRtuMaster>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.PortName))
        {
            throw new ArgumentException("PortName is required.", nameof(options));
        }

        _options = options;
        _logger = logger ?? NullLogger<ModbusRtuMaster>.Instance;
    }

    public event EventHandler<ModbusDevicePolledEventArgs>? DevicePolled;

    public event EventHandler<ModbusDevicePollFailedEventArgs>? DevicePollFailed;

    public ModbusRtuMasterOptions Options => _options;

    public bool IsConnected => _client?.IsConnected == true;

    public bool IsPolling => _pollTask is { IsCompleted: false };

    public IReadOnlyCollection<byte> RegisteredUnitIds => _devices.Keys.ToArray();

    public IReadOnlyCollection<IModbusDevice> RegisteredDevices => _devices.Values.ToArray();

    public void Register(byte unitId, Func<IModbusDeviceChannel, CancellationToken, ValueTask> pollAsync, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(pollAsync);
        Register(new CallbackModbusDevice(unitId, pollAsync, name));
    }

    public void Register(IModbusDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_devices.TryAdd(device.UnitId, device))
        {
            throw new InvalidOperationException($"A device with unit id {device.UnitId} is already registered.");
        }

        _logger.LogInformation("Registered Modbus device unit {UnitId} ({Name}).", device.UnitId, device.Name ?? "unnamed");
    }

    public bool Unregister(byte unitId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var removed = _devices.TryRemove(unitId, out var device);
        if (removed)
        {
            _logger.LogInformation("Unregistered Modbus device unit {UnitId} ({Name}).", unitId, device?.Name ?? "unnamed");
        }

        return removed;
    }

    public void Connect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lifecycleLock)
        {
            if (IsConnected)
            {
                return;
            }

            var client = new ModbusRtuClient
            {
                BaudRate = _options.BaudRate,
                Parity = _options.Parity,
                StopBits = _options.StopBits,
                Handshake = _options.Handshake,
                ReadTimeout = (int)_options.ReadTimeout.TotalMilliseconds,
                WriteTimeout = (int)_options.WriteTimeout.TotalMilliseconds
            };

            try
            {
                client.Connect(_options.PortName, _options.Endianness);
                _client = client;
                _logger.LogInformation(
                    "Modbus RTU master connected on {Port} at {Baud} baud, parity {Parity}.",
                    _options.PortName,
                    _options.BaudRate,
                    _options.Parity);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
    }

    public void Disconnect()
    {
        StopPollingAsync().GetAwaiter().GetResult();

        lock (_lifecycleLock)
        {
            if (_client is null)
            {
                return;
            }

            try
            {
                _client.Close();
            }
            finally
            {
                _client.Dispose();
                _client = null;
                _logger.LogInformation("Modbus RTU master disconnected.");
            }
        }
    }

    public Task StartPollingAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected)
        {
            throw new InvalidOperationException("Connect the master before starting polling.");
        }

        lock (_lifecycleLock)
        {
            if (IsPolling)
            {
                return Task.CompletedTask;
            }

            _pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pollTask = Task.Run(() => PollLoopAsync(_pollCts.Token), CancellationToken.None);
            _logger.LogInformation("Modbus RTU poll loop started ({DeviceCount} device(s)).", _devices.Count);
            return Task.CompletedTask;
        }
    }

    public async Task StopPollingAsync()
    {
        Task? pollTask;
        CancellationTokenSource? cts;

        lock (_lifecycleLock)
        {
            cts = _pollCts;
            pollTask = _pollTask;
            _pollCts = null;
            _pollTask = null;
        }

        if (cts is null)
        {
            return;
        }

        try
        {
            await cts.CancelAsync().ConfigureAwait(false);
            if (pollTask is not null)
            {
                try
                {
                    await pollTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // expected on stop
                }
            }
        }
        finally
        {
            cts.Dispose();
            _logger.LogInformation("Modbus RTU poll loop stopped.");
        }
    }

    public async Task ExecuteAsync(
        byte unitId,
        Func<IModbusDeviceChannel, CancellationToken, ValueTask> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var client = _client ?? throw new InvalidOperationException("The master is not connected.");

        await _busLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var channel = new ModbusDeviceChannel(client, unitId);
            await action(channel, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _busLock.Release();
        }
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var devices = _devices.Values.OrderBy(d => d.UnitId).ToArray();
            var client = _client;

            if (client is null || !client.IsConnected)
            {
                _logger.LogWarning("Poll loop exiting because the RTU client is not connected.");
                break;
            }

            for (var i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                cancellationToken.ThrowIfCancellationRequested();

                await _busLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var channel = new ModbusDeviceChannel(client, device.UnitId);
                    await device.PollAsync(channel, cancellationToken).ConfigureAwait(false);
                    stopwatch.Stop();
                    DevicePolled?.Invoke(this, new ModbusDevicePolledEventArgs(device.UnitId, device.Name, stopwatch.Elapsed));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogWarning(ex, "Poll failed for unit {UnitId} ({Name}).", device.UnitId, device.Name ?? "unnamed");
                    DevicePollFailed?.Invoke(this, new ModbusDevicePollFailedEventArgs(device.UnitId, device.Name, ex));
                }
                finally
                {
                    _busLock.Release();
                }

                if (i < devices.Length - 1 && _options.InterDeviceDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_options.InterDeviceDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            if (_options.PollInterval > TimeSpan.Zero)
            {
                await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Disconnect();
        _busLock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopPollingAsync().ConfigureAwait(false);

        lock (_lifecycleLock)
        {
            if (_client is not null)
            {
                try
                {
                    _client.Close();
                }
                finally
                {
                    _client.Dispose();
                    _client = null;
                }
            }
        }

        _busLock.Dispose();
    }
}
