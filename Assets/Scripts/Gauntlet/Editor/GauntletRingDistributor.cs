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
    /// - Evenly spaces all 26 rings between Canvas Stadium and Folsom Field
    /// - Scales each ring to RingScale
    /// - Rotates each ring so its opening faces toward the next ring
    ///   (last ring faces same direction as second-to-last)
    /// </summary>
    public static class GauntletRingDistributor
    {

        // ── Endpoints ─────────────────────────────────────────────────────────
        private const double StartLat    =  40.5764;
        private const double StartLon    = -105.0841;
        private const double StartHeight =  1750.0;

        private const double EndLat      =  40.0076;
        private const double EndLon      = -105.2659;
        private const double EndHeight   =  1750.0;

        // ── Scale ─────────────────────────────────────────────────────────────
        private const float RingScale = 20f;

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

            int count = rings.Length;

            // Pass 1: geo positions
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : (float)i / (count - 1);

                double lat    = Lerp(StartLat,    EndLat,    t);
                double lon    = Lerp(StartLon,    EndLon,    t);
                double height = Lerp(StartHeight, EndHeight, t);

                var anchor = rings[i].GetComponent<CesiumGlobeAnchor>();
                if (anchor == null)
                {
                    Debug.LogWarning($"[GauntletDistributor] '{rings[i].gameObject.name}' has no CesiumGlobeAnchor — skipped.");
                    continue;
                }

                Undo.RecordObject(anchor, "Distribute Gauntlet Rings");
                anchor.longitudeLatitudeHeight = new Unity.Mathematics.double3(lon, lat, height);
                EditorUtility.SetDirty(anchor);
            }

            // Pass 2: scale and rotation — runs after Cesium has updated transforms
            for (int i = 0; i < count; i++)
            {
                var tf = rings[i].transform;
                Undo.RecordObject(tf, "Distribute Gauntlet Rings");

                tf.localScale = Vector3.one * RingScale;

                int lookTarget = i < count - 1 ? i + 1 : i - 1;
                Vector3 dir = rings[lookTarget].transform.position - tf.position;

                if (dir.sqrMagnitude > 0.001f)
                    tf.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    tf.rotation *= Quaternion.Euler(90f, 0f, 0f);

                EditorUtility.SetDirty(tf);
            }

            EditorUtility.DisplayDialog("Gauntlet Distributor",
                $"Distributed {count} rings — scaled x{RingScale}, oriented toward next gate.", "OK");
        }

        private static double Lerp(double a, double b, float t) => a + (b - a) * t;
    }
}
#endif