using UnityEngine;

namespace Ginei
{
    /// <summary>隘路戦闘の調整係数（イゼルローン回廊型＝狭所で数の利が消える）。</summary>
    public readonly struct ChokeholdBattleParams
    {
        /// <summary>展開正面の下限（回廊幅0でも完全には正面が消えない＝1艦は通れる）。</summary>
        public readonly float minFrontage;
        /// <summary>回廊幅→展開正面のスケール（地形幅がそのまま投入可能な正面幅になる係数）。</summary>
        public readonly float frontageWidthScale;
        /// <summary>守備側有利の最大上乗せ幅（正面が狭いほど少数の守備が得る防御倍率の伸び）。</summary>
        public readonly float defenderAdvantageScale;
        /// <summary>正面突破に要る消耗のスケール（守備を抜くために攻者が払う代償の係数）。</summary>
        public readonly float breakthroughCostScale;
        /// <summary>守備側の継戦持久のスケール（守備兵力が攻者の損耗率に対し何 tick 粘れるか）。</summary>
        public readonly float persistenceScale;

        public ChokeholdBattleParams(float minFrontage, float frontageWidthScale, float defenderAdvantageScale,
                                     float breakthroughCostScale, float persistenceScale)
        {
            this.minFrontage = Mathf.Max(0.01f, minFrontage);
            this.frontageWidthScale = Mathf.Max(0f, frontageWidthScale);
            this.defenderAdvantageScale = Mathf.Max(0f, defenderAdvantageScale);
            this.breakthroughCostScale = Mathf.Max(0f, breakthroughCostScale);
            this.persistenceScale = Mathf.Max(0f, persistenceScale);
        }

        /// <summary>既定＝正面下限0.1・幅スケール1.0・守備有利2.0・突破消耗1.5・持久1.0。</summary>
        public static ChokeholdBattleParams Default => new ChokeholdBattleParams(0.1f, 1.0f, 2.0f, 1.5f, 1.0f);
    }

