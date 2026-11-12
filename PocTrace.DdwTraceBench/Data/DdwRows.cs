using System;
using System.Globalization;
using System.Text;

namespace PocTrace.DdwTraceBench.Data
{
    public static class Hex
    {
        public static string FromBytes(byte[] bytes)
        {
            if (bytes == null)
                return null;
            var sb = new StringBuilder(2 + bytes.Length * 2);
            sb.Append("0x");
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public static bool IsEmpty(string id)
        {
            return string.IsNullOrEmpty(id) || IsZero(id);
        }

        public static bool IsZero(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length <= 2 || id[0] != '0' || (id[1] != 'x' && id[1] != 'X'))
                return false;
            for (int i = 2; i < id.Length; i++)
            {
                if (id[i] != '0')
                    return false;
            }
            return true;
        }

        public static bool IsValid(string id)
        {
            if (id == null || id.Length != 34 || id[0] != '0' || (id[1] != 'x' && id[1] != 'X'))
                return false;
            for (int i = 2; i < id.Length; i++)
            {
                char c = id[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok)
                    return false;
            }
            return true;
        }

        public static string Short(string id)
        {
            if (string.IsNullOrEmpty(id))
                return string.Empty;
            return id.Length <= 10 ? id : id.Substring(0, 10);
        }
    }

    public sealed class TagOwnerRow
    {
        public string OwnerId;
        public long? OwnerType;
        public long? VersionSequence;
    }

    public sealed class ModelRow
    {
        public string Id;
        public long? ModelType;
        public string TemplateId;
        public long? CompositeYn;
        public long? VersionSequence;
    }

    public sealed class TemplateRow
    {
        public string Id;
        public string DisplayName;
        public string FamilyKey;
        public long? BimYn;
        public long? VersionSequence;
    }

    public sealed class PocRow
    {
        public string Id;
        public string OwnerId;
        public string PocTemplateId;
        public string UtilityId;
        public string ConnectedPocId;
        public long? OwnerType;
        public long? ConnectionOrder;
        public long? VersionSequence;
    }

    public sealed class PipeRow
    {
        public string Id;
        public long? PipeType;
        public double? Length1;
        public double? Length2;
        public string PipeSizeId;
        public DateTime? LastUpdate;
        public long? VersionSequence;
    }

    public sealed class PipeSizeRow
    {
        public string Id;
        public string Name;
    }

    public sealed class PocTemplateRow
    {
        public string Id;
        public string ModelTemplateId;
        public string Name;
        public string UtilityId;
        public long? EquipmentYn;
        public string Memo;
    }
}
