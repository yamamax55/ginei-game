using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の特殊指揮（アクティブ指揮 #2175）の会戦配線。1部隊（旗艦）の発令・効果持続・クールダウンを管理する。
    /// `ActiveCommandRules`（Core）の仕様に従い、効果中は `FleetStrength` の active系倍率を書き込み、
    /// 終了でリセットする（実効値パターン・基準値非破壊）。発令は `Issue` の static 窓口（無ければ自動付与）。
    /// </summary>
    public class ActiveCommandState : MonoBehaviour
    {
        private FleetStrength strength;
        private ActiveCommand current;
        private bool active;
        private float activeUntil;
        private readonly Dictionary<ActiveCommand, float> readyAt = new Dictionary<ActiveCommand, float>();

        /// <summary>発令中か。</summary>
        public bool IsActive => active;
        /// <summary>発令中の指揮。</summary>
        public ActiveCommand Current => current;

        private void Awake() => strength = GetComponent<FleetStrength>();

        /// <summary>残りクールダウン秒（0＝発令可能）。</summary>
        public float RemainingCooldown(ActiveCommand cmd)
            => readyAt.TryGetValue(cmd, out float t) ? Mathf.Max(0f, t - Time.time) : 0f;

        /// <summary>残り効果秒（0＝効果なし）。</summary>
        public float RemainingDuration => active ? Mathf.Max(0f, activeUntil - Time.time) : 0f;

        /// <summary>
        /// 指定旗艦へ特殊指揮を発令（無ければ ActiveCommandState を自動付与）。
        /// 退却・撃墜中や、別指揮が効果中、クールダウン中は失敗（false）。
        /// </summary>
        public static bool Issue(FleetStrength target, ActiveCommand cmd)
        {
            if (target == null || !target.IsAlive) return false;
            ActiveCommandState st = target.GetComponent<ActiveCommandState>();
            if (st == null) st = target.gameObject.AddComponent<ActiveCommandState>();
            return st.Activate(cmd);
        }

        private bool Activate(ActiveCommand cmd)
        {
            if (strength == null || !strength.IsAlive) return false;
            if (active) return false;                       // 効果中は重ねがけ不可
            if (RemainingCooldown(cmd) > 0f) return false;  // クールダウン中

            ActiveCommandSpec spec = ActiveCommandRules.Spec(cmd);
            current = cmd;
            active = true;
            activeUntil = Time.time + Mathf.Max(0.1f, spec.duration);

            float leadership = strength.admiralData != null ? strength.admiralData.EffectiveLeadership : 50f;
            readyAt[cmd] = Time.time + ActiveCommandRules.EffectiveCooldown(cmd, leadership);

            strength.activeAttackFactor = spec.attackFactor;
            strength.activeDamageTakenFactor = spec.damageTakenFactor;
            strength.activeSpeedFactor = spec.speedFactor;
            strength.activeMoraleLock = spec.moraleLock;

            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                $"{strength.admiralName} 隊：特殊指揮『{cmd}』発令");
            return true;
        }

        private void Update()
        {
            if (!active) return;
            if (strength == null || !strength.IsAlive || Time.time >= activeUntil)
            {
                ClearEffect();
            }
        }

        private void ClearEffect()
        {
            active = false;
            if (strength != null)
            {
                strength.activeAttackFactor = 1f;
                strength.activeDamageTakenFactor = 1f;
                strength.activeSpeedFactor = 1f;
                strength.activeMoraleLock = false;
            }
        }

        private void OnDisable() => ClearEffect();
    }
}