    /// <summary>
    /// 隘路戦闘の純ロジック（イゼルローン回廊型＝狭所で数の利が消える）。狭い回廊・隘路では大軍も
    /// 一度に展開できず**正面幅が地形に制限される**＝投入できる兵力は正面ぶんだけで、残りは後方に渋滞する。
    /// その結果、少数で大軍を食い止められ（数の優位が無効化される）、突破には正面の消耗を強いられる。
    /// <see cref="ChokepointValueRules"/>（要衝の戦略的価値＝盤面上どれだけ重要か）とは別＝こちらは
    /// **隘路の戦術的な正面制限／数の無効化**そのもの。<see cref="FortressRules"/>（回廊要塞＝艦隊が籠もる
    /// 人工構造物）とも別＝こちらは**地形そのものの隘路効果**（要塞が無くても狭ければ効く）。
    /// 盤面非依存の plain 引数・入力クランプ・実効値パターン・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ChokeholdBattleRules
    {
        /// <summary>
        /// 展開正面（実効値）＝回廊幅 corridorWidth に比例して制限される、一度に並べられる正面幅。
        /// 狭い回廊ほど正面が痩せる。下限 minFrontage で完全には消えない（1個部隊は通れる）。
        /// </summary>
        public static float EffectiveFrontage(float corridorWidth, ChokeholdBattleParams p)
        {
            float w = Mathf.Max(0f, corridorWidth);
            return Mathf.Max(p.minFrontage, w * p.frontageWidthScale);
        }

        public static float EffectiveFrontage(float corridorWidth)
            => EffectiveFrontage(corridorWidth, ChokeholdBattleParams.Default);

        /// <summary>
        /// 一度に投入できる兵力＝総兵力 totalStrength と展開正面 effectiveFrontage の小さい方。
        /// 正面が狭ければ大軍でも正面ぶんしか前に出せない（残りは控え＝<see cref="BottleneckCongestion"/>）。
        /// </summary>
        public static float CommittableStrength(float totalStrength, float effectiveFrontage)
        {
            float total = Mathf.Max(0f, totalStrength);
            float front = Mathf.Max(0f, effectiveFrontage);
            return Mathf.Min(total, front);
        }

        /// <summary>
        /// 数の優位の無効化率（0..1）＝攻者 attackerStrength のうち正面に出られず無効化された割合
        /// ＝1−投入可能兵力/攻者兵力。狭い正面に大軍をぶつけるほど 1 に近づく（数が効かない）。攻者0なら0。
        /// </summary>
        public static float NumericalNullification(float attackerStrength, float effectiveFrontage)
        {
            float att = Mathf.Max(0f, attackerStrength);
            if (att <= 0f) return 0f;
            float committable = CommittableStrength(att, effectiveFrontage);
            return Mathf.Clamp01(1f - committable / att);
        }

        /// <summary>
        /// 守備側の防御倍率（≥1）＝正面 effectiveFrontage が守備兵力 defenderStrength に対し狭いほど大きい
        /// ＝1＋scale×defender/(defender+frontage)。狭所では少数の守備が数倍の戦闘力を発揮する。守備0なら等倍。
        /// </summary>
        public static float DefenderAdvantage(float effectiveFrontage, float defenderStrength, ChokeholdBattleParams p)
        {
            float def = Mathf.Max(0f, defenderStrength);
            float front = Mathf.Max(0f, effectiveFrontage);
            if (def <= 0f) return 1f;
            return 1f + p.defenderAdvantageScale * (def / (def + front));
        }

        public static float DefenderAdvantage(float effectiveFrontage, float defenderStrength)
            => DefenderAdvantage(effectiveFrontage, defenderStrength, ChokeholdBattleParams.Default);

        /// <summary>
        /// 正面突破に要る消耗＝守備兵力 defenderStrength を抜くために攻者が払う代償
        /// ＝scale×defender×defender/(defender+committable)。投入できる攻者 attackerCommittable が
        /// 細い（隘路）ほど相対コストが上がる＝逐次投入では高くつく。
        /// </summary>
        public static float BreakthroughCost(float defenderStrength, float attackerCommittable, ChokeholdBattleParams p)
        {
            float def = Mathf.Max(0f, defenderStrength);
            float committable = Mathf.Max(0f, attackerCommittable);
            if (def <= 0f) return 0f;
            return p.breakthroughCostScale * def * (def / (def + committable));
        }

        public static float BreakthroughCost(float defenderStrength, float attackerCommittable)
            => BreakthroughCost(defenderStrength, attackerCommittable, ChokeholdBattleParams.Default);

        /// <summary>
        /// 後方渋滞率（0..1）＝展開しきれず控えに留まる兵力の割合＝1−投入可能兵力/総兵力。
        /// 大軍が狭い正面に詰まるほど 1 に近づく（補給線が伸び、機動が利かない＝隘路の代償）。総兵力0なら0。
        /// </summary>
        public static float BottleneckCongestion(float totalStrength, float effectiveFrontage)
        {
            float total = Mathf.Max(0f, totalStrength);
            if (total <= 0f) return 0f;
            float committable = CommittableStrength(total, effectiveFrontage);
            return Mathf.Clamp01(1f - committable / total);
        }

        /// <summary>
        /// 守備側の継戦持久（≥0）＝守備兵力 defenderStrength を攻者の損耗率 attackerAttrition で割った持ちこたえ
        /// ＝scale×defender/attrition。攻者の削りが小さいほど（隘路で火力が集中できないほど）長く守れる。
        /// </summary>
        public static float ChokePersistence(float defenderStrength, float attackerAttrition, ChokeholdBattleParams p)
        {
            float def = Mathf.Max(0f, defenderStrength);
            float attr = Mathf.Max(0.0001f, attackerAttrition);
            return p.persistenceScale * def / attr;
        }

        public static float ChokePersistence(float defenderStrength, float attackerAttrition)
            => ChokePersistence(defenderStrength, attackerAttrition, ChokeholdBattleParams.Default);

        /// <summary>
        /// 隘路が守られているか＝正面密度（守備兵力/展開正面）が閾値 threshold 以上なら持ちこたえている。
        /// 狭い正面ほど少ない守備で密度が上がる＝少数で隘路を封じられる。
        /// </summary>
        public static bool IsChokeHeld(float defenderStrength, float effectiveFrontage, float threshold)
        {
            float def = Mathf.Max(0f, defenderStrength);
            float front = Mathf.Max(0.0001f, effectiveFrontage);
            float density = def / front;
            return density >= Mathf.Max(0f, threshold);
        }
    }
}
