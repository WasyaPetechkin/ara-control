using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class StartupRegistration {
    const string ValueName = "Ara Control";
    const string LogonArgument = " --startup";
    readonly string keyPath, executable;

    public StartupRegistration()
        : this(@"Software\Microsoft\Windows\CurrentVersion\Run", Application.ExecutablePath) {}

    internal StartupRegistration(string keyPath, string executable) {
        this.keyPath = keyPath;
        this.executable = Path.GetFullPath(executable);
    }

    string Command { get { return "\"" + executable + "\"" + LogonArgument; } }

    string ReadCommand() {
        using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
            return key == null ? null : key.GetValue(ValueName) as string;
    }

    public bool IsEnabled() {
        return String.Equals(ReadCommand(), Command, StringComparison.OrdinalIgnoreCase);
    }

    public void RepairMovedExecutable() {
        string registered = ReadCommand();
        const string suffix = "\"" + LogonArgument;
        if (String.IsNullOrEmpty(registered) || !registered.StartsWith("\"", StringComparison.Ordinal)
            || !registered.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            || registered.Length <= suffix.Length + 1) return;
        string oldPath = registered.Substring(1, registered.Length - suffix.Length - 1);
        if (!String.Equals(oldPath, executable, StringComparison.OrdinalIgnoreCase)
            && Path.IsPathRooted(oldPath) && !File.Exists(oldPath))
            SetEnabled(true);
    }

    public void SetEnabled(bool enable) {
        if (enable) {
            // Windows documents a 260-character maximum for a Run command.
            if (Command.Length > 260) throw new PathTooLongException();
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
                key.SetValue(ValueName, Command, RegistryValueKind.String);
        } else {
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, true)) {
                if (key != null && String.Equals(key.GetValue(ValueName) as string,
                    Command, StringComparison.OrdinalIgnoreCase))
                    key.DeleteValue(ValueName, false);
            }
        }
    }
}
