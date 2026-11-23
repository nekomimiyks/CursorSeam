# CursorSeam

[日本語](../../README.md) · **English** · [한국어](../ko/README.md) · [简体中文](../zh-Hans/README.md) · [繁體中文](../zh-Hant/README.md) · [Deutsch](../de/README.md) · [Français](../fr/README.md) · [Español](../es/README.md) · [Português (Brasil)](../pt-BR/README.md)

CursorSeam keeps the mouse pointer aligned as it crosses between monitors. It works with two or more Windows displays, including ones with different sizes or resolutions.

![Display layout with portrait and landscape monitors. Green lines show where the pointer can cross.](layout.png)

## Install

Download and run `CursorSeam-…-Setup.exe` from the [latest release](https://github.com/nekomimiyks/CursorSeam/releases/latest). When setup finishes, select **Yes** to launch the app or **No** to finish without launching it. Updates keep your existing settings.

## How to use

1. Select **Identify displays** to check the numbers, then drag the display outlines to match your desk.
2. Select **Calibrate size**, then drag an edge or corner to match the monitor's physical size. Hold Shift while dragging to snap to a neighboring edge.
3. Select **Save settings**, then move the pointer across a display boundary to check the result.

## What’s new in 1.1.1

CursorSeam now follows the Windows light or dark setting. The settings panel has on/off switches, a wider language picker, and dedicated backup and restore buttons. The GitHub installer can close a running copy of 1.1.1 or later during an update. When upgrading from 1.1.0 or earlier, exit CursorSeam from the system tray first.

## Back up and restore settings

Choose **Save backup** to save a `.cursorseam` file. To restore it, choose **Restore backup**, select the file, and confirm. This restores the layout, physical sizes, tray preference, and language. Set Windows startup separately.

![Confirmation dialog for restoring a settings backup](backup.png)

## License

[MIT License](../../LICENSE)
