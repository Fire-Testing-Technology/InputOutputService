namespace InputOutput.ModbusMaster;

/// <summary>
/// Bus access scoped to a single registered device's unit address for the duration of a
/// poll or <see cref="IModbusMaster.ExecuteAsync"/> call.
/// </summary>
public interface IModbusDeviceChannel
{
    byte UnitId { get; }

    T[] ReadHoldingRegisters<T>(int startingAddress, int count) where T : unmanaged;

    T[] ReadInputRegisters<T>(int startingAddress, int count) where T : unmanaged;

    bool[] ReadCoils(int startingAddress, int count);

    bool[] ReadDiscreteInputs(int startingAddress, int count);

    void WriteSingleRegister(int registerAddress, ushort value);

    void WriteSingleCoil(int coilAddress, bool value);

    void WriteMultipleCoils(int startingAddress, bool[] values);

    void WriteMultipleRegisters<T>(int startingAddress, T[] data) where T : unmanaged;
}
