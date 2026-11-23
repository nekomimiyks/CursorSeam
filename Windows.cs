using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;

namespace CursorSeam
{
    internal static class Windows
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayDevice
        {
            internal int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Description;
            internal uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Key;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice info, uint flags);

        internal static List<Display> ReadDisplays()
        {
            var result = new List<Display>();
            foreach (Screen screen in Screen.AllScreens.OrderByDescending(s => s.Primary).ThenBy(s => s.DeviceName))
            {
                var device = new DisplayDevice { Size = Marshal.SizeOf(typeof(DisplayDevice)) };
                for (uint i = 0; EnumDisplayDevices(screen.DeviceName, i, ref device, 1); i++)
                    if ((device.Flags & 1) != 0) break;
                string id = string.IsNullOrWhiteSpace(device.Id) || result.Any(d => d.Id == device.Id) ? screen.DeviceName : device.Id;
                Rectangle px = screen.Bounds;
                SizeF size = ReadEdidSize(id, px.Width < px.Height);
                bool estimated = size.IsEmpty;
                if (estimated) size = new SizeF(px.Width * 25.4f / 96, px.Height * 25.4f / 96);
                int number;
                if (!int.TryParse(new string(screen.DeviceName.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray()), out number)) number = result.Count + 1;
                result.Add(new Display {
                    Id = id, Number = number, Primary = screen.Primary, Pixels = px, Estimated = estimated,
                    Area = new RectangleF(PointF.Empty, size), Name = device.Description
                });
            }
            Layout.Arrange(result);
            return result;
        }

