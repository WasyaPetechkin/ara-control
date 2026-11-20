using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Keep the platform text editor (including the editable combo's keyboard and
// popup behaviour). The viewport exposes its text area inside a themed border.
internal sealed class ProfileField : Panel
{
    readonly Palette palette;
    readonly float scale;
    readonly Panel viewport = new Panel();
    Control editor;
    ComboBox combo;

    public ProfileField(Palette palette, float scale)
    {
        this.palette = palette;
        this.scale = scale;
        BackColor = viewport.BackColor = palette.Input;
        TabStop = viewport.TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Controls.Add(viewport);
    }

    public void SetEditor(Control control)
    {
        editor = control;
        combo = control as ComboBox;
        viewport.Controls.Add(editor);
        editor.GotFocus += delegate { Invalidate(); };
        editor.LostFocus += delegate { Invalidate(); };
        PerformLayout();
    }

    int Pixels(int value) { return (int)Math.Round(value * scale); }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (editor == null) return;
        int left = Pixels(10);
        int right = combo == null ? left : Pixels(32);
        int height = Math.Min(editor.Font.Height + Pixels(2), ClientSize.Height);
        viewport.SetBounds(left, (Height - height) / 2, Math.Max(1, Width - left - right), height);
        if (combo == null) {
            editor.SetBounds(0, (height - editor.Height) / 2, viewport.Width, editor.Height);
        } else {
            // Position the native frame and arrow outside the visible viewport.
            // The popup is a separate window and still shows the full list.
            combo.SetBounds(-Pixels(1), (height - combo.Height) / 2, Width - left + Pixels(1), combo.Height);
            combo.DropDownWidth = Math.Max(1, Width);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        RoundedPaint.Surface(e.Graphics, this, BackColor, ContainsFocus ? palette.Accent : palette.Border, 8);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (combo == null) return;
        float x = ClientSize.Width - Pixels(16);
        float y = ClientSize.Height / 2f;
        using (var pen = new Pen(palette.Text, 1.5f * scale)) {
            pen.StartCap = pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawLines(pen, new[] { new PointF(x - 3.5f * scale, y - 1.5f * scale),
                new PointF(x, y + 2 * scale), new PointF(x + 3.5f * scale, y - 1.5f * scale) });
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || editor == null) return;
        editor.Focus();
        if (combo != null && e.X >= Width - Pixels(32))
            combo.DroppedDown = !combo.DroppedDown;
    }
}

