using System;
using System.Collections.Generic;

namespace PocTrace.Core.Models
{
    /// <summary>
    /// 배관 끝점 하나. S5D 3슬롯과 DDW OWNER_TYPE=256 POC가 같은 모양으로 들어간다.
    /// </summary>
    public sealed class PipeEndpoint
    {
        public Guid PocInstanceId { get; set; }
        public int ConnectionOrder { get; set; }
        public Guid? NormalizedTarget { get; set; }
        public PipeEndpointResolution Resolution { get; set; }
    }

    public enum PipeEndpointResolution
    {
        Unconnected = 0,
        Connected = 1,
        Dangling = 2
    }

    /// <summary>
    /// 순회는 NormalizedTarget이 있는 Connected 끝점만 탄다. null은 압축하지 않고 리스트에 남긴다.
    /// </summary>
    public static class PipeEndpointTraversal
    {
        public static IEnumerable<Guid> ConnectedTargets(PipeData pipe)
        {
            if (pipe == null || pipe.Endpoints == null)
                yield break;

            for (int i = 0; i < pipe.Endpoints.Count; i++)
            {
                var ep = pipe.Endpoints[i];
                if (ep == null || ep.Resolution != PipeEndpointResolution.Connected)
                    continue;
                if (!ep.NormalizedTarget.HasValue)
                    continue;
                yield return ep.NormalizedTarget.Value;
            }
        }
    }
}
