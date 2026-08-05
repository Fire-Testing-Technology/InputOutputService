using System.IO.Ports;
using FluentModbus;

namespace InputOutput.ModbusMaster.Hosting;

/// <summary>
/// Bindable configuration for the RTU master and its registered devices (appsettings section "ModbusRtu").
/// </summary>
public sealed class ModbusRtuHostOptions
{
    public const string SectionName = "ModbusRtu";

    public string PortName { get; set; } = "COM1";

    public int BaudRate { get; set; } = 9600;

    public Parity Parity { get; set; } = Parity.None;

    public StopBits StopBits { get; set; } = StopBits.One;

    public Handshake Handshake { get; set; } = Handshake.None;

    public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromSeconds(1);

    public ModbusEndianness Endianness { get; set; } = ModbusEndianness.BigEndian;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan InterDeviceDelay { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>When false, the hosted service skips Connect/StartPolling (API still lists configured units).</summary>
    public bool AutoConnect { get; set; } = true;

    /// <summary>When true (and connected), run an RS-485 unit-id scan after startup registration.</summary>
    public bool AutoScan { get; set; } = true;

    /// <summary>Inclusive start of the unit-id probe range (1–247).</summary>
    public byte ScanUnitIdFrom { get; set; } = 1;

    /// <summary>Inclusive end of the unit-id probe range (1–247).</summary>
    public byte ScanUnitIdTo { get; set; } = 32;

    /// <summary>Quiet time between consecutive unit probes during a scan.</summary>
    public TimeSpan ScanInterProbeDelay { get; set; } = TimeSpan.FromMilliseconds(20);

    /// <summary>When true, discovered known device types are registered if not already present.</summary>
    public bool RegisterDiscoveredDevices { get; set; } = true;

    public List<ModbusDeviceRegistrationOptions> Devices { get; set; } = [];

    public ModbusRtuMasterOptions ToMasterOptions() => new()
    {
        PortName = PortName,
        BaudRate = BaudRate,
        Parity = Parity,
        StopBits = StopBits,
        Handshake = Handshake,
        ReadTimeout = ReadTimeout,
        WriteTimeout = WriteTimeout,
        Endianness = Endianness,
        PollInterval = PollInterval,
        InterDeviceDelay = InterDeviceDelay
    };
}

/// <summary>One device entry under <see cref="ModbusRtuHostOptions.Devices"/>.</summary>
public sealed class ModbusDeviceRegistrationOptions
{
    /// <summary><c>Sequent16UOut</c> or <c>WaveshareAnalogOutput8Ch</c>.</summary>
    public string Type { get; set; } = string.Empty;

    public byte UnitId { get; set; }

    public string? Name { get; set; }

    public string? Identifier { get; set; }

    public string? SerialNumber { get; set; }

    /// <summary>Waveshare only: <c>Current0To20mA</c> or <c>Voltage0To10V</c>.</summary>
    public string? Mode { get; set; }

    /// <summary>Optional per-channel name and measurand↔voltage scaling.</summary>
    public List<ModbusChannelOptions> Channels { get; set; } = [];
}

/// <summary>One channel entry under <see cref="ModbusDeviceRegistrationOptions.Channels"/>.</summary>
public sealed class ModbusChannelOptions
{
    /// <summary>1-based channel index on the device.</summary>
    public int Channel { get; set; }

    public string? Name { get; set; }

    /// <summary>Measurand unit of measure (e.g. <c>°C</c>, <c>Pa</c>, <c>%</c>, <c>V</c>).</summary>
    public string Unit { get; set; } = "V";

    public double ZeroVoltage { get; set; }

    public double SpanVoltage { get; set; } = 10;

    public double ZeroMeasurand { get; set; }

    public double SpanMeasurand { get; set; } = 10;
}
