using System;
using System.Collections.Generic;

namespace PocTrace.Core.Engine.DeletedPipe
{
    /// <summary>
    /// 삭제 배관 길이 VOC. GraphCache와 SqlQueries의 OperationType &lt; 2 경로는 사용하지 않는다.
    /// </summary>
    public sealed class DeletedPipeReport
    {
        public string DbName { get; set; }
        public DateTime Cutover { get; set; }
        public List<DeletedPipeSummaryRow> Summaries { get; set; } = new List<DeletedPipeSummaryRow>();
        public List<DeletedPipeDetailRow> Details { get; set; } = new List<DeletedPipeDetailRow>();
        public List<DeletedPipeUnmatchedRow> Unmatched { get; set; } = new List<DeletedPipeUnmatchedRow>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public sealed class DeletedPipeSummaryRow
    {
        public string DbName { get; set; }
        public string EqpId { get; set; }
        public int EquipmentCount { get; set; }
        public int PocCount { get; set; }
        public int PipeCount { get; set; }
        public double LengthMeters { get; set; }
        public double LengthMillimeters { get; set; }
        public string Note { get; set; }
    }

    public sealed class DeletedPipeDetailRow
    {
        public string DbName { get; set; }
        public string EqpId { get; set; }
        public Guid EquipmentInstanceId { get; set; }
        public string EquipmentTag { get; set; }
        public DateTime? EquipmentDeletedAt { get; set; }
        public Guid StartPocId { get; set; }
        public Guid PipeId { get; set; }
        public string UtilityName { get; set; }
        public double LengthMeters { get; set; }
        public double LengthMillimeters { get; set; }
        public DateTime? PipeDeletedAt { get; set; }
        public int Order { get; set; }
        public bool Shared { get; set; }
        public string UserName { get; set; }
        public string CompanyName { get; set; }
    }

    public sealed class DeletedPipeUnmatchedRow
    {
        public string DbName { get; set; }
        public string EqpId { get; set; }
        public string Reason { get; set; }
    }

    public sealed class DeletedEquipmentRow
    {
        public Guid InstanceId { get; set; }
        public string Tag { get; set; }
        public string Name { get; set; }
        public DateTime? OpTime { get; set; }
        public int OperationType { get; set; }
        public int Division { get; set; }
        public string Sub1Name { get; set; }
        public string Sub2Name { get; set; }
    }

    public sealed class DeletedPocRow
    {
        public Guid InstanceId { get; set; }
        public Guid OwnerId { get; set; }
        public Guid? Outlet { get; set; }
        public string Tag { get; set; }
        public string PocName { get; set; }
        public string UtilityName { get; set; }
        public DateTime? OpTime { get; set; }
        public int OperationType { get; set; }
    }

    public sealed class DeletedPipeRow
    {
        public Guid PipeId { get; set; }
        public Guid? Outlet1 { get; set; }
        public Guid? Outlet2 { get; set; }
        public Guid? Outlet3 { get; set; }
        public double Length { get; set; }
        public string UtilityName { get; set; }
        public DateTime? OpTime { get; set; }
        public int OperationType { get; set; }
        public string UserName { get; set; }
        public string CompanyName { get; set; }
    }

    public sealed class DeletedNodeRow
    {
        public Guid InstanceId { get; set; }
        public string Tag { get; set; }
        public string Name { get; set; }
        public DateTime? OpTime { get; set; }
        public int OperationType { get; set; }
        public int Division { get; set; }
        public string Sub1Name { get; set; }
        public string Sub2Name { get; set; }
    }

    public sealed class DeletedTakeoffRow
    {
        public Guid TakeoffId { get; set; }
        public string Tag { get; set; }
        public Guid? PipeId { get; set; }
        public DateTime? OpTime { get; set; }
        public int OperationType { get; set; }
    }
}
