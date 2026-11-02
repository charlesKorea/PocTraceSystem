using System;
using System.Collections.Generic;
using System.Threading;

namespace PocTrace.Web
{
    /// <summary>
    /// 브라우저 화면이 모두 닫혔는지 지켜본다. 화면은 몇 초마다 heartbeat 를 보내고,
    /// 닫힐 때(pagehide) bye 를 보낸다. 마지막 화면이 사라지고 유예 시간이 지나면 종료를 알린다.
    /// 작업이 돌고 있는 동안은 종료하지 않는다.
    /// </summary>
    internal sealed class BrowserWatch
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, DateTime> _clients = new Dictionary<string, DateTime>();
        private readonly TimeSpan _staleAfter;
        private readonly TimeSpan _emptyGrace;
        private readonly Func<DateTime> _now;
        private readonly Func<bool> _isBusy;
        private bool _everSeen;
        private DateTime? _emptySince;
        private bool _fired;
        private Timer _timer;

        public event Action IdleExit;

        public BrowserWatch(TimeSpan staleAfter, TimeSpan emptyGrace, Func<bool> isBusy, Func<DateTime> now = null)
        {
            _staleAfter = staleAfter;
            _emptyGrace = emptyGrace;
            _isBusy = isBusy ?? (() => false);
            _now = now ?? (() => DateTime.UtcNow);
        }

        public int ClientCount
        {
            get { lock (_gate) return _clients.Count; }
        }

        public void Start()
        {
            _timer = new Timer(_ => Evaluate(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        public void Stop()
        {
            Timer timer = _timer;
            _timer = null;
            if (timer != null)
                timer.Dispose();
        }

        public void Touch(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                return;
            lock (_gate)
            {
                _clients[clientId] = _now();
                _everSeen = true;
                _emptySince = null;
            }
        }

        public void Bye(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                return;
            lock (_gate)
            {
                if (_clients.Remove(clientId) && _clients.Count == 0)
                    _emptySince = _now();
            }
        }

        /// <summary>종료할 때가 되었으면 true. 시계를 받아 시험할 수 있게 따로 둔다.</summary>
        public bool ShouldExit()
        {
            lock (_gate)
            {
                DateTime now = _now();
                var stale = new List<string>();
                foreach (var pair in _clients)
                {
                    if (now - pair.Value > _staleAfter)
                        stale.Add(pair.Key);
                }
                foreach (string id in stale)
                    _clients.Remove(id);
                if (stale.Count > 0 && _clients.Count == 0 && _emptySince == null)
                    _emptySince = now - _emptyGrace;

                if (!_everSeen || _clients.Count > 0)
                    return false;

                if (_isBusy())
                {
                    _emptySince = now;
                    return false;
                }

                if (_emptySince == null)
                    _emptySince = now;
                return now - _emptySince.Value >= _emptyGrace;
            }
        }

        private void Evaluate()
        {
            bool fire = false;
            lock (_gate)
            {
                if (_fired)
                    return;
            }
            if (ShouldExit())
            {
                lock (_gate)
                {
                    if (!_fired)
                    {
                        _fired = true;
                        fire = true;
                    }
                }
            }
            if (fire)
            {
                Action handler = IdleExit;
                if (handler != null)
                    handler();
            }
        }
    }
}
