using System.Globalization;

namespace InputOutput.ModbusMaster.Devices.Waveshare;

/// <summary>
/// <see cref="IModbusDevice"/> for the Waveshare Modbus RTU Analog Output 8CH module.
/// Queued setpoints are written on each poll, then all eight channels are read back.
/// Protocol: https://www.waveshare.com/wiki/Modbus_RTU_Analog_Output_8CH
/// </summary>
public sealed class WaveshareAnalogOutput8Ch : IModbusDevice, IModbusDeviceIdentity, IAnalogOutputDevice
{
    private readonly object _gate = new();
    private readonly ushort?[] _pending = new ushort?[WaveshareAnalogOutputRegisters.ChannelCount];
    private readonly ushort[] _outputs = new ushort[WaveshareAnalogOutputRegisters.ChannelCount];
    private bool _identityRead;

    public WaveshareAnalogOutput8Ch(
        byte unitId,
        WaveshareAnalogOutputMode mode = WaveshareAnalogOutputMode.Current0To20mA,
        string? name = null,
        string? identifier = null,
        string? serialNumber = null)
    {
        if (unitId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitId), "Unit id 0 is broadcast-only; use 1–255 for polling.");
        }

        UnitId = unitId;
        Mode = mode;
        Name = name ?? $"Waveshare AO8CH @{unitId}";
        Identifier = identifier;
        SerialNumber = serialNumber;
    }

    public byte UnitId { get; }

    public string? Name { get; }

    public string DeviceType => nameof(WaveshareAnalogOutput8Ch);

    public string? Identifier { get; }

    public string? SerialNumber { get; }

    public WaveshareAnalogOutputMode Mode { get; }

    public int ChannelCount => WaveshareAnalogOutputRegisters.ChannelCount;

    public AnalogOutputEngineeringUnit EngineeringUnit =>
        Mode == WaveshareAnalogOutputMode.Current0To20mA
            ? AnalogOutputEngineeringUnit.MilliAmps
            : AnalogOutputEngineeringUnit.Volts;

    /// <summary>Last values read from registers 0x0000–0x0007 (µA or mV).</summary>
    public IReadOnlyList<ushort> RawOutputs
    {
        get
        {
            lock (_gate)
            {
                return _outputs.ToArray();
            }
        }
    }

    /// <summary>Raw software version register, or null until first successful identity read.</summary>
    public ushort? SoftwareVersionRaw { get; private set; }

    /// <summary>Formatted version such as "1.00", or null until read.</summary>
    public string? SoftwareVersion =>
        SoftwareVersionRaw is { } raw
            ? (raw / 100.0).ToString("0.00", CultureInfo.InvariantCulture)
            : null;

    /// <summary>Raised after a successful poll that refreshed <see cref="RawOutputs"/>.</summary>
    public event EventHandler? OutputsUpdated;

    /// <summary>Queue a raw register write for a 1-based channel (1–8).</summary>
    public void SetChannelRaw(int channel, ushort rawValue)
    {
        ValidateChannel(channel);
        ValidateRaw(rawValue);

        lock (_gate)
        {
            _pending[channel - 1] = rawValue;
        }
    }

    /// <summary>Queue outputs for channels 1–N from a 0-based span of raw register values.</summary>
    public void SetChannelsRaw(ReadOnlySpan<ushort> rawValues)
    {
        if (rawValues.Length is < 1 or > WaveshareAnalogOutputRegisters.ChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rawValues), "Provide 1–8 channel values.");
        }

        for (var i = 0; i < rawValues.Length; i++)
        {
            ValidateRaw(rawValues[i]);
        }

        lock (_gate)
        {
            for (var i = 0; i < rawValues.Length; i++)
            {
                _pending[i] = rawValues[i];
            }
        }
    }

    /// <summary>Queue a current setpoint in milliamps (standard 0–20 mA module).</summary>
    public void SetChannelMilliAmps(int channel, double milliAmps)
    {
        EnsureMode(WaveshareAnalogOutputMode.Current0To20mA);
        if (milliAmps is < 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(milliAmps), "Current must be between 0 and 20 mA.");
        }

        SetChannelRaw(channel, (ushort)Math.Clamp(Math.Round(milliAmps * 1000.0), 0, WaveshareAnalogOutputRegisters.MaxCurrentMicroAmps));
    }

    /// <summary>Queue a voltage setpoint in volts (version B 0–10 V module).</summary>
    public void SetChannelVolts(int channel, double volts)
    {
        EnsureMode(WaveshareAnalogOutputMode.Voltage0To10V);
        if (volts is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(volts), "Voltage must be between 0 and 10 V.");
        }

        SetChannelRaw(channel, (ushort)Math.Clamp(Math.Round(volts * 1000.0), 0, WaveshareAnalogOutputRegisters.MaxVoltageMilliVolts));
    }

    /// <summary>Last polled channel value as milliamps (current module).</summary>
    public double GetChannelMilliAmps(int channel)
    {
        EnsureMode(WaveshareAnalogOutputMode.Current0To20mA);
        return GetChannelRaw(channel) / 1000.0;
    }

    /// <summary>Last polled channel value as volts (voltage module).</summary>
    public double GetChannelVolts(int channel)
    {
        EnsureMode(WaveshareAnalogOutputMode.Voltage0To10V);
        return GetChannelRaw(channel) / 1000.0;
    }

    public ushort GetChannelRaw(int channel)
    {
        ValidateChannel(channel);
        lock (_gate)
        {
            return _outputs[channel - 1];
        }
    }

    public double GetChannelValue(int channel) =>
        Mode == WaveshareAnalogOutputMode.Current0To20mA
            ? GetChannelMilliAmps(channel)
            : GetChannelVolts(channel);

    public void SetChannelValue(int channel, double value)
    {
        if (Mode == WaveshareAnalogOutputMode.Current0To20mA)
        {
            SetChannelMilliAmps(channel, value);
        }
        else
        {
            SetChannelVolts(channel, value);
        }
    }

    /// <summary>
    /// Write pending setpoints immediately using exclusive bus access, without waiting for the poll loop.
    /// </summary>
    public Task FlushAsync(IModbusMaster master, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(master);
        return master.ExecuteAsync(UnitId, (channel, ct) =>
        {
            ApplyPendingWrites(channel);
            RefreshFromBus(channel);
            return ValueTask.CompletedTask;
        }, cancellationToken);
    }

    /// <summary>Read software version via exclusive bus access.</summary>
    public Task ReadIdentityAsync(IModbusMaster master, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(master);
        return master.ExecuteAsync(UnitId, (channel, _) =>
        {
            ReadIdentity(channel);
            return ValueTask.CompletedTask;
        }, cancellationToken);
    }

    public ValueTask PollAsync(IModbusDeviceChannel channel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_identityRead)
        {
            try
            {
                ReadIdentity(channel);
            }
            catch
            {
                // Identity is optional; channel I/O still proceeds.
            }
        }

        ApplyPendingWrites(channel);
        RefreshFromBus(channel);
        OutputsUpdated?.Invoke(this, EventArgs.Empty);
        return ValueTask.CompletedTask;
    }

    private void RefreshFromBus(IModbusDeviceChannel channel)
    {
        var values = channel.ReadHoldingRegisters<ushort>(
            WaveshareAnalogOutputRegisters.Channel1,
            WaveshareAnalogOutputRegisters.ChannelCount);

        lock (_gate)
        {
            Array.Copy(values, _outputs, WaveshareAnalogOutputRegisters.ChannelCount);
        }
    }

    private void ApplyPendingWrites(IModbusDeviceChannel channel)
    {
        ushort?[] snapshot;
        lock (_gate)
        {
            snapshot = (ushort?[])_pending.Clone();
            Array.Clear(_pending);
        }

        var first = -1;
        var last = -1;
        for (var i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i] is null)
            {
                continue;
            }

            if (first < 0)
            {
                first = i;
            }

            last = i;
        }

        if (first < 0)
        {
            return;
        }

        // Contiguous block → one FC16; otherwise individual FC06 writes.
        var contiguous = true;
        for (var i = first; i <= last; i++)
        {
            if (snapshot[i] is null)
            {
                contiguous = false;
                break;
            }
        }

        if (contiguous && last > first)
        {
            var block = new ushort[last - first + 1];
            for (var i = 0; i < block.Length; i++)
            {
                block[i] = snapshot[first + i]!.Value;
            }

            channel.WriteMultipleRegisters(WaveshareAnalogOutputRegisters.Channel1 + first, block);
            return;
        }

        for (var i = first; i <= last; i++)
        {
            if (snapshot[i] is { } value)
            {
                channel.WriteSingleRegister(WaveshareAnalogOutputRegisters.Channel1 + i, value);
            }
        }
    }

    private void ReadIdentity(IModbusDeviceChannel channel)
    {
        var version = channel.ReadHoldingRegisters<ushort>(WaveshareAnalogOutputRegisters.SoftwareVersion, 1);
        SoftwareVersionRaw = version[0];
        _identityRead = true;
    }

    private void EnsureMode(WaveshareAnalogOutputMode expected)
    {
        if (Mode != expected)
        {
            throw new InvalidOperationException($"This device is configured as {Mode}, not {expected}.");
        }
    }

    private void ValidateRaw(ushort rawValue)
    {
        var max = Mode == WaveshareAnalogOutputMode.Current0To20mA
            ? WaveshareAnalogOutputRegisters.MaxCurrentMicroAmps
            : WaveshareAnalogOutputRegisters.MaxVoltageMilliVolts;

        if (rawValue > max)
        {
            throw new ArgumentOutOfRangeException(nameof(rawValue), rawValue, $"Value must be 0–{max} for {Mode}.");
        }
    }

    private static void ValidateChannel(int channel)
    {
        if (channel is < 1 or > WaveshareAnalogOutputRegisters.ChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Channel must be 1–8.");
        }
    }
}
