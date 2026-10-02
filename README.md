# InputOutputService

Host for driving analog outputs over Modbus RTU (RS-485). It runs a single Modbus master on one serial port, keeps a set of analog-output modules polled, and exposes them through a REST API and a small web UI. It can run as a Windows service (MSI installer included) or on Linux (e.g. a Raspberry Pi).

## Supported hardware

| Device | `Type` in config | Channels | Output |
|---|---|---|---|
| Sequent Microsystems Sixteen 0–10 V Analog Outputs (`16uout`) | `Sequent16UOut` (alias `16 Out`) | 16 | 0–10 V (raw register = millivolts; 14-bit PWM-generated, 1 % factory accuracy — see Limitations), plus a status LED per channel |
| Waveshare Modbus RTU Analog Output 8CH | `WaveshareAnalogOutput8Ch` (alias `8 Out`) | 8 | 0–20 mA or 0–10 V. These are two separate hardware versions (current and voltage), not a runtime setting: set `Mode` (`Current0To20mA` default, or `Voltage0To10V`) to match the module you have |

All channels are **1-based**.

## Solution layout

| Project | Purpose |
|---|---|
| `InputOutput.ModbusMaster` | Class library. RTU master (FluentModbus), device drivers, RS-485 bus scanner, DI hosting (`AddModbusRtuMaster`). No web dependencies. |
| `InputOutput.ModbusMaster.Api` | ASP.NET Core (net10) host: FastEndpoints REST API, Razor Pages UI, Windows-service self-install, runtime config store. |
| `InputOutput.ModbusMaster.Api.Installer` | WiX v3.11 MSI that publishes the API self-contained and registers the `FTTInputOutput` Windows service. |

## Quick start (development)

Requires the .NET 10 SDK.

```powershell
cd InputOutput.ModbusMaster.Api
dotnet run --launch-profile https
```

This opens `https://localhost:7015/Devices` (HTTP is on `http://localhost:5217`). In the Development environment `AutoConnect` and `AutoScan` are **off**, so the app starts without touching the serial port; connect from the UI or turn them on in config when hardware is attached.

VS Code launch configurations and tasks are in `.vscode/`.

## Configuration

Everything lives under the `ModbusRtu` section of `appsettings.json` (bound to `ModbusRtuHostOptions`).

| Key | Default in code | Notes |
|---|---|---|
| `PortName` | `COM1` | Serial port (e.g. `COM7`, `/dev/ttyUSB0`) |
| `BaudRate` | `9600` | The committed `appsettings.json` sets 38400 |
| `Parity` / `StopBits` / `Handshake` | `None` / `One` / `None` | |
| `ReadTimeout` / `WriteTimeout` | `00:00:01` | |
| `Endianness` | `BigEndian` | |
| `PollInterval` | `00:00:00.5` | Pause after a full pass over all devices |
| `InterDeviceDelay` | `00:00:00.05` | Pause between devices on the bus |
| `AutoConnect` | `true` | Connect and start polling on startup. If `PortName` isn't present on the machine, an error is logged (with the ports that *are* available), connecting is skipped and the units stay listed |
| `AutoScan` | `true` | Scan the unit-id range after connecting |
| `ScanUnitIdFrom` / `ScanUnitIdTo` | `1` / `32` | Inclusive probe range |
| `ScanInterProbeDelay` | `00:00:00.02` | |
| `RegisterDiscoveredDevices` | `true` | Register known device types found by a scan |
| `Devices` | `[]` | See below |

A device entry:

```json
{
  "Type": "8 Out",
  "UnitId": 2,
  "Name": "AO B 8 channel",
  "Identifier": "AO-B",
  "SerialNumber": "SN-001",
  "Mode": "Voltage0To10V",
  "MinVolts": 0.02,
  "Channels": [
    { "Channel": 1, "Name": "Oxygen", "Unit": "%",
      "ZeroVoltage": 0, "SpanVoltage": 10,
      "ZeroMeasurand": 0, "SpanMeasurand": 20 }
  ]
}
```

`Mode` applies to Waveshare modules only. Per-channel scaling is optional: it maps a measurand (e.g. 0–20 % O₂) linearly onto the electrical range (`ZeroVoltage`–`SpanVoltage`). Channels without an entry default to 1:1 in volts or milliamps. Requested voltages are clamped to the configured range.

