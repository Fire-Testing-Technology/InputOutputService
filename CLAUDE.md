# CLAUDE.md

Guidance for Claude Code when working in this repo. See `README.md` for what the product does, configuration keys and the API surface; this file covers how the code is put together and what bites.

## What this is

A Modbus RTU master host for analog-output modules (Sequent 16uout, Waveshare AO 8CH). ASP.NET Core (net10) host with a FastEndpoints REST API and Razor Pages UI, optionally running as the `FTTInputOutput` Windows service, with a WiX v3 MSI installer. **The outputs are real hardware** — a `PUT` to `/api/units/{id}/channels/...` changes a physical output.

## Layout

- `InputOutput.ModbusMaster/` — class library, no web dependencies.
  - `ModbusRtuMaster` — one serial port (FluentModbus `ModbusRtuClient`), devices keyed by unit id, a poll loop, and a `SemaphoreSlim` bus lock so only one transaction is on the wire at a time.
  - `Devices/Sequent`, `Devices/Waveshare` — drivers implementing `IModbusDevice`, `IModbusDeviceIdentity`, `IAnalogOutputDevice`.
  - `Scanning/` — RS-485 unit-id scanner (`ModbusRtuBusScanner`) and its event types.
  - `Hosting/` — `ModbusRtuHostOptions` (the `ModbusRtu` config section), `ModbusDeviceFactory`, `ModbusRtuMasterHostedService`, `SerialPortAvailability` (is the configured port present?), `AddModbusRtuMaster`.
  - `ChannelScale` — linear measurand ↔ voltage/mA mapping and clamping.
- `InputOutput.ModbusMaster.Api/` — web host.
  - `Program.cs` — wiring; handles `/Install` and `/Uninstall` args before building the host.
  - `Endpoints/` — one FastEndpoints class per route (`Channels`, `Units`), plus `ScanApi` (minimal API) for the scan routes.
  - `AnalogDeviceAccess` — shared resolve / apply-setpoint / DTO mapping used by the channel endpoints.
  - `Services/` — `ModbusConfigStore` (apply + persist config), `ScanProgressService`, `DiagnosticLogService` + `DiagnosticLoggerProvider` (500-entry in-memory log for the Log page), `NaturalStringComparer` (orders `COM2` before `COM10` in the port dropdown).
  - `Pages/` + `wwwroot/` — Razor Pages UI and its JS/CSS.
  - `WindowsServiceInstaller` — `sc.exe` wrapper (CliWrap).
- `InputOutput.ModbusMaster.Api.Installer/` — WiX v3.11 project. `HeatTransform.xslt` injects the `ServiceInstall`/`ServiceControl` elements into the harvested component for the exe.

## Build and run

There are no tests and no CI config in the repo. Commands below come from `.vscode/tasks.json`, `launchSettings.json` and the installer README.

```powershell
dotnet build InputOutput.ModbusMaster.Api/InputOutput.ModbusMaster.Api.csproj
cd InputOutput.ModbusMaster.Api; dotnet run --launch-profile https   # https://localhost:7015/Devices
```

- Build the API csproj, not the `.sln`: the solution includes the WiX project, which needs full MSBuild with WiX 3.11 installed (and `heat.exe` at the hard-coded path in the `.wixproj`).
- The API project sets `TreatWarningsAsErrors` in Debug and Release.
- Installer: see `InputOutput.ModbusMaster.Api.Installer/README.md`. A Release build runs `dotnet publish`, then `heat`, then regenerates `PublishFiles.g.wxs`.
- Do not run, or call the API against, a machine with live hardware attached unless asked. Development config has `AutoConnect` and `AutoScan` off for exactly this reason.

## How the pieces interact

