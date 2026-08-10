using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Ring buffer of lead world positions — wingmen replay this path (terrain-aware route copy).
    /// </summary>
    internal sealed class RcFormationTrail
    {
        private const int Capacity = 128;
        private const float MinStepM = 18f;
        private const float SampleInterval = 0.12f;

        private readonly Vector3[] _pos = new Vector3[Capacity];
        private int _count;
        private int _head; // next write index
        private float _nextSample;
        private Vector3 _lastAccepted;

        internal void Clear()
        {
            _count = 0;
            _head = 0;
            _nextSample = 0f;
            _lastAccepted = Vector3.zero;
        }

        internal void PushLead(Vector3 worldPos)
        {
            float now = Time.fixedTime;
            if (_count > 0 && now < _nextSample)
                return;

            if (_count > 0 && (worldPos - _lastAccepted).sqrMagnitude < MinStepM * MinStepM)
            {
                _nextSample = now + SampleInterval;
                return;
            }

            _pos[_head] = worldPos;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity)
                _count++;
            _lastAccepted = worldPos;
            _nextSample = now + SampleInterval;
        }

        internal int Count => _count;

        /// <summary>
        /// Point on the recorded path that is <paramref name="backM"/> meters behind the newest sample,
        /// walking the polyline toward older samples. Tangent points toward the lead (newer).
        /// </summary>
        internal bool TryGetBehind(float backM, out Vector3 point, out Vector3 tangentTowardLead)
        {
            point = default;
            tangentTowardLead = Vector3.forward;
            if (_count < 2)
                return false;

            float need = Mathf.Max(5f, backM);
            Vector3 newer = GetNewest();
            float walked = 0f;

            for (int i = 1; i < _count; i++)
            {
                Vector3 older = GetFromNewest(i);
                Vector3 seg = newer - older;
                float len = seg.magnitude;
                if (len < 0.01f)
                {
                    newer = older;
                    continue;
                }

                if (walked + len >= need)
                {
                    float t = (need - walked) / len;
                    point = Vector3.Lerp(newer, older, t);
                    tangentTowardLead = seg.normalized; // older → newer = toward lead
                    return true;
                }

                walked += len;
                newer = older;
            }

            // Path shorter than backM — use oldest sample, tangent toward lead.
            point = GetFromNewest(_count - 1);
            Vector3 a = GetFromNewest(Mathf.Max(0, _count - 2));
            Vector3 b = GetNewest();
            Vector3 d = b - a;
            tangentTowardLead = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
            return true;
        }

        /// <summary>Nearest point on the polyline to <paramref name="world"/> (for solo / distance checks).</summary>
        internal float NearestDistance(Vector3 world)
        {
            if (!TryGetNearest(world, out Vector3 nearest, out _))
                return float.MaxValue;
            return Vector3.Distance(world, nearest);
        }

        /// <summary>Closest point on the recorded path + local tangent toward lead.</summary>
        internal bool TryGetNearest(Vector3 world, out Vector3 nearest, out Vector3 tangentTowardLead)
        {
            nearest = default;
            tangentTowardLead = Vector3.forward;
            if (_count < 1)
                return false;
            if (_count == 1)
            {
                nearest = GetNewest();
                return true;
            }

            float best = float.MaxValue;
            Vector3 bestPt = GetNewest();
            Vector3 bestTan = Vector3.forward;
            Vector3 a = GetFromNewest(0);
            for (int i = 1; i < _count; i++)
            {
                Vector3 b = GetFromNewest(i);
                // a = newer, b = older; tangent toward lead = a - b direction from older to newer
                Vector3 ab = a - b; // wait: DistPointToSegment(world, a, b) - segment from newer to older
                // Closest on segment a(newer)--b(older)
                Vector3 ba = b - a;
                float ba2 = ba.sqrMagnitude;
                Vector3 pt;
                if (ba2 < 1e-8f)
                {
                    pt = a;
                }
                else
                {
                    float t = Mathf.Clamp01(Vector3.Dot(world - a, ba) / ba2);
                    pt = a + ba * t;
                }

                float d = Vector3.Distance(world, pt);
                if (d < best)
                {
                    best = d;
                    bestPt = pt;
                    Vector3 seg = a - b; // older → newer
                    bestTan = seg.sqrMagnitude > 1e-6f ? seg.normalized : bestTan;
                }

                a = b;
            }

            nearest = bestPt;
            tangentTowardLead = bestTan;
            return true;
        }

        private Vector3 GetNewest() => GetFromNewest(0);

        /// <summary>0 = newest, count-1 = oldest.</summary>
        private Vector3 GetFromNewest(int ageIndex)
        {
            int idx = _head - 1 - ageIndex;
            while (idx < 0)
                idx += Capacity;
            return _pos[idx % Capacity];
        }

        private static float DistPointToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float ab2 = ab.sqrMagnitude;
            if (ab2 < 1e-8f)
                return Vector3.Distance(p, a);
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab2);
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
