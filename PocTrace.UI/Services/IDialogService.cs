namespace PocTrace.UI.Services
{
    /// <summary>
    /// [Refactor] ViewModel이 UI 프레임워크(WinForms MessageBox)에 직접 의존하지 않도록 하는 다이얼로그 추상화.
    /// ViewModel은 이 인터페이스에만 의존하고, 실제 표시는 View 계층의 구현체(<see cref="WinFormsDialogService"/>)가 담당한다.
    /// </summary>
    public interface IDialogService
    {
        void ShowInfo(string message, string title = "Info");
        void ShowWarning(string message, string title = "Warning");
        void ShowError(string message, string title = "Error");
    }
}
