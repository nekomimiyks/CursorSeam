using System;
using System.IO;
using Microsoft.Win32;
#if STORE_BUILD
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
#endif

namespace CursorSeam
{
    internal static class StartupRegistration
    {
        internal const string Argument = "--startup";
        private const string ValueName = "CursorSeam";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

#if STORE_BUILD
        private const string StartupTaskId = "CursorSeamStartup";

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentPackageFullName(ref uint length, IntPtr name);

        internal static bool HasPackageIdentity
        {
            get
            {
                const int insufficientBuffer = 122;
                uint length = 0;
                return GetCurrentPackageFullName(ref length, IntPtr.Zero) == insufficientBuffer;
            }
        }

        internal static async Task<bool> IsPackageStartupEnabledAsync()
        {
            var task = await CompleteAsync(StartupTask.GetAsync(StartupTaskId));
            return IsEnabled(task.State);
        }

        internal static async Task<bool> SetPackageStartupEnabledAsync(bool enabled)
        {
            var task = await CompleteAsync(StartupTask.GetAsync(StartupTaskId));
            if (enabled && task.State == StartupTaskState.Disabled)
                return IsEnabled(await CompleteAsync(task.RequestEnableAsync()));
            if (!enabled && task.State == StartupTaskState.Enabled)
                task.Disable();
            return IsEnabled(task.State);
        }

        private static bool IsEnabled(StartupTaskState state)
        {
            return state == StartupTaskState.Enabled || state == StartupTaskState.EnabledByPolicy;
        }

        private static Task<T> CompleteAsync<T>(IAsyncOperation<T> operation)
        {
            var completion = new TaskCompletionSource<T>();
            operation.Completed = (result, status) =>
            {
                try { completion.SetResult(result.GetResults()); }
                catch (Exception error) { completion.SetException(error); }
            };
            return completion.Task;
        }
#endif

        internal static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && !string.IsNullOrEmpty(key.GetValue(ValueName, null) as string);
        }

        internal static void SetEnabled(bool enabled, string executablePath)
        {
            using (var key = enabled ? Registry.CurrentUser.CreateSubKey(RunKey) : Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null)
                {
                    if (enabled) throw new UnauthorizedAccessException();
                    return;
                }
                WriteEntry(key, enabled, executablePath);
            }
        }

        private static void WriteEntry(RegistryKey key, bool enabled, string executablePath)
        {
            if (enabled)
            {
                string command = Command(executablePath);
                if (command.Length > 260) throw new IOException();
                key.SetValue(ValueName, command, RegistryValueKind.String);
            }
            else key.DeleteValue(ValueName, false);
        }

        internal static string Command(string executablePath)
        {
            return "\"" + executablePath + "\" " + Argument;
        }
    }
}