`MinVolts` (default `0` = none) is the lowest voltage that module can reliably output, for voltage outputs only (it is ignored for current outputs). It is set per device, on the Configure page or in the config file — see [Minimum voltage](#minimum-voltage).

The UI's **Configure** page edits these settings, applies them to the running master and writes the `ModbusRtu` section back to `appsettings.json`.

## Web UI

| Page | Path |
|---|---|
| Home | `/` |
| Devices (shows a warning if the configured serial port isn't present on the machine) | `/Devices` |
| Device detail (channel control) | `/Devices/Detail/{unitId}` |
| Configure | `/Configure` |
| RS-485 scan | `/Scan` |
| Diagnostic log (last 500 entries, in memory) | `/Log` |

## REST API

Swagger UI is enabled via FastEndpoints.Swagger (default `/swagger`).

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/units` | List configured units (type, name, identifier, serial number, online) |
| `GET` | `/api/units/{unitId}/channels` | All channels of a unit (last polled values) |
| `GET` | `/api/units/{unitId}/channels/{channel}` | One channel |
| `PUT` | `/api/units/{unitId}/channels/{channel}` | Set one channel |
| `PUT` | `/api/units/{unitId}/channels` | Set several channels in one request |
| `POST` | `/api/scan/start` | Start an RS-485 scan (`202`, or `409` if one is running) |
| `GET` | `/api/scan/progress?after=N` | Poll scan events from cursor `N` |
| `GET` | `/api/scan/status` | Whether a scan is running |

Setting a channel takes exactly one of:

```json
{ "measurand": 12.5 }   // engineering value, converted through the channel scaling (preferred)
{ "value": 5.0 }        // electrical value: volts or milliamps depending on the device
{ "raw": 5000 }         // raw register value (mV for Sequent; µA or mV for Waveshare)
```

`flush` (default `true`) writes the setpoint to the device immediately rather than waiting for the next poll. Errors return `{ "message": "..." }` with `400` (bad channel or setpoint), `404` (no device at that unit id) or `409` (scan already running).

### Voltage precision

Voltage outputs are set to **3 decimal places** (0.001 V = 1 mV). The modules take whole millivolts, so a `value` — or the measurand of a plain-volts channel — is rounded to the nearest millivolt before it is written (e.g. `0.0104` → 10 mV, `1.234` → 1234 mV). On the Device detail page, plain-volts channels step by 0.001 V in the number box and slider and are shown to 3 decimals; for channels with other units one step is 1 mV of output.

### Minimum voltage

Some modules can't produce very small voltages (the Waveshare 0–10 V module tested bottoms out around 20 mV; see Limitations). Set `MinVolts` on the device (0–10 V, default 0) to stop the service offering setpoints the module can't deliver:

- **API:** a `value`, `raw` or measurand setpoint below the minimum is **raised to it** (not rejected). `GET` channel responses include `minVoltage`, and `minMeasurand` / `maxMeasurand`, the lowest and highest measurand setpoints that respect the minimum (these equal the scale's own bounds when no minimum is set).
- **UI:** on the Device detail page the slider, number box and Set-all inputs start at the minimum, and each channel card shows "Module minimum … V".
- A channel's electrical `0 V` is therefore never written when a minimum is set, so a Sequent channel's status LED (which turns off at 0 V) can't be turned off through setpoints.

## Ports and environments

| | HTTP | HTTPS |
|---|---|---|
| Development / default | `localhost:5217` | `localhost:7015` |
| Production | `0.0.0.0:5217` | `0.0.0.0:7015` |

Ports are set in the `Kestrel` section of `appsettings.json` and `appsettings.Production.json`. A Windows-service install uses Production unless `ASPNETCORE_ENVIRONMENT` is already set. HTTPS uses the PEM files in `InputOutput.ModbusMaster.Api/certs/`.

## Windows service and installer

The API can install itself: run `InputOutput.ModbusMaster.Api.exe /Install` (stop, delete, create and start the `FTTInputOutput` service, auto-start) or `/Uninstall`, from an elevated prompt.

To build the MSI you need WiX Toolset v3.11 and a .NET SDK that targets net10.0. From a VS Developer prompt:

```powershell
cd InputOutput.ModbusMaster.Api.Installer
msbuild InputOutput.ModbusMaster.Api.Installer.wixproj /p:Configuration=Release /p:Platform=x64
```

The MSI lands in `bin\x64\Release\` (name and version come from the `.wixproj`). See `InputOutput.ModbusMaster.Api.Installer/README.md` for more.

## Raspberry Pi debugging

`InputOutput.ModbusMaster.Api/scripts/Deploy-PiDebug.ps1` publishes `linux-arm64`, copies it to the Pi over SSH, rebinds Kestrel to `0.0.0.0` and optionally starts it (`-Start`). Attach from Visual Studio over SSH, or use the "Pi:" configurations in `.vscode/launch.json`. Host, user and remote directory are parameters of the script; the VS Code configurations hard-code `pi@192.168.1.64`.

## Security

**There is currently no authentication.** All API endpoints allow anonymous access and the Production configuration listens on all interfaces, so anyone who can reach the port can change analog outputs. Run it only on a trusted network until this is addressed. The HTTPS certificate in `certs/` is a shared self-signed development certificate.

## Limitations

- One serial port and one bus; devices are polled sequentially.
- If the serial connection drops, polling stops and is not restarted automatically.
- Output accuracy is the module's, not the software's: the service writes exact millivolt values. Waveshare publishes ±0.01 V (±10 mV) accuracy for its 0–10 V module (12-bit), and on the unit tested the lowest output measured was about 20 mV. Sequent publishes 1 % factory accuracy for the 16uout (about ±100 mV on a 10 V output; 0.1 %, about ±10 mV, after calibration). So very small setpoints (and probably 0 V) are indicative only — roughly below 20 mV on the Waveshare and below 100 mV on an uncalibrated Sequent. Check low-voltage accuracy on your module with a meter before relying on small setpoints, and set `MinVolts` on the device to match.
- There is no automated test project.
