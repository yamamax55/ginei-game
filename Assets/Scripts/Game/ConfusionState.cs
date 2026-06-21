using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 混乱（突撃を受けた側の一時的な統制崩れ #突撃）の会戦配線。1部隊（旗艦）の混乱効果と持続を管理する。
    /// <see cref="ConfusionRules"/>（Core）の仕様に従い、効果中は <see cref="FleetStrength"/> の confusion 系倍率を
    /// 書き込み（与ダメ↓・被ダメ↑・機動↓）、終了でリセットする（実効値パターン・基準値非破壊）。
    /// 付与は <see cref="Inflict"/> の static 窓口（無ければ自動付与）。統率が高い提督ほど効果は小さい。
    /// </summary>
    public class ConfusionState : MonoBehaviour
    {
        private FleetStrength strength;
        private bool active;
        private float activeUntil;

        /// <summary>混乱中か。</summary>
        public bool IsConfused => active;
        /// <summary>残り混乱秒（0＝なし）。</summary>
        public float Remaining => active ? Mathf.Max(0f, activeUntil - Time.time) : 0f;

        private void Awake() => strength = GetComponent<FleetStrength>();

        /// <summary>
        /// 指定旗艦を混乱させる（無ければ ConfusionState を自動付与）。退却・撃墜中は無効。
        /// 効果は被混乱側の統率で軽減（高統率ほど弱く・短い）。既に混乱中なら強い方／長い方で上書き。
        /// </summary>
        public static bool Inflict(FleetStrength target)
        {
            if (target == null || !target.IsAlive) return false;
            ConfusionState st = target.GetComponent<ConfusionState>();
            if (st == null) st = target.gameObject.AddComponent<ConfusionState>();
            return st.Apply();
        }

        private bool Apply()
        {
            if (strength == null || !strength.IsAlive) return false;

            float leadership = strength.admiralData != null ? strength.admiralData.EffectiveLeadership : 50f;
            ConfusionEffect e = ConfusionRules.Effect(leadership);

            // 怒涛の上書き：被ダメ倍率は大きい方、与ダメ/機動は小さい方（より厳しい混乱を優先）。
            strength.confusionAttackFactor = active ? Mathf.Min(strength.confusionAttackFactor, e.attackFactor) : e.attackFactor;
            strength.confusionDamageTakenFactor = active ? Mathf.Max(strength.confusionDamageTakenFactor, e.damageTakenFactor) : e.damageTakenFactor;
            strength.confusionMobilityFactor = active ? Mathf.Min(strength.confusionMobilityFactor, e.mobilityFactor) : e.mobilityFactor;

            float until = Time.time + Mathf.Max(0.1f, e.duration);
            activeUntil = active ? Mathf.Max(activeUntil, until) : until;
            if (!active)
            {
                active = true;
                NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                    $"{strength.admiralName} 隊：突撃を受け混乱（統制が乱れる）");
            }
            return true;
        }

        private void Update()
        {
            if (!active) return;
            if (strength == null || !strength.IsAlive || Time.time >= activeUntil)
                ClearEffect();
        }

        private void ClearEffect()
        {
            active = false;
            if (strength != null)
            {
                strength.confusionAttackFactor = 1f;
                strength.confusionDamageTakenFactor = 1f;
                strength.confusionMobilityFactor = 1f;
            }
        }

        private void OnDisable() => ClearEffect();
    }
}
