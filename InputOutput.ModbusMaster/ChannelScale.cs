using InputOutput.ModbusMaster.Hosting;

namespace InputOutput.ModbusMaster;

/// <summary>Linear measurand ↔ voltage (or mA) interpolation for configured channels.</summary>
public static class ChannelScale
{
    public static ModbusChannelOptions DefaultOptions(int channel, AnalogOutputEngineeringUnit engineeringUnit)
    {
        var span = engineeringUnit == AnalogOutputEngineeringUnit.MilliAmps ? 20.0 : 10.0;
        return new ModbusChannelOptions
        {
            Channel = channel,
            Name = $"Channel {channel}",
            Unit = engineeringUnit == AnalogOutputEngineeringUnit.MilliAmps ? "mA" : "V",
            ZeroVoltage = 0,
            SpanVoltage = span,
            ZeroMeasurand = 0,
            SpanMeasurand = span
        };
    }

    public static ModbusChannelOptions Resolve(
        ModbusRtuHostOptions host,
        byte unitId,
        int channel,
        AnalogOutputEngineeringUnit engineeringUnit)
    {
        var registration = host.Devices.FirstOrDefault(d => d.UnitId == unitId);
        var configured = registration?.Channels.FirstOrDefault(c => c.Channel == channel);
        if (configured is null)
        {
            return DefaultOptions(channel, engineeringUnit);
        }

        return new ModbusChannelOptions
        {
            Channel = channel,
            Name = string.IsNullOrWhiteSpace(configured.Name)
                ? $"Channel {channel}"
                : configured.Name.Trim(),
            Unit = string.IsNullOrWhiteSpace(configured.Unit)
                ? DefaultOptions(channel, engineeringUnit).Unit
                : configured.Unit.Trim(),
            ZeroVoltage = configured.ZeroVoltage,
            SpanVoltage = configured.SpanVoltage,
            ZeroMeasurand = configured.ZeroMeasurand,
            SpanMeasurand = configured.SpanMeasurand
        };
    }

    public static double ToVoltage(ModbusChannelOptions scale, double measurand)
    {
        var denom = scale.SpanMeasurand - scale.ZeroMeasurand;
        if (Math.Abs(denom) < double.Epsilon)
        {
            throw new InvalidOperationException(
                $"Channel {scale.Channel}: SpanMeasurand and ZeroMeasurand must differ.");
        }

        var voltage = scale.ZeroVoltage
            + (measurand - scale.ZeroMeasurand) * (scale.SpanVoltage - scale.ZeroVoltage) / denom;
        return ClampToVoltageRange(scale, voltage);
    }

    public static double ToMeasurand(ModbusChannelOptions scale, double voltage)
    {
        var denom = scale.SpanVoltage - scale.ZeroVoltage;
        if (Math.Abs(denom) < double.Epsilon)
        {
            throw new InvalidOperationException(
                $"Channel {scale.Channel}: SpanVoltage and ZeroVoltage must differ.");
        }

        return scale.ZeroMeasurand
            + (voltage - scale.ZeroVoltage) * (scale.SpanMeasurand - scale.ZeroMeasurand) / denom;
    }

    public static double MeasurandMin(ModbusChannelOptions scale) =>
        Math.Min(scale.ZeroMeasurand, scale.SpanMeasurand);

    public static double MeasurandMax(ModbusChannelOptions scale) =>
        Math.Max(scale.ZeroMeasurand, scale.SpanMeasurand);

    public static double ClampToVoltageRange(ModbusChannelOptions scale, double voltage)
    {
        var min = Math.Min(scale.ZeroVoltage, scale.SpanVoltage);
        var max = Math.Max(scale.ZeroVoltage, scale.SpanVoltage);
        return Math.Clamp(voltage, min, max);
    }
}
