using System.IO.Ports;

namespace InputOutput.ModbusMaster.Hosting;

/// <summary>Checks whether a configured serial port is currently present on this machine.</summary>
public static class SerialPortAvailability
{
    /// <summary>Serial port names the OS currently reports (registered COM ports on Windows, device nodes on Linux).</summary>
    public static IReadOnlyList<string> GetAvailablePorts() => SerialPort.GetPortNames();

    /// <summary>
    /// True when <paramref name="portName"/> is among <paramref name="availablePorts"/> (case-insensitive).
    /// Presence only — this does not open the port, so a port that is present but in use still returns true.
    /// </summary>
    public static bool IsAvailable(string? portName, IReadOnlyList<string> availablePorts)
    {
        ArgumentNullException.ThrowIfNull(availablePorts);

        if (string.IsNullOrWhiteSpace(portName))
        {
            return false;
        }

        var name = portName.Trim();

        // Windows also accepts the device-path form, e.g. \\.\COM12.
        if (name.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            name = name[4..];
        }

        if (availablePorts.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // On Linux GetPortNames only recognises well-known device name prefixes, so also accept
        // any configured device node that exists (e.g. /dev/serial/by-id/..., custom RS-485 HAT names).
        return !OperatingSystem.IsWindows() && name.StartsWith('/') && File.Exists(name);
    }
}
