# InputOutput.ModbusMaster.Api Installer

WiX v3.11 MSI for **InputOutput.ModbusMaster.Api**. Publishes a self-contained build and registers the **FTTInputOutput** Windows service (display name **FTT Input Output**) via native `ServiceInstall` / `ServiceControl` (injected by `HeatTransform.xslt`).

## Prerequisites

- [WiX Toolset v3.11](https://wixtoolset.org/releases/v3.11/stable)
- .NET SDK that can build `net10.0`

## Build

```powershell
& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" `
  InputOutput.ModbusMaster.Api.Installer.wixproj /p:Configuration=Release /p:Platform=x64
```

Or from a VS Developer Command Prompt:

```powershell
msbuild InputOutput.ModbusMaster.Api.Installer.wixproj /p:Configuration=Release /p:Platform=x64
```

Output MSI: `bin\x64\Release\FTTInputOutput-1.0.0-BETA0001-x64.msi` (version from the `.wixproj`).

## Service

After install, Apps & Features shows **FTT Input Output v{version} (x64)**.

In **services.msc** look for **FTT Input Output v{version}** (service name `FTTInputOutput`).

Start Menu / Desktop shortcuts open `https://localhost:7015/Devices`.
