using FastEndpoints;
using InputOutput.ModbusMaster.Api.Models;

namespace InputOutput.ModbusMaster.Api.Endpoints.Channels;

public sealed class UnitIdRequest
{
    public int UnitId { get; set; }
}

public sealed class UnitChannelRequest
{
    public int UnitId { get; set; }

    public int Channel { get; set; }
}

public sealed class ListChannelsEndpoint(IModbusMaster master) : Endpoint<UnitIdRequest, IReadOnlyList<ChannelDto>>
{
    public override void Configure()
    {
        Get("/api/units/{UnitId}/channels");
        AllowAnonymous();
        Summary(s => s.Summary = "Get all analog output channels for a unit (last polled values).");
    }

    public override async Task HandleAsync(UnitIdRequest req, CancellationToken ct)
    {
        if (!AnalogDeviceAccess.TryResolve(master, req.UnitId, out var device, out var status, out var message))
        {
            await HttpContext.Response.SendAsync(new ApiError { Message = message }, status, cancellation: ct);
            return;
        }

        var channels = Enumerable.Range(1, device.ChannelCount)
            .Select(ch => AnalogDeviceAccess.ToDto(device, ch))
            .ToArray();

        await Send.OkAsync(channels, ct);
    }
}

public sealed class GetChannelEndpoint(IModbusMaster master) : Endpoint<UnitChannelRequest, ChannelDto>
{
    public override void Configure()
    {
        Get("/api/units/{UnitId}/channels/{Channel}");
        AllowAnonymous();
        Summary(s => s.Summary = "Get a single channel (1-based).");
    }

    public override async Task HandleAsync(UnitChannelRequest req, CancellationToken ct)
    {
        if (!AnalogDeviceAccess.TryResolve(master, req.UnitId, out var device, out var status, out var message))
        {
            await HttpContext.Response.SendAsync(new ApiError { Message = message }, status, cancellation: ct);
            return;
        }

        try
        {
            await Send.OkAsync(AnalogDeviceAccess.ToDto(device, req.Channel), ct);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            await HttpContext.Response.SendAsync(
                new ApiError { Message = ex.Message },
                StatusCodes.Status400BadRequest,
                cancellation: ct);
        }
    }
}

public sealed class SetChannelRequestDto
{
    public int UnitId { get; set; }

    public int Channel { get; set; }

    public double? Value { get; set; }

    public ushort? Raw { get; set; }

    public bool Flush { get; set; } = true;
}

public sealed class SetChannelEndpoint(IModbusMaster master) : Endpoint<SetChannelRequestDto, ChannelDto>
{
    public override void Configure()
    {
        Put("/api/units/{UnitId}/channels/{Channel}");
        AllowAnonymous();
        Summary(s => s.Summary = "Set a single channel. Provide either value (engineering) or raw.");
    }

    public override async Task HandleAsync(SetChannelRequestDto req, CancellationToken ct)
    {
        if (!AnalogDeviceAccess.TryResolve(master, req.UnitId, out var device, out var status, out var message))
        {
            await HttpContext.Response.SendAsync(new ApiError { Message = message }, status, cancellation: ct);
            return;
        }

        try
        {
            AnalogDeviceAccess.ApplySetpoint(device, req.Channel, req.Value, req.Raw);
            if (req.Flush)
            {
                await device.FlushAsync(master, ct).ConfigureAwait(false);
            }

            await Send.OkAsync(AnalogDeviceAccess.ToDto(device, req.Channel), ct);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            await HttpContext.Response.SendAsync(
                new ApiError { Message = ex.Message },
                StatusCodes.Status400BadRequest,
                cancellation: ct);
        }
        catch (InvalidOperationException ex)
        {
            await HttpContext.Response.SendAsync(
                new ApiError { Message = ex.Message },
                StatusCodes.Status400BadRequest,
                cancellation: ct);
        }
    }
}

public sealed class SetChannelsRequestDto
{
    public int UnitId { get; set; }

    public required IReadOnlyList<ChannelSetpointDto> Channels { get; set; }

    public bool Flush { get; set; } = true;
}

public sealed class SetChannelsEndpoint(IModbusMaster master) : Endpoint<SetChannelsRequestDto, IReadOnlyList<ChannelDto>>
{
    public override void Configure()
    {
        Put("/api/units/{UnitId}/channels");
        AllowAnonymous();
        Summary(s => s.Summary = "Set multiple channels in one request.");
    }

    public override async Task HandleAsync(SetChannelsRequestDto req, CancellationToken ct)
    {
        if (!AnalogDeviceAccess.TryResolve(master, req.UnitId, out var device, out var status, out var message))
        {
            await HttpContext.Response.SendAsync(new ApiError { Message = message }, status, cancellation: ct);
            return;
        }

        if (req.Channels is null || req.Channels.Count == 0)
        {
            await HttpContext.Response.SendAsync(
                new ApiError { Message = "Provide at least one channel setpoint." },
                StatusCodes.Status400BadRequest,
                cancellation: ct);
            return;
        }

        try
        {
            foreach (var setpoint in req.Channels)
            {
                AnalogDeviceAccess.ApplySetpoint(device, setpoint.Channel, setpoint.Value, setpoint.Raw);
            }

            if (req.Flush)
            {
                await device.FlushAsync(master, ct).ConfigureAwait(false);
            }

            await Send.OkAsync(req.Channels.Select(s => AnalogDeviceAccess.ToDto(device, s.Channel)).ToArray(), ct);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            await HttpContext.Response.SendAsync(
                new ApiError { Message = ex.Message },
                StatusCodes.Status400BadRequest,
                cancellation: ct);
        }
        catch (InvalidOperationException ex)
        {
            await HttpContext.Response.SendAsync(
                new ApiError { Message = ex.Message },
                StatusCodes.Status400BadRequest,
                cancellation: ct);
        }
    }
}
