using System.Windows.Forms;

namespace PocTrace.UI.Services
{
    /// <summary>
    /// <see cref="IDialogService"/>의 WinForms(MessageBox) 구현. View 계층에 위치한다.
    /// </summary>
    public class WinFormsDialogService : IDialogService
    {
        public void ShowInfo(string message, string title = "Info")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
