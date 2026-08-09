using System.Collections.Generic;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Living RC controllable missiles — filled on stamp/Register; RefreshPool prefers this over FindObjectsOfType.
    /// </summary>
    internal static class RcLivingRcRegistry
    {
        private static readonly List<Missile> _living = new List<Missile>(32);

        internal static void Notify(Missile? missile)
        {
            if (missile == null || missile.disabled)
                return;
            for (int i = 0; i < _living.Count; i++)
            {
                if (ReferenceEquals(_living[i], missile))
                    return;
            }

            _living.Add(missile);
        }

        internal static void Clear() => _living.Clear();

        /// <summary>Prune dead; copy survivors into <paramref name="dst"/>. Returns false if registry empty after prune.</summary>
        internal static bool TryCopyAlive(List<Missile> dst)
        {
            Prune();
            if (_living.Count == 0)
                return false;

            for (int i = 0; i < _living.Count; i++)
                dst.Add(_living[i]);
            return true;
        }

        private static void Prune()
        {
            for (int i = _living.Count - 1; i >= 0; i--)
            {
                Missile? m = _living[i];
                if (m == null || m.disabled)
                    _living.RemoveAt(i);
            }
        }
    }
}
