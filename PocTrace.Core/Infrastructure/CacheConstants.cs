namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// Binary cache layout version and freshness policy.
    /// v4: single GraphCache_{db}.bin
    /// v5: per-DB folder with section files (nodes/pocs/pipes/takeoffs/dynprops) + manifest
    /// v6: nodes.bin += InstanceNumericId + ConstructionCode (v5 still readable; fields default)
    /// v7: nodes.bin += BimId (ObjectInstanceLog.BIMID)
    /// v8: nodes.bin += BimGroupName — SQL 적재 금지(노드 OUTER APPLY 타임아웃).
    ///     BIM Group Name은 sidecar BimGroupCache_{db} (GetSelectBimGroupNames) 사용.
    /// v9: dynprops.bin += ConnectorUse (WEBDB CONNECTORUSE). v8 이하는 빈 값 → 조회 시 Y.
    /// v10: pipes.bin 끝점 목록(개수 + PipeEndpoint). v9 이하는 Outlet1~3 세 칸만 Endpoints로 읽는다.
    /// v11: pocs.bin 끝에 ConnectionOrder. v10 이하는 0.
    /// v12: nodes에 ModelType·DivisionName, pipes에 PipeType·PipeSizeMm, dynprops에 PipeNumber.
    /// v13: takeoffs에 ConnectionTag, PocId, DuctPipeId. v12 이하는 빈 값.
    /// v14: nodes에 PosX, PosY, PosZ. tb_modelinstances 좌표를 소수 1자리로 둔다. v13 이하는 0.
    /// v15: pocs에 PosX, PosY, PosZ. tb_pocinstances 좌표를 소수 1자리로 둔다. v14 이하는 0.
    /// Folder layout (independent of record version): GraphCache_{db}/current.txt
    /// points at g{utc}_{id}/ containing all section bins + manifest. Legacy flat
    /// GraphCache_{db}/*.bin is still readable. Manifest may append per-section versions.
    /// </summary>
    public static class CacheConstants
    {
        public const int CurrentVersion = 15;
        /// <summary>v5+ split still loadable (newer fields defaulted). Prefer Admin re-save to CurrentVersion.</summary>
        public const int MinimumSupportedVersion = 5;

        /// <summary>Soft stale warning (Viewer load banner / IsStale). Does not block startup.</summary>
        public const double DefaultStaleHours = 24;

        /// <summary>Hard gate: Viewer WinForms must not start if any TargetDatabase cache is older than this.</summary>
        public const double DefaultMaxAgeHours = 48;

        /// <summary>Manifest magic 'PTCH' (PocTrace Cache Header).</summary>
        public const int ManifestMagic = 0x50544348;

        public const string ManifestFileName = "manifest.bin";
        public const string SectionNodes = "nodes.bin";
        public const string SectionPocs = "pocs.bin";
        public const string SectionPipes = "pipes.bin";
        public const string SectionTakeoffs = "takeoffs.bin";
        public const string SectionDynProps = "dynprops.bin";
    }

    public enum CacheSectionKind
    {
        Nodes = 1,
        Pocs = 2,
        Pipes = 3,
        Takeoffs = 4,
        DynProps = 5
    }
}
