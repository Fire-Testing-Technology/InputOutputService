using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using InputOutput.ModbusMaster.Api.Models;
using InputOutput.ModbusMaster.Api.Services;
using InputOutput.ModbusMaster.Scanning;

namespace InputOutput.ModbusMaster.Api.Endpoints.Scan;

public static class ScanApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IEndpointRouteBuilder MapScanApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/scan/start", StartAsync)
            .WithName("StartScan")
            .WithTags("Scan")
            .Produces(StatusCodes.Status202Accepted)
            .Produces<ApiError>(StatusCodes.Status409Conflict);

        endpoints.MapGet("/api/scan/progress", Progress)
            .WithName("ScanProgress")
            .WithTags("Scan")
            .Produces(StatusCodes.Status200OK);

        return endpoints;
    }

    private static IResult StartAsync(ScanProgressService progress)
    {
        if (!progress.TryStart(out var error))
        {
            return Results.Json(
                new ApiError { Message = error ?? "Scan already in progress." },
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Accepted("/api/scan/progress");
    }

    private static IResult Progress(ScanProgressService progress, int after = 0)
    {
        var snapshot = progress.GetSnapshot(after);
        // Serialize with camelCase so the Razor page can read properties directly.
        return Results.Json(snapshot, JsonOptions);
    }
}

public sealed class ScanStatusResponse
{
    public bool Scanning { get; init; }
}

public sealed class GetScanStatusEndpoint(IModbusBusScanner scanner, ScanProgressService progress)
    : EndpointWithoutRequest<ScanStatusResponse>
{
    public override void Configure()
    {
        Get("/api/scan/status");
        AllowAnonymous();
        Summary(s => s.Summary = "Returns whether a scan is currently running.");
    }

    public override Task HandleAsync(CancellationToken ct) =>
        Send.OkAsync(
            new ScanStatusResponse { Scanning = scanner.IsScanning || progress.IsRunning },
            ct);
}
