using System;
using System.Collections.Generic;
using System.IO;

namespace PocTrace.Core.Engine.DeletedPipe
{
    /// <summary>
    /// 한 라인의 최신 배관·POC·노드·테이크오프.
    /// 공유 GraphCache에는 삭제 행이 없으므로 exe 옆 파일에만 둔다.
    /// </summary>
    internal sealed class DeletedPipeSnapshot
    {
        public string DbName { get; set; }
        public DateTime Cutover { get; set; }
        public DateTime BuiltAt { get; set; }
        public List<DeletedPipeRow> Pipes { get; set; } = new List<DeletedPipeRow>();
        public List<DeletedPocRow> Pocs { get; set; } = new List<DeletedPocRow>();
        public List<DeletedNodeRow> Nodes { get; set; } = new List<DeletedNodeRow>();
        public List<DeletedTakeoffRow> Takeoffs { get; set; } = new List<DeletedTakeoffRow>();
    }

    internal static class DeletedPipeSnapshotStore
    {
        public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);
        private const int Version = 2;
        private const int MaxRows = 5000000;

        public static string FilePath(string dbName, DateTime cutover)
        {
            string folder = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "cache",
                "deleted-pipe");
            string name = Safe(dbName) + "-" + cutover.Date.ToString("yyyyMMdd") + ".bin";
            return Path.Combine(folder, name);
        }

        public static DeletedPipeSnapshot TryLoad(string dbName, DateTime cutover)
        {
            string path = FilePath(dbName, cutover);
            if (!File.Exists(path))
                return null;

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024))
                using (var reader = new BinaryReader(stream))
                {
                    var snapshot = Read(reader, dbName, cutover.Date);
                    if (snapshot == null)
                        return null;
                    if (DateTime.Now - snapshot.BuiltAt > MaxAge)
                        return null;
                    return snapshot;
                }
            }
            catch (IOException)
            {
                return null;
            }
            catch (InvalidDataException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public static bool TrySave(string dbName, DeletedPipeSnapshot snapshot)
        {
            if (snapshot == null)
                return false;

            string path = FilePath(dbName, snapshot.Cutover);
            string folder = Path.GetDirectoryName(path);
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(folder);
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024))
                using (var writer = new BinaryWriter(stream))
                {
                    Write(writer, snapshot);
                }

                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
                return true;
            }
            catch (IOException)
            {
                TryDelete(temp);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                TryDelete(temp);
                return false;
            }
        }

        private static void Write(BinaryWriter writer, DeletedPipeSnapshot snapshot)
        {
            writer.Write((byte)'P');
            writer.Write((byte)'T');
            writer.Write((byte)'D');
            writer.Write((byte)'P');
            writer.Write(Version);
            writer.Write(snapshot.Cutover.Date.Ticks);
            writer.Write(snapshot.BuiltAt.Ticks);
            WriteText(writer, snapshot.DbName);

            WritePipes(writer, snapshot.Pipes);
            WritePocs(writer, snapshot.Pocs);
            WriteNodes(writer, snapshot.Nodes);
            WriteTakeoffs(writer, snapshot.Takeoffs);
        }

        private static DeletedPipeSnapshot Read(BinaryReader reader, string dbName, DateTime cutover)
        {
            if (reader.ReadByte() != (byte)'P'
                || reader.ReadByte() != (byte)'T'
                || reader.ReadByte() != (byte)'D'
                || reader.ReadByte() != (byte)'P')
                return null;
            if (reader.ReadInt32() != Version)
                return null;

            var storedCutover = new DateTime(reader.ReadInt64(), DateTimeKind.Unspecified).Date;
            var builtAt = new DateTime(reader.ReadInt64(), DateTimeKind.Unspecified);
            string storedDb = ReadText(reader);
            if (storedCutover != cutover)
                return null;
            if (!string.Equals(storedDb, dbName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                return null;

            return new DeletedPipeSnapshot
            {
                DbName = storedDb,
                Cutover = storedCutover,
                BuiltAt = builtAt,
                Pipes = ReadPipes(reader),
                Pocs = ReadPocs(reader),
                Nodes = ReadNodes(reader),
                Takeoffs = ReadTakeoffs(reader)
            };
        }

        private static void WritePipes(BinaryWriter writer, IList<DeletedPipeRow> rows)
        {
            int count = rows == null ? 0 : rows.Count;
            writer.Write(count);
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                WriteGuid(writer, row.PipeId);
                WriteGuid(writer, row.Outlet1);
                WriteGuid(writer, row.Outlet2);
                WriteGuid(writer, row.Outlet3);
                writer.Write(row.Length);
                WriteText(writer, row.UtilityName);
                WriteTime(writer, row.OpTime);
                writer.Write(row.OperationType);
                WriteText(writer, row.UserName);
                WriteText(writer, row.CompanyName);
            }
        }

        private static List<DeletedPipeRow> ReadPipes(BinaryReader reader)
        {
            int count = ReadCount(reader);
            var list = new List<DeletedPipeRow>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new DeletedPipeRow
                {
                    PipeId = ReadGuid(reader),
                    Outlet1 = ReadOptionalGuid(reader),
                    Outlet2 = ReadOptionalGuid(reader),
                    Outlet3 = ReadOptionalGuid(reader),
                    Length = reader.ReadDouble(),
                    UtilityName = ReadText(reader),
                    OpTime = ReadTime(reader),
                    OperationType = reader.ReadInt32(),
                    UserName = ReadText(reader),
                    CompanyName = ReadText(reader)
                });
            }
            return list;
        }

        private static void WritePocs(BinaryWriter writer, IList<DeletedPocRow> rows)
        {
            int count = rows == null ? 0 : rows.Count;
            writer.Write(count);
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                WriteGuid(writer, row.InstanceId);
                WriteGuid(writer, row.OwnerId);
                WriteGuid(writer, row.Outlet);
                WriteText(writer, row.Tag);
                WriteText(writer, row.PocName);
                WriteText(writer, row.Information);
                WriteText(writer, row.UtilityName);
                WriteText(writer, row.UtilityGroupName);
                WriteTime(writer, row.OpTime);
                writer.Write(row.OperationType);
            }
        }

        private static List<DeletedPocRow> ReadPocs(BinaryReader reader)
        {
            int count = ReadCount(reader);
            var list = new List<DeletedPocRow>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new DeletedPocRow
                {
                    InstanceId = ReadGuid(reader),
                    OwnerId = ReadGuid(reader),
                    Outlet = ReadOptionalGuid(reader),
                    Tag = ReadText(reader),
                    PocName = ReadText(reader),
                    Information = ReadText(reader),
                    UtilityName = ReadText(reader),
                    UtilityGroupName = ReadText(reader),
                    OpTime = ReadTime(reader),
                    OperationType = reader.ReadInt32()
                });
            }
            return list;
        }

        private static void WriteNodes(BinaryWriter writer, IList<DeletedNodeRow> rows)
        {
            int count = rows == null ? 0 : rows.Count;
            writer.Write(count);
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                WriteGuid(writer, row.InstanceId);
                WriteText(writer, row.Tag);
                WriteText(writer, row.Name);
                WriteTime(writer, row.OpTime);
                writer.Write(row.OperationType);
                writer.Write(row.Division);
                WriteText(writer, row.Sub1Name);
                WriteText(writer, row.Sub2Name);
                WriteText(writer, row.ConstructionCode);
                WriteText(writer, row.QCode);
            }
        }

        private static List<DeletedNodeRow> ReadNodes(BinaryReader reader)
        {
            int count = ReadCount(reader);
            var list = new List<DeletedNodeRow>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new DeletedNodeRow
                {
                    InstanceId = ReadGuid(reader),
                    Tag = ReadText(reader),
                    Name = ReadText(reader),
                    OpTime = ReadTime(reader),
                    OperationType = reader.ReadInt32(),
                    Division = reader.ReadInt32(),
                    Sub1Name = ReadText(reader),
                    Sub2Name = ReadText(reader),
                    ConstructionCode = ReadText(reader),
                    QCode = ReadText(reader)
                });
            }
            return list;
        }

        private static void WriteTakeoffs(BinaryWriter writer, IList<DeletedTakeoffRow> rows)
        {
            int count = rows == null ? 0 : rows.Count;
            writer.Write(count);
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                WriteGuid(writer, row.TakeoffId);
                WriteText(writer, row.Tag);
                WriteGuid(writer, row.PipeId);
                WriteTime(writer, row.OpTime);
                writer.Write(row.OperationType);
            }
        }

        private static List<DeletedTakeoffRow> ReadTakeoffs(BinaryReader reader)
        {
            int count = ReadCount(reader);
            var list = new List<DeletedTakeoffRow>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new DeletedTakeoffRow
                {
                    TakeoffId = ReadGuid(reader),
                    Tag = ReadText(reader),
                    PipeId = ReadOptionalGuid(reader),
                    OpTime = ReadTime(reader),
                    OperationType = reader.ReadInt32()
                });
            }
            return list;
        }

        private static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxRows)
                throw new InvalidDataException("snapshot row count");
            return count;
        }

        private static void WriteGuid(BinaryWriter writer, Guid value)
        {
            if (value == Guid.Empty)
            {
                writer.Write((byte)0);
                return;
            }
            writer.Write((byte)1);
            writer.Write(value.ToByteArray());
        }

        private static void WriteGuid(BinaryWriter writer, Guid? value)
        {
            WriteGuid(writer, value ?? Guid.Empty);
        }

        private static Guid ReadGuid(BinaryReader reader)
        {
            Guid? value = ReadOptionalGuid(reader);
            return value ?? Guid.Empty;
        }

        private static Guid? ReadOptionalGuid(BinaryReader reader)
        {
            if (reader.ReadByte() == 0)
                return null;
            var value = new Guid(reader.ReadBytes(16));
            if (value == Guid.Empty)
                return null;
            return value;
        }

        private static void WriteTime(BinaryWriter writer, DateTime? value)
        {
            if (!value.HasValue)
            {
                writer.Write((byte)0);
                return;
            }
            writer.Write((byte)1);
            writer.Write(value.Value.Ticks);
        }

        private static DateTime? ReadTime(BinaryReader reader)
        {
            if (reader.ReadByte() == 0)
                return null;
            return new DateTime(reader.ReadInt64(), DateTimeKind.Unspecified);
        }

        private static void WriteText(BinaryWriter writer, string value)
        {
            writer.Write(value ?? string.Empty);
        }

        private static string ReadText(BinaryReader reader)
        {
            return reader.ReadString();
        }

        private static string Safe(string dbName)
        {
            if (string.IsNullOrWhiteSpace(dbName))
                return "db";
            var chars = dbName.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '_'
                    || c == '-';
                if (!ok)
                    chars[i] = '_';
            }
            return new string(chars);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
