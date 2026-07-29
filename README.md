[日本語](README.ja.md)

# WinKvm

A software KVM console for Windows that combines a CH9329 (USB HID emulator) with USB video capture.

> **Note**
> "KVM" here means **Keyboard / Video / Mouse**. It is unrelated to Linux KVM (kernel virtualization).

## What it is

It displays the video output of a target PC in a window via USB capture, and sends the keyboard/mouse actions performed in that window back to the target PC as a USB HID device through the CH9329.

```
Video:   [target PC] --HDMI--> [USB capture] --USB--> [Windows PC / WinKvm]
Control: [target PC] <--USB HID-- [CH9329] <--USB serial-- [Windows PC / WinKvm]
```

Because the target PC sees it as an ordinary USB keyboard/mouse, **no driver or agent is required**, and you can operate it even at the BIOS/UEFI setup screen or before the OS boots.

## Required hardware

| Purpose | Example |
| --- | --- |
| USB HID emulator | A CH9329-based module (USB serial ⇔ USB HID converter) |
| Video capture | A UVC-compatible USB HDMI capture device |

## Requirements

- Windows
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Usage

1. Launch `WinKvm.exe`.
2. From **"Mouse/Keyboard settings"** in the menu, select the COM port the CH9329 is connected to.
3. From **"Video settings"** in the menu, select the capture device and resolution / frame rate / FourCC.
4. Also under "Video settings", you can change the display scale (50%–200%).

The selected port, device, resolution, and scale are saved automatically and restored on the next launch.

## Command specification

Commands are sent to the CH9329 at 9600 bps using three frame types, each with checksum validation:

| Type | Example frame |
| --- | --- |
| Keyboard | `57 AB 00 02 08 ...` |
| Mouse (relative) | `57 AB 00 05 05 ...` |
| Mouse (absolute) | `57 AB 00 04 07 02 ...` |

Absolute coordinates are sent normalized to 0–4096 across the whole screen. For the implementation, see `MoveMouseAbsolute` / `MoveMouseRelative` / `SendKeyStatus` in [`WinKvm/MainForm.cs`](WinKvm/MainForm.cs).

## Build

```
dotnet build WinKvm.sln -c Release
```

You can also open `WinKvm.sln` directly in Visual Studio 2022.

Main dependencies (all restored from NuGet):

- [OpenCvSharp4](https://github.com/shimat/opencvsharp) — video capture and drawing
- [DirectShowLib.Standard](https://www.nuget.org/packages/DirectShowLib.Standard/) — enumeration of capture devices and supported resolutions
- [NReco.Logging.File](https://github.com/nreco/logging) — file log output

The log destination and log level can be changed in [`WinKvm/appsettings.json`](WinKvm/appsettings.json).

## Known limitations

- Key-code conversion assumes a Japanese keyboard layout.
- The hankaku/zenkaku key and the Windows key are not supported.
- Only the Ctrl / Alt / Shift modifier keys are handled.

## References

### CH9329 documentation

The command specification above is **not written in the datasheet**; the datasheet itself instructs you to refer to another document ("For the specific protocol format, refer to `CH9329 Serial Communication Protocol_Vx.x.PDF`").

| Document | Content | Source |
| --- | --- | --- |
| CH9329 datasheet (`CH9329DS1.PDF` V1.1) | Pinout, operating modes, electrical characteristics | [Akizuki Denshi](https://akizukidenshi.com/goodsaffix/ch9329.pdf) / [alldatasheet](https://www.alldatasheet.com/datasheet-pdf/pdf/1148630/WCH/CH9329.html) |
| CH9329 Serial Communication Protocol (V1.2, Chinese) | **The serial-command frame specification**; this app's implementation is based on it | Bundled in `CH9329EVT.ZIP` distributed by the maker (WCH); a [copy on Gitee](https://gitee.com/dsiclu/mouse) |
| Japanese explanation (Marutsu) | A Japanese walkthrough of the commands — the easiest place to start | [Marutsu](https://www.marutsu.co.jp/contents/shop/marutsu/datasheet/minnanolab_MR-CH9329EMU.pdf) |

### Tools

- [hry2566/v4w2-ctl](https://github.com/hry2566/v4w2-ctl) — a Windows equivalent of `v4l2-ctl`, handy for checking capture-device names and supported formats beforehand (not required to run WinKvm).

## License

[MIT License](LICENSE)
