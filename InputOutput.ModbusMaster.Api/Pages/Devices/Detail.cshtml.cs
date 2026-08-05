using System.ComponentModel.DataAnnotations;
using InputOutput.ModbusMaster.Api.Models;
using InputOutput.ModbusMaster.Api.Services;
using InputOutput.ModbusMaster.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InputOutput.ModbusMaster.Api.Pages.Devices;

public sealed class DetailModel(IModbusMaster master, ModbusConfigStore configStore) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int UnitId { get; set; }

    public UnitDto? Unit { get; private set; }

    public IReadOnlyList<ChannelDto> Channels { get; private set; } = [];

    [BindProperty]
    public List<ChannelConfigInput> ChannelConfigs { get; set; } = [];

    public string? LoadError { get; private set; }

    public IActionResult OnGet()
    {
        if (!TryLoad())
        {
            return Page();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveChannelConfigAsync(CancellationToken cancellationToken)
    {
        if (!AnalogDeviceAccess.TryResolve(master, UnitId, out var device, out _, out var message))
        {
            TempData["Error"] = message;
            return RedirectToPage(new { unitId = UnitId });
        }

        if (!ModelState.IsValid)
        {
            TryLoad();
            return Page();
        }

        try
        {
            EnsureDeviceRegistration(device);
            var registration = configStore.Host.Devices.First(d => d.UnitId == device.UnitId);
            registration.Channels = ChannelConfigs
                .Where(c => c.Channel is >= 1 && c.Channel <= device.ChannelCount)
                .Select(c => new ModbusChannelOptions
                {
                    Channel = c.Channel,
                    Name = string.IsNullOrWhiteSpace(c.Name) ? $"Channel {c.Channel}" : c.Name.Trim(),
                    Unit = string.IsNullOrWhiteSpace(c.Unit) ? "V" : c.Unit.Trim(),
                    ZeroVoltage = c.ZeroVoltage,
                    SpanVoltage = c.SpanVoltage,
                    ZeroMeasurand = c.ZeroMeasurand,
                    SpanMeasurand = c.SpanMeasurand
                })
                .OrderBy(c => c.Channel)
                .ToList();

            await configStore.SaveAsync(cancellationToken).ConfigureAwait(false);
            TempData["Message"] = "Channel configuration saved.";
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

            var host = configStore.Host;
            for (var i = 0; i < values.Length; i++)
            {
                AnalogDeviceAccess.ApplySetpoint(host, device, i + 1, values[i], value: null, raw: null);
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

        var host = configStore.Host;
        Channels = Enumerable.Range(1, device.ChannelCount)
            .Select(ch => AnalogDeviceAccess.ToDto(host, device, ch))
            .ToArray();

        ChannelConfigs = Channels.Select(ch => new ChannelConfigInput
        {
            Channel = ch.Channel,
            Name = ch.Name,
            Unit = ch.Unit,
            ZeroVoltage = ch.ZeroVoltage,
            SpanVoltage = ch.SpanVoltage,
            ZeroMeasurand = ch.ZeroMeasurand,
            SpanMeasurand = ch.SpanMeasurand
        }).ToList();

        return true;
    }

    private void EnsureDeviceRegistration(IAnalogOutputDevice device)
    {
        var host = configStore.Host;
        if (host.Devices.Any(d => d.UnitId == device.UnitId))
        {
            return;
        }

        var identity = device as IModbusDeviceIdentity;
        host.Devices.Add(new ModbusDeviceRegistrationOptions
        {
            Type = identity?.DeviceType ?? device.GetType().Name,
            UnitId = device.UnitId,
            Name = device.Name,
            Identifier = identity?.Identifier,
            SerialNumber = identity?.SerialNumber,
            Channels = []
        });
    }

    public sealed class ChannelConfigInput
    {
        [Range(1, 255)]
        public int Channel { get; set; }

        [StringLength(128)]
        public string? Name { get; set; }

        [Required, StringLength(32)]
        public string Unit { get; set; } = "V";

        public double ZeroVoltage { get; set; }

        public double SpanVoltage { get; set; } = 10;

        public double ZeroMeasurand { get; set; }

        public double SpanMeasurand { get; set; } = 10;
    }
}
