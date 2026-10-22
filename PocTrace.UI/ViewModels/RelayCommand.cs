using System;
using System.Windows.Input; // ICommand 인터페이스 사용

namespace PocTrace.UI.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        // [수정] WPF 전용 CommandManager 의존성 제거
        // WinForms에서는 단순히 이벤트만 정의하면 됩니다.
        public event EventHandler CanExecuteChanged;

        // (선택사항) 외부에서 강제로 버튼 활성화 상태를 갱신해야 할 때 사용
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}