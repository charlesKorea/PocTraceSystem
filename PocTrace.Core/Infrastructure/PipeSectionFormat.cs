using System;
using System.Collections.Generic;
using System.IO;
using PocTrace.Core.Models;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// pipes.bin. v10은 끝점 개수 + PipeEndpoint. v9 이하는 Outlet1~3 세 칸만 읽고 4번째를 만들지 않는다.
    /// </summary>
    public static class PipeSectionFormat
    {
        public const int ListFormatVersion = 10;

        public static List<PipeData> Read(BinaryReader r, int cacheVersion)
        {
            int count = r.ReadInt32();
            var list = new List<PipeData>(count);
            for (int i = 0; i < count; i++)
                list.Add(ReadPipe(r, cacheVersion));
            return list;
        }

        public static void Write(BinaryWriter w, IList<PipeData> data)
        {
            int count = data == null ? 0 : data.Count;
            w.Write(count);
            if (data == null)
                return;
            for (int i = 0; i < data.Count; i++)
                WritePipeV10(w, data[i]);
        }

        private static PipeData ReadPipe(BinaryReader r, int cacheVersion)
        {
            var pipe = new PipeData
            {
                PipeId = new Guid(r.ReadBytes(16))
            };

            if (cacheVersion >= ListFormatVersion)
            {
                int endpointCount = r.ReadInt32();
                if (endpointCount < 0)
                    throw new InvalidDataException("Pipe endpoint count is negative.");
                var endpoints = new List<PipeEndpoint>(endpointCount);
                for (int i = 0; i < endpointCount; i++)
                {
                    var ep = new PipeEndpoint
                    {
                        PocInstanceId = new Guid(r.ReadBytes(16)),
                        ConnectionOrder = r.ReadInt32()
                    };
                    bool hasTarget = r.ReadBoolean();
                    ep.NormalizedTarget = hasTarget ? (Guid?)new Guid(r.ReadBytes(16)) : null;
                    ep.Resolution = (PipeEndpointResolution)r.ReadInt32();
                    endpoints.Add(ep);
                }
                pipe.ReplaceEndpoints(endpoints);
            }
            else
            {
                pipe.Outlet1 = ReadNullableGuid(r);
                pipe.Outlet2 = ReadNullableGuid(r);
                pipe.Outlet3 = ReadNullableGuid(r);
                pipe.EnsureThreeS5dSlots();
            }

            pipe.Length = r.ReadDouble();
            pipe.UtilityName = r.ReadString();
            pipe.ModuleName = r.ReadString();

            if (cacheVersion >= 2)
            {
                bool hasOpTime = r.ReadBoolean();
                pipe.OpTime = hasOpTime ? DateTime.FromBinary(r.ReadInt64()) : (DateTime?)null;
                pipe.UserName = r.ReadString();
                pipe.CompanyName = r.ReadString();
            }
            else
            {
                pipe.OpTime = null;
                pipe.UserName = null;
                pipe.CompanyName = null;
            }

            return pipe;
        }

        private static void WritePipeV10(BinaryWriter w, PipeData item)
        {
            w.Write(item.PipeId.ToByteArray());
            var endpoints = item.Endpoints;
            int n = endpoints == null ? 0 : endpoints.Count;
            w.Write(n);
            for (int i = 0; i < n; i++)
            {
                var ep = endpoints[i] ?? new PipeEndpoint();
                w.Write(ep.PocInstanceId.ToByteArray());
                w.Write(ep.ConnectionOrder);
                bool hasTarget = ep.NormalizedTarget.HasValue;
                w.Write(hasTarget);
                if (hasTarget)
                    w.Write(ep.NormalizedTarget.Value.ToByteArray());
                w.Write((int)ep.Resolution);
            }
            w.Write(item.Length);
            w.Write(item.UtilityName ?? "");
            w.Write(item.ModuleName ?? "");
            w.Write(item.OpTime.HasValue);
            if (item.OpTime.HasValue)
                w.Write(item.OpTime.Value.ToBinary());
            w.Write(item.UserName ?? "");
            w.Write(item.CompanyName ?? "");
        }

        private static Guid? ReadNullableGuid(BinaryReader r)
        {
            bool has = r.ReadBoolean();
            return has ? (Guid?)new Guid(r.ReadBytes(16)) : null;
        }
    }
}
