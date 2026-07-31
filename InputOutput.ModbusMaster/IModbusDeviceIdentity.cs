namespace InputOutput.ModbusMaster;

/// <summary>
/// Optional identity metadata for a registered Modbus device (type, identifier, serial).
/// </summary>
public interface IModbusDeviceIdentity
{
    /// <summary>Stable type key, e.g. <c>Sequent16UOut</c> or <c>WaveshareAnalogOutput8Ch</c>.</summary>
    string DeviceType { get; }

    /// <summary>Optional application identifier from configuration.</summary>
    string? Identifier { get; }

    /// <summary>Optional serial number from configuration (hardware serial is not exposed by these modules).</summary>
    string? SerialNumber { get; }
}
