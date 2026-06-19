using UnityEngine;

namespace Ginei
{
    /// <summary>軍艦設計（攻防走のトレードオフ）の調整係数。</summary>
    public readonly struct WarshipDesignParams
    {
        /// <summary>火力定格の変換係数（火力配分→攻撃力定格の効き）。</summary>
        public readonly float firepowerScale;
        /// <summary>装甲定格の変換係数（装甲配分→防御力定格の効き）。</summary>
        public readonly float armorScale;
        /// <summary>速力定格の変換係数（機動配分→速力定格の効き）。</summary>
        public readonly float speedScale;
        /// <summary>技術水準による底上げの下駄（tech0 のときの倍率＝この値、tech1 で1.0へ伸びる）。</summary>
        public readonly float techFloor;
        /// <summary>特化ボーナスの最大幅（最大配分が単独突出のとき）。</summary>
        public readonly float specializationGain;
        /// <summary>過積載ペナルティの係数（合計配分が1を超えた分に掛ける）。</summary>
        public readonly float overloadGain;

        public WarshipDesignParams(float firepowerScale, float armorScale, float speedScale,
            float techFloor, float specializationGain, float overloadGain)
        {
            this.firepowerScale = Mathf.Clamp01(firepowerScale);
            this.armorScale = Mathf.Clamp01(armorScale);
            this.speedScale = Mathf.Clamp01(speedScale);
            this.techFloor = Mathf.Clamp01(techFloor);
            this.specializationGain = Mathf.Clamp01(specializationGain);
            this.overloadGain = Mathf.Max(0f, overloadGain);
        }

        /// <summary>既定＝火力/装甲/速力変換1.0・技術下駄0.5・特化幅0.5・過積載係数1.0。</summary>
        public static WarshipDesignParams Default => new WarshipDesignParams(1f, 1f, 1f, 0.5f, 0.5f, 1f);
    }

    /// <summary>
    /// 軍艦設計の純ロジック（攻防走のトレードオフと設計バランス）。限られた排水量（トン数）を
    /// 火力/装甲/機動へどう配分するか＝特化型（一点突出で尖るが脆い）と汎用型（均整は取れるが平凡）の
    /// 綱引き、合計が排水量を超えれば過積載ペナルティ、技術水準が高ければ同じ配分でも定格が底上げされる。
    /// 速力は大型艦ほど鈍る（機動配分÷(1+トン数)）。乱数なし・決定論・実効値パターン（入力非破壊）。
    /// 純ロジック（非 MonoBehaviour・test-first・LINQ なし）。
    /// </summary>
    public static class WarshipDesignRules
    {
        /// <summary>
        /// 配分の正規化窓口＝火力/装甲/機動の3配分を合計1へ正規化し、火力ぶんの有効配分を返す（按分）。
        /// 過配分（合計&gt;1）でも按分で割合に直す。合計が0なら0（配分なし）。
        /// </summary>
        public static float TonnageAllocation(float firepowerShare, float armorShare, float speedShare, WarshipDesignParams p)
        {
            float f = Mathf.Clamp01(firepowerShare);
            float a = Mathf.Clamp01(armorShare);
            float s = Mathf.Clamp01(speedShare);
            float sum = f + a + s;
            if (sum <= 0f) return 0f;
            return f / sum;
        }

        public static float TonnageAllocation(float firepowerShare, float armorShare, float speedShare)
            => TonnageAllocation(firepowerShare, armorShare, speedShare, WarshipDesignParams.Default);

        /// <summary>
        /// 攻撃力定格（0..1）＝火力配分×技術倍率×火力変換。技術倍率は techFloor〜1.0（tech0で下駄・tech1で満額）。
        /// </summary>
        public static float FirepowerRating(float firepowerShare, float techLevel, WarshipDesignParams p)
        {
            float tech = p.techFloor + (1f - p.techFloor) * Mathf.Clamp01(techLevel);
            return Mathf.Clamp01(Mathf.Clamp01(firepowerShare) * tech * p.firepowerScale);
        }

        public static float FirepowerRating(float firepowerShare, float techLevel)
            => FirepowerRating(firepowerShare, techLevel, WarshipDesignParams.Default);