- Setpoints are **queued** on the device object (`SetChannelRaw` / `SetChannelValue`) and written to the module on the next poll, or immediately when the endpoint calls `FlushAsync` (the default, `flush: true`). Each poll writes pending values, then reads everything back, so `GET` returns the *last polled* values, not a live read.
- Polling and `ExecuteAsync` both take the master's bus lock. `DevicePolled` / `DevicePollFailed` events are raised **while the lock is held**, so keep handlers fast.
- A setpoint request carries exactly one of `measurand`, `value` or `raw` (`AnalogDeviceAccess.ApplySetpoint`). Measurand goes through `ChannelScale.ToVoltage`, which clamps to the channel's electrical range.
- **Minimum voltage (`ModbusDeviceRegistrationOptions.MinVolts`, per device, default 0 = none).** `AnalogDeviceAccess.ResolveMinVolts` returns it for voltage outputs only (0 for current outputs; NaN/negative → 0, capped at 10). `ApplySetpoint` raises `value`, `raw` (mV) and measurand-derived voltages below it to it (`RaiseToMinimum`; with no minimum a negative voltage still reaches the device and is rejected by it). `ToDto` reports `MinVoltage` plus `MinMeasurand`/`MaxMeasurand`, computed by `AllowedMeasurandRange` (handles increasing and decreasing scales), and `Detail.cshtml` uses those for the slider/number/Set-all bounds. Because 0 V is never written when a minimum is set, a Sequent LED (off at 0 V) can't be turned off via setpoints.
- `ModbusConfigStore.ApplyConnectionSettingsToMaster` copies host options onto the live `ModbusRtuMasterOptions`; `SyncDevicesFromHost` re-registers devices; `SaveAsync` rewrites the `ModbusRtu` section of `appsettings.json` in the **content root**.
- Startup (`ModbusRtuMasterHostedService`): register configured devices → if `AutoConnect`, check the configured port exists (`SerialPortAvailability`, presence only — nothing is opened) → connect → optional scan → start polling. A missing port logs an error listing available ports and skips connect/scan/poll; units stay listed. There is no retry, so a USB adapter that enumerates after the service starts needs a manual connect or restart. On Linux `GetPortNames()` only knows common device prefixes, so an existing `/dev/...` node is also accepted.
- Device `Type` accepts the class name or the UI alias (`"16 Out"`, `"8 Out"`) — see `ModbusDeviceFactory`.

## Gotchas

