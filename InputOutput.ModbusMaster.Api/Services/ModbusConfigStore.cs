using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using InputOutput.ModbusMaster.Hosting;
using Microsoft.Extensions.Options;

namespace InputOutput.ModbusMaster.Api.Services;

/// <summary>
/// Applies host options to the live master and persists the ModbusRtu section to appsettings.json.
/// </summary>
public sealed class ModbusConfigStore(
    IWebHostEnvironment environment,
    IOptions<ModbusRtuHostOptions> hostOptions,
    IModbusMaster master)
{
    private static readonly JsonSerializerOptions WriteJson = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ModbusRtuHostOptions Host => hostOptions.Value;

    public void ApplyConnectionSettingsToMaster()
    {
        var host = Host;
        var options = master.Options;
        options.PortName = host.PortName;
        options.BaudRate = host.BaudRate;
        options.Parity = host.Parity;
        options.StopBits = host.StopBits;
        options.Handshake = host.Handshake;
        options.ReadTimeout = host.ReadTimeout;
        options.WriteTimeout = host.WriteTimeout;
        options.Endianness = host.Endianness;
        options.PollInterval = host.PollInterval;
        options.InterDeviceDelay = host.InterDeviceDelay;
    }

    public void SyncDevicesFromHost()
    {
        var desired = Host.Devices.ToDictionary(d => d.UnitId);
        foreach (var unitId in master.RegisteredUnitIds.ToArray())
        {
            if (!desired.ContainsKey(unitId))
            {
                master.Unregister(unitId);
            }
        }

        foreach (var registration in Host.Devices)
        {
            if (master.RegisteredUnitIds.Contains(registration.UnitId))
            {
                master.Unregister(registration.UnitId);
            }

            master.Register(ModbusDeviceFactory.Create(registration));
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(environment.ContentRootPath, "appsettings.json");
        JsonNode root;
        if (File.Exists(path))
        {
            await using var read = File.OpenRead(path);
            root = (await JsonNode.ParseAsync(read, cancellationToken: cancellationToken).ConfigureAwait(false))
                   ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        root[ModbusRtuHostOptions.SectionName] = JsonSerializer.SerializeToNode(Host, WriteJson);
        await File.WriteAllTextAsync(path, root.ToJsonString(WriteJson) + Environment.NewLine, cancellationToken)
            .ConfigureAwait(false);
    }
}
