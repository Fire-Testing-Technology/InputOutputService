using System.IO.Ports;
using FluentModbus;

namespace InputOutput.ModbusMaster;

/// <summary>
/// Shared serial-line settings for an RTU master. All registered devices use these settings;
/// only the Modbus unit/slave address differs per device.
/// </summary>
public sealed class ModbusRtuMasterOptions
{
    /// <summary>COM port name, e.g. COM3.</summary>
    public string PortName { get; set; } = "COM1";

    public int BaudRate { get; set; } = 9600;

    /// <summary>Default is Even per the Modbus serial-line specification.</summary>
    public Parity Parity { get; set; } = Parity.Even;

    public StopBits StopBits { get; set; } = StopBits.One;

    public Handshake Handshake { get; set; } = Handshake.None;

    public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromSeconds(1);

    public ModbusEndianness Endianness { get; set; } = ModbusEndianness.BigEndian;

    /// <summary>Delay between complete poll cycles of all registered devices.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Optional quiet time between consecutive device transactions on the bus.</summary>
    public TimeSpan InterDeviceDelay { get; set; } = TimeSpan.FromMilliseconds(50);
}
