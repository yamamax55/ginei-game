using UnityEngine;

namespace Ginei
{
    /// <summary>守備隊防衛の調整係数（駐留部隊の拠点防衛＝守備兵×防御施設・籠城士気・攻囲消耗・降伏/玉砕の数値モデル）。</summary>
    public readonly struct GarrisonDefenseParams
    {
        /// <summary>防御施設1段あたりの防御力倍率の伸び（堅城ほど少数で守れる＝乗数）。</summary>
        public readonly float fortificationGain;
        /// <summary>救援期待が籠城士気に効く重み（援軍が近いほど踏みとどまる）。</summary>
        public readonly float reliefMoraleWeight;
        /// <summary>残存補給が籠城士気に効く重み（兵糧が尽きれば崩れる）。</summary>
        public readonly float supplyMoraleWeight;
        /// <summary>指揮官の意志が籠城士気に効く重み（不屈の将は持ちこたえる）。</summary>
        public readonly float resolveMoraleWeight;
        /// <summary>攻囲下の消耗の基準率（防御力に対する敵圧力の比に掛ける）。</summary>
        public readonly float attritionRate;
        /// <summary>降伏圧力に消耗が効く重み。</summary>
        public readonly float attritionPressureWeight;
        /// <summary>降伏圧力に補給枯渇が効く重み。</summary>
        public readonly float supplyPressureWeight;
        /// <summary>降伏圧力に絶望（救援なし）が効く重み。</summary>
        public readonly float hopelessPressureWeight;
        /// <summary>玉砕の決意に指揮官の意志が効く重み（士気が崩れても将が引っ張る）。</summary>
        public readonly float lastStandResolveWeight;
        /// <summary>持ちこたえる期間の基準日数係数（防御力×補給×士気の合成に掛ける）。</summary>
        public readonly float durationScale;

        public GarrisonDefenseParams(float fortificationGain, float reliefMoraleWeight, float supplyMoraleWeight,
                                     float resolveMoraleWeight, float attritionRate, float attritionPressureWeight,
                                     float supplyPressureWeight, float hopelessPressureWeight,
                                     float lastStandResolveWeight, float durationScale)
        {
            this.fortificationGain = Mathf.Max(0f, fortificationGain);
            this.reliefMoraleWeight = Mathf.Clamp01(reliefMoraleWeight);
            this.supplyMoraleWeight = Mathf.Clamp01(supplyMoraleWeight);
            this.resolveMoraleWeight = Mathf.Clamp01(resolveMoraleWeight);
            this.attritionRate = Mathf.Max(0f, attritionRate);
            this.attritionPressureWeight = Mathf.Clamp01(attritionPressureWeight);
            this.supplyPressureWeight = Mathf.Clamp01(supplyPressureWeight);
            this.hopelessPressureWeight = Mathf.Clamp01(hopelessPressureWeight);
            this.lastStandResolveWeight = Mathf.Clamp01(lastStandResolveWeight);
            this.durationScale = Mathf.Max(0f, durationScale);
        }

        /// <summary>
        /// 既定＝施設1段0.5伸び・籠城士気の重み(救援0.4/補給0.4/意志0.2)・消耗率0.5・
        /// 降伏圧力の重み(消耗0.5/補給0.3/絶望0.2)・玉砕意志0.5・期間係数30。
        /// 重みは士気と降伏圧力でそれぞれ和=1.0 とし、加重平均として読める。
        /// </summary>
        public static GarrisonDefenseParams Default
            => new GarrisonDefenseParams(0.5f, 0.4f, 0.4f, 0.2f, 0.5f, 0.5f, 0.3f, 0.2f, 0.5f, 30f);
    }

