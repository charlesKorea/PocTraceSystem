using System;
using System.Collections.Generic;

namespace PocTrace.Core.Engine.DeletedPipe
{
    /// <summary>
    /// 삭제 배관 길이 VOC. 공유 GraphCache는 쓰지 않는다. 최신 로그는 exe 옆 스냅샷으로 읽는다.
    /// </summary>
    public sealed class DeletedPipeReport
    {
        public string DbName { get; set; }
        public DateTime Cutover { get; set; }
        public List<DeletedPipeSummaryRow> Summaries { get; set; } = new List<DeletedPipeSummaryRow>();
        public List<DeletedFittingBomRow> Bom { get; set; } = new List<DeletedFittingBomRow>();
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
        public int FittingCount { get; set; }
        public double LengthMeters { get; set; }
        public double LengthMillimeters { get; set; }
        public string Kind { get; set; }
        public string Note { get; set; }
    }

    /// <summary>피팅 이름·QCode별 수량. 인스턴스 목록은 쓰지 않는다.</summary>
    public sealed class DeletedFittingBomRow
    {
        public string DbName { get; set; }
        public string EqpId { get; set; }
        public string FittingName { get; set; }
        public string QCode { get; set; }
        public int Count { get; set; }
    }

    public sealed class DeletedPipeDetailRow
    {
        public string DbName { get; set; }
        public string EqpId { get; set; }
        public Guid EquipmentInstanceId { get; set; }
        public string EquipmentInstanceName { get; set; }
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
        public string ConstructionCode { get; set; }
        public string QCode { get; set; }
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
        public string ConstructionCode { get; set; }
        public string QCode { get; set; }
    }

    public enum DeletedWalkMode
    {
        DeletedSinceCutover = 0,
        StillPresent = 1
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
