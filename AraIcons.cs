using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal static class AraIcons
{
    internal static readonly Icon Application = Load("AraControl.ico", SystemInformation.IconSize);
    internal static readonly Icon Connected = Load("TrayConnected.ico", SystemInformation.SmallIconSize);
    internal static readonly Icon Paused = Load("TrayPaused.ico", SystemInformation.SmallIconSize);
    internal static readonly Icon Disconnected = Load("TrayDisconnected.ico", SystemInformation.SmallIconSize);

    internal static Bitmap Logo(int size)
    {
        using (var icon = Load("AraControl.ico", new Size(size, size)))
            return icon.ToBitmap();
    }

    private static Icon Load(string name, Size size)
    {
        using (Stream stream = typeof(AraIcons).Assembly.GetManifestResourceStream(name))
        {
            if (stream == null)
                throw new InvalidOperationException("Missing icon resource: " + name);

            using (var icon = new Icon(stream, size))
                return (Icon)icon.Clone();
        }
    }
}

