using InputOutput.ModbusMaster.Api.Models;
using InputOutput.ModbusMaster.Api.Services;
using InputOutput.ModbusMaster.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InputOutput.ModbusMaster.Api.Pages.Devices;

public sealed class IndexModel(IModbusMaster master) : PageModel
{
    public IReadOnlyList<UnitDto> Units { get; private set; } = [];

    public bool IsConnected => master.IsConnected;

    public string PortName => master.Options.PortName;

    /// <summary>False when the configured serial port is not currently present on this machine.</summary>
    public bool PortAvailable { get; private set; } = true;

    public IReadOnlyList<string> AvailablePorts { get; private set; } = [];

    public void OnGet()
    {
        AvailablePorts = SerialPortAvailability.GetAvailablePorts()
            .OrderBy(p => p, NaturalStringComparer.Instance)
            .ToArray();
        PortAvailable = SerialPortAvailability.IsAvailable(PortName, AvailablePorts);

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
                    Online = master.IsUnitOnline(d.UnitId)
                };
            })
            .ToArray();
    }
}
