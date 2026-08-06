using FluentModbus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InputOutput.ModbusMaster;

internal sealed class ModbusDeviceChannel(
    FluentModbus.ModbusClient client,
    byte unitId,
    ILogger? logger = null) : IModbusDeviceChannel
{
    private readonly ILogger _logger = logger ?? NullLogger.Instance;

    public byte UnitId { get; } = unitId;

    public T[] ReadHoldingRegisters<T>(int startingAddress, int count) where T : unmanaged =>
        Trace(
            "READ",
            "holding",
            startingAddress,
            count,
            null,
            () => client.ReadHoldingRegisters<T>(UnitId, startingAddress, count).ToArray());

    public T[] ReadInputRegisters<T>(int startingAddress, int count) where T : unmanaged =>
        Trace(
            "READ",
            "input",
            startingAddress,
            count,
            null,
            () => client.ReadInputRegisters<T>(UnitId, startingAddress, count).ToArray());

    public bool[] ReadCoils(int startingAddress, int count) =>
        Trace(
            "READ",
            "coil",
            startingAddress,
            count,
            null,
            () =>
            {
                var bytes = client.ReadCoils(UnitId, startingAddress, count);
                return UnpackBits(bytes, count);
            });

    public bool[] ReadDiscreteInputs(int startingAddress, int count) =>
        Trace(
            "READ",
            "discrete",
            startingAddress,
            count,
            null,
            () =>
            {
                var bytes = client.ReadDiscreteInputs(UnitId, startingAddress, count);
                return UnpackBits(bytes, count);
            });

    public void WriteSingleRegister(int registerAddress, ushort value) =>
        Trace(
            "WRITE",
            "holding",
            registerAddress,
            1,
            value.ToString(),
            () =>
            {
                client.WriteSingleRegister(UnitId, registerAddress, value);
                return true;
            });

    public void WriteSingleCoil(int coilAddress, bool value) =>
        Trace(
            "WRITE",
            "coil",
            coilAddress,
            1,
            value ? "1" : "0",
            () =>
            {
                client.WriteSingleCoil(UnitId, coilAddress, value);
                return true;
            });

    public void WriteMultipleCoils(int startingAddress, bool[] values) =>
        Trace(
            "WRITE",
            "coil",
            startingAddress,
            values.Length,
            string.Join(",", values.Select(v => v ? "1" : "0")),
            () =>
            {
                client.WriteMultipleCoils(UnitId, startingAddress, values);
                return true;
            });

    public void WriteMultipleRegisters<T>(int startingAddress, T[] data) where T : unmanaged =>
        Trace(
            "WRITE",
            "holding",
            startingAddress,
            data.Length,
            string.Join(",", data.Select(static v => v.ToString())),
            () =>
            {
                client.WriteMultipleRegisters(UnitId, startingAddress, data);
                return true;
            });

    private T Trace<T>(
        string operation,
        string area,
        int address,
        int count,
        string? payload,
        Func<T> action)
    {
        var range = count <= 1
            ? $"0x{address:X4}"
            : $"0x{address:X4}..0x{address + count - 1:X4}";
        var detail = payload is null ? string.Empty : $" value=[{payload}]";

        try
        {
            var result = action();
            _logger.LogInformation(
                "Modbus unit {UnitId} {Operation} {Area} {Range}{Detail}",
                UnitId,
                operation,
                area,
                range,
                detail);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Modbus unit {UnitId} {Operation} {Area} {Range}{Detail} failed",
                UnitId,
                operation,
                area,
                range,
                detail);
            throw;
        }
    }

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
