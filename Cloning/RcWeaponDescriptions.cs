using MissileCameraRemoteControl.Config;

namespace MissileCameraRemoteControl.Cloning
{
    /// <summary>Original vanilla-tone encyclopedia / hangar blurbs for RC variants (not 1:1 stock copies).</summary>
    internal static class RcWeaponDescriptions
    {
        internal static string Resolve(string? baseWeaponName, RcGuidanceKind guidance)
        {
            string name = baseWeaponName ?? string.Empty;
            bool sat = guidance == RcGuidanceKind.Satcom;

            if (Contains(name, "ALND") || Contains(name, "20kt") && Contains(name, "ALM") == false && Contains(name, "Cruise"))
            {
                // ALND-4 nuclear cruise — usually SATCOM
                return sat
                    ? "Strategic nuclear cruise missile commanded over a hardened satellite uplink. Mid-course corrections remain available regardless of terrain masking or local jamming, with the warhead package unchanged from the land-attack baseline."
                    : "Strategic nuclear cruise missile fitted for long-range data-link mid-course updates from friendly airborne and surface nodes before terminal approach.";
            }

            if (Contains(name, "ALM-C450") || (Contains(name, "Cruise") && !Contains(name, "20kt") && !Contains(name, "ALND")))
            {
                return sat
                    ? "Long-range land-attack cruise missile with satellite mid-course command. Maintains a low-level ingress profile and accepts updated aimpoints through the satcom channel until terminal guidance takes over."
                    : "Long-range land-attack cruise missile equipped for two-way data-link mid-course updates from friendly units. Retains its terrain-following cruise and optical terminal phase if the link drops.";
            }

            if (Contains(name, "AGM-99"))
            {
                return sat
                    ? "Subsonic anti-ship missile with satellite mid-course cueing. Keeps the low-altitude approach and pop-up terminal attack while accepting revised intercept points from orbit."
                    : "Subsonic anti-ship missile with a data-linked mid-course channel. Designed for sea-skimming approach and pop-up terminal attack; friendly units can update the intercept before the seeker commits.";
            }

            if (Contains(name, "AGM-68"))
            {
                return "Air-to-ground guided missile with a two-way data-link for mid-course aimpoint updates. Optical terminal phase matches the AGM-68 baseline when the link is idle.";
            }

            if (Contains(name, "AAM-36") || Contains(name, "AAM-46") || Contains(name, "Longstrong"))
            {
                return "Extended-range air-to-air missile with data-link mid-course updates and optical/radar terminal engagement. Supports in-flight retarget and relock under remote control.";
            }

            if ((Contains(name, "76mm") && Contains(name, "Guided")) || Contains(name, "DLG Shell"))
            {
                return "Data-link guided 76mm shell. Unpowered ballistic projectile that steers toward a designated aimpoint; no remote-pilot stick and no onboard fuel motor.";
            }

            if (Contains(name, "AShM-300"))
            {
                return sat
                    ? "Supersonic anti-ship missile under satellite command for over-the-horizon mid-course steering. High-speed dash and terminal profile match the baseline weapon."
                    : "Supersonic anti-ship missile with data-link mid-course control from friendly aircraft or surface combatants. Built for high-speed sea skimming with late-course seeker handover.";
            }

            if (Contains(name, "Tusko"))
            {
                return sat
                    ? "Heavy semi-ballistic strike missile with satellite mid-course updates for lofted trajectories against hardened or time-sensitive targets."
                    : "Heavy semi-ballistic strike missile fitted for data-link course corrections during loft and descent, bridging the gap between cruise and tactical ballistic profiles.";
            }

            if (Contains(name, "Piledriver"))
            {
                if (Contains(name, "20kt") || Contains(name, "tacNuke") || Contains(name, "Nuke"))
                {
                    return sat
                        ? "Air-launched tactical ballistic missile with a nuclear payload and satellite mid-course command for strategic reach against high-value targets."
                        : "Air-launched tactical ballistic missile with a nuclear payload and data-link mid-course updates from the launching force.";
                }

                return sat
                    ? "Air-launched tactical ballistic missile under satellite mid-course command. High-arc trajectory and conventional warhead for deep strike against fixed infrastructure."
                    : "Air-launched tactical ballistic missile with data-link mid-course guidance for deep conventional strike along a high-arc profile.";
            }

            // Generic fallback — still vanilla tone, not a stock paste.
            return sat
                ? "Guided munition with satellite mid-course command for remote course updates. Terminal seeker behaviour matches the baseline weapon when the uplink is idle."
                : "Guided munition fitted with a two-way data-link for mid-course updates from friendly units. Falls back to its onboard seeker if the link is interrupted.";
        }

        private static bool Contains(string hay, string needle) =>
            hay.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