- **Saving config modifies a tracked file.** `SaveAsync` writes `appsettings.json` in the content root; in dev that is the checked-in file under `InputOutput.ModbusMaster.Api/`, so using the Configure page dirties the working tree. Check `git diff` before committing and don't commit machine-specific values (COM port, devices). The write is not atomic. The installer treats `appsettings.json` as an ordinary harvested file (no special handling seen), so an upgrade may overwrite config saved at runtime — unverified.
- **`PublishFiles.g.wxs` is generated and churns.** Every Release build of the installer regenerates it with fresh component GUIDs, so it shows up as a ~380-line diff. Don't commit that churn unless the installer content actually changed.
- **Scan progress is polled, not SSE.** `ScanProgressService` documents that browser streaming was unreliable; `/api/scan/progress?after=N` returns a JSON snapshot. The Swagger description in `Program.cs` still says "(SSE)" — correct the text if you touch it; don't add SSE.
- **Unit-id ranges differ.** The scanner probes 1–247 (Modbus limit); `ModbusDeviceFactory`/`AnalogDeviceAccess` accept 1–255. Unit 0 is broadcast and rejected by the drivers.
- **Certificates.** `certs/` holds a committed self-signed dev cert **including the private key** (`localhost-key.pem`, `localhost.pfx`), and it ships in the published output. Kestrel uses the PEM pair (commit 8319d1c), but `scripts/Generate-KestrelCertificate.ps1` still produces `.pfx`/`.cer` with password `localhost` and its header comment refers to a `Password` setting that no longer exists. Don't commit new keys or reuse this cert for anything but localhost.
- **No authentication.** Every endpoint is `AllowAnonymous()` and Production binds `0.0.0.0`. Don't add endpoints that assume callers are trusted without flagging it.
- **The Waveshare 0–10 V module is only accurate to about ±10 mV, and its lowest output measured ~20 mV.** Vendor docs ([product page](https://www.waveshare.com/modbus-rtu-analog-output-8ch-b.htm), [wiki](https://www.waveshare.com/wiki/Modbus_RTU_Analog_Output_8CH)) for the voltage (B) version: "12-bit, 1mV" resolution, output accuracy **±0.01 V**, register values "in mV" (0x03E8 = 1000 mV), so the `mV` register unit used by `WaveshareAnalogOutputRegisters` is correct. No linearity, offset or minimum-output figures are published. (A 12-bit DAC over 10 V steps ~2.4 mV, so the 1 mV register granularity is finer than the hardware.) Measured on unit 3 (`Voltage0To10V`): the lowest output the module produces is ~20 mV, so setpoints below that — probably including 0 V — can't go lower. That is within spec only if it was seen at a ~10 mV setpoint; at a 0 V setpoint it is outside ±10 mV. An earlier reading of **66** on the meter for a 10 mV setpoint on channel 8 is outside spec and unexplained. The software side is verified at register level: 10 mV writes `10` to `0x0007`, 1 mV writes `1`. A 0–100 mV range switch (direct output) was built and then backed out because ±10 mV is ±10 % of 100 mV; don't re-add it (an external 100:1 divider with the software writing ×100 was the alternative). Set the device's `MinVolts` (Configure page) to stop the UI and API offering setpoints below that floor — about 0.020 for this module. It is not set in the committed config.
- **The Sequent 16uout is only ~1 % accurate unless calibrated.** Vendor documentation: the register map ([MODBUS.md](https://github.com/SequentMicrosystems/16uout-rpi/blob/main/MODBUS.md)) confirms holding registers `0x00–0x0F` take 0–10000 mV and LED coils sit at `0x00–0x0F`, matching `Sequent16UOutRegisters`, but publishes no accuracy, resolution, offset or minimum-output figures and no calibration registers. The reseller product page ([The Pi Hut](https://thepihut.com/collections/latest-raspberry-pi-products/products/sixteen-0-10v-analogue-outputs-8-layer-stackable-hat-for-raspberry-pi), quoting the vendor) says the outputs are "14-bit PWM" with factory accuracy of "1%", improvable to 0.1 % by calibration. 1 % of 10 V is ~100 mV, so sub-100 mV setpoints are indicative only unless the module has been calibrated; I couldn't find the calibration procedure (it is probably a vendor CLI step on the Pi, not a Modbus register). PWM-derived outputs may also show ripple at low voltages. Not measured on hardware here.
- **New per-device settings must be threaded through the Configure page.** `Configure/Index.cshtml.cs` rebuilds every `ModbusDeviceRegistrationOptions` from `DeviceInput` on save (`ApplyInputToHost`), copying fields one by one. A new device setting must be added to `DeviceInput`, `LoadFromStore`, `ApplyInputToHost` *and* the form in `Index.cshtml`, or saving the page silently drops it (`MinVolts` is the worked example). Channel entries are carried over by unit id, so channel-level fields have the same hazard in that method's `existingChannels` copy.
- **Voltages are 3 dp by design.** `Sequent16UOut`/`WaveshareAnalogOutput8Ch.SetChannelVolts` round to whole millivolts (`Math.Round(volts * 1000)`, ties to even), and `Detail.cshtml`'s `StepFor` steps by 1 mV of output.
- **Stale `Range` keys in `appsettings.json` are harmless.** An earlier (backed-out) range feature may have saved `"Range": "..."` into channel entries; the options binder ignores unknown keys and the next config save drops them.
- **Connection loss is terminal for polling.** `PollLoopAsync` exits (logs a warning) if the RTU client is disconnected; nothing reconnects it.
- **Pi debug config hard-codes `pi@192.168.1.64`** in `.vscode/` (`launch.json`, `tasks.json`), and the address 192.168.1.64 in `launchSettings.json`'s launch URL; the deploy script takes parameters.
- `Microsoft.Extensions.Hosting.WindowsServices` is pinned at 8.0.1 on a net10 project.

## Conventions

C# with `Nullable` and `ImplicitUsings` enabled, file-scoped namespaces, primary constructors for DI, one FastEndpoints `Endpoint<TReq,TRes>` class per route with `AllowAnonymous()` and a `Summary`. Public types carry XML doc comments (documentation file is generated; CS1591 is suppressed). Errors from endpoints are returned as `ApiError { Message }` with an explicit status code. Keep `InputOutput.ModbusMaster` free of ASP.NET dependencies.

## Versioning

The installer version is defined in `InputOutput.ModbusMaster.Api.Installer.wixproj` (`MajorVersion`/`MinorVersion`/`PatchVersion`/`BetaVersion`, currently 1.0.0 beta 001). The API publish step receives them as MSBuild properties.
