using FastEndpoints;
using InputOutput.ModbusMaster.Api.Models;
using InputOutput.ModbusMaster.Hosting;
using Microsoft.Extensions.Options;

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

public sealed class ListChannelsEndpoint(IModbusMaster master, IOptions<ModbusRtuHostOptions> hostOptions)
    : Endpoint<UnitIdRequest, IReadOnlyList<ChannelDto>>
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

        var host = hostOptions.Value;
        var channels = Enumerable.Range(1, device.ChannelCount)
            .Select(ch => AnalogDeviceAccess.ToDto(host, device, ch))
            .ToArray();

        await Send.OkAsync(channels, ct);
    }
}

public sealed class GetChannelEndpoint(IModbusMaster master, IOptions<ModbusRtuHostOptions> hostOptions)
    : Endpoint<UnitChannelRequest, ChannelDto>
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
            await Send.OkAsync(AnalogDeviceAccess.ToDto(hostOptions.Value, device, req.Channel), ct);
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

    /// <summary>Measurand setpoint (preferred). Converted via channel scaling.</summary>
    public double? Measurand { get; set; }

    /// <summary>Legacy electrical setpoint (V or mA).</summary>
    public double? Value { get; set; }

    public ushort? Raw { get; set; }

    public bool Flush { get; set; } = true;
}

public sealed class SetChannelEndpoint(IModbusMaster master, IOptions<ModbusRtuHostOptions> hostOptions)
    : Endpoint<SetChannelRequestDto, ChannelDto>
{
    public override void Configure()
    {
        Put("/api/units/{UnitId}/channels/{Channel}");
        AllowAnonymous();
        Summary(s => s.Summary = "Set a single channel. Provide measurand (preferred), value (electrical), or raw.");
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
            AnalogDeviceAccess.ApplySetpoint(
                hostOptions.Value,
                device,
                req.Channel,
                req.Measurand,
                req.Value,
                req.Raw);
            if (req.Flush)
            {
                await device.FlushAsync(master, ct).ConfigureAwait(false);
            }

            await Send.OkAsync(AnalogDeviceAccess.ToDto(hostOptions.Value, device, req.Channel), ct);
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

public sealed class SetChannelsEndpoint(IModbusMaster master, IOptions<ModbusRtuHostOptions> hostOptions)
    : Endpoint<SetChannelsRequestDto, IReadOnlyList<ChannelDto>>
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
            var host = hostOptions.Value;
            foreach (var setpoint in req.Channels)
            {
                AnalogDeviceAccess.ApplySetpoint(
                    host,
                    device,
                    setpoint.Channel,
                    setpoint.Measurand,
                    setpoint.Value,
                    setpoint.Raw);
            }

            if (req.Flush)
            {
                await device.FlushAsync(master, ct).ConfigureAwait(false);
            }

            await Send.OkAsync(
                req.Channels.Select(s => AnalogDeviceAccess.ToDto(host, device, s.Channel)).ToArray(),
                ct);
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
