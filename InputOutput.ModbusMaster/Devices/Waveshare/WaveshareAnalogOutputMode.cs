namespace InputOutput.ModbusMaster.Devices.Waveshare;

/// <summary>
/// Hardware variant of the Waveshare Modbus RTU Analog Output 8CH module.
/// See https://www.waveshare.com/wiki/Modbus_RTU_Analog_Output_8CH
/// </summary>
public enum WaveshareAnalogOutputMode
{
    /// <summary>Standard module: 0–20 mA, register values in microamps (0–20000).</summary>
    Current0To20mA,

    /// <summary>Version (B): 0–10 V, register values in millivolts (0–10000).</summary>
    Voltage0To10V
}
