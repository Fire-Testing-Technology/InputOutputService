namespace InputOutput.ModbusMaster;

public sealed class ModbusDevicePolledEventArgs : EventArgs
{
    public ModbusDevicePolledEventArgs(byte unitId, string? name, TimeSpan duration)
    {
        UnitId = unitId;
        Name = name;
        Duration = duration;
    }

    public byte UnitId { get; }

    public string? Name { get; }

    public TimeSpan Duration { get; }
}
