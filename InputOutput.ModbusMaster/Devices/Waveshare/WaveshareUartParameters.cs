using System.IO.Ports;

namespace InputOutput.ModbusMaster.Devices.Waveshare;

/// <summary>
/// Baud / parity encoding for register 0x2000 (UART Parameter).
/// </summary>
public readonly record struct WaveshareUartParameters(WaveshareBaudRate BaudRate, WaveshareParity Parity)
{
    public ushort ToRegisterValue() => (ushort)(((byte)Parity << 8) | (byte)BaudRate);

    public static WaveshareUartParameters FromRegisterValue(ushort value) =>
        new((WaveshareBaudRate)(value & 0xFF), (WaveshareParity)((value >> 8) & 0xFF));

    /// <summary>Factory defaults used by Waveshare docs / Modbus Poll examples: 9600 8N1.</summary>
    public static WaveshareUartParameters Default { get; } = new(WaveshareBaudRate.Baud9600, WaveshareParity.None);

    public ModbusRtuMasterOptions ToMasterOptions(string portName) => new()
    {
        PortName = portName,
        BaudRate = BaudRate.ToBaudRate(),
        Parity = Parity.ToParity(),
        StopBits = StopBits.One
    };
}

public enum WaveshareBaudRate : byte
{
    Baud4800 = 0x00,
    Baud9600 = 0x01,
    Baud19200 = 0x02,
    Baud38400 = 0x03,
    Baud57600 = 0x04,
    Baud115200 = 0x05,
    Baud128000 = 0x06,
    Baud256000 = 0x07
}

public enum WaveshareParity : byte
{
    None = 0x00,
    Even = 0x01,
    Odd = 0x02
}

public static class WaveshareUartExtensions
{
    public static int ToBaudRate(this WaveshareBaudRate baud) => baud switch
    {
        WaveshareBaudRate.Baud4800 => 4800,
        WaveshareBaudRate.Baud9600 => 9600,
        WaveshareBaudRate.Baud19200 => 19200,
        WaveshareBaudRate.Baud38400 => 38400,
        WaveshareBaudRate.Baud57600 => 57600,
        WaveshareBaudRate.Baud115200 => 115200,
        WaveshareBaudRate.Baud128000 => 128000,
        WaveshareBaudRate.Baud256000 => 256000,
        _ => throw new ArgumentOutOfRangeException(nameof(baud), baud, null)
    };

    /// <remarks>
    /// Wiki send-table lists Even=0x01 / Odd=0x02; the echo table swaps those labels.
    /// Encoding follows the send command description.
    /// </remarks>
    public static Parity ToParity(this WaveshareParity parity) => parity switch
    {
        WaveshareParity.None => Parity.None,
        WaveshareParity.Even => Parity.Even,
        WaveshareParity.Odd => Parity.Odd,
        _ => throw new ArgumentOutOfRangeException(nameof(parity), parity, null)
    };
}
