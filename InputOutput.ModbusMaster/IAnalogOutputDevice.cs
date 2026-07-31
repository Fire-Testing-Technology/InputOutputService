namespace InputOutput.ModbusMaster;

/// <summary>Engineering quantity for analog output setpoints.</summary>
public enum AnalogOutputEngineeringUnit
{
    Volts,
    MilliAmps
}

/// <summary>
/// Analog output channel access shared by Sequent and Waveshare devices.
/// </summary>
public interface IAnalogOutputDevice : IModbusDevice
{
    int ChannelCount { get; }

    AnalogOutputEngineeringUnit EngineeringUnit { get; }

    IReadOnlyList<ushort> RawOutputs { get; }

    ushort GetChannelRaw(int channel);

    /// <summary>Last polled value in <see cref="EngineeringUnit"/> (V or mA).</summary>
    double GetChannelValue(int channel);

    void SetChannelRaw(int channel, ushort raw);

    /// <summary>Queue a setpoint in <see cref="EngineeringUnit"/> (V or mA).</summary>
    void SetChannelValue(int channel, double value);

    Task FlushAsync(IModbusMaster master, CancellationToken cancellationToken = default);
}
