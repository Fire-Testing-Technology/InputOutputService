using InputOutput.ModbusMaster.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InputOutput.ModbusMaster.Api.Pages.Devices;

public sealed class DetailModel(IModbusMaster master) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int UnitId { get; set; }

    public UnitDto? Unit { get; private set; }

    public IReadOnlyList<ChannelDto> Channels { get; private set; } = [];

    public string? LoadError { get; private set; }

    public IActionResult OnGet()
    {
        if (!TryLoad())
        {
            return Page();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSetAsync(int channel, double value, CancellationToken cancellationToken)
    {
        if (!AnalogDeviceAccess.TryResolve(master, UnitId, out var device, out _, out var message))
        {
            TempData["Error"] = message;
            return RedirectToPage(new { unitId = UnitId });
        }

        try
        {
            device.SetChannelValue(channel, value);
            await device.FlushAsync(master, cancellationToken).ConfigureAwait(false);
            TempData["Message"] = $"Channel {channel} set to {value} {EngineeringLabel(device)}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage(new { unitId = UnitId });
    }

    public async Task<IActionResult> OnPostSetAllAsync(double[] values, CancellationToken cancellationToken)
    {
        if (!AnalogDeviceAccess.TryResolve(master, UnitId, out var device, out _, out var message))
        {
            TempData["Error"] = message;
            return RedirectToPage(new { unitId = UnitId });
        }

        try
        {
            if (values.Length != device.ChannelCount)
            {
                TempData["Error"] = $"Expected {device.ChannelCount} values.";
                return RedirectToPage(new { unitId = UnitId });
            }

            for (var i = 0; i < values.Length; i++)
            {
                device.SetChannelValue(i + 1, values[i]);
            }

            await device.FlushAsync(master, cancellationToken).ConfigureAwait(false);
            TempData["Message"] = "All channels updated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage(new { unitId = UnitId });
    }

    private bool TryLoad()
    {
        if (!AnalogDeviceAccess.TryResolve(master, UnitId, out var device, out _, out var message))
        {
            LoadError = message;
            return false;
        }

        var identity = device as IModbusDeviceIdentity;
        Unit = new UnitDto
        {
            UnitId = device.UnitId,
            Type = identity?.DeviceType ?? device.GetType().Name,
            Name = device.Name,
            Identifier = identity?.Identifier,
            SerialNumber = identity?.SerialNumber,
            Online = master.IsConnected
        };

        Channels = Enumerable.Range(1, device.ChannelCount)
            .Select(ch => AnalogDeviceAccess.ToDto(device, ch))
            .ToArray();
        return true;
    }

    private static string EngineeringLabel(IAnalogOutputDevice device) =>
        device.EngineeringUnit == AnalogOutputEngineeringUnit.Volts ? "V" : "mA";
}