    /// <summary>
    /// 守備隊防衛の純ロジック（駐留部隊の拠点防衛）。守備兵力と防御施設の組み合わせで防御力を出し、
    /// 救援への期待・残存補給・指揮官の意志で籠城の士気が決まり、敵の攻囲で守備隊が消耗する。
    /// 消耗と補給枯渇と絶望が降伏圧力を生み、士気がそれを上回れば守りきり、下回れば降伏する。
    /// 崩れてもなお指揮官が引っ張れば玉砕（最後の抵抗）に転ぶ＝降伏か玉砕かの分岐。
    /// 占領そのものの解決は <see cref="PlanetSiegeRules"/>（攻城）、惑星の層別迎撃は
    /// <see cref="PlanetaryDefenseRules"/>（惑星防衛）、単一要塞の点防御は <see cref="FortressRules"/>（要塞）が担う（別系統）。
    /// 乱数なし・決定論（判定は呼び出し側 roll）。実効値パターン（基準値非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class GarrisonDefenseRules
    {
        /// <summary>
        /// 守備の防御力（≧0）＝守備兵 × (1 + 施設段数 × fortificationGain)。
        /// 堅城ほど少数の守備で大きな防御力を発揮する（施設の乗数効果）。施設段数・兵力は負をクランプ。
        /// </summary>
        public static float DefensiveStrength(float garrisonSize, float fortificationLevel, GarrisonDefenseParams p)
        {
            float size = Mathf.Max(0f, garrisonSize);
            float fort = Mathf.Max(0f, fortificationLevel);
            return size * (1f + fort * p.fortificationGain);
        }

        public static float DefensiveStrength(float garrisonSize, float fortificationLevel)
            => DefensiveStrength(garrisonSize, fortificationLevel, GarrisonDefenseParams.Default);

        /// <summary>
        /// 籠城士気（0..1）＝救援期待・残存補給・指揮官の意志の加重平均（重みは Params・既定で和=1.0）。
        /// 援軍が近く兵糧があり将が不屈なら高い。全入力 0..1 にクランプ。
        /// </summary>
        public static float SiegeMorale(float reliefHope, float suppliesRemaining, float leadershipResolve,
                                        GarrisonDefenseParams p)
        {
            float relief = Mathf.Clamp01(reliefHope);
            float supplies = Mathf.Clamp01(suppliesRemaining);
            float resolve = Mathf.Clamp01(leadershipResolve);
            float morale = relief * p.reliefMoraleWeight
                         + supplies * p.supplyMoraleWeight
                         + resolve * p.resolveMoraleWeight;
            return Mathf.Clamp01(morale);
        }

        public static float SiegeMorale(float reliefHope, float suppliesRemaining, float leadershipResolve)
            => SiegeMorale(reliefHope, suppliesRemaining, leadershipResolve, GarrisonDefenseParams.Default);

        /// <summary>
        /// 攻囲下の消耗（0..1）＝敵圧力 ÷ 防御力 に消耗率を掛ける。防御力が高いほど痩せにくい。
        /// 防御力 0（裸の守備）は最大消耗1.0。敵圧力は負をクランプ。
        /// </summary>
        public static float AttritionUnderSiege(float attackerPressure, float defensiveStrength, GarrisonDefenseParams p)
        {
            float pressure = Mathf.Max(0f, attackerPressure);
            float defense = Mathf.Max(0f, defensiveStrength);
            if (defense <= 0f) return pressure > 0f ? 1f : 0f;
            float ratio = pressure / defense;
            return Mathf.Clamp01(ratio * p.attritionRate);
        }

        public static float AttritionUnderSiege(float attackerPressure, float defensiveStrength)
            => AttritionUnderSiege(attackerPressure, defensiveStrength, GarrisonDefenseParams.Default);

        /// <summary>
        /// 救援への期待（0..1）＝救援の近さ（距離が近いほど高い）× 救援戦力。
        /// reliefForceDistance は 0..1（0=隣接で即着・1=遠方で間に合わない）として近さ=1-距離。
        /// 戦力 0 や遠すぎ（距離1）なら期待0。全入力 0..1 にクランプ。
        /// </summary>
        public static float ReliefExpectation(float reliefForceDistance, float reliefForceStrength, GarrisonDefenseParams p)
        {
            float nearness = 1f - Mathf.Clamp01(reliefForceDistance);
            float strength = Mathf.Clamp01(reliefForceStrength);
            return Mathf.Clamp01(nearness * strength);
        }

        public static float ReliefExpectation(float reliefForceDistance, float reliefForceStrength)
            => ReliefExpectation(reliefForceDistance, reliefForceStrength, GarrisonDefenseParams.Default);

        /// <summary>
        /// 降伏圧力（0..1）＝消耗・補給枯渇・絶望の加重平均（重みは Params・既定で和=1.0）。
        /// 痩せて兵糧が尽き援軍の望みが絶たれるほど高い。全入力 0..1 にクランプ。
        /// </summary>
        public static float SurrenderPressure(float attritionUnderSiege, float lowSupplies, float hopelessness,
                                              GarrisonDefenseParams p)
        {
            float attrition = Mathf.Clamp01(attritionUnderSiege);
            float lowSup = Mathf.Clamp01(lowSupplies);
            float hopeless = Mathf.Clamp01(hopelessness);
            float pressure = attrition * p.attritionPressureWeight
                           + lowSup * p.supplyPressureWeight
                           + hopeless * p.hopelessPressureWeight;
            return Mathf.Clamp01(pressure);
        }

        public static float SurrenderPressure(float attritionUnderSiege, float lowSupplies, float hopelessness)
            => SurrenderPressure(attritionUnderSiege, lowSupplies, hopelessness, GarrisonDefenseParams.Default);

        /// <summary>
        /// 玉砕の決意（0..1）＝籠城士気と指揮官の意志の合成（意志を重み lastStandResolveWeight で混ぜる）。
        /// 士気が崩れても不屈の将が引っ張れば最後の抵抗へ転ぶ。全入力 0..1 にクランプ。
        /// </summary>
        public static float LastStandResolve(float siegeMorale, float leadershipResolve, GarrisonDefenseParams p)
        {
            float morale = Mathf.Clamp01(siegeMorale);
            float resolve = Mathf.Clamp01(leadershipResolve);
            return Mathf.Clamp01(Mathf.Lerp(morale, resolve, p.lastStandResolveWeight));
        }

        public static float LastStandResolve(float siegeMorale, float leadershipResolve)
            => LastStandResolve(siegeMorale, leadershipResolve, GarrisonDefenseParams.Default);

        /// <summary>
        /// 持ちこたえる期間（≧0・日数）＝防御力 × 残存補給 × 籠城士気 の合成に durationScale を掛ける。
        /// 補給か士気が 0 なら 0 日（即落城）。士気・補給は 0..1、防御力は負をクランプ。
        /// </summary>
        public static float DefenseDuration(float defensiveStrength, float suppliesRemaining, float siegeMorale,
                                            GarrisonDefenseParams p)
        {
            float defense = Mathf.Max(0f, defensiveStrength);
            float supplies = Mathf.Clamp01(suppliesRemaining);
            float morale = Mathf.Clamp01(siegeMorale);
            return Mathf.Max(0f, Mathf.Sqrt(defense) * supplies * morale * p.durationScale);
        }

        public static float DefenseDuration(float defensiveStrength, float suppliesRemaining, float siegeMorale)
            => DefenseDuration(defensiveStrength, suppliesRemaining, siegeMorale, GarrisonDefenseParams.Default);

        /// <summary>
        /// 守りきる意志があるか＝籠城士気が降伏圧力を threshold ぶん上回るか（bool）。
        /// 士気 ≥ 圧力 + 閾値 なら踏みとどまる。入力は 0..1、threshold は負をクランプ。
        /// </summary>
        public static bool WillHold(float siegeMorale, float surrenderPressure, float threshold)
        {
            float morale = Mathf.Clamp01(siegeMorale);
            float pressure = Mathf.Clamp01(surrenderPressure);
            float margin = Mathf.Max(0f, threshold);
            return morale >= pressure + margin;
        }

        /// <summary>既定閾値（0.0＝士気が圧力以上なら守りきる）で <see cref="WillHold"/> を委譲。</summary>
        public static bool WillHold(float siegeMorale, float surrenderPressure)
            => WillHold(siegeMorale, surrenderPressure, 0f);
    }
}
