using FluentModbus;

namespace InputOutput.ModbusMaster;

internal sealed class ModbusDeviceChannel(FluentModbus.ModbusClient client, byte unitId) : IModbusDeviceChannel
{
    public byte UnitId { get; } = unitId;

    public T[] ReadHoldingRegisters<T>(int startingAddress, int count) where T : unmanaged =>
        client.ReadHoldingRegisters<T>(UnitId, startingAddress, count).ToArray();

    public T[] ReadInputRegisters<T>(int startingAddress, int count) where T : unmanaged =>
        client.ReadInputRegisters<T>(UnitId, startingAddress, count).ToArray();

    public bool[] ReadCoils(int startingAddress, int count)
    {
        var bytes = client.ReadCoils(UnitId, startingAddress, count);
        return UnpackBits(bytes, count);
    }

    public bool[] ReadDiscreteInputs(int startingAddress, int count)
    {
        var bytes = client.ReadDiscreteInputs(UnitId, startingAddress, count);
        return UnpackBits(bytes, count);
    }

    public void WriteSingleRegister(int registerAddress, ushort value) =>
        client.WriteSingleRegister(UnitId, registerAddress, value);

    public void WriteSingleCoil(int coilAddress, bool value) =>
        client.WriteSingleCoil(UnitId, coilAddress, value);

    public void WriteMultipleCoils(int startingAddress, bool[] values) =>
        client.WriteMultipleCoils(UnitId, startingAddress, values);

    public void WriteMultipleRegisters<T>(int startingAddress, T[] data) where T : unmanaged =>
        client.WriteMultipleRegisters(UnitId, startingAddress, data);

    private static bool[] UnpackBits(Span<byte> bytes, int count)
    {
        var bits = new bool[count];
        for (var i = 0; i < count; i++)
        {
            bits[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
        }

        return bits;
    }
}
