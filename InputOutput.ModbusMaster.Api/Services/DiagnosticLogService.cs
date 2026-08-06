using System.Collections.Concurrent;

namespace InputOutput.ModbusMaster.Api.Services;

public sealed class DiagnosticLogEntry
{
    public required DateTimeOffset Timestamp { get; init; }

    public required LogLevel Level { get; init; }

    public required string Category { get; init; }

    public required string Message { get; init; }

    public string? Exception { get; init; }
}

/// <summary>In-memory ring buffer of recent application log entries for the UI Log page.</summary>
public sealed class DiagnosticLogService
{
    private const int Capacity = 500;
    private readonly ConcurrentQueue<DiagnosticLogEntry> _entries = new();
    private int _count;

    public void Add(DiagnosticLogEntry entry)
    {
        _entries.Enqueue(entry);
        if (Interlocked.Increment(ref _count) <= Capacity)
        {
            return;
        }

        if (_entries.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
        }
    }

    public IReadOnlyList<DiagnosticLogEntry> Snapshot() => _entries.ToArray();

    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
        }

        Interlocked.Exchange(ref _count, 0);
    }
}
