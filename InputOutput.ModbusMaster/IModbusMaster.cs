namespace InputOutput.ModbusMaster;

/// <summary>
/// Polls multiple Modbus devices that share one connection. Devices are registered by unit
/// address; connection settings are owned by the master.
/// </summary>
public interface IModbusMaster : IAsyncDisposable, IDisposable
{
    /// <summary>Live connection settings used on the next <see cref="Connect"/>.</summary>
    ModbusRtuMasterOptions Options { get; }

    bool IsConnected { get; }

    bool IsPolling { get; }

    IReadOnlyCollection<byte> RegisteredUnitIds { get; }

    /// <summary>Currently registered devices (including identity metadata when implemented).</summary>
    IReadOnlyCollection<IModbusDevice> RegisteredDevices { get; }

    /// <summary>Register a device by unit address and poll callback.</summary>
    void Register(byte unitId, Func<IModbusDeviceChannel, CancellationToken, ValueTask> pollAsync, string? name = null);

    /// <summary>Register a device implementation.</summary>
    void Register(IModbusDevice device);

    /// <summary>Remove a previously registered device. Returns false if it was not registered.</summary>
    bool Unregister(byte unitId);

    void Connect();

    void Disconnect();

    /// <summary>Start the background poll loop. Requires an open connection.</summary>
    Task StartPollingAsync(CancellationToken cancellationToken = default);

    Task StopPollingAsync();

    /// <summary>
    /// Run an ad-hoc transaction with exclusive bus access (e.g. a write between poll cycles).
    /// </summary>
    Task ExecuteAsync(
        byte unitId,
        Func<IModbusDeviceChannel, CancellationToken, ValueTask> action,
        CancellationToken cancellationToken = default);

    event EventHandler<ModbusDevicePolledEventArgs>? DevicePolled;

    event EventHandler<ModbusDevicePollFailedEventArgs>? DevicePollFailed;
}
