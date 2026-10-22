using System;

namespace PocTrace.Core.Infrastructure
{
  /// <summary>ConfigurationGroupLog 1건 (Group sidecar v2).</summary>
  public sealed class BimGroupRecord
  {
    public Guid GroupId { get; set; }
    public string Name { get; set; }
    public Guid ParentId { get; set; }
  }
}
