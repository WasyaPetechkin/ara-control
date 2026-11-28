using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// A temporary topmost surface consumes the entire selection click before
// looking beneath it. Normal application clicks never need to be monitored.
internal sealed class WindowPicker : Form
{
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr window, StringBuilder name, int length);

    readonly ToolTip hint = new ToolTip();
    readonly string instruction, retry;
    readonly Point hintLocation;
    bool selecting;
    string selected;

    WindowPicker(Form owner, string instruction, string retry) {
        this.instruction = instruction;
        this.retry = retry;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.14;
        Cursor = Cursors.Cross;
        Text = AccessibleName = instruction;
        var desktop = SystemInformation.VirtualScreen;
        MaximumSize = desktop.Size;
        Bounds = desktop;
        var screen = Screen.FromControl(owner).WorkingArea;
        hintLocation = new Point(screen.Left - desktop.Left + 24, screen.Top - desktop.Top + 24);
    }

    public static string Pick(Form editor, string instruction, string retry) {
        Form main = editor.Owner;
        double editorOpacity = editor.Opacity;
        double mainOpacity = main == null ? 1 : main.Opacity;
        using (var picker = new WindowPicker(editor, instruction, retry)) {
            try {
                // Zero-alpha layered windows do not participate in hit testing.
                // Keep the existing modal editor and its unsaved values alive.
                editor.Opacity = 0;
                if (main != null) main.Opacity = 0;
                return picker.ShowDialog(editor) == DialogResult.OK ? picker.selected : null;
            } finally {
                if (main != null) main.Opacity = mainOpacity;
                editor.Opacity = editorOpacity;
                editor.Activate();
            }
        }
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        hint.Show(instruction, this, hintLocation.X, hintLocation.Y);
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keys) {
        if (keys == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
        return base.ProcessCmdKey(ref message, keys);
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        selecting = e.Button == MouseButtons.Left;
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !selecting) return;
        selecting = false;
        var point = PointToScreen(e.Location);
        hint.Hide(this);
        Opacity = 0;
        selected = ProcessAt(point);
        if (selected != null) DialogResult = DialogResult.OK;
        else {
            Opacity = 0.14;
            hint.Show(retry, this, hintLocation.X, hintLocation.Y);
        }
    }

    static string ProcessAt(Point point) {
        const uint rootWindow = 2;
        IntPtr window = GetAncestor(WindowFromPoint(point), rootWindow);
        if (window == IntPtr.Zero) return null;
        var cls = new StringBuilder(128);
        GetClassName(window, cls, cls.Capacity);
        switch (cls.ToString()) {
            case "Progman": case "WorkerW": case "Shell_TrayWnd": case "Shell_SecondaryTrayWnd":
                return null;
        }
        uint id;
        GetWindowThreadProcessId(window, out id);
        using (var self = Process.GetCurrentProcess())
            if (id == 0 || id == self.Id) return null;
        try {
            using (var process = Process.GetProcessById((int)id)) return process.ProcessName;
        } catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (Win32Exception) { return null; }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) hint.Dispose();
        base.Dispose(disposing);
    }
}
