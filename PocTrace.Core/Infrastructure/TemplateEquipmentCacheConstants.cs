namespace PocTrace.Core.Infrastructure
{
  /// <summary>
  /// 철거 검수 전용 3-% Equipment sidecar (GraphCache에 넣지 않음).
  /// Magic PTTE.
  /// </summary>
  public static class TemplateEquipmentCacheConstants
  {
    public const int CurrentVersion = 1;
    /// <summary>Manifest magic 'PTTE'.</summary>
    public const int ManifestMagic = 0x50545445;
    public const string ManifestFileName = "manifest.bin";
    public const string NodesFileName = "nodes.bin";
  }
}
