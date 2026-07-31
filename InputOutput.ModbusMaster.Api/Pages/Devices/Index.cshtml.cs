using InputOutput.ModbusMaster.Api.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InputOutput.ModbusMaster.Api.Pages.Devices;

public sealed class IndexModel(IModbusMaster master) : PageModel
{
    public IReadOnlyList<UnitDto> Units { get; private set; } = [];

    public bool IsConnected => master.IsConnected;

    public void OnGet()
    {
        var online = master.IsConnected;
        Units = master.RegisteredDevices
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
                    Online = online
                };
            })
            .ToArray();
    }
}
