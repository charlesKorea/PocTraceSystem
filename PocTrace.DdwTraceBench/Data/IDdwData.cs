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
    }
}
