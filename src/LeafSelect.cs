using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SoundLeaf
{
    internal static class LeafDrawing
    {
        internal static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            var path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (d <= 0) { path.AddRectangle(rect); return path; }
            path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
    }
    internal sealed class LeafSelect : Control
    {
        private static LeafSelect active;
        private int index = -1;
        private ToolStripDropDown popup;
        private Choices choices;
        internal readonly List<object> Items = new List<object>();
        internal event EventHandler SelectedIndexChanged;
        internal event Action<bool> PopupClosed;
        internal float Scale = 1;
        internal bool Dark;
        internal ToolStripDropDownCloseReason? LastPopupCloseReason { get; private set; }
        internal bool IsOpen { get { return popup != null && popup.Visible; } }
        internal static bool OpenFor(Form form) { return active != null && active.IsOpen && active.FindForm() == form; }
        internal static bool CloseFor(Form form) { if (!OpenFor(form)) return false; var owner = active; owner.ClosePopup(); owner.RestoreFocus(); return true; }
        internal int SelectedIndex
        {
            get { return index; }
            set
            {
                if (value < -1 || value >= Items.Count) throw new ArgumentOutOfRangeException("value");
                if (index == value) return; index = value; Invalidate(); AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }
        internal object SelectedItem { get { return index < 0 ? null : Items[index]; } set { SelectedIndex = Items.IndexOf(value); } }
        internal LeafSelect()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            TabStop = true; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.ComboBox;
        }
        protected override AccessibleObject CreateAccessibilityInstance() { return new SelectAccess(this); }
        private sealed class SelectAccess : ControlAccessibleObject
        {
            private readonly LeafSelect owner;
            internal SelectAccess(LeafSelect owner) : base(owner) { this.owner = owner; }
            public override AccessibleRole Role { get { return AccessibleRole.ComboBox; } }
            public override string Value { get { return owner.SelectedItem == null ? "" : owner.SelectedItem.ToString(); } set { for (int i = 0; i < owner.Items.Count; i++) if (owner.Items[i].ToString() == value) { owner.SelectedIndex = i; break; } } }
            public override string DefaultAction { get { return TextCatalog.T("choose"); } }
            public override AccessibleStates State { get { return base.State | (owner.IsOpen ? AccessibleStates.Expanded : AccessibleStates.Collapsed); } }
            public override int GetChildCount() { return owner.IsOpen ? 1 : 0; }
            public override AccessibleObject GetChild(int child) { return child == 0 && owner.IsOpen ? owner.choices.AccessibilityObject : null; }
            public override void DoDefaultAction() { if (owner.Enabled) owner.TogglePopup(); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var path = LeafDrawing.Rounded(rect, 7 * Scale))
            using (var fill = new SolidBrush(BackColor))
            using (var edge = new Pen(Focused || IsOpen ? Color.FromArgb(61, 152, 98) : Dark ? Color.FromArgb(63, 91, 73) : Color.FromArgb(197, 216, 200), Math.Max(1, Scale)))
            { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(edge, path); }
            string text = SelectedItem == null ? "" : SelectedItem.ToString();
            TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle((int)(10 * Scale), 0, Math.Max(1, Width - (int)(42 * Scale)), Height), Enabled ? ForeColor : Color.FromArgb(125, 144, 132), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            float cx = Width - 19 * Scale, cy = Height / 2f, direction = IsOpen ? -1 : 1;
            using (var pen = new Pen(Enabled ? ForeColor : Color.Gray, 1.6f * Scale))
            { pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round; e.Graphics.DrawLines(pen, new[] { new PointF(cx - 4 * Scale, cy - 2 * Scale * direction), new PointF(cx, cy + 2 * Scale * direction), new PointF(cx + 4 * Scale, cy - 2 * Scale * direction) }); }
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (Enabled && e.Button == MouseButtons.Left) { Focus(); TogglePopup(); } }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override bool ProcessCmdKey(ref Message msg, Keys key)
        {
            if (!Enabled) return base.ProcessCmdKey(ref msg, key);
            if (key == Keys.Enter || key == Keys.Space || key == (Keys.Alt | Keys.Down)) { TogglePopup(); return true; }
            if (key == Keys.Down || key == Keys.Up) { if (Items.Count > 0) SelectedIndex = Math.Max(0, Math.Min(Items.Count - 1, index + (key == Keys.Down ? 1 : -1))); return true; }
            if (key == Keys.Escape && IsOpen) { ClosePopup(); RestoreFocus(); return true; }
            return base.ProcessCmdKey(ref msg, key);
        }
        internal void TogglePopup()
        {
            if (IsOpen) { ClosePopup(); return; }
            if (!Enabled || Items.Count == 0) return;
            ClosePopup(); // Dispose a previously auto-closed dropdown before creating another.
            if (active != null) active.ClosePopup();
            var anchor = RectangleToScreen(ClientRectangle); Rectangle work = Screen.FromRectangle(anchor).WorkingArea;
            int row = Math.Max(24, (int)Math.Round(30 * Scale));
            int width = Math.Min(Width, Math.Max(1, work.Width - 8));
            int available = Math.Max(work.Bottom - anchor.Bottom - 4, anchor.Top - work.Top - 4);
            int rows = Math.Max(1, Math.Min(Items.Count, (available - 8) / row));
            int height = rows * row + 4;
            choices = new Choices(this, rows, row) { Size = new Size(width - 4, height - 4), Font = Font, BackColor = BackColor, ForeColor = ForeColor };
            popup = new ToolStripDropDown { AutoSize = false, Size = new Size(width, height), Padding = new Padding(2), Margin = Padding.Empty, BackColor = BackColor, AutoClose = true };
            var host = new ToolStripControlHost(choices) { AutoSize = false, Size = choices.Size, Margin = Padding.Empty, Padding = Padding.Empty };
            popup.Items.Add(host); active = this;
            LastPopupCloseReason = null;
            popup.Closed += delegate(object sender, ToolStripDropDownClosedEventArgs e)
            {
                LastPopupCloseReason = e.CloseReason;
                if (active == this) active = null; Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
                bool outside = e.CloseReason == ToolStripDropDownCloseReason.AppClicked || e.CloseReason == ToolStripDropDownCloseReason.AppFocusChange;
                if (PopupClosed != null) PopupClosed(outside);
            };
            int top = work.Bottom - anchor.Bottom >= height ? anchor.Bottom : anchor.Top - height;
            Point point = new Point(Math.Max(work.Left + 2, Math.Min(anchor.Left, work.Right - width - 2)), Math.Max(work.Top + 2, Math.Min(top, work.Bottom - height - 2)));
            popup.Show(this, PointToClient(point)); choices.Focus(); Invalidate(); AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        }
        internal void ClosePopup() { if (popup != null) { popup.Close(ToolStripDropDownCloseReason.CloseCalled); popup.Dispose(); popup = null; choices = null; } }
        internal Rectangle PopupBounds { get { return IsOpen ? popup.Bounds : Rectangle.Empty; } }
        private void RestoreFocus() { var form = FindForm(); if (!IsDisposed && form != null && form.Visible && !form.IsDisposed) { form.Activate(); Focus(); } }
        internal void Choose(int value)
        {
            ClosePopup(); RestoreFocus(); SelectedIndex = value;
        }
        protected override void Dispose(bool disposing) { if (disposing) ClosePopup(); base.Dispose(disposing); }
        private sealed class Choices : Control
        {
            private readonly LeafSelect owner; private readonly int count, row;
            private int selection;
            internal Choices(LeafSelect owner, int count, int row)
            {
                this.owner = owner; this.count = count; this.row = row; selection = Math.Max(0, owner.SelectedIndex);
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true); TabStop = true; AccessibleRole = AccessibleRole.List;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int start = selection / count * count;
                for (int n = 0; n < count && start + n < owner.Items.Count; n++)
                {
                    int item = start + n; var rect = new Rectangle(2, n * row + 1, Width - 4, row - 2);
                    if (item == selection) using (var path = LeafDrawing.Rounded(rect, 5 * owner.Scale)) using (var fill = new SolidBrush(owner.Dark ? Color.FromArgb(45, 84, 59) : Color.FromArgb(220, 237, 220))) e.Graphics.FillPath(fill, path);
                    TextRenderer.DrawText(e.Graphics, owner.Items[item].ToString(), Font, new Rectangle(rect.X + (int)(9 * owner.Scale), rect.Y, rect.Width - (int)(32 * owner.Scale), rect.Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    if (item == owner.SelectedIndex) using (var pen = new Pen(Color.FromArgb(57, 143, 87), 1.7f * owner.Scale))
                    { float x = rect.Right - 19 * owner.Scale, y = rect.Y + rect.Height / 2f; pen.StartCap = pen.EndCap = LineCap.Round; e.Graphics.DrawLines(pen, new[] { new PointF(x - 3 * owner.Scale, y), new PointF(x, y + 3 * owner.Scale), new PointF(x + 6 * owner.Scale, y - 4 * owner.Scale) }); }
                }
            }
            protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); int item = selection / count * count + e.Y / row; if (e.Button == MouseButtons.Left && item < owner.Items.Count) owner.Choose(item); }
            protected override void OnMouseWheel(MouseEventArgs e) { Move(e.Delta < 0 ? 1 : -1); }
            private void Move(int direction) { selection = Math.Max(0, Math.Min(owner.Items.Count - 1, selection + direction)); Invalidate(); AccessibilityNotifyClients(AccessibleEvents.Selection, selection); }
            protected override bool ProcessCmdKey(ref Message msg, Keys key)
            {
                if (key == Keys.Down || key == Keys.Up) { Move(key == Keys.Down ? 1 : -1); return true; }
                if (key == Keys.PageDown || key == Keys.PageUp) { Move(key == Keys.PageDown ? count : -count); return true; }
                if (key == Keys.Home || key == Keys.End) { selection = key == Keys.Home ? 0 : owner.Items.Count - 1; Invalidate(); return true; }
                if (key == Keys.Enter || key == Keys.Space) { owner.Choose(selection); return true; }
                if (key == Keys.Escape) { owner.ClosePopup(); owner.RestoreFocus(); return true; }
                if (key == Keys.Tab || key == (Keys.Shift | Keys.Tab)) { var form = owner.FindForm(); owner.ClosePopup(); owner.RestoreFocus(); if (form != null) form.SelectNextControl(owner, key == Keys.Tab, true, true, true); return true; }
                return base.ProcessCmdKey(ref msg, key);
            }
            protected override AccessibleObject CreateAccessibilityInstance() { return new ChoicesAccess(this); }
            private sealed class ChoicesAccess : ControlAccessibleObject
            {
                private readonly Choices choices;
                internal ChoicesAccess(Choices choices) : base(choices) { this.choices = choices; }
                public override AccessibleObject Parent { get { return choices.owner.AccessibilityObject; } }
                public override int GetChildCount() { return choices.owner.Items.Count; }
                public override AccessibleObject GetChild(int index) { return index < 0 || index >= GetChildCount() ? null : new ItemAccess(choices, index); }
                public override AccessibleObject GetSelected() { return GetChild(choices.selection); }
                public override AccessibleObject GetFocused() { return choices.Focused ? GetChild(choices.selection) : null; }
            }
            private sealed class ItemAccess : AccessibleObject
            {
                private readonly Choices choices; private readonly int index;
                internal ItemAccess(Choices choices, int index) { this.choices = choices; this.index = index; }
                public override string Name { get { return choices.owner.Items[index].ToString(); } set { } }
                public override AccessibleRole Role { get { return AccessibleRole.ListItem; } }
                public override AccessibleObject Parent { get { return choices.AccessibilityObject; } }
                public override Rectangle Bounds
                {
                    get { int first = choices.selection / choices.count * choices.count; return index < first || index >= first + choices.count ? Rectangle.Empty : choices.RectangleToScreen(new Rectangle(0, (index - first) * choices.row, choices.Width, choices.row)); }
                }
                public override AccessibleStates State { get { return AccessibleStates.Selectable | (Bounds.IsEmpty ? AccessibleStates.Offscreen : AccessibleStates.None) | (choices.selection == index ? AccessibleStates.Selected | (choices.Focused ? AccessibleStates.Focused : AccessibleStates.None) : AccessibleStates.None); } }
                public override string DefaultAction { get { return TextCatalog.T("choose"); } }
                public override void DoDefaultAction() { choices.owner.Choose(index); }
            }
        }
    }
}
