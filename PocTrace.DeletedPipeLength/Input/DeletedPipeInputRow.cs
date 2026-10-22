namespace PocTrace.DeletedPipeLength.Input
{
    public sealed class DeletedPipeInputRow
    {
        public int RowNumber { get; set; }
        public string SiteRaw { get; set; }
        public string MappedLine { get; set; }
        public string EqpId { get; set; }
        public string LoadStatus { get; set; }
        public bool IsValid { get; set; }
    }
}
