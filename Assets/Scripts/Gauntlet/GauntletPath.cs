using System;
using System.Collections.Generic;
using UnityEngine;

namespace AerialNav.Gauntlet
{
    /// <summary>
    /// Owns an ordered list of GauntletRings and a hard cursor (_nextExpected).
    ///
    /// Rules:
    ///   - Any ring entry fires OnTriggerEntered, which this class evaluates.
    ///   - If the entered ring IS _nextExpected  → mark Passed, advance cursor.
    ///   - If the entered ring is NOT _nextExpected → flash missed ring, warn HUD.
    ///   - Cursor never moves backwards and never skips.
    ///   - Race ends only when cursor reaches rings.Count.
    /// </summary>
    public class GauntletPath : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Identity")]
        [SerializeField] private string pathName = "CSU to CU";

        [Header("Rings — drag in order, gate 1 first")]
        [SerializeField] private List<GauntletRing> rings = new();

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<GauntletPath>              OnRaceStarted;
        public event Action<GauntletPath, float>       OnRaceFinished;   // (path, totalSeconds)
        public event Action<int, int>                  OnProgressChanged; // (passed, total)
        public event Action<int>                       OnGateMissed;      // missed gate index (0-based)

        // ── State ─────────────────────────────────────────────────────────────
        public string PathName    => pathName;
        public int    TotalGates  => rings.Count;
        public bool   IsRacing    { get; private set; }

        private int   _nextExpected;   // 0-based index of the gate the player must hit next
        private float _startTime;
        private int   _passedCount;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Start()
        {
            for (int i = 0; i < rings.Count; i++)
            {
                if (rings[i] == null)
                {
                    Debug.LogError($"[GauntletPath] Ring slot {i} is null — check Inspector.");
                    continue;
                }
                rings[i].Initialize(i);
                rings[i].OnTriggerEntered += HandleRingEntered;
            }
        }

        private void OnDestroy()
        {
            foreach (var ring in rings)
                if (ring != null) ring.OnTriggerEntered -= HandleRingEntered;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called when the player flies through gate 0 while no race is running.
        /// Gate 0 is both the start trigger and the first gate.
        /// </summary>
        public void StartRace()
        {
            if (IsRacing) return;

            IsRacing       = true;
            _startTime     = Time.time;
            _nextExpected  = 1;          // gate 0 was just passed to trigger the start
            _passedCount   = 1;

            // Gate 0 → Passed immediately; all others → Ready
            rings[0].SetState(GauntletRing.RingState.Passed);
            for (int i = 1; i < rings.Count; i++)
                rings[i].SetState(GauntletRing.RingState.Ready);

            OnRaceStarted?.Invoke(this);
            OnProgressChanged?.Invoke(_passedCount, TotalGates);

            Debug.Log($"[GauntletPath] Race started: '{pathName}'");
        }

        /// <summary>Resets all rings to Passive without raising any events.</summary>
        public void ResetToPassive()
        {
            IsRacing = false;
            foreach (var ring in rings)
                ring?.SetState(GauntletRing.RingState.Passive);
        }

        // ── Ring Entry Evaluation ─────────────────────────────────────────────
        private void HandleRingEntered(GauntletRing ring)
        {
            // Gate 0 while not racing → start the race
            if (!IsRacing && ring.Index == 0)
            {
                StartRace();
                return;
            }

            if (!IsRacing) return;

            if (ring.Index == _nextExpected)
            {
                PassGate(ring);
            }
            else
            {
                // Wrong gate — warn about the one they should have hit
                WarnMissed();
            }
        }

        private void PassGate(GauntletRing ring)
        {
            ring.SetState(GauntletRing.RingState.Passed);
            _passedCount++;
            _nextExpected++;

            // Clear any Missed flash on this gate (was already set to Passed above)
            // Also clear Missed state on all currently-flashing rings below cursor
            // (there can only ever be one missed ring at a time — the previous _nextExpected)
            ClearMissedFlash();

            OnProgressChanged?.Invoke(_passedCount, TotalGates);

            if (_nextExpected >= rings.Count)
                FinishRace();
        }

        private void WarnMissed()
        {
            // The ring they should have hit is _nextExpected
            if (_nextExpected < rings.Count)
                rings[_nextExpected].SetState(GauntletRing.RingState.Missed);

            OnGateMissed?.Invoke(_nextExpected); // 0-based; HUD adds 1 for display
        }

        private void ClearMissedFlash()
        {
            // After correctly passing a gate, ensure no ring is left flashing.
            // In practice only _nextExpected - 1 could have been Missed, but
            // iterating is cheap and defensive.
            for (int i = 0; i < _nextExpected - 1; i++)
            {
                if (rings[i].State == GauntletRing.RingState.Missed)
                    rings[i].SetState(GauntletRing.RingState.Ready);
            }
        }

        private void FinishRace()
        {
            IsRacing = false;
            float total = Time.time - _startTime;

            Debug.Log($"[GauntletPath] '{pathName}' finished — {total:F2}s");
            OnRaceFinished?.Invoke(this, total);
        }
    }
}