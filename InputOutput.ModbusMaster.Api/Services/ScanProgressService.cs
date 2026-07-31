using InputOutput.ModbusMaster.Scanning;

namespace InputOutput.ModbusMaster.Api.Services;

public sealed class ScanUiEvent
{
    public required string Type { get; init; }

    public required object Data { get; init; }
}

public sealed class ScanProgressSnapshot
{
    public required bool Running { get; init; }

    public required bool Done { get; init; }

    public required int Next { get; init; }

    public required IReadOnlyList<ScanUiEvent> Events { get; init; }

    public string? Error { get; init; }
}

/// <summary>
/// Holds live scan events for the Razor UI. Browser streaming is unreliable here,
/// so the page polls <see cref="GetSnapshot"/> instead of consuming SSE.
/// </summary>
public sealed class ScanProgressService(IModbusBusScanner scanner, ILogger<ScanProgressService> logger)
{
    private readonly object _gate = new();
    private readonly List<ScanUiEvent> _events = [];
    private CancellationTokenSource? _cts;
    private Task? _run;
    private bool _running;
    private bool _done;
    private string? _error;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    public bool TryStart(out string? error)
    {
        lock (_gate)
        {
            if (_running || scanner.IsScanning)
            {
                error = "An RS-485 scan is already in progress.";
                return false;
            }

            _events.Clear();
            _running = true;
            _done = false;
            _error = null;
            _cts = new CancellationTokenSource();
            _run = Task.Run(() => RunAsync(_cts.Token));
            error = null;
            return true;
        }
    }

    public ScanProgressSnapshot GetSnapshot(int after)
    {
        lock (_gate)
        {
            if (after < 0)
            {
                after = 0;
            }

            if (after > _events.Count)
            {
                after = _events.Count;
            }

            return new ScanProgressSnapshot
            {
                Running = _running,
                Done = _done,
                Next = _events.Count,
                Events = _events.Skip(after).ToArray(),
                Error = _error
            };
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var evt in scanner.ScanStreamAsync(ct).ConfigureAwait(false))
            {
                Append(Map(evt));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Append(new ScanUiEvent { Type = "failed", Data = new { message = "Scan cancelled." } });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background RS-485 scan failed.");
            Append(new ScanUiEvent { Type = "failed", Data = new { message = ex.Message } });
            lock (_gate)
            {
                _error = ex.Message;
            }
        }
        finally
        {
            lock (_gate)
            {
                _running = false;
                _done = true;
                _cts?.Dispose();
                _cts = null;
            }
        }
    }

    private void Append(ScanUiEvent evt)
    {
        lock (_gate)
        {
            _events.Add(evt);
        }
    }

    private static ScanUiEvent Map(ModbusScanEvent evt) => evt switch
    {
        ModbusScanStartedEvent s => new ScanUiEvent
        {
            Type = "started",
            Data = new
            {
                unitIdFrom = s.UnitIdFrom,
                unitIdTo = s.UnitIdTo,
                portName = s.PortName
            }
        },
        ModbusScanProbingEvent p => new ScanUiEvent
        {
            Type = "probing",
            Data = new
            {
                unitId = p.UnitId,
                index = p.Index,
                total = p.Total,
                percent = Math.Round(100.0 * p.Index / p.Total, 1)
            }
        },
        ModbusScanDiscoveredEvent d => new ScanUiEvent
        {
            Type = "discovered",
            Data = new
            {
                unitId = d.Unit.UnitId,
                detectedType = d.Unit.DetectedType,
                detail = d.Unit.Detail,
                registered = d.Unit.Registered
            }
        },
        ModbusScanCompletedEvent c => new ScanUiEvent
        {
            Type = "completed",
            Data = new
            {
                units = c.Result.Units.Select(u => new
                {
                    unitId = u.UnitId,
                    detectedType = u.DetectedType,
                    detail = u.Detail,
                    registered = u.Registered
                }).ToArray(),
                durationMilliseconds = c.Result.Duration.TotalMilliseconds,
                unitIdFrom = c.Result.UnitIdFrom,
                unitIdTo = c.Result.UnitIdTo
            }
        },
        ModbusScanFailedEvent f => new ScanUiEvent
        {
            Type = "failed",
            Data = new { message = f.Message }
        },
        _ => new ScanUiEvent
        {
            Type = "failed",
            Data = new { message = $"Unexpected event: {evt.EventName}" }
        }
    };
}
