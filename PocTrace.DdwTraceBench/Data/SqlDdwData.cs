using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PocTrace.DdwTraceBench.Data
{
    /// <summary>
    /// FRM 서버에 붙은 연결로 Linked Server 4-part 이름의 tb_* 를 읽는다.
    /// 원격 테이블을 로컬 임시 테이블과 조인하지 않는다. 키 목록은 0x 리터럴 IN 으로 200개씩 보낸다.
    /// id 열은 binary 이든 uniqueidentifier 이든 CONVERT(varbinary(16), ...) 로 받는다.
    /// </summary>
    public sealed class SqlDdwData : IDdwData
    {
        private const int BatchSize = 200;
        private const string Live = "1000000000";

        private const string TAttributes = "tb_attributes";
        private const string TModels = "tb_modelinstances";
        private const string TTemplates = "tb_modeltemplates";
        private const string TPocTemplates = "tb_poctemplates";
        private const string TPocs = "tb_pocinstances";
        private const string TPipes = "tb_pipeinstances";

        private readonly SqlConnection _conn;
        private readonly DdwSettings _settings;
        private readonly Action<string> _log;
        private readonly string _prefix;
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _schemaNotes = new List<string>();
        private Dictionary<string, HashSet<string>> _columns;

        private string _attrValue;
        private string _pocOwner;
        private string _pipeSizeTable;

        public SqlDdwData(SqlConnection connection, DdwSettings settings, Action<string> log)
        {
            _conn = connection;
            _settings = settings;
            _log = log ?? (s => { });
            _prefix = settings.Prefix;
        }

        public IList<string> Warnings { get { return _warnings; } }

        public IList<string> SchemaNotes { get { return _schemaNotes; } }

        public void Initialize()
        {
            using (var iso = new SqlCommand("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;", _conn))
                iso.ExecuteNonQuery();

            LoadColumns();

            var missing = new List<string>();
            Require(missing, TAttributes, "owner_id", "attribute_nm", "version_sequence");
            Require(missing, TModels, "instance_id", "model_type", "model_template_id", "version_sequence");
            Require(missing, TTemplates, "model_template_id", "display_nm", "family_key", "bim_yn", "version_sequence");
            Require(missing, TPocTemplates, "poc_template_id", "model_template_id", "poc_template_nm", "version_sequence");
            Require(missing, TPocs, "instance_id", "poc_template_id", "connected_poc_id", "version_sequence");
            Require(missing, TPipes, "instance_id", "length2", "version_sequence");

            _attrValue = Pick(TAttributes, "attribute_val", "attribute_value");
            if (_attrValue == null)
                missing.Add(TAttributes + ".attribute_val(attribute_value)");
            else if (_attrValue != "attribute_val")
                _schemaNotes.Add(TAttributes + ": 값 열 이름은 " + _attrValue);

            _pocOwner = Pick(TPocs, "owner_instance_id", "owner_id");
            if (_pocOwner == null)
                missing.Add(TPocs + ".owner_instance_id(owner_id)");
            else if (_pocOwner != "owner_instance_id")
                _schemaNotes.Add(TPocs + ": 소유 객체 열 이름은 " + _pocOwner);

            _pipeSizeTable = null;
            foreach (var candidate in new[] { "tb_pipesize", "tb_pipeszie" })
            {
                if (_columns.ContainsKey(candidate))
                {
                    _pipeSizeTable = candidate;
                    break;
                }
            }
            if (_pipeSizeTable == null)
                _schemaNotes.Add("배관 구경 테이블(tb_pipesize, tb_pipeszie)이 없다. 구경 이름은 비운다.");
            else if (_pipeSizeTable != "tb_pipesize")
                _schemaNotes.Add("배관 구경 테이블 이름은 " + _pipeSizeTable);
            if (_pipeSizeTable != null && !Has(_pipeSizeTable, "pipesize_id"))
                _schemaNotes.Add(_pipeSizeTable + ".pipesize_id 열이 없다. 구경 이름은 비운다.");
            if (_pipeSizeTable != null && !Has(_pipeSizeTable, "pipesize_nm"))
                _schemaNotes.Add(_pipeSizeTable + ".pipesize_nm 열이 없다. 구경 이름은 비운다.");

            if (!Has(TPipes, "length1"))
                _schemaNotes.Add(TPipes + ".length1 없음. length1은 비운다.");
            if (!Has(TPipes, "pipesize_id"))
                _schemaNotes.Add(TPipes + ".pipesize_id 없음. 구경은 비운다.");
            if (!Has(TPipes, "last_update"))
                _schemaNotes.Add(TPipes + ".last_update 없음.");
            if (!Has(TPocTemplates, "memo"))
                _schemaNotes.Add(TPocTemplates + ".memo 없음.");
            if (!Has(TPocTemplates, "equipment_yn"))
                _schemaNotes.Add(TPocTemplates + ".equipment_yn 없음.");
            if (!Has(TPocs, "owner_type"))
                _schemaNotes.Add(TPocs + ".owner_type 없음.");
            if (!Has(TPocs, "utility_id"))
                _schemaNotes.Add(TPocs + ".utility_id 없음.");

            if (missing.Count > 0)
                throw new InvalidOperationException("필수 열 또는 테이블이 없습니다: " + string.Join(", ", missing));
        }

        private void LoadColumns()
        {
            _columns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            string sql = "SELECT LOWER(TABLE_NAME) AS t, LOWER(COLUMN_NAME) AS c FROM " + _settings.InformationSchema
                + " WHERE LOWER(TABLE_NAME) IN ('tb_attributes','tb_modelinstances','tb_modeltemplates','tb_poctemplates','tb_pocinstances','tb_pipeinstances','tb_pipesize','tb_pipeszie')";
            foreach (var row in Run("열 명세", sql))
            {
                string t = Convert.ToString(row["t"], CultureInfo.InvariantCulture);
                string c = Convert.ToString(row["c"], CultureInfo.InvariantCulture);
                HashSet<string> set;
                if (!_columns.TryGetValue(t, out set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _columns[t] = set;
                }
                set.Add(c);
            }
        }

        private bool Has(string table, string column)
        {
            HashSet<string> set;
            return _columns.TryGetValue(table, out set) && set.Contains(column);
        }

        private string Pick(string table, params string[] candidates)
        {
            foreach (var c in candidates)
            {
                if (Has(table, c))
                    return c;
            }
            return null;
        }

        private void Require(List<string> missing, string table, params string[] columns)
        {
            if (!_columns.ContainsKey(table))
            {
                missing.Add(table + "(테이블)");
                return;
            }
            foreach (var c in columns)
            {
                if (!Has(table, c))
                    missing.Add(table + "." + c);
            }
        }

        private string IdSel(string alias, string table, string column, string asName)
        {
            if (column != null && Has(table, column))
                return "CONVERT(varbinary(16), " + alias + ".[" + column + "]) AS [" + asName + "]";
            return "CAST(NULL AS varbinary(16)) AS [" + asName + "]";
        }

        private string ValSel(string alias, string table, string column, string asName)
        {
            if (column != null && Has(table, column))
                return alias + ".[" + column + "] AS [" + asName + "]";
            return "NULL AS [" + asName + "]";
        }

        private List<Dictionary<string, object>> Run(string label, string sql, params SqlParameter[] parameters)
        {
            var sw = Stopwatch.StartNew();
            var rows = new List<Dictionary<string, object>>();
            using (var cmd = new SqlCommand(sql, _conn))
            {
                cmd.CommandTimeout = _settings.CommandTimeoutSeconds;
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < reader.FieldCount; i++)
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        rows.Add(row);
                    }
                }
            }
            _log("[SQL] " + label + " " + rows.Count.ToString(CultureInfo.InvariantCulture) + "행 " + sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms");
            return rows;
        }

        private static long? L(object v)
        {
            if (v == null)
                return null;
            if (v is bool)
                return ((bool)v) ? 1L : 0L;
            try
            {
                return Convert.ToInt64(v, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static double? D(object v)
        {
            if (v == null)
                return null;
            try
            {
                return Convert.ToDouble(v, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string S(object v)
        {
            return v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        private static string H(object v)
        {
            var bytes = v as byte[];
            if (bytes != null)
                return Hex.FromBytes(bytes);
            if (v is Guid)
                return Hex.FromBytes(((Guid)v).ToByteArray());
            return null;
        }

        private static DateTime? T(object v)
        {
            if (v == null)
                return null;
            if (v is DateTime)
                return (DateTime)v;
            DateTime parsed;
            return DateTime.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) ? parsed : (DateTime?)null;
        }

        private static IEnumerable<IList<string>> Chunks(IList<string> ids)
        {
            var distinct = ids.Where(Hex.IsValid).Select(i => i.ToUpperInvariant().Replace("0X", "0x")).Distinct().ToList();
            for (int i = 0; i < distinct.Count; i += BatchSize)
                yield return distinct.Skip(i).Take(BatchSize).ToList();
        }

        private static string InList(IList<string> chunk)
        {
            return string.Join(",", chunk);
        }

        private List<Dictionary<string, object>> ByIds(string label, string table, string keyColumn, string select, IList<string> ids)
        {
            var all = new List<Dictionary<string, object>>();
            foreach (var chunk in Chunks(ids))
            {
                string sql = "SELECT " + select + " FROM " + _prefix + table + " AS x WITH (NOLOCK) WHERE x.[" + keyColumn + "] IN ("
                    + InList(chunk) + ") AND x.[version_sequence] < " + Live;
                all.AddRange(Run(label + " (" + chunk.Count.ToString(CultureInfo.InvariantCulture) + "개 키)", sql));
            }
            return all;
        }

        private void AddOnce<T>(Dictionary<string, T> map, string key, T value, string table)
        {
            if (key == null)
                return;
            if (map.ContainsKey(key))
                _warnings.Add(table + ": 삭제 행 제외 후에도 같은 id가 여러 행이다 " + Hex.Short(key) + ". 첫 행을 쓴다.");
            else
                map[key] = value;
        }

        public IList<TagOwnerRow> FindTagOwners(string tag)
        {
            string select = IdSel("a", TAttributes, "owner_id", "owner_id") + ", "
                + ValSel("a", TAttributes, "owner_type", "owner_type") + ", a.[version_sequence] AS version_sequence";
            string from = " FROM " + _prefix + TAttributes + " AS a WITH (NOLOCK) WHERE a.[attribute_nm] = @name AND ";
            string tail = " AND a.[version_sequence] < " + Live;
            List<Dictionary<string, object>> rows;
            try
            {
                rows = Run("Tag 속성", "SELECT " + select + from + "a.[" + _attrValue + "] = @tag" + tail, TagParams(tag));
            }
            catch (SqlException ex)
            {
                if (ex.Number != 402)
                    throw;
                _log("attribute 값 열이 text/ntext 형식이라 CONVERT 로 다시 조회한다.");
                rows = Run("Tag 속성(CONVERT)", "SELECT " + select + from + "CONVERT(nvarchar(4000), a.[" + _attrValue + "]) = @tag" + tail, TagParams(tag));
            }

            var list = new List<TagOwnerRow>();
            foreach (var r in rows)
            {
                list.Add(new TagOwnerRow
                {
                    OwnerId = H(r["owner_id"]),
                    OwnerType = L(r["owner_type"]),
                    VersionSequence = L(r["version_sequence"])
                });
            }
            return list;
        }

        private SqlParameter[] TagParams(string tag)
        {
            return new[]
            {
                new SqlParameter("@name", SqlDbType.NVarChar, 200) { Value = _settings.TagAttributeName },
                new SqlParameter("@tag", SqlDbType.NVarChar, 4000) { Value = tag }
            };
        }

        public IDictionary<string, ModelRow> GetModels(IList<string> instanceIds)
        {
            string select = IdSel("x", TModels, "instance_id", "instance_id") + ", x.[model_type] AS model_type, "
                + IdSel("x", TModels, "model_template_id", "model_template_id") + ", "
                + ValSel("x", TModels, "composite_yn", "composite_yn") + ", x.[version_sequence] AS version_sequence";
            var map = new Dictionary<string, ModelRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in ByIds("모델 인스턴스", TModels, "instance_id", select, instanceIds))
            {
                string id = H(r["instance_id"]);
                AddOnce(map, id, new ModelRow
                {
                    Id = id,
                    ModelType = L(r["model_type"]),
                    TemplateId = H(r["model_template_id"]),
                    CompositeYn = L(r["composite_yn"]),
                    VersionSequence = L(r["version_sequence"])
                }, TModels);
            }
            return map;
        }

        public IDictionary<string, TemplateRow> GetTemplates(IList<string> modelTemplateIds)
        {
            string select = IdSel("x", TTemplates, "model_template_id", "model_template_id") + ", x.[display_nm] AS display_nm, x.[family_key] AS family_key, x.[bim_yn] AS bim_yn, x.[version_sequence] AS version_sequence";
            var map = new Dictionary<string, TemplateRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in ByIds("모델 템플릿", TTemplates, "model_template_id", select, modelTemplateIds))
            {
                string id = H(r["model_template_id"]);
                AddOnce(map, id, new TemplateRow
                {
                    Id = id,
                    DisplayName = S(r["display_nm"]),
                    FamilyKey = S(r["family_key"]),
                    BimYn = L(r["bim_yn"]),
                    VersionSequence = L(r["version_sequence"])
                }, TTemplates);
            }
            return map;
        }

        private string PocSelect()
        {
            return IdSel("x", TPocs, "instance_id", "instance_id") + ", "
                + IdSel("x", TPocs, _pocOwner, "owner_instance_id") + ", "
                + IdSel("x", TPocs, "poc_template_id", "poc_template_id") + ", "
                + IdSel("x", TPocs, "utility_id", "utility_id") + ", "
                + IdSel("x", TPocs, "connected_poc_id", "connected_poc_id") + ", "
                + ValSel("x", TPocs, "owner_type", "owner_type") + ", "
                + ValSel("x", TPocs, "connection_order", "connection_order") + ", x.[version_sequence] AS version_sequence";
        }

        private PocRow ToPoc(Dictionary<string, object> r)
        {
            return new PocRow
            {
                Id = H(r["instance_id"]),
                OwnerId = H(r["owner_instance_id"]),
                PocTemplateId = H(r["poc_template_id"]),
                UtilityId = H(r["utility_id"]),
                ConnectedPocId = H(r["connected_poc_id"]),
                OwnerType = L(r["owner_type"]),
                ConnectionOrder = L(r["connection_order"]),
                VersionSequence = L(r["version_sequence"])
            };
        }

        public IDictionary<string, PocRow> GetPocsByIds(IList<string> pocInstanceIds)
        {
            var map = new Dictionary<string, PocRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in ByIds("POC 인스턴스(id)", TPocs, "instance_id", PocSelect(), pocInstanceIds))
            {
                var poc = ToPoc(r);
                AddOnce(map, poc.Id, poc, TPocs);
            }
            return map;
        }

        public IList<PocRow> GetPocsByOwners(IList<string> ownerInstanceIds)
        {
            var list = new List<PocRow>();
            foreach (var r in ByIds("POC 인스턴스(owner)", TPocs, _pocOwner, PocSelect(), ownerInstanceIds))
                list.Add(ToPoc(r));
            return list;
        }

        public IDictionary<string, PipeRow> GetPipes(IList<string> pipeInstanceIds)
        {
            string select = IdSel("x", TPipes, "instance_id", "instance_id") + ", "
                + ValSel("x", TPipes, "pipe_type", "pipe_type") + ", x.[length2] AS length2, "
                + ValSel("x", TPipes, "length1", "length1") + ", "
                + ValSel("x", TPipes, "pipesize_id", "pipesize_id") + ", "
                + ValSel("x", TPipes, "last_update", "last_update") + ", x.[version_sequence] AS version_sequence";
            var map = new Dictionary<string, PipeRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in ByIds("배관 인스턴스", TPipes, "instance_id", select, pipeInstanceIds))
            {
                string id = H(r["instance_id"]);
                object sizeId = r["pipesize_id"];
                AddOnce(map, id, new PipeRow
                {
                    Id = id,
                    PipeType = L(r["pipe_type"]),
                    Length1 = D(r["length1"]),
                    Length2 = D(r["length2"]),
                    PipeSizeId = SizeKey(sizeId),
                    LastUpdate = T(r["last_update"]),
                    VersionSequence = L(r["version_sequence"])
                }, TPipes);
            }
            return map;
        }

        private static string SizeKey(object v)
        {
            if (v == null)
                return null;
            var bytes = v as byte[];
            if (bytes != null)
                return Hex.FromBytes(bytes);
            if (v is Guid)
                return Hex.FromBytes(((Guid)v).ToByteArray());
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public IDictionary<string, PipeSizeRow> GetPipeSizes(IList<string> pipeSizeIds)
        {
            var map = new Dictionary<string, PipeSizeRow>(StringComparer.OrdinalIgnoreCase);
            if (_pipeSizeTable == null || !Has(_pipeSizeTable, "pipesize_id") || !Has(_pipeSizeTable, "pipesize_nm"))
                return map;
            var ids = pipeSizeIds.Where(i => !string.IsNullOrEmpty(i)).Distinct().ToList();
            if (ids.Count == 0)
                return map;

            bool hexIds = ids.All(Hex.IsValid);
            bool numericIds = ids.All(IsNumeric);
            if (!hexIds && !numericIds)
            {
                _warnings.Add("pipesize_id 값 형식을 알 수 없어 구경 이름을 조회하지 않았다.");
                return map;
            }

            foreach (var chunk in Chunks2(ids))
            {
                string list = string.Join(",", chunk);
                string keySel = hexIds
                    ? "CONVERT(varbinary(16), x.[pipesize_id])"
                    : "x.[pipesize_id]";
                string sql = "SELECT " + keySel + " AS pipesize_id, x.[pipesize_nm] AS pipesize_nm FROM " + _prefix + _pipeSizeTable
                    + " AS x WITH (NOLOCK) WHERE x.[pipesize_id] IN (" + list + ")"
                    + (Has(_pipeSizeTable, "version_sequence") ? " AND x.[version_sequence] < " + Live : string.Empty);
                foreach (var r in Run("배관 구경", sql))
                {
                    string id = SizeKey(r["pipesize_id"]);
                    if (id != null && !map.ContainsKey(id))
                        map[id] = new PipeSizeRow { Id = id, Name = S(r["pipesize_nm"]) };
                }
            }
            return map;
        }

        private static bool IsNumeric(string s)
        {
            long x;
            return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out x);
        }

        private static IEnumerable<IList<string>> Chunks2(IList<string> ids)
        {
            for (int i = 0; i < ids.Count; i += BatchSize)
                yield return ids.Skip(i).Take(BatchSize).ToList();
        }

        public IDictionary<string, PocTemplateRow> GetPocTemplates(IList<string> pocTemplateIds)
        {
            string select = IdSel("x", TPocTemplates, "poc_template_id", "poc_template_id") + ", "
                + IdSel("x", TPocTemplates, "model_template_id", "model_template_id") + ", x.[poc_template_nm] AS poc_template_nm, "
                + IdSel("x", TPocTemplates, "utility_id", "utility_id") + ", "
                + ValSel("x", TPocTemplates, "equipment_yn", "equipment_yn") + ", "
                + ValSel("x", TPocTemplates, "memo", "memo") + ", x.[version_sequence] AS version_sequence";
            var map = new Dictionary<string, PocTemplateRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in ByIds("POC 템플릿", TPocTemplates, "poc_template_id", select, pocTemplateIds))
            {
                string id = H(r["poc_template_id"]);
                AddOnce(map, id, new PocTemplateRow
                {
                    Id = id,
                    ModelTemplateId = H(r["model_template_id"]),
                    Name = S(r["poc_template_nm"]),
                    UtilityId = H(r["utility_id"]),
                    EquipmentYn = L(r["equipment_yn"]),
                    Memo = S(r["memo"])
                }, TPocTemplates);
            }
            return map;
        }
    }
}
