using System;
using System.Collections.Generic;
using UnityEngine;

namespace AerialNav.Gauntlet
{
    public class GauntletPath : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string pathName = "CSU to CU";

        [Header("Rings — drag in order, gate 1 first")]
        [SerializeField] private List<GauntletRing> rings = new();

        public event Action<GauntletPath>        OnRaceStarted;
        public event Action<GauntletPath, float> OnRaceFinished;
        public event Action<int, int>            OnProgressChanged;
        public event Action<int>                 OnGateMissed;

        public string PathName   => pathName;
        public int    TotalGates => rings.Count;
        public bool   IsRacing   { get; private set; }

        private int   _nextExpected;
        private float _startTime;
        private int   _passedCount;

        private void Start()
        {
            for (int i = 0; i < rings.Count; i++)
            {
                if (rings[i] == null)
                {
                    Debug.LogError($"[GauntletPath] Ring slot {i} is null.");
                    continue;
                }
                rings[i].Initialize(i);
                rings[i].OnRingPassed += HandleRingPassed;
            }
        }

        private void OnDestroy()
        {
            foreach (var ring in rings)
                if (ring != null) ring.OnRingPassed -= HandleRingPassed;
        }

        public void StartRace()
        {
            if (IsRacing) return;

            IsRacing      = true;
            _startTime    = Time.time;
            _nextExpected = 1;
            _passedCount  = 1;

            rings[0].SetState(GauntletRing.RingState.Passed);
            for (int i = 1; i < rings.Count; i++)
                rings[i].SetState(GauntletRing.RingState.Ready);

            OnRaceStarted?.Invoke(this);
            OnProgressChanged?.Invoke(_passedCount, TotalGates);

            Debug.Log($"[GauntletPath] Race started: '{pathName}'");
        }

        public void ResetToPassive()
        {
            IsRacing = false;
            foreach (var ring in rings)
                ring?.SetState(GauntletRing.RingState.Passive);
        }

        private void HandleRingPassed(GauntletRing ring)
        {
            if (!IsRacing && ring.Index == 0)
            {
                StartRace();
                return;
            }

            if (!IsRacing) return;

            if (ring.Index == _nextExpected)
                PassGate(ring);
            else
                WarnMissed();
        }

        private void PassGate(GauntletRing ring)
        {
            ring.SetState(GauntletRing.RingState.Passed);
            _passedCount++;
            _nextExpected++;

            OnProgressChanged?.Invoke(_passedCount, TotalGates);

            if (_nextExpected >= rings.Count)
                FinishRace();
        }

        private void WarnMissed()
        {
            if (_nextExpected < rings.Count)
                rings[_nextExpected].SetState(GauntletRing.RingState.Missed);

            OnGateMissed?.Invoke(_nextExpected);
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