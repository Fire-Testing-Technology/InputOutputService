using FastEndpoints;
using FastEndpoints.Swagger;
using InputOutput.ModbusMaster.Api;
using InputOutput.ModbusMaster.Api.Endpoints.Scan;
using InputOutput.ModbusMaster.Api.Services;
using InputOutput.ModbusMaster.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

var isWindowsService = false;

if (OperatingSystem.IsWindows())
{
    // If command line arguments are present i.e. '/Install' or '/Uninstall'
    // this class will install this application as a windows service.
    // See https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service-with-installer
    var installer = new WindowsServiceInstaller(args);
    if (await installer.InstallAsWindowsService())
    {
        return;
    }

    isWindowsService = WindowsServiceHelpers.IsWindowsService();

    // Service installs should use Production unless ASPNETCORE_ENVIRONMENT is already set.
    if (isWindowsService
        && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Production);
    }
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = isWindowsService ? AppContext.BaseDirectory : default
});

builder.Services.AddRazorPages();
builder.Services.AddCors();
builder.Services
    .AddFastEndpoints()
    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.Title = "Modbus Master API";
            s.Description = "REST host for InputOutput.ModbusMaster — units, channels, and RS-485 scan (SSE).";
            s.Version = "v1";
        };
    });

builder.Services.AddModbusRtuMaster(builder.Configuration);
builder.Services.AddSingleton<ModbusConfigStore>();
builder.Services.AddSingleton<ScanProgressService>();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options => { options.ServiceName = WindowsServiceInstaller.ServiceName; });
    builder.Host.UseWindowsService();
}

var app = builder.Build();

app.UseStaticFiles();
app.UseCors();
app.UseHttpsRedirection();
app.MapScanApi();
app.UseFastEndpoints();
app.UseSwaggerGen();
app.MapRazorPages();

app.Run();
