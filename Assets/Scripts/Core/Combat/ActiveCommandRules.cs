using UnityEngine;

namespace Ginei
{
    /// <summary>提督の特殊指揮（アクティブ指揮・#2175）。プレイヤーが能動的に発令する一手。</summary>
    public enum ActiveCommand { 一斉砲撃, 突撃, 不退転 }

    /// <summary>特殊指揮の効果仕様（倍率・持続・クールダウン）。実効値パターン＝倍率を返すだけ。</summary>
    public readonly struct ActiveCommandSpec
    {
        public readonly float attackFactor;      // 与ダメ倍率
        public readonly float damageTakenFactor; // 被ダメ倍率（<1で堅い・>1で脆い）
        public readonly float speedFactor;       // 機動倍率
        public readonly bool moraleLock;         // 敗走しない（不退転）
        public readonly float duration;          // 効果持続（秒）
        public readonly float cooldown;          // 再使用までの基準クールダウン（秒）

        public ActiveCommandSpec(float attackFactor, float damageTakenFactor, float speedFactor, bool moraleLock, float duration, float cooldown)
        {
            this.attackFactor = attackFactor; this.damageTakenFactor = damageTakenFactor; this.speedFactor = speedFactor;
            this.moraleLock = moraleLock; this.duration = duration; this.cooldown = cooldown;
        }
    }

    /// <summary>
    /// 提督の特殊指揮（アクティブ指揮・#2175）の純ロジック。クールダウン制の能動指揮で会戦を「眺める→決断する」へ。
    /// ・一斉砲撃＝短時間の与ダメージバースト。
    /// ・突撃＝速度・攻撃↑・被ダメ↑（前のめり）。
    /// ・不退転＝敗走しない＋被ダメ↓（踏みとどまる）。
    /// 効果は実効値パターンで倍率を返すだけ（基準値非破壊）。クールダウンは提督の統率で短縮。test-first。
    /// </summary>
    public static class ActiveCommandRules
    {
        /// <summary>指揮ごとの効果仕様。</summary>
        public static ActiveCommandSpec Spec(ActiveCommand cmd)
        {
            switch (cmd)
            {
                case ActiveCommand.一斉砲撃: return new ActiveCommandSpec(1.5f, 1.0f, 1.0f, false, 4f, 20f);
                case ActiveCommand.突撃:     return new ActiveCommandSpec(1.2f, 1.2f, 1.4f, false, 6f, 24f);
                case ActiveCommand.不退転:   return new ActiveCommandSpec(1.0f, 0.85f, 0.9f, true, 8f, 30f);
                default:                     return new ActiveCommandSpec(1f, 1f, 1f, false, 0f, 0f);
            }
        }

        /// <summary>統率でクールダウンを短縮（統率100で-30%・0で+30%・クランプ）。有能な提督は再令が速い。</summary>
        public static float EffectiveCooldown(ActiveCommand cmd, float leadership)
        {
            float baseCd = Spec(cmd).cooldown;
            float t = (Mathf.Clamp(leadership, 0f, 100f) - 50f) / 50f; // -1..+1
            return Mathf.Max(1f, baseCd * (1f - t * 0.3f));
        }
    }
}
