using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class RoundedPaint
{
    internal static GraphicsPath Path(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter < 1) {
            path.AddRectangle(bounds);
            return path;
        }
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void Surface(Graphics graphics, Control control, Color fill, Color border, int radius)
    {
        float scale = graphics.DpiX / 96f;
        var canvas = control.Parent == null ? SystemColors.Control : control.Parent.BackColor;
        using (var brush = new SolidBrush(canvas))
            graphics.FillRectangle(brush, control.ClientRectangle);
        if (control.ClientSize.Width < 2 || control.ClientSize.Height < 2) return;

        GraphicsState state = graphics.Save();
        try {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float line = Math.Max(1f, scale);
            RectangleF bounds = control.ClientRectangle;
            bounds.Inflate(-line / 2f, -line / 2f);
            using (var outline = Path(bounds, radius * scale))
            using (var brush = new SolidBrush(fill)) {
                graphics.FillPath(brush, outline);
                using (var pen = new Pen(border.A == 0 ? fill : border, line))
                    graphics.DrawPath(pen, outline);
            }
        } finally {
            graphics.Restore(state);
        }
    }

    internal static Color Blend(Color from, Color to, float amount)
    {
        return Color.FromArgb(
            (int)Math.Round(from.R + (to.R - from.R) * amount),
            (int)Math.Round(from.G + (to.G - from.G) * amount),
            (int)Math.Round(from.B + (to.B - from.B) * amount));
    }
}

internal sealed class RoundedCard : Panel
{
    public int CornerRadius { get; set; }
    public Color BorderColor { get; set; }

    public RoundedCard()
    {
        CornerRadius = 14;
        BorderColor = Color.Transparent;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        RoundedPaint.Surface(e.Graphics, this, BackColor, BorderColor, CornerRadius);
    }
}

// Keep the native combo box handle and its keyboard, accessibility and popup support.
// Only the closed, non-editable face is drawn here; the popup uses DrawItem as usual.
internal sealed class RoundedComboBox : ComboBox
{
    const int WmPaint = 0x000F, WmPrint = 0x0317, WmPrintClient = 0x0318;
    const int PrintClientFlag = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct PaintInfo
    {
        public IntPtr Hdc;
        public int Erase;
        public NativeRect Clip;
        public int Restore, Update;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Reserved;
    }
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr window, out PaintInfo info);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr window, ref PaintInfo info);

    public Color OutlineColor { get; set; }

    public RoundedComboBox()
    {
        OutlineColor = SystemColors.ControlDark;
    }

    protected override void WndProc(ref Message m)
    {
        if (DropDownStyle == ComboBoxStyle.DropDownList) {
            if (m.Msg == WmPaint) {
                PaintInfo paint;
                IntPtr hdc = BeginPaint(Handle, out paint);
                try { DrawFace(hdc); } finally { EndPaint(Handle, ref paint); }
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == WmPrintClient || (m.Msg == WmPrint && (m.LParam.ToInt64() & PrintClientFlag) != 0)) {
                DrawFace(m.WParam);
                m.Result = IntPtr.Zero;
                return;
            }
        }
        base.WndProc(ref m);
    }

    private void DrawFace(IntPtr hdc)
    {
        if (hdc == IntPtr.Zero || Width < 2 || Height < 2) return;
        using (Graphics target = Graphics.FromHdc(hdc))
        using (BufferedGraphics frame = BufferedGraphicsManager.Current.Allocate(target, ClientRectangle)) {
            Graphics g = frame.Graphics;
            float scale = g.DpiX / 96f;
            Color ink = Enabled ? ForeColor : (SystemInformation.HighContrast
                ? SystemColors.GrayText : RoundedPaint.Blend(ForeColor, BackColor, .56f));
            Color stroke = Focused && ShowFocusCues ? ForeColor : OutlineColor;
            RoundedPaint.Surface(g, this, BackColor, stroke, 8);
            int pad = (int)Math.Round(10 * scale), arrowWidth = (int)Math.Round(31 * scale);
            var textRect = new Rectangle(pad, 0, Math.Max(0, Width - pad - arrowWidth), Height);
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                        TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, Text, Font, textRect, ink, flags);

            float x = Width - arrowWidth / 2f, y = Height / 2f;
            var chevron = new[] { new PointF(x - 3.5f * scale, y - 1.5f * scale),
                                  new PointF(x, y + 2f * scale),
                                  new PointF(x + 3.5f * scale, y - 1.5f * scale) };
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(ink, 1.5f * scale)) {
                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, chevron);
            }
            frame.Render(target);
        }
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
    protected override void OnDropDownClosed(EventArgs e) { base.OnDropDownClosed(e); Invalidate(); }
}

internal sealed class RoundedButton : Button
{
    private bool hover, mouseDown, spaceDown;
    public int CornerRadius { get; set; }

    public RoundedButton()
    {
        CornerRadius = 9;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        mouseDown = e.Button == MouseButtons.Left;
        base.OnMouseDown(e);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        mouseDown = false;
        base.OnMouseUp(e);
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture) mouseDown = false;
        base.OnMouseCaptureChanged(e);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) spaceDown = true;
        base.OnKeyDown(e);
        Invalidate();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) spaceDown = false;
        base.OnKeyUp(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        spaceDown = false;
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Color text = ForeColor, fill = BackColor;
        bool pressed = spaceDown || (hover && mouseDown);
        if (!Enabled) {
            text = SystemInformation.HighContrast ? SystemColors.GrayText : RoundedPaint.Blend(text, fill, .56f);
        } else if (hover || pressed) {
            if (SystemInformation.HighContrast) {
                fill = SystemColors.Highlight;
                text = SystemColors.HighlightText;
            } else {
                fill = RoundedPaint.Blend(fill, text, pressed ? .15f : .08f);
            }
        }

        RoundedPaint.Surface(e.Graphics, this, fill, FlatAppearance.BorderColor, CornerRadius);
        float scale = e.Graphics.DpiX / 96f;
        int padding = (int)Math.Ceiling(5 * scale);
        var bounds = Rectangle.Inflate(ClientRectangle, -padding, -1);
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping;
        if (!UseMnemonic) flags |= TextFormatFlags.NoPrefix;
        else if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, text, flags);

        if (Focused && ShowFocusCues) {
            RectangleF focus = ClientRectangle;
            focus.Inflate(-3f * scale, -3f * scale);
            if (focus.Width > 0 && focus.Height > 0) {
                var state = e.Graphics.Save();
                try {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var outline = RoundedPaint.Path(focus, Math.Max(0, CornerRadius - 3) * scale))
                    using (var pen = new Pen(text, Math.Max(1, scale)) { DashStyle = DashStyle.Dot })
                        e.Graphics.DrawPath(pen, outline);
                } finally { e.Graphics.Restore(state); }
            }
        }
    }
}
