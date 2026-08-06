namespace InputOutput.ModbusMaster.Api.Models;

/// <summary>A configured Modbus unit exposed by the master.</summary>
public sealed class UnitDto
{
    public byte UnitId { get; init; }

    public string Type { get; init; } = string.Empty;

    public string? Name { get; init; }

    public string? Identifier { get; init; }

    public string? SerialNumber { get; init; }

    /// <summary>True when the unit has recently responded on the bus (scan or poll).</summary>
    public bool Online { get; init; }
}
