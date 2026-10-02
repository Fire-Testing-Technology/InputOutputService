using System.ComponentModel.DataAnnotations;
using System.IO.Ports;
using InputOutput.ModbusMaster.Api.Services;
using InputOutput.ModbusMaster.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InputOutput.ModbusMaster.Api.Pages.Configure;

public sealed class IndexModel(IModbusMaster master, ModbusConfigStore configStore) : PageModel
{
    [BindProperty]
    public ConfigureInput Input { get; set; } = new();

    public bool IsConnected => master.IsConnected;

    public bool IsPolling => master.IsPolling;

    public IReadOnlyList<SelectListItem> DeviceTypeOptions { get; } =
    [
        new("Sequent 16× 0–10 V (16 Out)", "16 Out"),
        new("Waveshare 8CH (8 Out)", "8 Out")
    ];

    public IReadOnlyList<SelectListItem> ModeOptions { get; } =
    [
        new("Voltage 0–10 V", "Voltage0To10V"),
        new("Current 0–20 mA", "Current0To20mA")
    ];

    public IReadOnlyList<SelectListItem> ParityOptions { get; } =
        Enum.GetValues<Parity>().Select(p => new SelectListItem(p.ToString(), p.ToString())).ToArray();

    public IReadOnlyList<SelectListItem> StopBitsOptions { get; } =
        Enum.GetValues<StopBits>().Select(s => new SelectListItem(s.ToString(), s.ToString())).ToArray();

    public IReadOnlyList<SelectListItem> PortOptions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> BaudRateOptions { get; private set; } = [];

    private static readonly int[] StandardBaudRates =
    [
        1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200, 128000, 256000, 460800, 921600
    ];

    public void OnGet()
    {
        LoadFromStore();
        LoadSelectOptions();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            LoadSelectOptions();
            return Page();
        }