        private static SizeF ReadEdidSize(string interfacePath, bool portrait)
        {
            string[] parts = interfacePath.Split('#');
            if (parts.Length != 4 || !parts[0].EndsWith("DISPLAY", StringComparison.OrdinalIgnoreCase)) return SizeF.Empty;
            try
            {
                string path = @"SYSTEM\CurrentControlSet\Enum\DISPLAY\" + parts[1] + "\\" + parts[2] + @"\Device Parameters";
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
                {
                    byte[] edid = key == null ? null : key.GetValue("EDID") as byte[];
                    if (edid == null || edid.Length < 128) return SizeF.Empty;
                    int width = 0, height = 0;
                    for (int i = 54; i <= 108; i += 18)
                    {
                        if (edid[i] == 0 && edid[i + 1] == 0) continue;
                        width = edid[i + 12] | ((edid[i + 14] & 0xf0) << 4);
                        height = edid[i + 13] | ((edid[i + 14] & 0x0f) << 8);
                        if (width >= 40 && height >= 40) break;
                    }
                    if (width < 40 || height < 40) { width = edid[21] * 10; height = edid[22] * 10; }
                    if (width < 40 || height < 40) return SizeF.Empty;
                    return (width < height) == portrait ? new SizeF(width, height) : new SizeF(height, width);
                }
            }
            catch (System.Security.SecurityException) { return SizeF.Empty; }
            catch (UnauthorizedAccessException) { return SizeF.Empty; }
            catch (IOException) { return SizeF.Empty; }
        }
    }

    internal sealed class MouseRouter : IDisposable
    {
        private const int MouseMove = 0x0200;
        private const uint RawInput = 0x10000003;
        private readonly HookProc callback;
        private IntPtr hook;
        private bool moving, rawRegistered;
        internal Layout Active { get; private set; }

        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string module);
        [DllImport("user32.dll")] private static extern bool GetPhysicalCursorPos(out Point point);
        [DllImport("user32.dll")] private static extern bool SetPhysicalCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern bool GetClipCursor(out NativeRect rect);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(ref RawDevice device, uint count, uint size);
        [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr input, uint command, [Out] byte[] data, ref uint size, uint headerSize);

        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct RawDevice { internal ushort Page, Usage; internal uint Flags; internal IntPtr Window; }
        private readonly byte[] rawMouse = new byte[24 + 2 * IntPtr.Size + 8];

        internal MouseRouter() { callback = OnMouse; }

        internal void Start(Layout layout, IntPtr window)
        {
            if (hook == IntPtr.Zero)
            {
                var device = new RawDevice { Page = 1, Usage = 2, Flags = 0x100, Window = window };
                if (!RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf(typeof(RawDevice)))) throw new Win32Exception();
                rawRegistered = true;
                hook = SetWindowsHookEx(14, callback, GetModuleHandle(null), 0);
                if (hook == IntPtr.Zero)
                {
                    var error = new Win32Exception();
                    Stop();
                    throw error;
                }
            }
            Active = layout;
        }

        internal void Stop()
        {
            Active = null;
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            if (rawRegistered)
            {
                var device = new RawDevice { Page = 1, Usage = 2, Flags = 1 };
                rawRegistered = !RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf(typeof(RawDevice)));
            }
        }

        private bool CanRedirect()
        {
            NativeRect clip;
            return GetClipCursor(out clip) && Rectangle.FromLTRB(clip.Left, clip.Top, clip.Right, clip.Bottom) == Active.Desktop;
        }

        private bool Move(Point point)
        {
            moving = true;
            try { return SetPhysicalCursorPos(point.X, point.Y); }
            finally { moving = false; }
        }

        internal bool TryRoute(Point current, Point proposed, bool allowBlocking, out Point target)
        {
            target = proposed;
            Layout layout = Active;
            return layout != null && layout.Map(current, proposed, allowBlocking && layout.AllDisplaysReachable, out target);
        }

        private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
        {
            Point current, target;
            if (code == 0 && message.ToInt64() == MouseMove && Active != null && !moving &&
                (Marshal.ReadInt32(data, 12) & 1) == 0 && GetPhysicalCursorPos(out current))
            {
                var proposed = new Point(Marshal.ReadInt32(data), Marshal.ReadInt32(data, 4));
                if (TryRoute(current, proposed, true, out target) && target != proposed && CanRedirect() &&
                    (target == current || Move(target))) return new IntPtr(1);
            }
            return CallNextHookEx(hook, code, message, data);
        }

        // Windows may clip a move at the desktop boundary before the low-level hook sees it.
        internal void OnRawMouse(IntPtr data)
        {
            Point current;
            if (Active == null || moving || !GetPhysicalCursorPos(out current)) return;
            Display source = Active.DisplayAt(current);
            if (source == null) return;
            Rectangle px = source.Pixels;
            if (current.X != px.Left && current.X != px.Right - 1 && current.Y != px.Top && current.Y != px.Bottom - 1) return;
            uint size = (uint)rawMouse.Length;
            int header = 8 + 2 * IntPtr.Size;
            if (GetRawInputData(data, RawInput, rawMouse, ref size, (uint)header) != header + 24 || BitConverter.ToInt32(rawMouse, 0) != 0) return;
            if ((BitConverter.ToUInt16(rawMouse, header) & 1) != 0) return;
            int dx = BitConverter.ToInt32(rawMouse, header + 12), dy = BitConverter.ToInt32(rawMouse, header + 16);
            var proposed = new Point(current.X, current.Y);
            if ((current.X == px.Left && dx < 0) || (current.X == px.Right - 1 && dx > 0)) proposed.X += Math.Sign(dx);
            if ((current.Y == px.Top && dy < 0) || (current.Y == px.Bottom - 1 && dy > 0)) proposed.Y += Math.Sign(dy);
            Point target;
            if (proposed != current && TryRoute(current, proposed, false, out target) && CanRedirect()) Move(target);
        }

        public void Dispose() { Stop(); }
    }

    internal sealed class AppPreferences
    {
        internal bool TrayResident = true;
        internal string Language = "";
        internal bool? CheckUpdates;
    }

    internal static class Preferences
    {
        internal static string FilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CursorSeam", "layout.xml"); } }

        internal static void Save(string path, List<Display> displays, AppPreferences preferences = null)
        {
            SaveDocument(path, CreateLayout(displays, preferences));
        }

        internal static XElement CreateLayout(List<Display> displays, AppPreferences preferences)
        {
            return new XElement("layout", displays.Select(d => new XElement("display",
                new XAttribute("id", d.Id), new XAttribute("portrait", d.Pixels.Width < d.Pixels.Height),
                new XAttribute("x", d.Area.X), new XAttribute("y", d.Area.Y), new XAttribute("width", d.Area.Width),
                new XAttribute("height", d.Area.Height), new XAttribute("estimated", d.Estimated))),
                preferences == null ? null : CreateAppSettings(preferences));
        }

        internal static AppPreferences LoadAppPreferences(string path, string legacyPath = null)
        {
            try
            {
                if (File.Exists(path))
                {
                    XElement xml = XElement.Load(path);
                    XElement settings = xml.Name == "layout" ? xml.Element("settings") : xml;
                    if (settings != null)
                        return new AppPreferences
                        {
                            Language = (string)settings.Attribute("language") ?? "",
                            TrayResident = (bool?)settings.Attribute("trayResident") ?? true,
                            CheckUpdates = (bool?)settings.Attribute("checkUpdates")
                        };
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Xml.XmlException) { }
            catch (FormatException) { }
            return legacyPath == null ? new AppPreferences() : LoadAppPreferences(legacyPath);
        }

        internal static void SaveAppPreferences(string path, bool trayResident, string language)
        {
            SaveDocument(path, CreateAppSettings(new AppPreferences { TrayResident = trayResident, Language = language }));
        }

        internal static void UpdateAppPreferences(string path, AppPreferences preferences)
        {
            var layout = new XElement("layout");
            if (File.Exists(path))
            {
                try
                {
                    var saved = XElement.Load(path);
                    if (saved.Name == "layout") layout = saved;
                }
                catch (System.Xml.XmlException) { }
            }
            layout.Elements("settings").Remove();
            layout.Add(CreateAppSettings(preferences));
            SaveDocument(path, layout);
        }

        private static XElement CreateAppSettings(AppPreferences preferences)
        {
            return new XElement("settings", new XAttribute("trayResident", preferences.TrayResident), new XAttribute("language", preferences.Language),
                preferences.CheckUpdates.HasValue ? new XAttribute("checkUpdates", preferences.CheckUpdates.Value) : null);
        }

        internal static void ArchiveSettings(string layoutPath, string settingsPath)
        {
            string[] files = new[] { layoutPath, settingsPath }.Where(File.Exists).ToArray();
            if (files.Length == 0) return;

            string directory = Path.Combine(Path.GetDirectoryName(layoutPath), "backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);
            var moved = new List<string>();
            try
            {
                foreach (string file in files)
                {
                    File.Move(file, Path.Combine(directory, Path.GetFileName(file)));
                    moved.Add(file);
                }
            }
            catch
            {
                foreach (string file in moved)
                    File.Move(Path.Combine(directory, Path.GetFileName(file)), file);
                throw;
            }
        }

        internal static void SaveDocument(string path, XElement doc)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string pending = Path.Combine(Path.GetDirectoryName(path), ".cursorseam-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                doc.Save(pending);
                if (File.Exists(path)) File.Replace(pending, path, null);
                else File.Move(pending, path);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }

        internal static List<Display> Load(string path, List<Display> defaults)
        {
            if (!File.Exists(path)) return defaults;
            try
            {
                return MatchLayout(XElement.Load(path), defaults) ?? defaults;
            }
            catch (IOException) { return defaults; }
            catch (UnauthorizedAccessException) { return defaults; }
            catch (System.Xml.XmlException) { return defaults; }
            catch (FormatException) { return defaults; }
            catch (ArgumentException) { return defaults; }
            catch (OverflowException) { return defaults; }
        }

        internal static List<Display> MatchLayout(XElement layout, List<Display> defaults)
        {
            var saved = layout.Elements("display").ToList();
            if (layout.Name != "layout" || saved.Count != defaults.Count) return null;
            var loaded = defaults.Select(d => d.Copy()).ToList();
            foreach (Display display in loaded)
            {
                XElement entry = saved.FirstOrDefault(d => (string)d.Attribute("id") == display.Id && (bool?)d.Attribute("portrait") == (display.Pixels.Width < display.Pixels.Height));
                if (entry == null) return null;
                saved.Remove(entry);
                float x = (float)entry.Attribute("x"), y = (float)entry.Attribute("y");
                float w = (float)entry.Attribute("width"), h = (float)entry.Attribute("height");
                if (!(Math.Abs(x) <= 30000 && Math.Abs(y) <= 30000 && w >= 40 && w <= 10000 && h >= 40 && h <= 10000)) return null;
                display.Area = new RectangleF(x, y, w, h);
                display.Estimated = (bool?)entry.Attribute("estimated") ?? true;
            }
            return loaded;
        }
    }
}
