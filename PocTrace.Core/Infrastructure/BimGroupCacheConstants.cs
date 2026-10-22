namespace PocTrace.Core.Infrastructure
{
  /// <summary>
  /// GraphCache와 분리된 Group(BIM Group) sidecar 캐시.
  /// Magic PTBG — GraphCache PTCH와 구분. nodes.bin OUTER APPLY 재도입 금지.
  /// v1: names.bin (Name만)
  /// v2: groups.bin (GroupId + Name + ParentId)
  /// </summary>
  public static class BimGroupCacheConstants
  {
    public const int CurrentVersion = 2;
    /// <summary>Manifest magic 'PTBG' (PocTrace Bim Group).</summary>
    public const int ManifestMagic = 0x50544247;
    public const string ManifestFileName = "manifest.bin";
    public const string NamesFileName = "names.bin";
    public const string GroupsFileName = "groups.bin";
  }
}
