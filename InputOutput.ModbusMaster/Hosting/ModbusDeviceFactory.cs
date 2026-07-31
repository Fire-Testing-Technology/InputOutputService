using InputOutput.ModbusMaster.Devices.Sequent;
using InputOutput.ModbusMaster.Devices.Waveshare;

namespace InputOutput.ModbusMaster.Hosting;

public static class ModbusDeviceFactory
{
    public static IModbusDevice Create(ModbusDeviceRegistrationOptions registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (string.IsNullOrWhiteSpace(registration.Type))
        {
            throw new InvalidOperationException("Device Type is required.");
        }

        if (registration.UnitId == 0)
        {
            throw new InvalidOperationException("Device UnitId must be 1–255.");
        }

        return registration.Type switch
        {
            nameof(Sequent16UOut) or "16 Out" => new Sequent16UOut(
                registration.UnitId,
                registration.Name,
                registration.Identifier,
                registration.SerialNumber),

            nameof(WaveshareAnalogOutput8Ch) or "8 Out" => new WaveshareAnalogOutput8Ch(
                registration.UnitId,
                ParseWaveshareMode(registration.Mode),
                registration.Name,
                registration.Identifier,
                registration.SerialNumber),

            _ => throw new InvalidOperationException($"Unknown Modbus device type '{registration.Type}'.")
        };
    }

    private static WaveshareAnalogOutputMode ParseWaveshareMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return WaveshareAnalogOutputMode.Current0To20mA;
        }

        if (Enum.TryParse<WaveshareAnalogOutputMode>(mode, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Unknown Waveshare mode '{mode}'. Use {nameof(WaveshareAnalogOutputMode.Current0To20mA)} or {nameof(WaveshareAnalogOutputMode.Voltage0To10V)}.");
    }
}
