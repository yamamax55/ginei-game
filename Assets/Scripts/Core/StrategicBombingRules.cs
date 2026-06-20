using UnityEngine;

namespace Ginei
{
    /// <summary>戦略爆撃の調整係数。</summary>
    public readonly struct StrategicBombingParams
    {
        /// <summary>防空が爆撃到達度を削る強さ（大きいほど防空が効く）。</summary>
        public readonly float airDefenseWeight;
        /// <summary>無差別爆撃が敵の結束を固める逆効果の強さ（0..1）。</summary>
        public readonly float hardeningStrength;
        /// <summary>爆撃側の損耗が防空に比例する強さ（0..1）。</summary>
        public readonly float attritionWeight;
        /// <summary>持続爆撃の累積効果が頭打ちになるまでの時間（飽和時定数・正）。</summary>
        public readonly float cumulativeTimeConstant;

        public StrategicBombingParams(float airDefenseWeight, float hardeningStrength,
            float attritionWeight, float cumulativeTimeConstant)
        {
            this.airDefenseWeight = Mathf.Max(0f, airDefenseWeight);
            this.hardeningStrength = Mathf.Clamp01(hardeningStrength);
            this.attritionWeight = Mathf.Clamp01(attritionWeight);
            this.cumulativeTimeConstant = Mathf.Max(0.01f, cumulativeTimeConstant);
        }

        /// <summary>既定＝防空重み1.0・結束硬化0.5・損耗重み0.3・累積時定数10。</summary>
        public static StrategicBombingParams Default => new StrategicBombingParams(1f, 0.5f, 0.3f, 10f);
    }

    /// <summary>
    /// 戦略爆撃の純ロジック。前線でなく敵の後方の生産拠点・インフラ・人口中枢を叩いて、
    /// 戦争遂行能力と戦意を削ぐ＝<see cref="DeepStrikeRules"/>（縦深打撃＝前線後方の作戦目標を叩く）とも
    /// <see cref="InterdictionRules"/>（補給線遮断）とも別の系統。爆撃の効果は防空で薄れ、産業の分散・冗長で
    /// 抗われ、国民の不屈で和らぎ、無差別なら逆に敵の結束を固める（モラル・ボンディングの逆効果）。
    /// 倍率・度合いはすべて 0..1 等に正規化した plain 引数で、盤面非依存・乱数なし・決定論。純ロジック（test-first）。
    /// </summary>
    public static class StrategicBombingRules
    {
        /// <summary>
        /// 防空を抜いて爆撃できる度合い（0..1）。爆撃機戦力 bomberStrength(0..1) が敵防空 enemyAirDefense(0..1)を
        /// 重みぶん差し引いた残り＝reach。防空が爆撃戦力を重み超で上回れば 0（突破できない）。
        /// </summary>
        public static float BombingReach(float bomberStrength, float enemyAirDefense, StrategicBombingParams p)
        {
            float bomb = Mathf.Clamp01(bomberStrength);
            float defense = Mathf.Clamp01(enemyAirDefense) * p.airDefenseWeight;
            return Mathf.Clamp01(bomb - defense);
        }

        public static float BombingReach(float bomberStrength, float enemyAirDefense)
            => BombingReach(bomberStrength, enemyAirDefense, StrategicBombingParams.Default);

        /// <summary>
        /// 生産基盤への損害（0..1）＝到達度 bombingReach × 目標の集中度 targetConcentration。
        /// 産業が一点に集中しているほど一撃が効く（分散していれば爆撃は薄まる）。
        /// </summary>
        public static float IndustrialDamage(float bombingReach, float targetConcentration)
        {
            return Mathf.Clamp01(bombingReach) * Mathf.Clamp01(targetConcentration);
        }

        /// <summary>
        /// 敵の生産力低下（0..1）＝産業損害 industrialDamage を冗長性 industryRedundancy(0..1)が打ち消す。
        /// 冗長・分散した産業（地下化・予備ライン）ほど損害が生産低下に直結しない。
        /// </summary>
        public static float ProductionReduction(float industrialDamage, float industryRedundancy)
        {
            float damage = Mathf.Clamp01(industrialDamage);
            float redundancy = Mathf.Clamp01(industryRedundancy);
            return Mathf.Clamp01(damage * (1f - redundancy));
        }

        /// <summary>
        /// 国民の戦意への影響（0..1）＝到達度 bombingReach を国民の不屈 civilianResilience(0..1)が和らげる。
        /// 不屈な国民ほど爆撃に屈しない（戦意が折れない）。
        /// </summary>
        public static float MoraleEffect(float bombingReach, float civilianResilience)
        {
            float reach = Mathf.Clamp01(bombingReach);
            float resilience = Mathf.Clamp01(civilianResilience);
            return Mathf.Clamp01(reach * (1f - resilience));
        }

        /// <summary>
        /// 無差別爆撃が敵の結束を固める逆効果（0..1）。無差別度 indiscriminateness(0..1)が高いほど
        /// 敵の抗戦意志を結束させる＝hardeningStrength を上限に増える。戦意を削ぐ MoraleEffect と相殺する向き。
        /// </summary>
        public static float ResolveHardening(float indiscriminateness, StrategicBombingParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(indiscriminateness) * p.hardeningStrength);
        }

        public static float ResolveHardening(float indiscriminateness)
            => ResolveHardening(indiscriminateness, StrategicBombingParams.Default);

        /// <summary>
        /// 爆撃側の損耗（0..1）＝爆撃機戦力 bomberStrength を敵防空 enemyAirDefense が削る度合い。
        /// 出撃規模が大きく防空が厚いほど損耗する＝深く突くほど代償を払う。
        /// </summary>
        public static float BomberAttrition(float bomberStrength, float enemyAirDefense, StrategicBombingParams p)
        {
            float bomb = Mathf.Clamp01(bomberStrength);
            float defense = Mathf.Clamp01(enemyAirDefense);
            return Mathf.Clamp01(bomb * defense * p.attritionWeight);
        }

        public static float BomberAttrition(float bomberStrength, float enemyAirDefense)
            => BomberAttrition(bomberStrength, enemyAirDefense, StrategicBombingParams.Default);

        /// <summary>
        /// 持続爆撃で敵の戦争遂行能力が累積的に削れる度合い（0..1）。瞬間の生産低下 productionReduction を
        /// 持続時間 duration ぶん積み上げる＝飽和曲線 d/(d+τ) で頭打ち（τ=cumulativeTimeConstant）。
        /// 短期では薄いが、叩き続ければ着実に効く（Mathf.Exp 不可のため有理飽和で近似）。
        /// </summary>
        public static float CumulativeWarEffort(float productionReduction, float duration, StrategicBombingParams p)
        {
            float reduction = Mathf.Clamp01(productionReduction);
            float d = Mathf.Max(0f, duration);
            float saturation = d / (d + p.cumulativeTimeConstant); // 0..1、duration→∞ で1へ
            return Mathf.Clamp01(reduction * saturation);
        }

        public static float CumulativeWarEffort(float productionReduction, float duration)
            => CumulativeWarEffort(productionReduction, duration, StrategicBombingParams.Default);

        /// <summary>敵の戦争経済を麻痺させたか＝生産低下 productionReduction が閾値 threshold 以上で true。</summary>
        public static bool IsWarEconomyCrippled(float productionReduction, float threshold)
        {
            return Mathf.Clamp01(productionReduction) >= Mathf.Clamp01(threshold);
        }
    }
}
