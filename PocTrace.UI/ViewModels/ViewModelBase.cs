using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PocTrace.UI.ViewModels
{
    // INotifyPropertyChanged 구현을 캡슐화한 부모 클래스
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // 속성 변경 알림을 편하게 보내기 위한 Helper 메서드
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}