        ApplyInputToHost();
        configStore.ApplyConnectionSettingsToMaster();
        configStore.SyncDevicesFromHost();
        await configStore.SaveAsync(cancellationToken).ConfigureAwait(false);
        TempData["Message"] = "Configuration saved to appsettings.json and applied to the live device list.";
        return RedirectToPage();
    }

    public IActionResult OnPostApply()
    {
        if (!ModelState.IsValid)
        {
            LoadSelectOptions();
            return Page();
        }

        ApplyInputToHost();
        configStore.ApplyConnectionSettingsToMaster();
        configStore.SyncDevicesFromHost();
        TempData["Message"] = "Live settings applied (not written to disk).";
        return RedirectToPage();
    }

    public IActionResult OnPostConnect()
    {
        configStore.ApplyConnectionSettingsToMaster();
        try
        {
            if (master.IsConnected)
            {
                master.Disconnect();
            }

            master.Connect();
            TempData["Message"] = $"Connected on {master.Options.PortName}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage();
    }

    public IActionResult OnPostDisconnect()
    {
        try
        {
            master.Disconnect();
            TempData["Message"] = "Disconnected.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStartPollingAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!master.IsConnected)
            {
                TempData["Error"] = "Connect before starting polling.";
                return RedirectToPage();
            }

            await master.StartPollingAsync(cancellationToken).ConfigureAwait(false);
            TempData["Message"] = "Polling started.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStopPollingAsync()
    {
        try
        {
            await master.StopPollingAsync().ConfigureAwait(false);
            TempData["Message"] = "Polling stopped.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage();
    }

    public IActionResult OnPostAddDevice()
    {
        Input.Devices.Add(new DeviceInput
        {
            Type = "16 Out",
            UnitId = NextUnitId(),
            Name = "New device",
            Mode = "Voltage0To10V"
        });
        LoadSelectOptions();
        return Page();
    }

    public IActionResult OnPostRemoveDevice(int index)
    {
        if (index >= 0 && index < Input.Devices.Count)
        {
            Input.Devices.RemoveAt(index);
        }

        LoadSelectOptions();
        return Page();
    }

    private void LoadSelectOptions()
    {
        LoadPortOptions();
        LoadBaudRateOptions();
    }

    private void LoadPortOptions()
    {
        var ports = SerialPort.GetPortNames()
            .OrderBy(p => p, NaturalStringComparer.Instance)
            .ToArray();
        var items = ports.Select(p => new SelectListItem(p, p)).ToList();

        if (!string.IsNullOrWhiteSpace(Input.PortName)
            && !ports.Contains(Input.PortName, StringComparer.OrdinalIgnoreCase))
        {
            items.Insert(0, new SelectListItem($"{Input.PortName} (not found)", Input.PortName));
        }

        PortOptions = items;
    }

    private void LoadBaudRateOptions()
    {
        var rates = StandardBaudRates.ToList();
        if (!rates.Contains(Input.BaudRate))
        {
            rates.Insert(0, Input.BaudRate);
        }

        BaudRateOptions = rates
            .Select(b => new SelectListItem(
                StandardBaudRates.Contains(b) ? b.ToString() : $"{b} (custom)",
                b.ToString()))
            .ToArray();
    }

    private void LoadFromStore()
    {
        var host = configStore.Host;
        Input = new ConfigureInput
        {
            PortName = host.PortName,
            BaudRate = host.BaudRate,
            Parity = host.Parity,
            StopBits = host.StopBits,
            PollIntervalMs = (int)host.PollInterval.TotalMilliseconds,
            InterDeviceDelayMs = (int)host.InterDeviceDelay.TotalMilliseconds,
            AutoConnect = host.AutoConnect,
            AutoScan = host.AutoScan,
            ScanUnitIdFrom = host.ScanUnitIdFrom,
            ScanUnitIdTo = host.ScanUnitIdTo,
            ScanInterProbeDelayMs = (int)host.ScanInterProbeDelay.TotalMilliseconds,
            RegisterDiscoveredDevices = host.RegisterDiscoveredDevices,
            Devices = host.Devices.Select(d => new DeviceInput
            {
                Type = d.Type,
                UnitId = d.UnitId,
                Name = d.Name,
                Identifier = d.Identifier,
                SerialNumber = d.SerialNumber,
                Mode = d.Mode ?? "Voltage0To10V",
                MinVolts = d.MinVolts
            }).ToList()
        };
    }

    private void ApplyInputToHost()
    {
        var host = configStore.Host;
        host.PortName = Input.PortName.Trim();
        host.BaudRate = Input.BaudRate;
        host.Parity = Input.Parity;
        host.StopBits = Input.StopBits;
        host.PollInterval = TimeSpan.FromMilliseconds(Input.PollIntervalMs);
        host.InterDeviceDelay = TimeSpan.FromMilliseconds(Input.InterDeviceDelayMs);
        host.AutoConnect = Input.AutoConnect;
        host.AutoScan = Input.AutoScan;
        host.ScanUnitIdFrom = Input.ScanUnitIdFrom;
        host.ScanUnitIdTo = Input.ScanUnitIdTo;
        host.ScanInterProbeDelay = TimeSpan.FromMilliseconds(Input.ScanInterProbeDelayMs);
        host.RegisterDiscoveredDevices = Input.RegisterDiscoveredDevices;
        var existingChannels = host.Devices.ToDictionary(
            d => d.UnitId,
            d => d.Channels.Select(c => new ModbusChannelOptions
            {
                Channel = c.Channel,
                Name = c.Name,
                Unit = c.Unit,
                ZeroVoltage = c.ZeroVoltage,
                SpanVoltage = c.SpanVoltage,
                ZeroMeasurand = c.ZeroMeasurand,
                SpanMeasurand = c.SpanMeasurand
            }).ToList());
        host.Devices = Input.Devices.Select(d => new ModbusDeviceRegistrationOptions
        {
            Type = d.Type,
            UnitId = d.UnitId,
            Name = string.IsNullOrWhiteSpace(d.Name) ? null : d.Name.Trim(),
            Identifier = string.IsNullOrWhiteSpace(d.Identifier) ? null : d.Identifier.Trim(),
            SerialNumber = string.IsNullOrWhiteSpace(d.SerialNumber) ? null : d.SerialNumber.Trim(),
            Mode = d.Type is "8 Out" or "WaveshareAnalogOutput8Ch"
                ? d.Mode
                : null,
            MinVolts = d.MinVolts,
            Channels = existingChannels.TryGetValue(d.UnitId, out var channels) ? channels : []
        }).ToList();
    }

    private byte NextUnitId()
    {
        var used = Input.Devices.Select(d => d.UnitId).ToHashSet();
        for (byte i = 1; i < 247; i++)
        {
            if (!used.Contains(i))
            {
                return i;
            }
        }

        return 1;
    }

    public sealed class ConfigureInput
    {
        [Required, StringLength(64)]
        public string PortName { get; set; } = "COM1";

        [Range(1200, 921600)]
        public int BaudRate { get; set; } = 9600;

        public Parity Parity { get; set; } = Parity.None;

        public StopBits StopBits { get; set; } = StopBits.One;

        [Range(10, 60_000)]
        public int PollIntervalMs { get; set; } = 500;

        [Range(0, 5_000)]
        public int InterDeviceDelayMs { get; set; } = 50;

        public bool AutoConnect { get; set; }

        public bool AutoScan { get; set; }

        [Range(1, 247)]
        public byte ScanUnitIdFrom { get; set; } = 1;

        [Range(1, 247)]
        public byte ScanUnitIdTo { get; set; } = 32;

        [Range(0, 5_000)]
        public int ScanInterProbeDelayMs { get; set; } = 20;

        public bool RegisterDiscoveredDevices { get; set; } = true;

        public List<DeviceInput> Devices { get; set; } = [];
    }

    public sealed class DeviceInput
    {
        [Required]
        public string Type { get; set; } = "16 Out";

        [Range(1, 255)]
        public byte UnitId { get; set; } = 1;

        public string? Name { get; set; }

        public string? Identifier { get; set; }

        public string? SerialNumber { get; set; }

        public string? Mode { get; set; } = "Voltage0To10V";

        /// <summary>Lowest voltage the module can reliably output; 0 = no minimum. Voltage outputs only.</summary>
        [Range(0, 10)]
        public double MinVolts { get; set; }
    }
}
