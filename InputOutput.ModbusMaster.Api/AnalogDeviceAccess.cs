using InputOutput.ModbusMaster.Api.Models;
using InputOutput.ModbusMaster.Devices.Sequent;

namespace InputOutput.ModbusMaster.Api;

internal static class AnalogDeviceAccess
{
    public static bool TryResolve(
        IModbusMaster master,
        int unitId,
        out IAnalogOutputDevice device,
        out int statusCode,
        out string message)
    {
        device = null!;
        statusCode = StatusCodes.Status200OK;
        message = string.Empty;

        if (unitId is < 1 or > 255)
        {
            statusCode = StatusCodes.Status400BadRequest;
            message = "unitId must be 1–255.";
            return false;
        }

        var found = master.RegisteredDevices.FirstOrDefault(d => d.UnitId == unitId);
        if (found is null)
        {
            statusCode = StatusCodes.Status404NotFound;
            message = $"No device registered at unit id {unitId}.";
            return false;
        }

        if (found is not IAnalogOutputDevice analog)
        {
            statusCode = StatusCodes.Status400BadRequest;
            message = $"Unit {unitId} does not support analog output channels.";
            return false;
        }

        device = analog;
        return true;
    }

    public static void ApplySetpoint(IAnalogOutputDevice device, int channel, double? value, ushort? raw)
    {
        if (value is not null && raw is not null)
        {
            throw new InvalidOperationException("Specify either value or raw, not both.");
        }

        if (value is null && raw is null)
        {
            throw new InvalidOperationException("Specify value or raw.");
        }

        if (raw is not null)
        {
            device.SetChannelRaw(channel, raw.Value);
        }
        else
        {
            device.SetChannelValue(channel, value!.Value);
        }
    }

    public static ChannelDto ToDto(IAnalogOutputDevice device, int channel)
    {
        bool? led = device is Sequent16UOut sequent ? sequent.GetLed(channel) : null;
        return new ChannelDto
        {
            Channel = channel,
            Raw = device.GetChannelRaw(channel),
            Value = device.GetChannelValue(channel),
            Unit = device.EngineeringUnit == AnalogOutputEngineeringUnit.Volts ? "V" : "mA",
            Led = led
        };
    }
}
