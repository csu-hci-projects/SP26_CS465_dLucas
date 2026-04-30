using UnityEngine;

namespace AerialNav.Gauntlet
{
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

            path.OnRaceStarted     += p             => hud?.OnRaceStarted(p.PathName, p.TotalGates);
            path.OnProgressChanged += (passed, total) => hud?.OnProgressChanged(passed, total);
            path.OnGateMissed      += index          => hud?.OnGateMissed(index);
            path.OnRaceFinished    += (_, secs)      => hud?.OnRaceFinished(secs);
        }
    }
}