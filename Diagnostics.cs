using System;
using System.Drawing;
using System.Windows.Forms;

sealed partial class MainWindow
{
    // Device notifications and the diagnostic timer are dispatched on the UI thread.
    // Removing this handler completes the one-shot check before any keys are sent.
    Action<string> diagnosticEvent;

    sealed class ShortcutAttempt
    {
        public bool? Running;
        public string Result;
        public string Detail;
    }

    Label DiagnosticRow(Control parent, int y, string title, string value)
    {
        Caption(parent, T(title), 18, y, 166, 45, small, colors.Muted);
        return Caption(parent, value, 191, y, 331, 45, normal, colors.Text);
    }

    void CheckButton()
    {
        testTimer.Stop();
        if (calibrating) {
            calibrating = false;
            Note("Ready", null, null);
            Render();
        }
        var profile = active;
        using (var form = MakeDialog("Check button", 580, 378))
        using (var timeout = new System.Windows.Forms.Timer { Interval = 30000 }) {
            Caption(form, profile.Name, 20, 10, 540, 32, heading, colors.Text);
            var help = Caption(form, T(profile.FocusOnly
                ? "Activate the target window, then press the microphone button within 30 seconds."
                : "Press the button on your AKG Ara within 30 seconds."),
                20, 47, 540, 53, normal, colors.Text);
            var rows = new RoundedCard { BackColor = colors.Surface };
            Put(form, rows, 20, 106, 540, 151);
            var buttonState = DiagnosticRow(rows, 6, "Physical button", T("Waiting for a press"));
            var application = DiagnosticRow(rows, 52, "Target application", T("Not checked"));
            var shortcut = DiagnosticRow(rows, 98, "Keyboard shortcut", T("Not sent"));
            var result = Caption(form, ChordLabel(profile.Button), 20, 263, 540, 46, small, colors.Muted);
            var close = Btn(form, T("Cancel"), 393, 322, 167, form.Close, false);
            form.CancelButton = close;

            diagnosticEvent = delegate(string signal) {
                diagnosticEvent = null;
                timeout.Stop();
                close.Text = T("Close");
                if (signal != "button") {
                    string reason = signal == "timeout" ? "No press received within 30 seconds."
                        : signal == "profile-changed" ? "The selected profile changed."
                        : signal == "unavailable" ? "Connect the AKG Ara and try again." : "Microphone disconnected.";
                    help.Text = T(reason);
                    buttonState.Text = T("Not checked");
                    return;
                }

                buttonState.Text = T("Received");
                buttonState.ForeColor = colors.Good;
                var attempt = Dispatch(profile, profile.Button, settings.Enabled);
                string state = !attempt.Running.HasValue ? "Status unavailable" : attempt.Running.Value ? "Running" : "Not running";
                application.Text = profile.Process + "  ·  " + T(state);
                bool sent = attempt.Result == "Sent";
                shortcut.Text = sent ? T("Sent") + "  ·  " + profile.Button : T("Not sent");
                if (sent) shortcut.ForeColor = colors.Good;
                help.Text = T(sent ? "Check the result in the target application." : attempt.Result);
                result.Text = !sent && attempt.Detail != null ? attempt.Detail : ChordLabel(profile.Button);
                Note("Press", attempt.Result, attempt.Detail);
            };
            timeout.Tick += delegate { if (diagnosticEvent != null) diagnosticEvent("timeout"); };
            try {
                dialogOpen = true;
                if (connected) timeout.Start();
                else diagnosticEvent("unavailable");
                form.ShowDialog(this);
            } finally {
                diagnosticEvent = null;
                dialogOpen = false;
                timeout.Stop();
            }
        }
    }
}
