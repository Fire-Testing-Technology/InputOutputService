namespace InputOutput.ModbusMaster;

/// <summary>
/// Convenience device that wraps a unit address and poll callback.
/// </summary>
public sealed class CallbackModbusDevice(
    byte unitId,
    Func<IModbusDeviceChannel, CancellationToken, ValueTask> pollAsync,
    string? name = null) : IModbusDevice
{
    public byte UnitId { get; } = unitId;

    public string? Name { get; } = name;

    public ValueTask PollAsync(IModbusDeviceChannel channel, CancellationToken cancellationToken) =>
        pollAsync(channel, cancellationToken);
}
