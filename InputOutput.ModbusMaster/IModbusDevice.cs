namespace InputOutput.ModbusMaster;

/// <summary>
/// A device registered with a Modbus master. Devices share the master's connection settings
/// and are distinguished by <see cref="UnitId"/>.
/// </summary>
public interface IModbusDevice
{
    /// <summary>Modbus unit / slave address (1–247 typical; 0 is broadcast).</summary>
    byte UnitId { get; }

    /// <summary>Optional display name for logging and diagnostics.</summary>
    string? Name { get; }

    /// <summary>
    /// Invoked by the master during each poll cycle with exclusive access to the serial bus.
    /// </summary>
    ValueTask PollAsync(IModbusDeviceChannel channel, CancellationToken cancellationToken);
}
