using UnityEngine;

namespace Ginei
{
    /// <summary>混乱（突撃を受けた側の一時的な統制崩れ）の効果仕様。実効値パターン＝倍率を返すだけ。</summary>
    public readonly struct ConfusionEffect
    {
        public readonly float attackFactor;      // 与ダメ倍率（<1＝反撃低下）
        public readonly float damageTakenFactor; // 被ダメ倍率（>1＝怒涛で押される）
        public readonly float mobilityFactor;    // 機動倍率（<1＝統制乱れで鈍る）
        public readonly float duration;          // 持続（秒）

        public ConfusionEffect(float attackFactor, float damageTakenFactor, float mobilityFactor, float duration)
        {
            this.attackFactor = attackFactor; this.damageTakenFactor = damageTakenFactor;
            this.mobilityFactor = mobilityFactor; this.duration = duration;
        }

        public static ConfusionEffect None => new ConfusionEffect(1f, 1f, 1f, 0f);
    }

    /// <summary>
    /// 混乱（#突撃）の純ロジック・test-first。正面からの突撃を受けた側は一時的に統制を崩し、
    /// <b>与ダメ↓・被ダメ↑・機動↓</b>になる。ただし<b>統率が高い提督ほど影響を受けにくい</b>（崩れにくく早く立て直す）。
    /// 効果は実効値パターンで倍率を返すだけ（基準値非破壊）。
    /// </summary>
    public static class ConfusionRules
    {
        /// <summary>統率50（標準）での与ダメ低下量（0.35＝-35%）。</summary>
        public const float BaseAttackPenalty = 0.35f;
        /// <summary>統率50での被ダメ増加量（0.40＝+40%）。</summary>
        public const float BaseDamageTakenBonus = 0.40f;
        /// <summary>統率50での機動低下量（0.40＝-40%）。</summary>
        public const float BaseMobilityPenalty = 0.40f;
        /// <summary>統率50での持続（秒）。</summary>
        public const float BaseDuration = 5f;

        /// <summary>統率による強さ倍率＝統率100で0.5（半減）・50で1.0・0で1.5。崩れにくさ・回復の速さを兼ねる。</summary>
        public static float Scale(float leadership)
        {
            float t = (Mathf.Clamp(leadership, 0f, 100f) - 50f) / 50f; // -1..+1
            return Mathf.Clamp(1f - t * 0.5f, 0.25f, 1.5f);
        }

        /// <summary>被混乱側の統率から混乱効果を算出する。統率が高いほど各ペナルティと持続が小さい。</summary>
        public static ConfusionEffect Effect(float leadership)
        {
            float s = Scale(leadership);
            float attack = Mathf.Max(0.1f, 1f - BaseAttackPenalty * s);
            float dmgTaken = 1f + BaseDamageTakenBonus * s;
            float mobility = Mathf.Max(0.1f, 1f - BaseMobilityPenalty * s);
            float duration = Mathf.Max(0.5f, BaseDuration * s);
            return new ConfusionEffect(attack, dmgTaken, mobility, duration);
        }
    }
}
