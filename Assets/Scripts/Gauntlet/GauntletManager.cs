using UnityEngine;

namespace AerialNav.Gauntlet
{
    /// <summary>
    /// Wires GauntletPath events to GauntletHUD.
    /// Deliberately thin — all race logic lives in GauntletPath,
    /// all display logic lives in GauntletHUD.
    ///
    /// Setup: attach to any scene GameObject; assign path and hud in Inspector.
    /// </summary>
    public class GauntletManager : MonoBehaviour
    {
        [SerializeField] private GauntletPath path;
        [SerializeField] private GauntletHUD  hud;

        private void Awake()
        {
            if (path == null)
            {
                Debug.LogError("[GauntletManager] GauntletPath not assigned.");
                return;
            }

            path.OnRaceStarted    += p          => hud?.OnRaceStarted(p.PathName, p.TotalGates);
            path.OnProgressChanged += (passed, total) => hud?.OnProgressChanged(passed, total);
            path.OnGateMissed     += index      => hud?.OnGateMissed(index);
            path.OnRaceFinished   += (_, secs)  => hud?.OnRaceFinished(secs);
        }
    }
}