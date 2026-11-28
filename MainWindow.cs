using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

sealed partial class MainWindow : Form
{
    readonly Settings settings;
    readonly StartupRegistration startup = new StartupRegistration();
    bool hideFirstShow, startupRepairFailed;
    readonly NotifyIcon tray = new NotifyIcon();
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly System.Windows.Forms.Timer testTimer = new System.Windows.Forms.Timer();
    readonly List<Font> fonts = new List<Font>();
    readonly Font normal, heading, title, small;
    readonly float scale;
    Palette colors;
    bool settingsPage, connected, calibrating, dialogOpen;
    Label status, last;
    PageView dashboard, preferences;
    string lastKind = "Ready", lastResult, lastValue;
    Profile active { get { return settings.Profiles[settings.Selected]; } }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

    public MainWindow()
    {
        settings = SettingsStore.Load();
        hideFirstShow = settings.StartInTray;
        try {
            if (!Environment.GetCommandLineArgs().Contains("--startup"))
                startup.RepairMovedExecutable();
        }
        catch (Exception) { startupRepairFailed = true; }
        if (Environment.GetCommandLineArgs().Contains("--enabled")) {
            settings.Enabled = true;
            SettingsStore.Save(settings);
        }
        colors = Appearance.Colors(settings.Theme);
        using (var g = CreateGraphics()) scale = g.DpiX / 96f;
        normal = MakeFont("Segoe UI Variable Text", 10.25f, FontStyle.Regular);
        small = MakeFont("Segoe UI Variable Text", 9.5f, FontStyle.Regular);
        heading = MakeFont("Bahnschrift SemiBold", 13.5f, FontStyle.Regular);
        title = MakeFont("Bahnschrift SemiBold", 22, FontStyle.Regular);
        Font = normal;
        Text = "Ara Control";
        Icon = AraIcons.Application;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        SetClientSizeCore(U(624), Math.Min(U(689), Math.Max(U(340), Screen.FromControl(this).WorkingArea.Height - U(65))));
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        menu.Font = normal;
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { Restore(); };
        menu.Opening += delegate { BuildMenu(); };
        menu.Items.Add("Ara Control");
        testTimer.Tick += delegate {
            testTimer.Stop();
            Send(active.Button, "Test shortcut");
        };
        Render();
        tray.Visible = true;
        // The HID reader must also run when the first window stays in the tray.
        BeginInvoke(new Action(delegate {
            var worker = new Thread(delegate() { Device.Loop(OnConnection, e => Post(delegate { OnDeviceEvent(e); })); });
            worker.IsBackground = true;
            worker.Start();
        }));
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
    }

    protected override void SetVisibleCore(bool value) {
        if (value && hideFirstShow) {
            hideFirstShow = false;
            value = false;
        }
        base.SetVisibleCore(value);
    }

