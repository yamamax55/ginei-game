using UnityEngine;

namespace Ginei
{
    public struct FormationDensityState
    {
        public bool cruising;
        public bool pending;
        public bool pendingCruising;
        public float pendingSeconds;
    }

    /// <summary>航行隊形と停止隊形の速度境界にヒステリシスと保持時間を与え、密度のちらつきを防ぐ。</summary>
    public static class FormationDensityRules
    {
        public static FormationDensityState Step(FormationDensityState state, float speed, bool inCombat,
                                                 float enterSpeed, float exitSpeedRatio,
                                                 float holdSeconds, float deltaTime)
        {
            if (inCombat)
            {
                state.cruising = false;
                state.pending = false;
                state.pendingSeconds = 0f;
                return state;
            }

            float enter = Mathf.Max(0f, enterSpeed);
            float exit = enter * Mathf.Clamp01(exitSpeedRatio);
            bool desired = state.cruising ? speed > exit : speed >= enter;
            if (desired == state.cruising)
            {
                state.pending = false;
                state.pendingSeconds = 0f;
                return state;
            }

            if (!state.pending || state.pendingCruising != desired)
            {
                state.pending = true;
                state.pendingCruising = desired;
                state.pendingSeconds = 0f;
            }
            state.pendingSeconds += Mathf.Max(0f, deltaTime);
            if (state.pendingSeconds >= Mathf.Max(0f, holdSeconds))
            {
                state.cruising = desired;
                state.pending = false;
                state.pendingSeconds = 0f;
            }
            return state;
        }
    }
}
