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

        var minVolts = ResolveMinVolts(host, device);

        if (raw is not null)
        {
            // Raw register values are millivolts on voltage outputs.
            var rawValue = minVolts > 0
                ? Math.Max(raw.Value, (ushort)Math.Round(minVolts * 1000.0))
                : raw.Value;
            device.SetChannelRaw(channel, rawValue);
            return;
        }

        if (measurand is not null)
        {
            var scale = ResolveScale(host, device, channel);
            device.SetChannelValue(channel, RaiseToMinimum(ChannelScale.ToVoltage(scale, measurand.Value), minVolts));
            return;
        }

        device.SetChannelValue(channel, RaiseToMinimum(value!.Value, minVolts));
    }

    /// <summary>
    /// The device's configured minimum output voltage in volts, for voltage outputs only. 0 means no minimum (also
    /// returned for current outputs, where the setting does not apply).
    /// </summary>
    public static double ResolveMinVolts(ModbusRtuHostOptions host, IAnalogOutputDevice device)
    {
        if (device.EngineeringUnit != AnalogOutputEngineeringUnit.Volts)
        {
            return 0;
        }

        var configured = host.Devices.FirstOrDefault(d => d.UnitId == device.UnitId)?.MinVolts ?? 0;
        return double.IsNaN(configured) || configured <= 0 ? 0 : Math.Min(configured, MaxVolts);
    }

    private const double MaxVolts = 10;

    /// <summary>Leaves <paramref name="volts"/> alone unless a minimum is set and the value is below it.</summary>
    private static double RaiseToMinimum(double volts, double minVolts) =>
        minVolts > 0 && volts < minVolts ? minVolts : volts;

    /// <summary>
    /// The measurand setpoints whose voltage is at or above <paramref name="minVolts"/>, within the scale's own
    /// bounds. This is what the UI offers; setpoints outside it are raised to the minimum on write.
    /// </summary>
    private static (double Min, double Max) AllowedMeasurandRange(ModbusChannelOptions scale, double minVolts)
    {
        var lo = ChannelScale.MeasurandMin(scale);
        var hi = ChannelScale.MeasurandMax(scale);
        if (minVolts <= 0)
        {
            return (lo, hi);
        }

        double atMinimum;
        try
        {
            atMinimum = ChannelScale.ToMeasurand(scale, minVolts);
        }
        catch (InvalidOperationException)
        {
            // Degenerate voltage span: nothing sensible to derive, keep the scale's own bounds.
            return (lo, hi);
        }

        var increasing = (scale.SpanMeasurand - scale.ZeroMeasurand) * (scale.SpanVoltage - scale.ZeroVoltage) > 0;
        return increasing
            ? (Math.Min(Math.Max(lo, atMinimum), hi), hi)
            : (lo, Math.Max(Math.Min(hi, atMinimum), lo));
    }

    public static ChannelDto ToDto(ModbusRtuHostOptions host, IAnalogOutputDevice device, int channel)
    {
        var scale = ResolveScale(host, device, channel);
        var voltage = device.GetChannelValue(channel);
        var measurand = ChannelScale.ToMeasurand(scale, voltage);
        bool? led = device is Sequent16UOut sequent ? sequent.GetLed(channel) : null;
        var minVolts = ResolveMinVolts(host, device);
        var allowed = AllowedMeasurandRange(scale, minVolts);

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
            MinVoltage = minVolts,
            MinMeasurand = allowed.Min,
            MaxMeasurand = allowed.Max,
            Led = led
        };
    }
}
