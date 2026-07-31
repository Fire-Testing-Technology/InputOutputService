using InputOutput.ModbusMaster.Scanning;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using InputOutput.ModbusMaster.Hosting;

namespace InputOutput.ModbusMaster.Api.Pages.Scan;

public sealed class IndexModel(
    IModbusBusScanner scanner,
    IModbusMaster master,
    IOptions<ModbusRtuHostOptions> options) : PageModel
{
    public bool IsScanning => scanner.IsScanning;

    public bool IsConnected => master.IsConnected;

    public byte ScanFrom => options.Value.ScanUnitIdFrom;

    public byte ScanTo => options.Value.ScanUnitIdTo;

    public string PortName => master.Options.PortName;

    public void OnGet()
    {
    }
}