        /// <summary>
        /// 防御力定格（0..1）＝装甲配分×技術倍率×装甲変換。技術倍率は <see cref="FirepowerRating"/> と同形。
        /// </summary>
        public static float ArmorRating(float armorShare, float techLevel, WarshipDesignParams p)
        {
            float tech = p.techFloor + (1f - p.techFloor) * Mathf.Clamp01(techLevel);
            return Mathf.Clamp01(Mathf.Clamp01(armorShare) * tech * p.armorScale);
        }

        public static float ArmorRating(float armorShare, float techLevel)
            => ArmorRating(armorShare, techLevel, WarshipDesignParams.Default);

        /// <summary>
        /// 速力定格（0..1）＝機動配分÷(1+トン数)×速力変換。大型艦（トン数大）ほど同じ配分でも鈍い。
        /// </summary>
        public static float SpeedRating(float speedShare, float tonnage, WarshipDesignParams p)
        {
            float t = Mathf.Max(0f, tonnage);
            return Mathf.Clamp01(Mathf.Clamp01(speedShare) / (1f + t) * p.speedScale);
        }

        public static float SpeedRating(float speedShare, float tonnage)
            => SpeedRating(speedShare, tonnage, WarshipDesignParams.Default);

        /// <summary>
        /// 設計の均整（0..1）＝三定格の幾何平均（pow(f·a·s, 1/3)）。一点でも欠ける（0に近い）と全体が下がる
        /// ＝偏った特化型は均整が低い。三定格とも高い汎用設計でのみ高くなる。
        /// </summary>
        public static float DesignBalance(float firepowerRating, float armorRating, float speedRating, WarshipDesignParams p)
        {
            float f = Mathf.Clamp01(firepowerRating);
            float a = Mathf.Clamp01(armorRating);
            float s = Mathf.Clamp01(speedRating);
            float prod = f * a * s;
            if (prod <= 0f) return 0f;
            return Mathf.Clamp01(Mathf.Pow(prod, 1f / 3f));
        }

        public static float DesignBalance(float firepowerRating, float armorRating, float speedRating)
            => DesignBalance(firepowerRating, armorRating, speedRating, WarshipDesignParams.Default);

        /// <summary>
        /// 特化ボーナス（0..specializationGain）＝最大配分 maxShare が均等(1/3)から1へ突出するほど増える。
        /// 1/3（完全な汎用）で0、単独最大配分1で満額。特化型と汎用型の対比を数値化する。
        /// </summary>
        public static float SpecializationBonus(float maxShare, WarshipDesignParams p)
        {
            const float even = 1f / 3f;
            float prominence = Mathf.Clamp01((Mathf.Clamp01(maxShare) - even) / (1f - even));
            return prominence * p.specializationGain;
        }

        public static float SpecializationBonus(float maxShare)
            => SpecializationBonus(maxShare, WarshipDesignParams.Default);

        /// <summary>
        /// 過積載ペナルティ（0..1）＝合計配分 totalShare が1を超えた分×過積載係数。1以下なら0（積載内）。
        /// 設計に詰め込みすぎると排水量を超え、戦闘力が削られる。
        /// </summary>
        public static float OverloadPenalty(float totalShare, WarshipDesignParams p)
        {
            float over = Mathf.Max(0f, totalShare - 1f);
            return Mathf.Clamp01(over * p.overloadGain);
        }

        public static float OverloadPenalty(float totalShare)
            => OverloadPenalty(totalShare, WarshipDesignParams.Default);

        /// <summary>
        /// 戦闘力（0..1）＝三定格の総合（平均）から過積載ペナルティを引いたもの。攻防走のいずれも0に近ければ低く、
        /// 過積載なら更に削られる。設計の良し悪しを単一スカラに集約する最終窓口。
        /// </summary>
        public static float CombatEffectiveness(float firepowerRating, float armorRating, float speedRating,
            float overloadPenalty, WarshipDesignParams p)
        {
            float f = Mathf.Clamp01(firepowerRating);
            float a = Mathf.Clamp01(armorRating);
            float s = Mathf.Clamp01(speedRating);
            float overall = (f + a + s) / 3f;
            return Mathf.Clamp01(overall - Mathf.Clamp01(overloadPenalty));
        }

        public static float CombatEffectiveness(float firepowerRating, float armorRating, float speedRating, float overloadPenalty)
            => CombatEffectiveness(firepowerRating, armorRating, speedRating, overloadPenalty, WarshipDesignParams.Default);
    }
}
