namespace InputOutput.ModbusMaster.Devices.Waveshare;

/// <summary>
/// Holding-register map for Waveshare Modbus RTU Analog Output 8CH (protocol V2).
/// </summary>
public static class WaveshareAnalogOutputRegisters
{
    public const int ChannelCount = 8;

    /// <summary>Channel 1 output (channels 2–8 follow at +1 … +7).</summary>
    public const ushort Channel1 = 0x0000;

    /// <summary>UART parameters: high byte = parity mode, low byte = baud mode.</summary>
    public const ushort UartParameter = 0x2000;

    /// <summary>Modbus device address (0x0001–0x00FF).</summary>
    public const ushort DeviceAddress = 0x4000;

    /// <summary>Software version; e.g. 0x0064 → V1.00.</summary>
    public const ushort SoftwareVersion = 0x8000;

    public const ushort MaxCurrentMicroAmps = 20_000;

    public const ushort MaxVoltageMilliVolts = 10_000;
}
