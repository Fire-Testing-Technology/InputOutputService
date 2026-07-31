using InputOutput.ModbusMaster.Scanning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InputOutput.ModbusMaster.Hosting;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IModbusMaster"/>, <see cref="IModbusBusScanner"/>, binds options from configuration,
    /// and starts a hosted service that registers devices and optionally connects / scans / polls.
    /// </summary>
    public static IServiceCollection AddModbusRtuMaster(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ModbusRtuHostOptions>(configuration.GetSection(ModbusRtuHostOptions.SectionName));
        services.AddSingleton<IModbusMaster>(sp =>
        {
            var hostOptions = sp.GetRequiredService<IOptions<ModbusRtuHostOptions>>().Value;
            var logger = sp.GetService<ILogger<ModbusRtuMaster>>();
            return new ModbusRtuMaster(hostOptions.ToMasterOptions(), logger);
        });
        services.AddSingleton<IModbusBusScanner, ModbusRtuBusScanner>();
        services.AddHostedService<ModbusRtuMasterHostedService>();
        return services;
    }
}
