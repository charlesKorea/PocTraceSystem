namespace PocTrace.Core.Engine
{
    /// <summary>배치 업로드에서 태그가 없거나 배관이 없을 때의 결과 문구.</summary>
    public static class BatchPlacement
    {
        public const string Unplaced = "미배치설비";
        public const string Undesigned = "미설계 배치 설비";
        public const string IntegrityIssue = "설비정합오류";
        public const string IntegrityOwner = "ft 기술";
    }
}
