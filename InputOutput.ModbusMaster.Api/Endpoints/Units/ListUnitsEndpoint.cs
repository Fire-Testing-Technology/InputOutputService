using FastEndpoints;
using InputOutput.ModbusMaster.Api.Models;

namespace InputOutput.ModbusMaster.Api.Endpoints.Units;

public sealed class ListUnitsEndpoint(IModbusMaster master) : EndpointWithoutRequest<IReadOnlyList<UnitDto>>
{
    public override void Configure()
    {
        Get("/api/units");
        AllowAnonymous();
        Summary(s => s.Summary = "List configured Modbus units with type, name, identifier, and serial number.");
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var units = master.RegisteredDevices
            .OrderBy(d => d.UnitId)
            .Select(d =>
            {
                var identity = d as IModbusDeviceIdentity;
                return new UnitDto
                {
                    UnitId = d.UnitId,
                    Type = identity?.DeviceType ?? d.GetType().Name,
                    Name = d.Name,
                    Identifier = identity?.Identifier,
                    SerialNumber = identity?.SerialNumber,
                    Online = master.IsUnitOnline(d.UnitId)
                };
            })
            .ToArray();

        return Send.OkAsync(units, ct);
    }
}
