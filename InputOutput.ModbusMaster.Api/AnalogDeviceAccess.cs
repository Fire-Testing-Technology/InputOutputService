using InputOutput.ModbusMaster.Api.Models;
using InputOutput.ModbusMaster.Devices.Sequent;
using InputOutput.ModbusMaster.Hosting;

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

    public static ModbusChannelOptions ResolveScale(
        ModbusRtuHostOptions host,
        IAnalogOutputDevice device,
        int channel) =>
        ChannelScale.Resolve(host, device.UnitId, channel, device.EngineeringUnit);

    public static void ApplySetpoint(
        ModbusRtuHostOptions host,
        IAnalogOutputDevice device,
        int channel,
        double? measurand,
        double? value,
        ushort? raw)
    {
        var specified = (measurand is not null ? 1 : 0)
            + (value is not null ? 1 : 0)
            + (raw is not null ? 1 : 0);

        if (specified == 0)
        {
            throw new InvalidOperationException("Specify measurand, value, or raw.");
        }

        if (specified > 1)
        {
            throw new InvalidOperationException("Specify only one of measurand, value, or raw.");
        }

        if (raw is not null)
        {
            device.SetChannelRaw(channel, raw.Value);
            return;
        }

        if (measurand is not null)
        {
            var scale = ResolveScale(host, device, channel);
            device.SetChannelValue(channel, ChannelScale.ToVoltage(scale, measurand.Value));
            return;
        }

        device.SetChannelValue(channel, value!.Value);
    }

    public static ChannelDto ToDto(ModbusRtuHostOptions host, IAnalogOutputDevice device, int channel)
    {
        var scale = ResolveScale(host, device, channel);
        var voltage = device.GetChannelValue(channel);
        var measurand = ChannelScale.ToMeasurand(scale, voltage);
        bool? led = device is Sequent16UOut sequent ? sequent.GetLed(channel) : null;

        return new ChannelDto
        {
            Channel = channel,
            Name = scale.Name ?? $"Channel {channel}",
            Unit = scale.Unit,
            Measurand = measurand,
            Voltage = voltage,
            ElectricalUnit = device.EngineeringUnit == AnalogOutputEngineeringUnit.Volts ? "V" : "mA",
            Value = voltage,
            Raw = device.GetChannelRaw(channel),
            ZeroVoltage = scale.ZeroVoltage,
            SpanVoltage = scale.SpanVoltage,
            ZeroMeasurand = scale.ZeroMeasurand,
            SpanMeasurand = scale.SpanMeasurand,
            Led = led
        };
    }
}
