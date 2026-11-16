using System.Collections.Generic;

namespace PocTrace.DdwTraceBench.Data
{
    /// <summary>
    /// DDW tb_* 조회 경계. 모든 조회는 version_sequence &lt; 1000000000 인 행만 돌려준다.
    /// 키는 0x 대문자 16진 문자열이다.
    /// </summary>
    public interface IDdwData
    {
        IList<string> Warnings { get; }

        IList<TagOwnerRow> FindTagOwners(string tag);
        IDictionary<string, ModelRow> GetModels(IList<string> instanceIds);
        IDictionary<string, TemplateRow> GetTemplates(IList<string> modelTemplateIds);
        IDictionary<string, PocRow> GetPocsByIds(IList<string> pocInstanceIds);
        IList<PocRow> GetPocsByOwners(IList<string> ownerInstanceIds);
        IDictionary<string, PipeRow> GetPipes(IList<string> pipeInstanceIds);
        IDictionary<string, PipeSizeRow> GetPipeSizes(IList<string> pipeSizeIds);
        IDictionary<string, PocTemplateRow> GetPocTemplates(IList<string> pocTemplateIds);

        /// <summary>
        /// tb_attributes 를 owner_id 와 attribute_nm 목록으로 읽는다. Takeoff 종료 행의 tag, connectiontag, hookup number 용이다.
        /// </summary>
        IList<AttributeRow> GetAttributes(IList<string> ownerIds, IList<string> attributeNames);

        /// <summary>
        /// tb_division. CONVERT(uniqueidentifier, division_id) 로 맞춘 행의 division_value.
        /// </summary>
        IDictionary<string, DivisionRow> GetDivisions(IList<string> divisionIds);
    }
}
