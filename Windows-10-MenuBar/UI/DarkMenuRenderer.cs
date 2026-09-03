using System.Drawing;
using WinForms = System.Windows.Forms;

namespace Windows_10_MenuBar.UI;

internal sealed class DarkMenuRenderer : WinForms.ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderItemText(WinForms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Color.White : Color.FromArgb(100, 100, 100);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(WinForms.ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(60, 60, 60));
        int y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }
}

internal sealed class DarkColorTable : WinForms.ProfessionalColorTable
{
    private static readonly Color Bg      = Color.FromArgb(30, 30, 30);
    private static readonly Color Hover   = Color.FromArgb(55, 55, 55);
    private static readonly Color Border  = Color.FromArgb(60, 60, 60);
    private static readonly Color Pressed = Color.FromArgb(45, 45, 45);

    public override Color MenuItemSelected               => Hover;
    public override Color MenuItemBorder                 => Border;
    public override Color MenuBorder                     => Border;
    public override Color ToolStripDropDownBackground    => Bg;
    public override Color MenuItemSelectedGradientBegin  => Hover;
    public override Color MenuItemSelectedGradientEnd    => Hover;
    public override Color MenuItemPressedGradientBegin   => Pressed;
    public override Color MenuItemPressedGradientEnd     => Pressed;
    public override Color ImageMarginGradientBegin       => Bg;
    public override Color ImageMarginGradientMiddle      => Bg;
    public override Color ImageMarginGradientEnd         => Bg;
    public override Color SeparatorLight                 => Border;
    public override Color SeparatorDark                  => Border;
}