    Font MakeFont(string familyName, float size, FontStyle style) {
        Font f;
        try {
            using (var family = new FontFamily(familyName))
                f = new Font(family, size, style);
        } catch (ArgumentException) {
            f = new Font("Segoe UI", size, familyName == "Bahnschrift SemiBold" ? FontStyle.Bold : style);
        }
        fonts.Add(f);
        return f;
    }
    int U(int value) { return (int)Math.Round(value * scale); }
    string T(string key) { return Texts.Get(settings.Language, key); }
    void Put(Control parent, Control control, int x, int y, int w, int h) {
        control.SetBounds(U(x), U(y), U(w), U(h));
        parent.Controls.Add(control);
    }
    Label Caption(Control parent, string text, int x, int y, int w, int h, Font font, Color color) {
        var label = new Label { Text = text, Font = font, ForeColor = color, BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false };
        Put(parent, label, x, y, w, h);
        return label;
    }
    Button Btn(Control parent, string text, int x, int y, int w, Action click, bool prominent) {
        var btn = new RoundedButton { Text = text, Font = normal, UseMnemonic = false,
            BackColor = prominent ? colors.Accent : colors.Surface, ForeColor = prominent ? colors.AccentText : colors.Text,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        btn.FlatAppearance.BorderColor = prominent ? colors.Accent : colors.Border;
        Put(parent, btn, x, y, w, 34);
        btn.Click += delegate { click(); };
        return btn;
    }
    CheckBox Check(Control parent, string text, bool value, int x, int y, int width, Action<bool> change) {
        var box = new CheckBox { Text = text, Checked = value, ForeColor = colors.Text,
            BackColor = colors.Surface, UseVisualStyleBackColor = false, UseMnemonic = false,
            FlatStyle = FlatStyle.Flat };
        Put(parent, box, x, y, width, 31);
        box.CheckedChanged += delegate { change(box.Checked); };
        return box;
    }
    ComboBox Combo(Control parent, int x, int y, int w) {
        var box = new RoundedComboBox { OutlineColor = colors.Border, DropDownStyle = ComboBoxStyle.DropDownList, ForeColor = colors.Text,
            BackColor = colors.Input, FlatStyle = FlatStyle.Flat, Font = normal, IntegralHeight = true };
        box.DrawMode = DrawMode.OwnerDrawFixed;
        box.ItemHeight = U(22);
        box.DrawItem += delegate(object sender, DrawItemEventArgs e) {
            bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            Color background = selected ? colors.Accent : colors.Input;
            Color foreground = selected ? colors.AccentText : colors.Text;
            using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            string item = e.Index < 0 ? box.Text : box.GetItemText(box.Items[e.Index]);
            var rect = Rectangle.Inflate(e.Bounds, -U(3), 0);
            TextRenderer.DrawText(e.Graphics, item, box.Font, rect, foreground,
                TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        };
        Put(parent, box, x, y, w, 32);
        return box;
    }
    Panel Card(Control parent, int y, int h) {
        var panel = new RoundedCard { BackColor = colors.Surface };
        Put(parent, panel, 20, y, 566, h);
        return panel;
    }

    sealed class PageView {
        public Panel Surface;
        public Label Status, Last;
        public Button Tab;
    }

    PageView BuildPage(bool forSettings) {
        // Set the final bounds before attaching any HWND to the visible window.
        var surface = new Panel { Size = ClientSize, AutoScroll = true, BackColor = colors.Background,
            Dock = DockStyle.Fill, Font = normal, Visible = false };
        try {
            var logo = new PictureBox { Image = AraIcons.Logo(U(48)), SizeMode = PictureBoxSizeMode.Zoom };
            logo.Disposed += delegate { logo.Image.Dispose(); };
            Put(surface, logo, 21, 18, 48, 52);
            const int headerTextX = 83;
            const int actionX = 382;
            int headerTextWidth = actionX - headerTextX - 18;
            Caption(surface, "Ara Control", headerTextX, 10, headerTextWidth, 37, title, colors.Text);
            status = Caption(surface, "", headerTextX, 49, headerTextWidth, 23, small, colors.Muted);
            Btn(surface, T(settings.Enabled ? "Control enabled" : "Control paused"), actionX, 27, 586 - actionX, ToggleControl, settings.Enabled);
            var controlsTab = Btn(surface, T("Controls"), 20, 90, 162, delegate { ChangePage(false); }, !forSettings);
            var settingsTab = Btn(surface, T("Settings"), 188, 90, 162, delegate { ChangePage(true); }, forSettings);
            if (forSettings) BuildSettings(surface);
            else BuildDashboard(surface);
            return new PageView { Surface = surface, Status = status, Last = last,
                Tab = forSettings ? settingsTab : controlsTab };
        } catch {
            surface.Dispose();
            throw;
        }
    }

    void Render() {
        colors = Appearance.Colors(settings.Theme);
        var previousDashboard = dashboard;
        var previousPreferences = preferences;
        dashboard = BuildPage(false);
        preferences = BuildPage(true);
        Controls.Add(dashboard.Surface);
        Controls.Add(preferences.Surface);
        BackColor = colors.Background;
        DisplayPage(false);
        if (previousDashboard != null) previousDashboard.Surface.Dispose();
        if (previousPreferences != null) previousPreferences.Surface.Dispose();
        SetTrayState();
    }

    void DisplayPage(bool focusTab) {
        var current = settingsPage ? preferences : dashboard;
        var other = settingsPage ? dashboard : preferences;
        status = current.Status;
        last = current.Last;
        UpdateStatus();
        current.Surface.BringToFront();
        current.Surface.Show();
        other.Surface.Hide();
        if (focusTab && Visible) current.Tab.Focus();
    }

    void BuildDashboard(Panel surface) {
        var profile = Card(surface, 140, 100);
        Caption(profile, T("Active profile"), 16, 8, 535, 30, heading, colors.Text);
        var chooser = Combo(profile, 16, 51, 191);
        chooser.Items.AddRange(settings.Profiles.Cast<object>().ToArray());
        chooser.SelectedIndex = settings.Selected;
        chooser.SelectionChangeCommitted += delegate { SelectProfile(chooser.SelectedIndex); };
        Btn(profile, T("Edit profile"), 217, 46, 162, delegate { EditProfile(false); }, false);
        Btn(profile, T("Add profile"), 389, 46, 161, delegate { EditProfile(true); }, false);

        var undo = active.LastShortcutChange;
        int undoHeight = undo == null ? 0 : 44;
        var input = Card(surface, 251, 141 + undoHeight);
        Caption(input, T("Microphone button"), 16, 7, 540, 32, heading, colors.Text);
        Caption(input, ChordLabel(active.Button), 16, 43, 183, 34, normal, colors.Text);
        Btn(input, T("Record"), 206, 43, 110, delegate { Record("button"); }, false);
        Btn(input, T("Reset"), 324, 43, 102, ResetButton, false);
        Btn(input, T("Test"), 434, 43, 116, Test, false).Enabled = !String.IsNullOrEmpty(active.Button);
        var owner = settings.Profiles.Take(settings.Selected).FirstOrDefault(p => EqualChord(p.Button, active.Button));
        string guide = owner == null ? T("For background control, assign this shortcut as a global shortcut in the target application.")
            : T("This shortcut is already assigned") + " · " + owner.Name;
        Caption(input, guide,
            16, 82, 533, 51, small, colors.Muted);
        if (undo != null) {
            Btn(input, T("Undo shortcut change"), 16, 134, 247, UndoShortcut, false);
            string target = undo.Slot == "up" ? "Volume up" : undo.Slot == "down" ? "Volume down" : "Microphone button";
            Caption(input, T(target) + " → " + ChordLabel(undo.Previous), 275, 134, 275, 41, small, colors.Muted);
        }

        int height = active.Wheel ? 221 : 106;
        var rotation = Card(surface, 403 + undoHeight, height);
        Caption(rotation, T("Knob rotation"), 16, 5, 538, 31, heading, colors.Text);
        Check(rotation, T("Send shortcuts on rotation"), active.Wheel, 16, 42, 533, v => {
            active.Wheel = v; Save(); Render();
        });
        Caption(rotation, T("Headphone volume still follows the knob."), 16, 73, 531, 23, small, colors.Muted);
        if (active.Wheel) {
            string clockwise = settings.ClockwiseCode;
            string first = clockwise == "20" ? "down" : "up";
            string second = first == "up" ? "down" : "up";
            bool known = clockwise == "02" || clockwise == "20";
            Caption(rotation, T(known ? "Clockwise" : "Volume up"), 16, 97, 261, 25, small, colors.Muted);
            Caption(rotation, T(known ? "Counterclockwise" : "Volume down"), 285, 97, 265, 25, small, colors.Muted);
            Btn(rotation, ChordLabel(GetChord(first)), 16, 125, 261, delegate { Record(first); }, false);
            Btn(rotation, ChordLabel(GetChord(second)), 285, 125, 265, delegate { Record(second); }, false);
            Btn(rotation, T(calibrating ? "Cancel" : "Identify clockwise turn"), 16, 176, 261,
                delegate { calibrating = !calibrating; Note(calibrating ? "Turn the knob clockwise one step." : "Ready", null, null); Render(); }, false);
        }
        int bottom = 414 + height + undoHeight;
        Caption(surface, T("Last action"), 22, bottom, 530, 26, small, colors.Muted);
        last = Caption(surface, "", 22, bottom + 26, 548, 49, normal, colors.Text);
        Btn(surface, T("Minimize to tray"), 20, bottom + 87, 232, Hide, false);
        Btn(surface, T("Check button"), 264, bottom + 87, 238, CheckButton, false);
        surface.AutoScrollMinSize = new Size(0, U(bottom + 138));
    }

    string ChordLabel(string chord) {
        return String.IsNullOrWhiteSpace(chord) ? T("No shortcut") : chord.Replace("+", " + ");
    }
    void ResetButton() {
        testTimer.Stop();
        if (!ChangeShortcut("button", SettingsStore.DefaultButton(active.Process), true)) return;
        Note("Shortcut reset", null, ChordLabel(active.Button));
        Render();
    }

    static readonly string[] ShortcutSlots = { "button", "up", "down" };

    IEnumerable<ShortcutOwner> OtherOwners(string slot, string chord) {
        for (int i = 0; i < settings.Profiles.Count; i++) {
            var profile = settings.Profiles[i];
            foreach (string otherSlot in ShortcutSlots) {
                if (profile == active && otherSlot == slot) continue;
                if (EqualChord(chord, profile.GetShortcut(otherSlot)))
                    yield return new ShortcutOwner { ProfileIndex = i, Slot = otherSlot };
            }
        }
    }
    bool ChangeShortcut(string slot, string value, bool restoringDefault) {
        string previous = active.GetShortcut(slot);
        if (String.Equals(previous ?? "", value ?? "", StringComparison.OrdinalIgnoreCase) || EqualChord(previous, value))
            return true;
        if (!restoringDefault) {
            var conflict = OtherOwners(slot, value).FirstOrDefault();
            if (conflict != null) { ShowConflict(conflict); return false; }
        }
        var owners = OtherOwners(slot, previous).ToList();
        var edit = new ShortcutEdit { Slot = slot, Previous = previous, PreviousOwners = owners.Count == 0 ? null : owners };
        return StoreShortcut(slot, value, edit);
    }
    bool StoreShortcut(string slot, string value, ShortcutEdit nextUndo) {
        var previous = active.GetShortcut(slot);
        var previousUndo = active.LastShortcutChange;
        active.SetShortcut(slot, value);
        active.LastShortcutChange = nextUndo;
        if (Save()) return true;
        active.SetShortcut(slot, previous);
        active.LastShortcutChange = previousUndo;
        return false;
    }
    void ShowConflict(ShortcutOwner conflict) {
        MessageBox.Show(this, T("This shortcut is already assigned") + " · " + settings.Profiles[conflict.ProfileIndex].Name, Text);
    }
    void UndoShortcut() {
        var edit = active.LastShortcutChange;
        if (edit == null || !ShortcutSlots.Contains(edit.Slot)) return;
        // Old profiles can legitimately share a chord. Only reject new conflicts.
        var conflict = OtherOwners(edit.Slot, edit.Previous).FirstOrDefault(owner =>
            edit.PreviousOwners == null || !edit.PreviousOwners.Any(old => old.ProfileIndex == owner.ProfileIndex && old.Slot == owner.Slot));
        if (conflict != null) { ShowConflict(conflict); return; }
        testTimer.Stop();
        if (!StoreShortcut(edit.Slot, edit.Previous, null)) return;
        Note("Shortcut restored", null, ChordLabel(edit.Previous));
        Render();
    }
    void BuildSettings(Panel surface) {
        last = null;
        var general = Card(surface, 140, 166);
        Caption(general, T("Language"), 18, 20, 174, 33, heading, colors.Text);
        Choices(general, 192, 26, new[] { "system", "uk", "ru", "en" },
            new[] { T("Follow Windows"), "Українська", "Русский", "English" }, settings.Language,
            v => settings.Language = v);
        Caption(general, T("Theme"), 18, 95, 174, 33, heading, colors.Text);
        Choices(general, 192, 101, new[] { "system", "light", "dark" },
            new[] { T("Follow Windows"), T("Light"), T("Dark") }, settings.Theme, v => settings.Theme = v);
        var profile = Card(surface, 318, 107);
        Caption(profile, active.Name, 18, 12, 534, 30, heading, colors.Text);
        Check(profile, T("Only send when the target window is active"), active.FocusOnly, 18, 55, 530,
            v => { active.FocusOnly = v; testTimer.Stop(); Save(); });
        var launch = Card(surface, 437, 160);
        Caption(launch, T("Startup"), 18, 8, 530, 32, heading, colors.Text);
        bool registered = false, readable = true;
        try { registered = startup.IsEnabled(); }
        catch (Exception) { readable = false; }
        var logon = Check(launch, T("Start when I sign in to Windows"), registered, 18, 43, 530, value => {
            try { startup.SetEnabled(value); startupRepairFailed = false; }
            catch (Exception ex) {
                MessageBox.Show(this, T("Windows startup could not be updated.") + Environment.NewLine + ex.Message, Text);
            }
            Post(Render);
        });
        logon.Enabled = readable;
        Check(launch, T("Start in the system tray"), settings.StartInTray, 18, 78, 530, value => {
            bool previous = settings.StartInTray;
            settings.StartInTray = value;
            try { SettingsStore.Save(settings); }
            catch (Exception ex) {
                settings.StartInTray = previous;
                MessageBox.Show(this, T("Settings could not be saved.") + Environment.NewLine + ex.Message, Text);
                Post(Render);
            }
        });
        if (!readable || startupRepairFailed)
            Caption(launch, T("Windows startup settings are unavailable."), 18, 114, 530, 36, small, colors.Alert);
    }
    void Choices(Control parent, int x, int y, string[] values, string[] labels, string value, Action<string> change) {
        var box = Combo(parent, x, y, 350);
        box.Items.AddRange(labels); box.SelectedIndex = Math.Max(0, Array.IndexOf(values, value));
        box.SelectionChangeCommitted += delegate { change(values[box.SelectedIndex]); Save(); Post(Render); };
    }

    void ToggleControl() { settings.Enabled = !settings.Enabled; Save(); Render(); }
    void ChangePage(bool showSettings) {
        if (settingsPage == showSettings) return;
        settingsPage = showSettings;
        if (calibrating) {
            calibrating = false;
            Note("Ready", null, null);
            Render();
        } else DisplayPage(true);
    }
    void SelectProfile(int i) {
        if (i < 0 || i >= settings.Profiles.Count) return;
        if (i != settings.Selected && diagnosticEvent != null) diagnosticEvent("profile-changed");
        testTimer.Stop(); calibrating = false; settings.Selected = i;
        Note("Ready", null, null); Save(); Post(Render);
    }
    bool Save() {
        try { SettingsStore.Save(settings); return true; }
        catch (Exception ex) { MessageBox.Show(this, T("Settings could not be saved.") + Environment.NewLine + ex.Message, Text); return false; }
    }
    void Note(string kind, string result, string value) {
        lastKind = kind; lastResult = result; lastValue = value;
        UpdateStatus();
    }
    void UpdateStatus() {
        if (status != null && !status.IsDisposed) {
            status.Text = connected ? "AKG Ara  ·  " + T("Connected") : T("Waiting for microphone");
            status.ForeColor = connected ? colors.Good : colors.Muted;
        }
        if (last != null && !last.IsDisposed)
            last.Text = T(lastKind) + (lastResult == null ? "" : "  ·  " + T(lastResult))
                + (lastValue == null ? "" : "  " + lastValue);
    }
    void SetTrayState() {
        tray.Icon = !connected ? AraIcons.Disconnected : settings.Enabled ? AraIcons.Connected : AraIcons.Paused;
        string statusText = T(!connected ? "Waiting for microphone" : settings.Enabled ? "Control enabled" : "Control paused");
        string info = "Ara Control · " + active.Name + " · " + statusText;
        tray.Text = info.Substring(0, Math.Min(63, info.Length));
    }
    void BuildMenu() {
        while (menu.Items.Count > 0) menu.Items[0].Dispose();
        menu.Items.Add(T("Open"), null, delegate { Restore(); });
        var group = new ToolStripMenuItem(T("Active profile"));
        for (int i = 0; i < settings.Profiles.Count; i++) {
            int index = i;
            var item = new ToolStripMenuItem(settings.Profiles[i].Name.Replace("&", "&&"));
            item.Checked = i == settings.Selected;
            item.Click += delegate { SelectProfile(index); };
            group.DropDownItems.Add(item);
        }
        menu.Items.Add(group);
        menu.Items.Add(T(settings.Enabled ? "Pause control" : "Enable control"), null, delegate { ToggleControl(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(T("Exit"), null, delegate { Close(); });
        menu.Renderer = group.DropDown.Renderer = new ToolStripProfessionalRenderer(new MenuColors(colors));
        menu.BackColor = group.DropDown.BackColor = colors.Surface;
        menu.ForeColor = group.DropDown.ForeColor = colors.Text;
    }
    sealed class MenuColors : ProfessionalColorTable {
        readonly Palette palette;
        public MenuColors(Palette palette) { this.palette = palette; }
        public override Color ToolStripDropDownBackground { get { return palette.Surface; } }
        public override Color MenuBorder { get { return palette.Border; } }
        public override Color MenuItemSelected { get { return palette.Input; } }
        public override Color MenuItemBorder { get { return palette.Accent; } }
        public override Color ImageMarginGradientBegin { get { return palette.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return palette.Surface; } }
        public override Color ImageMarginGradientEnd { get { return palette.Surface; } }
        public override Color CheckBackground { get { return palette.Input; } }
        public override Color CheckSelectedBackground { get { return palette.Surface; } }
        public override Color SeparatorDark { get { return palette.Border; } }
        public override Color SeparatorLight { get { return palette.Border; } }
    }
    void Restore() { hideFirstShow = false; Show(); WindowState = FormWindowState.Normal; Activate(); }
    public void RequestShow() { Post(Restore); }

    void Post(Action action) {
        if (!IsHandleCreated || IsDisposed) return;
        try { BeginInvoke(action); } catch (InvalidOperationException) { }
    }
    void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) {
        Post(delegate { if (!dialogOpen && (settings.Theme == "system" || SystemInformation.HighContrast)) Render(); });
    }
    void OnConnection(bool value) {
        Post(delegate {
            if (connected == value) return;
            connected = value;
            UpdateStatus();
            SetTrayState();
            if (!value && diagnosticEvent != null) diagnosticEvent("disconnected");
        });
    }
    void OnDeviceEvent(string e) {
        if (diagnosticEvent != null && e == "button") {
            diagnosticEvent("button");
            return;
        }
        if (dialogOpen) return;
        if (calibrating) {
            if (e == "button") return;
            settings.ClockwiseCode = e == "up" ? "02" : "20";
            calibrating = false; Save(); Note("Clockwise direction saved.", null, null); Render();
            return;
        }
        string kind = e == "button" ? "Press" : "Turn";
        if (!settings.Enabled || (e != "button" && !active.Wheel)) { Note(kind, null, null); return; }
        Send(GetChord(e), kind);
    }
    string GetChord(string e) { return active.GetShortcut(e); }
    void Test() {
        testTimer.Stop();
        if (active.FocusOnly) {
            Note("Switch to the target window within 3 seconds.", null, null);
            testTimer.Interval = 3000;
            testTimer.Start();
        } else Send(active.Button, "Test shortcut");
    }
    void Send(string chord, string kind) {
        if (String.IsNullOrWhiteSpace(chord)) { Note(kind, "Shortcut is unassigned.", null); return; }
        var attempt = Dispatch(active, chord, true);
        Note(kind, attempt.Result, attempt.Detail);
    }
    static ShortcutAttempt Dispatch(Profile profile, string chord, bool enabled) {
        var attempt = new ShortcutAttempt();
        uint pid;
        GetWindowThreadProcessId(GetForegroundWindow(), out pid);
        Process[] apps = null;
        try {
            apps = Process.GetProcessesByName(profile.Process);
            attempt.Running = apps.Length != 0;
            if (!enabled) attempt.Result = "Control paused";
            else if (String.IsNullOrWhiteSpace(chord)) attempt.Result = "Shortcut is unassigned.";
            else if (!attempt.Running.Value) attempt.Result = "The target application is not running.";
            else if (profile.FocusOnly && !apps.Any(p => p.Id == pid)) attempt.Result = "The target window is not active.";
            else {
                attempt.Result = Shortcuts.Send(chord) ? "Sent" : "Windows rejected the shortcut.";
                attempt.Detail = chord;
            }
        } catch (Exception ex) {
            attempt.Result = attempt.Running.HasValue ? "Windows rejected the shortcut." : "Status unavailable";
            attempt.Detail = ex.Message;
        } finally {
            if (apps != null) foreach (var p in apps) p.Dispose();
        }
        return attempt;
    }

    Form MakeDialog(string titleKey, int w, int h) {
        return new Form { Text = T(titleKey), Font = normal, BackColor = colors.Background, ForeColor = colors.Text,
            ClientSize = new Size(U(w), U(h)), FormBorderStyle = FormBorderStyle.FixedDialog, Icon = AraIcons.Application,
            AutoScaleMode = AutoScaleMode.None, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent };
    }
    void EditProfile(bool adding) {
        dialogOpen = true; testTimer.Stop();
        try {
            using (var form = MakeDialog(adding ? "Add profile" : "Edit profile", 430, 242)) {
                Caption(form, T("Profile name"), 20, 10, 385, 27, normal, colors.Text);
                var nameField = new ProfileField(colors, scale) { Font = normal };
                Put(form, nameField, 20, 42, 390, 34);
                var name = new TextBox { Text = adding ? "" : active.Name, MaxLength = 80, AccessibleName = T("Profile name"),
                    BorderStyle = BorderStyle.None, ForeColor = colors.Text, BackColor = colors.Input, Font = normal };
                nameField.SetEditor(name);
                Caption(form, T("Application process"), 20, 85, 385, 27, normal, colors.Text);
                var processField = new ProfileField(colors, scale) { Font = normal };
                Put(form, processField, 20, 118, 219, 34);
                var process = Combo(processField, 0, 0, 390);
                process.DropDownStyle = ComboBoxStyle.DropDown;
                process.AccessibleName = T("Application process");
                processField.SetEditor(process);
                var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var proc in Process.GetProcesses()) {
                    using (proc) { try { names.Add(proc.ProcessName); } catch (InvalidOperationException) {} catch (System.ComponentModel.Win32Exception) {} }
                }
                process.Items.AddRange(names.Cast<object>().ToArray()); process.Text = adding ? "" : active.Process;
                Btn(form, T("Choose window"), 247, 118, 163, delegate {
                    var picked = WindowPicker.Pick(form, T("Click an application window. Press Esc to cancel."),
                        T("Choose an open application window, or press Esc to cancel."));
                    if (picked == null) return;
                    process.Text = picked;
                    if (adding && String.IsNullOrWhiteSpace(name.Text)) name.Text = picked;
                }, false);
                var ok = Btn(form, T("Save"), 22, 183, 183, delegate {
                    string n = name.Text.Trim(); string p = process.Text.Trim();
                    if (p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) p = p.Substring(0, p.Length - 4);
                    if (String.IsNullOrWhiteSpace(n) || String.IsNullOrWhiteSpace(p) || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                        MessageBox.Show(form, T("Enter a profile name and process."), Text); return;
                    }
                    if (settings.Profiles.Any(x => (adding || x != active) && String.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase))) {
                        MessageBox.Show(form, T("This profile name already exists."), Text); return;
                    }
                    if (adding) {
                        var profile = SettingsStore.NewProfile(n, p);
                        if (!String.IsNullOrEmpty(profile.Button) && settings.Profiles.Any(x => EqualChord(x.Button, profile.Button))) profile.Button = "";
                        settings.Profiles.Add(profile); settings.Selected = settings.Profiles.Count - 1;
                    } else { active.Name = n; active.Process = p; }
                    Save(); form.DialogResult = DialogResult.OK;
                }, true);
                var cancel = Btn(form, T("Cancel"), 218, 183, 190, delegate { form.DialogResult = DialogResult.Cancel; }, false);
                form.AcceptButton = ok; form.CancelButton = cancel;
                if (form.ShowDialog(this) == DialogResult.OK) { Note("Ready", null, null); Render(); }
            }
        } finally { dialogOpen = false; }
    }
    static bool EqualChord(string a, string b) {
        if (String.IsNullOrWhiteSpace(a) || String.IsNullOrWhiteSpace(b)) return false;
        try { return Shortcuts.Parse(a).OrderBy(k => k).SequenceEqual(Shortcuts.Parse(b).OrderBy(k => k)); }
        catch { return false; }
    }
    void Record(string slot) {
        dialogOpen = true; testTimer.Stop();
        try {
            using (var form = new Recorder()) {
                form.Text = T("Record"); form.ClientSize = new Size(U(460), U(209)); form.AutoScaleMode = AutoScaleMode.None; form.Font = normal; form.BackColor = colors.Background;
                form.Icon = AraIcons.Application; form.FormBorderStyle = FormBorderStyle.FixedDialog; form.MinimizeBox = form.MaximizeBox = false;
                form.StartPosition = FormStartPosition.CenterParent; form.ShowInTaskbar = false;
                Caption(form, T("Press a shortcut…"), 20, 12, 420, 36, heading, colors.Text);
                var preview = Caption(form, ChordLabel(GetChord(slot)), 20, 56, 420, 37, heading, colors.Text);
                Caption(form, T("Press Esc to cancel"), 20, 101, 420, 28, normal, colors.Muted);
                Btn(form, T("Clear"), 20, 151, 119, delegate { form.Stage(""); }, false);
                var save = Btn(form, T("Save"), 160, 151, 135, delegate { form.DialogResult = DialogResult.OK; }, true);
                var cancel = Btn(form, T("Cancel"), 305, 151, 135, delegate { form.DialogResult = DialogResult.Cancel; }, false);
                save.Enabled = false;
                form.AcceptButton = save;
                form.CancelButton = cancel;
                form.ActiveControl = cancel;
                form.DraftChanged += delegate {
                    preview.Text = ChordLabel(form.Chord);
                    save.Enabled = true;
                    save.Focus();
                };
                if (form.ShowDialog(this) != DialogResult.OK || form.Chord == null) return;
                if (ChangeShortcut(slot, form.Chord, false)) Render();
            }
        } finally { dialogOpen = false; }
    }
    sealed class Recorder : Form {
        public event EventHandler DraftChanged;
        public string Chord { get; private set; }
        public void Stage(string chord) {
            Chord = chord;
            var changed = DraftChanged;
            if (changed != null) changed(this, EventArgs.Empty);
        }
        bool IsDialogKey(Keys keys) {
            return keys == Keys.Escape || keys == Keys.Tab || keys == (Keys.Tab | Keys.Shift)
                || keys == Keys.Enter || keys == Keys.Space || keys == (Keys.Alt | Keys.F4);
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            if (IsDialogKey(keyData)) return base.ProcessCmdKey(ref msg, keyData);
            string chord = Shortcuts.Format(keyData);
            if (chord != null) Stage(chord);
            return true;
        }
        protected override bool ProcessKeyPreview(ref Message msg) {
            if (msg.Msg == 0x0100 || msg.Msg == 0x0104) {
                Keys keyData = (Keys)msg.WParam.ToInt32() | ModifierKeys;
                if (!IsDialogKey(keyData)) {
                    string chord = Shortcuts.Format(keyData);
                    if (chord != null) Stage(chord);
                    return true;
                }
            }
            return base.ProcessKeyPreview(ref msg);
        }
    }
    protected override void Dispose(bool disposing) {
        if (disposing) {
            SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
            tray.Visible = false; tray.Dispose(); menu.Dispose(); testTimer.Dispose();
        }
        base.Dispose(disposing);
        if (disposing) { foreach (Font f in fonts) f.Dispose(); fonts.Clear(); }
    }
}






