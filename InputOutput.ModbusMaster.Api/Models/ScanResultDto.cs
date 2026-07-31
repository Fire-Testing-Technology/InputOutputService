namespace InputOutput.ModbusMaster.Api.Models;

public sealed class DiscoveredUnitDto
{
    public byte UnitId { get; init; }

    public string DetectedType { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public bool Registered { get; init; }
}

public sealed class ScanResultDto
{
    public IReadOnlyList<DiscoveredUnitDto> Units { get; init; } = [];

    public double DurationMilliseconds { get; init; }

    public byte UnitIdFrom { get; init; }

    public byte UnitIdTo { get; init; }
}
