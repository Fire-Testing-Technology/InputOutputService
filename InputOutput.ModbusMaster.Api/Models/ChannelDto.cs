namespace InputOutput.ModbusMaster.Api.Models;

public sealed class ChannelDto
{
    public int Channel { get; init; }

    public ushort Raw { get; init; }

    /// <summary>Engineering value in <see cref="Unit"/> (volts or milliamps).</summary>
    public double Value { get; init; }

    /// <summary><c>V</c> or <c>mA</c>.</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>Sequent LED state when applicable.</summary>
    public bool? Led { get; init; }
}

public sealed class ChannelSetpointDto
{
    public int Channel { get; init; }

    public double? Value { get; init; }

    public ushort? Raw { get; init; }
}
