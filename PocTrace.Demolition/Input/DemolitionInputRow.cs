using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PocTrace.Demolition.Input
{
  /// <summary>Input Excel 한 행.</summary>
  public sealed class DemolitionInputRow : INotifyPropertyChanged
  {
    private string _traceStatus;

    public int RowNumber { get; set; }
    public string SiteRaw { get; set; }
    public string MappedLine { get; set; }
    public string Process { get; set; }
    public string ConstructionCode { get; set; }
    public string EqpId { get; set; }
    public string LoadStatus { get; set; }

    public string TraceStatus
    {
      get { return _traceStatus; }
      set
      {
        if (_traceStatus == value) return;
        _traceStatus = value;
        OnPropertyChanged();
      }
    }

    public bool IsValid =>
      string.Equals(LoadStatus, "OK", System.StringComparison.OrdinalIgnoreCase)
      && !string.IsNullOrWhiteSpace(EqpId)
      && !string.IsNullOrWhiteSpace(MappedLine);

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string name = null)
    {
      var handler = PropertyChanged;
      if (handler != null)
        handler(this, new PropertyChangedEventArgs(name));
    }
  }
}
