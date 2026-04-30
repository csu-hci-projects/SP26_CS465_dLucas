#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CesiumForUnity;

namespace AerialNav.Gauntlet.Editor
{
    /// <summary>
    /// Select GAUNTLET_CSU_CU in the Hierarchy, then run
    /// Tools → Gauntlet → Distribute Rings.
    ///
    /// - Fixes ring 0 at StartLat/Lon and ring 25 at EndLat/Lon
    /// - Distributes rings 1-24 along the path with seeded random
    ///   lateral and vertical offsets for an unpredictable course
    /// - Seed is set in Inspector — same seed = same course every time
    /// - Rotates each ring so its opening faces toward the next ring
    /// </summary>
    public static class GauntletRingDistributor
    {
        // ── Endpoints ─────────────────────────────────────────────────────────
        private const double StartLat    =  40.5764;
        private const double StartLon    = -105.0841;
        private const double StartHeight =  1850.0;

        private const double EndLat      =  40.00759999;
        private const double EndLon      = -105.2659;
        private const double EndHeight   =  1850.0;

        // ── Scale ─────────────────────────────────────────────────────────────
        private const float RingScale = 20f;

        // ── Course Variation ──────────────────────────────────────────────────
        // Change seed to generate a new course layout; redistribute to lock it in.
        private const int    Seed              = 42;
        private const double LateralAmplitude  = 0.0050; // degrees lat/lon ≈ 500m
        private const double VerticalAmplitude = 400.0;  // meters above/below base height
        private const double MinHeight         = 1800.0; // floor — never below this
        private const double MaxHeight         = 2500.0; // ceiling — never above this

        [MenuItem("Tools/Gauntlet/Distribute Rings")]
        public static void DistributeRings()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Gauntlet Distributor",
                    "Select GAUNTLET_CSU_CU in the Hierarchy first.", "OK");
                return;
            }

            var rings = selected.GetComponentsInChildren<GauntletRing>(includeInactive: true);
            if (rings.Length == 0)
            {
                EditorUtility.DisplayDialog("Gauntlet Distributor",
                    "No GauntletRing components found in children.", "OK");
                return;
            }

            int count = rings.Length; // expect 26
            var rng   = new System.Random(Seed);

            // Pass 1: geo positions
            for (int i = 0; i < count; i++)
            {
                double lat, lon, height;

                if (i == 0)
                {
                    lat    = StartLat;
                    lon    = StartLon;
                    height = StartHeight;
                }
                else if (i == count - 1)
                {
                    lat    = EndLat;
                    lon    = EndLon;
                    height = EndHeight;
                }
                else
                {
                    // Base position: linear interpolation along the path
                    float t = (float)i / (count - 1);

                    lat    = Lerp(StartLat,    EndLat,    t);
                    lon    = Lerp(StartLon,    EndLon,    t);
                    height = Lerp(StartHeight, EndHeight, t);

                    // Apply seeded random offsets
                    double latOffset  = (rng.NextDouble() * 2.0 - 1.0) * LateralAmplitude;
                    double lonOffset  = (rng.NextDouble() * 2.0 - 1.0) * LateralAmplitude;
                    double altOffset  = (rng.NextDouble() * 2.0 - 1.0) * VerticalAmplitude;

                    lat    += latOffset;
                    lon    += lonOffset;
                    height  = System.Math.Clamp(height + altOffset, MinHeight, MaxHeight);
                }

                var anchor = rings[i].GetComponent<CesiumGlobeAnchor>();
                if (anchor == null)
                {
                    Debug.LogWarning($"[GauntletDistributor] '{rings[i].gameObject.name}' " +
                                     $"has no CesiumGlobeAnchor — skipped.");
                    continue;
                }

                Undo.RecordObject(anchor, "Distribute Gauntlet Rings");
                anchor.longitudeLatitudeHeight =
                    new Unity.Mathematics.double3(lon, lat, height);
                EditorUtility.SetDirty(anchor);
            }

            // Pass 2: scale and rotation
            for (int i = 0; i < count; i++)
            {
                var tf = rings[i].transform;
                Undo.RecordObject(tf, "Distribute Gauntlet Rings");

                tf.localScale = Vector3.one * RingScale;

                int lookTarget = i < count - 1 ? i + 1 : i - 1;
                Vector3 dir    = rings[lookTarget].transform.position - tf.position;

                if (dir.sqrMagnitude > 0.001f)
                {
                    tf.rotation  = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    tf.rotation *= Quaternion.Euler(90f, 0f, 0f);
                }

                EditorUtility.SetDirty(tf);
            }

            EditorUtility.DisplayDialog("Gauntlet Distributor",
                $"Distributed {count} rings — seed {Seed}, " +
                $"lateral ±{LateralAmplitude * 111000:F0}m, " +
                $"vertical ±{VerticalAmplitude:F0}m.", "OK");
        }

        private static double Lerp(double a, double b, float t) => a + (b - a) * t;
    }
}
#endif