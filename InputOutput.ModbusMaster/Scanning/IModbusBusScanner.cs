namespace InputOutput.ModbusMaster.Scanning;

/// <summary>A unit that responded during an RS-485 Modbus RTU scan.</summary>
public sealed class ModbusDiscoveredUnit
{
    public required byte UnitId { get; init; }

    /// <summary><c>Sequent16UOut</c>, <c>WaveshareAnalogOutput8Ch</c>, or <c>Unknown</c>.</summary>
    public required string DetectedType { get; init; }

    /// <summary>Optional detail such as Waveshare software version.</summary>
    public string? Detail { get; init; }

    /// <summary>True when the unit was newly registered with the master as a result of this scan.</summary>
    public bool Registered { get; init; }
}

/// <summary>Outcome of a full unit-id range scan on the RTU bus.</summary>
public sealed class ModbusScanResult
{
    public required IReadOnlyList<ModbusDiscoveredUnit> Units { get; init; }

    public required TimeSpan Duration { get; init; }

    public required byte UnitIdFrom { get; init; }

    public required byte UnitIdTo { get; init; }
}

/// <summary>Progress / result events emitted while scanning the RS-485 bus.</summary>
public abstract record ModbusScanEvent(string EventName);

public sealed record ModbusScanStartedEvent(byte UnitIdFrom, byte UnitIdTo, string PortName)
    : ModbusScanEvent("started");

public sealed record ModbusScanProbingEvent(byte UnitId, int Index, int Total)
    : ModbusScanEvent("probing");

public sealed record ModbusScanDiscoveredEvent(ModbusDiscoveredUnit Unit)
    : ModbusScanEvent("discovered");

public sealed record ModbusScanCompletedEvent(ModbusScanResult Result)
    : ModbusScanEvent("completed");

public sealed record ModbusScanFailedEvent(string Message)
    : ModbusScanEvent("failed");

/// <summary>Scans Modbus unit addresses on the shared RS-485 line and optionally registers discoveries.</summary>
public interface IModbusBusScanner
{
    bool IsScanning { get; }

    /// <summary>
    /// Probes unit ids in the configured range. Pauses polling for the duration of the scan.
    /// </summary>
    Task<ModbusScanResult> ScanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="ScanAsync"/> but streams progress events (for SSE).
    /// </summary>
    IAsyncEnumerable<ModbusScanEvent> ScanStreamAsync(CancellationToken cancellationToken = default);
}
