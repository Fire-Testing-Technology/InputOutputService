using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InputOutput.ModbusMaster.Devices.Sequent;

/// <summary>
/// <see cref="IModbusDevice"/> for the Sequent Microsystems Sixteen 0–10 V Analog Outputs (16uout) over Modbus RTU.
/// Queued voltage / LED setpoints are written on each poll, then holding registers and coils are read back.
/// Protocol: https://github.com/SequentMicrosystems/16uout-rpi/blob/main/MODBUS.md
/// </summary>
public sealed class Sequent16UOut : IModbusDevice, IModbusDeviceIdentity, IAnalogOutputDevice
{
    private readonly object _gate = new();
    private readonly ushort?[] _pendingOutputs = new ushort?[Sequent16UOutRegisters.ChannelCount];
    private readonly bool?[] _pendingLeds = new bool?[Sequent16UOutRegisters.LedCount];
    private readonly ushort[] _outputs = new ushort[Sequent16UOutRegisters.ChannelCount];
    private readonly bool[] _leds = new bool[Sequent16UOutRegisters.LedCount];
    private readonly ILogger _logger;

    public Sequent16UOut(
        byte unitId,
        string? name = null,
        string? identifier = null,
        string? serialNumber = null,
        ILogger<Sequent16UOut>? logger = null)
    {
        if (unitId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitId), "Unit id 0 is broadcast-only; use 1–255 for polling.");
        }

        UnitId = unitId;
        Name = name ?? $"Sequent 16uout @{unitId}";
        Identifier = identifier;
        SerialNumber = serialNumber;
        _logger = logger ?? NullLogger<Sequent16UOut>.Instance;
    }

    public byte UnitId { get; }

    public string? Name { get; }

    public string DeviceType => nameof(Sequent16UOut);

    public string? Identifier { get; }

    public string? SerialNumber { get; }

    public int ChannelCount => Sequent16UOutRegisters.ChannelCount;

    public AnalogOutputEngineeringUnit EngineeringUnit => AnalogOutputEngineeringUnit.Volts;

    /// <summary>Last values read from holding registers 0x00–0x0f (mV).</summary>
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

    /// <summary>Last values read from coils 0x00–0x0f (LED1–16).</summary>
    public IReadOnlyList<bool> Leds
    {
        get
        {
            lock (_gate)
            {
                return _leds.ToArray();
            }
        }
    }

    /// <summary>Raised after a successful poll that refreshed outputs and LEDs.</summary>
    public event EventHandler? StateUpdated;

    /// <summary>Queue a raw register write for a 1-based channel (1–16), value in millivolts (0–10000).
    /// LED turns off at 0 V and on only for the first non-zero after zero — not on every voltage change.</summary>
    public void SetChannelRaw(int channel, ushort milliVolts)
    {
        ValidateChannel(channel);
        ValidateMilliVolts(milliVolts);

        lock (_gate)
        {
            var index = channel - 1;
            var previous = _pendingOutputs[index] ?? _outputs[index];
            _pendingOutputs[index] = milliVolts;
            QueueLedForVoltageTransition(index, previous, milliVolts);
        }
    }

    /// <summary>Queue outputs for channels 1–N from a 0-based span of millivolt values.
    /// LED turns off at 0 V and on only for the first non-zero after zero — not on every voltage change.</summary>
    public void SetChannelsRaw(ReadOnlySpan<ushort> milliVolts)
    {
        if (milliVolts.Length is < 1 or > Sequent16UOutRegisters.ChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(milliVolts), "Provide 1–16 channel values.");
        }

        for (var i = 0; i < milliVolts.Length; i++)
        {
            ValidateMilliVolts(milliVolts[i]);
        }

        lock (_gate)
        {
            for (var i = 0; i < milliVolts.Length; i++)
            {
                var previous = _pendingOutputs[i] ?? _outputs[i];
                _pendingOutputs[i] = milliVolts[i];
                QueueLedForVoltageTransition(i, previous, milliVolts[i]);
            }
        }
    }

    /// <summary>
    /// Queue an LED write only on 0↔non-zero transitions. Intermediate non-zero voltage changes leave the LED alone.
    /// </summary>
    private void QueueLedForVoltageTransition(int index, ushort previousMilliVolts, ushort nextMilliVolts)
    {
        if (nextMilliVolts == 0)
        {
            if (previousMilliVolts != 0 || _leds[index])
            {
                _pendingLeds[index] = false;
            }

            return;
        }

        if (previousMilliVolts == 0)
        {
            _pendingLeds[index] = true;
        }
    }

    /// <summary>Queue a voltage setpoint in volts (0–10 V).</summary>
    public void SetChannelVolts(int channel, double volts)
    {
        if (volts is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(volts), "Voltage must be between 0 and 10 V.");
        }

        SetChannelRaw(channel, (ushort)Math.Clamp(Math.Round(volts * 1000.0), 0, Sequent16UOutRegisters.MaxMilliVolts));
    }

    public ushort GetChannelRaw(int channel)
    {
        ValidateChannel(channel);
        lock (_gate)
        {
            return _outputs[channel - 1];
        }
    }

    public double GetChannelVolts(int channel) => GetChannelRaw(channel) / 1000.0;

    public double GetChannelValue(int channel) => GetChannelVolts(channel);

    public void SetChannelValue(int channel, double value) => SetChannelVolts(channel, value);

    /// <summary>Queue an LED coil write for a 1-based LED (1–16).</summary>
    public void SetLed(int led, bool on)
    {
        ValidateLed(led);
        lock (_gate)
        {
            _pendingLeds[led - 1] = on;
        }
    }

    public bool GetLed(int led)
    {
        ValidateLed(led);
        lock (_gate)
        {
            return _leds[led - 1];
        }
    }

    /// <summary>Write pending setpoints immediately using exclusive bus access.</summary>
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

    public ValueTask PollAsync(IModbusDeviceChannel channel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ApplyPendingWrites(channel);
        RefreshFromBus(channel);
        StateUpdated?.Invoke(this, EventArgs.Empty);
        return ValueTask.CompletedTask;
    }

    private void RefreshFromBus(IModbusDeviceChannel channel)
    {
        var outputs = channel.ReadHoldingRegisters<ushort>(
            Sequent16UOutRegisters.VoltageOutput1,
            Sequent16UOutRegisters.ChannelCount);

        lock (_gate)
        {
            Array.Copy(outputs, _outputs, Sequent16UOutRegisters.ChannelCount);
        }

        try
        {
            var leds = channel.ReadCoils(
                Sequent16UOutRegisters.Led1,
                Sequent16UOutRegisters.LedCount);

            lock (_gate)
            {
                Array.Copy(leds, _leds, Sequent16UOutRegisters.LedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Sequent unit {UnitId} failed to read LED coils 0x{Coil:X2}–{Count}.",
                UnitId,
                Sequent16UOutRegisters.Led1,
                Sequent16UOutRegisters.LedCount);
        }
    }

    private void ApplyPendingWrites(IModbusDeviceChannel channel)
    {
        ApplyPendingOutputs(channel);
        ApplyPendingLeds(channel);
    }

    private void ApplyPendingOutputs(IModbusDeviceChannel channel)
    {
        ushort?[] snapshot;
        lock (_gate)
        {
            snapshot = (ushort?[])_pendingOutputs.Clone();
            Array.Clear(_pendingOutputs);
        }

        if (!TryGetPendingRange(snapshot, out var first, out var last))
        {
            return;
        }

        if (IsContiguous(snapshot, first, last) && last > first)
        {
            var block = new ushort[last - first + 1];
            for (var i = 0; i < block.Length; i++)
            {
                block[i] = snapshot[first + i]!.Value;
            }

            channel.WriteMultipleRegisters(Sequent16UOutRegisters.VoltageOutput1 + first, block);
            lock (_gate)
            {
                for (var i = 0; i < block.Length; i++)
                {
                    _outputs[first + i] = block[i];
                }
            }

            return;
        }

        for (var i = first; i <= last; i++)
        {
            if (snapshot[i] is { } value)
            {
                channel.WriteSingleRegister(Sequent16UOutRegisters.VoltageOutput1 + i, value);
                lock (_gate)
                {
                    _outputs[i] = value;
                }
            }
        }
    }

    private void ApplyPendingLeds(IModbusDeviceChannel channel)
    {
        bool?[] snapshot;
        lock (_gate)
        {
            snapshot = (bool?[])_pendingLeds.Clone();
            Array.Clear(_pendingLeds);
        }

        if (!TryGetPendingRange(snapshot, out var first, out var last))
        {
            return;
        }

        // Prefer single-coil writes: some Sequent firmware is unreliable with WriteMultipleCoils.
        for (var i = first; i <= last; i++)
        {
            if (snapshot[i] is not { } value)
            {
                continue;
            }

            var led = i + 1;
            var coil = Sequent16UOutRegisters.Led1 + i;
            try
            {
                channel.WriteSingleCoil(coil, value);
                lock (_gate)
                {
                    _leds[i] = value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Sequent unit {UnitId} failed to set LED {Led} coil 0x{Coil:X2} to {State}.",
                    UnitId,
                    led,
                    coil,
                    value ? "on" : "off");
            }
        }
    }

    private static bool TryGetPendingRange<T>(T?[] snapshot, out int first, out int last) where T : struct
    {
        first = -1;
        last = -1;
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

        return first >= 0;
    }

    private static bool IsContiguous<T>(T?[] snapshot, int first, int last) where T : struct
    {
        for (var i = first; i <= last; i++)
        {
            if (snapshot[i] is null)
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateMilliVolts(ushort milliVolts)
    {
        if (milliVolts > Sequent16UOutRegisters.MaxMilliVolts)
        {
            throw new ArgumentOutOfRangeException(nameof(milliVolts), milliVolts, "Value must be 0–10000 mV.");
        }
    }

    private static void ValidateChannel(int channel)
    {
        if (channel is < 1 or > Sequent16UOutRegisters.ChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Channel must be 1–16.");
        }
    }

    private static void ValidateLed(int led)
    {
        if (led is < 1 or > Sequent16UOutRegisters.LedCount)
        {
            throw new ArgumentOutOfRangeException(nameof(led), led, "LED must be 1–16.");
        }
    }
}
