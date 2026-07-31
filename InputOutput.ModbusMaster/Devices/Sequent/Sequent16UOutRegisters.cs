using System.IO.Ports;

namespace InputOutput.ModbusMaster.Devices.Sequent;

/// <summary>
/// Register map for Sequent Microsystems Sixteen 0–10 V Analog Outputs (16uout).
/// See https://github.com/SequentMicrosystems/16uout-rpi/blob/main/MODBUS.md
/// </summary>
public static class Sequent16UOutRegisters
{
    public const int ChannelCount = 16;

    public const int LedCount = 16;

    /// <summary>Holding register for CH1 voltage (mV); CH2–16 follow at +1 … +15.</summary>
    public const ushort VoltageOutput1 = 0x00;

    /// <summary>Coil for LED1; LED2–16 follow at +1 … +15.</summary>
    public const ushort Led1 = 0x00;

    public const ushort MaxMilliVolts = 10_000;

    /// <summary>Defaults matching Sequent RS-485 example config: 9600 8N1.</summary>
    public static ModbusRtuMasterOptions DefaultMasterOptions(string portName) => new()
    {
        PortName = portName,
        BaudRate = 9600,
        Parity = Parity.None,
        StopBits = StopBits.One
    };
}
