using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace PocTrace.Core.Models
{
    public class PipeData
    {
        private readonly List<PipeEndpoint> _endpoints = new List<PipeEndpoint>();
        private bool _collectionOwned;

        public Guid PipeId { get; set; }

        /// <summary>
        /// 순서 보존 끝점. S5D는 ConnectionOrder 1/2/3 세 칸(빈 칸은 Unconnected).
        /// DDW는 CONNECTION_ORDER, POC id 순이며 4개 이상도 유지한다.
        /// </summary>
        public IList<PipeEndpoint> Endpoints
        {
            get { return _endpoints; }
        }

        /// <summary>앞 3개 끝점의 NormalizedTarget. 4번째 이후는 여기 들어오지 않는다.</summary>
        [JsonIgnore]
        public Guid? Outlet1
        {
            get { return TargetAt(0); }
            set { AssignS5dSlot(1, value); }
        }

        [JsonIgnore]
        public Guid? Outlet2
        {
            get { return TargetAt(1); }
            set { AssignS5dSlot(2, value); }
        }

        [JsonIgnore]
        public Guid? Outlet3
        {
            get { return TargetAt(2); }
            set { AssignS5dSlot(3, value); }
        }

        public double Length { get; set; }
        public string UtilityName { get; set; }
        public string ModuleName { get; set; }

        public DateTime? OpTime { get; set; }
        public string UserName { get; set; }
        public string CompanyName { get; set; }

        /// <summary>DDW·캐시 v10이 끝점 목록을 통째로 넣는다. 이후 Outlet 대입은 목록을 덮지 않는다.</summary>
        public void ReplaceEndpoints(IEnumerable<PipeEndpoint> endpoints)
        {
            _endpoints.Clear();
            if (endpoints != null)
            {
                foreach (var ep in endpoints)
                {
                    if (ep != null)
                        _endpoints.Add(ep);
                }
            }
            SortEndpoints();
            _collectionOwned = true;
        }

        /// <summary>S5D 매핑 직후. 없는 슬롯은 Unconnected로 세 칸을 채운다.</summary>
        public void EnsureThreeS5dSlots()
        {
            if (_collectionOwned)
                return;
            for (int order = 1; order <= 3; order++)
            {
                if (!HasOrder(order))
                    AssignS5dSlot(order, null);
            }
        }

        public string DescribeOrders()
        {
            if (_endpoints.Count == 0)
                return "";
            return string.Join(",", _endpoints.Select(ep => ep.ConnectionOrder.ToString()));
        }

        private bool HasOrder(int connectionOrder)
        {
            for (int i = 0; i < _endpoints.Count; i++)
            {
                if (_endpoints[i].ConnectionOrder == connectionOrder)
                    return true;
            }
            return false;
        }

        private void AssignS5dSlot(int connectionOrder, Guid? value)
        {
            if (_collectionOwned)
                return;

            PipeEndpoint ep = null;
            for (int i = 0; i < _endpoints.Count; i++)
            {
                if (_endpoints[i].ConnectionOrder == connectionOrder)
                {
                    ep = _endpoints[i];
                    break;
                }
            }
            if (ep == null)
            {
                ep = new PipeEndpoint
                {
                    ConnectionOrder = connectionOrder,
                    PocInstanceId = Guid.Empty
                };
                _endpoints.Add(ep);
            }

            if (!value.HasValue)
            {
                ep.NormalizedTarget = null;
                ep.Resolution = PipeEndpointResolution.Unconnected;
            }
            else
            {
                // S5D outlet은 이미 도메인 id. Empty도 HasValue와 같이 유지한다.
                ep.NormalizedTarget = value;
                ep.Resolution = PipeEndpointResolution.Connected;
            }
            SortEndpoints();
        }

        private Guid? TargetAt(int index)
        {
            if (index < 0 || index >= _endpoints.Count)
                return null;
            var target = _endpoints[index].NormalizedTarget;
            if (!target.HasValue)
                return null;
            return target;
        }

        private void SortEndpoints()
        {
            if (_endpoints.Count < 2)
                return;
            var sorted = _endpoints
                .Select((ep, index) => new SortedEndpoint { Endpoint = ep, Index = index })
                .OrderBy(x => x.Endpoint.ConnectionOrder)
                .ThenBy(x => x.Endpoint.PocInstanceId)
                .ThenBy(x => x.Index)
                .Select(x => x.Endpoint)
                .ToList();
            _endpoints.Clear();
            _endpoints.AddRange(sorted);
        }

        private sealed class SortedEndpoint
        {
            public PipeEndpoint Endpoint;
            public int Index;
        }
    }
}
