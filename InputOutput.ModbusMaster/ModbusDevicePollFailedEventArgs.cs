namespace InputOutput.ModbusMaster;

public sealed class ModbusDevicePollFailedEventArgs : EventArgs
{
    public ModbusDevicePollFailedEventArgs(byte unitId, string? name, Exception exception)
    {
        UnitId = unitId;
        Name = name;
        Exception = exception;
    }

    public byte UnitId { get; }

    public string? Name { get; }

    public Exception Exception { get; }
}
