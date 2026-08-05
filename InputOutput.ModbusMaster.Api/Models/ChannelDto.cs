namespace InputOutput.ModbusMaster.Api.Models;

public sealed class ChannelDto
{
    public int Channel { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Measurand unit of measure.</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>Current measurand derived from polled voltage via channel scaling.</summary>
    public double Measurand { get; init; }

    /// <summary>Last polled electrical value (V or mA on the wire).</summary>
    public double Voltage { get; init; }

    /// <summary><c>V</c> or <c>mA</c> for <see cref="Voltage"/>.</summary>
    public string ElectricalUnit { get; init; } = "V";

    /// <summary>Alias of <see cref="Voltage"/> for backward compatibility.</summary>
    public double Value { get; init; }

    public ushort Raw { get; init; }

    public double ZeroVoltage { get; init; }

    public double SpanVoltage { get; init; }

    public double ZeroMeasurand { get; init; }

    public double SpanMeasurand { get; init; }

    /// <summary>Sequent LED state when applicable.</summary>
    public bool? Led { get; init; }
}

public sealed class ChannelSetpointDto
{
    public int Channel { get; init; }

    /// <summary>Measurand setpoint (preferred). Converted to voltage via channel scaling.</summary>
    public double? Measurand { get; init; }

    /// <summary>Legacy electrical setpoint (V or mA).</summary>
    public double? Value { get; init; }

    public ushort? Raw { get; init; }
}
