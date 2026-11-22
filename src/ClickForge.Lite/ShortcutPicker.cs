using ClickForge.Core;
namespace ClickForge.Lite;

internal sealed class ShortcutPicker : Form
{
    private readonly Label _prompt = new()
    {
        Text = "Нажмите нужную клавишу или кнопку мыши в этом поле.\nEsc — отмена. F11 — аварийный стоп.",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter
    };

    public int SelectedKey { get; private set; }

    public ShortcutPicker()
    {
        Text = UiText.Get("Кнопка запуска / остановки");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(365, 115);
        _prompt.MouseDown += (_, e) =>
        {
            int key = e.Button switch
            {
                MouseButtons.Left => 1, MouseButtons.Right => 2, MouseButtons.Middle => 4,
                MouseButtons.XButton1 => 5, MouseButtons.XButton2 => 6, _ => 0
            };
            if (key != 0) SelectKey(key);
        };
        Controls.Add(_prompt);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        if (key == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
        if (key == Keys.F11) { _prompt.Text = UiText.Get("F11 зарезервирована. Нажмите другую кнопку."); return true; }
        if (key != Keys.None) { SelectKey((int)key); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void SelectKey(int key)
    {
        SelectedKey = key;
        DialogResult = DialogResult.OK;
    }
}
