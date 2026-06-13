using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 作戦様式（侵攻＝攻勢／保持＝防御／撤退）。作戦様式によって補給消費が大きく違う＝
    /// 侵攻は前進・展開・弾薬消費が激しく補給を大量に食い、保持は塹壕に拠って中程度、
    /// 撤退は戦線を畳んで少ない。
    /// </summary>
    public enum SupplyMode
    {
        /// <summary>侵攻＝攻勢。前進・展開・弾薬消費が激しく補給を大量に食う（消費最大）。</summary>
        侵攻,
        /// <summary>保持＝防御。塹壕・備蓄に拠って消費は中程度（守る側の兵站優位）。</summary>
        保持,
        /// <summary>撤退。戦線を畳んで消費が少ない（整然とした撤退は物資放棄を避ける）。</summary>
        撤退,
    }

    /// <summary>作戦様式別補給消費の調整係数。</summary>
    public readonly struct SupplyModeParams
    {
        /// <summary>侵攻（攻勢）の消費倍率（前進・展開・弾薬消費＝大量）。</summary>
        public readonly float offensiveMultiplier;
        /// <summary>保持（防御）の消費倍率（塹壕で中程度＝基準）。</summary>
        public readonly float defensiveMultiplier;
        /// <summary>撤退の消費倍率（戦線を畳んで少ない）。</summary>
        public readonly float retreatMultiplier;
        /// <summary>攻勢の前進深度が補給負担を増幅する重み（0..1・深く前進するほど補給線が伸びて負担増）。</summary>
        public readonly float advanceDepthWeight;
        /// <summary>防御の塹壕化が補給を節約できる最大割合（0..1・備蓄・拠点でどれだけ消費を抑えるか）。</summary>
        public readonly float entrenchmentSavingMax;
        /// <summary>撤退の整然さが消費を抑える最大割合（0..1・秩序ある撤退ほど物資放棄が少ない）。</summary>
        public readonly float withdrawalSavingMax;

        public SupplyModeParams(float offensiveMultiplier, float defensiveMultiplier, float retreatMultiplier,
            float advanceDepthWeight, float entrenchmentSavingMax, float withdrawalSavingMax)
        {
            this.offensiveMultiplier = Mathf.Max(0f, offensiveMultiplier);
            this.defensiveMultiplier = Mathf.Max(0f, defensiveMultiplier);
            this.retreatMultiplier = Mathf.Max(0f, retreatMultiplier);
            this.advanceDepthWeight = Mathf.Clamp01(advanceDepthWeight);
            this.entrenchmentSavingMax = Mathf.Clamp01(entrenchmentSavingMax);
            this.withdrawalSavingMax = Mathf.Clamp01(withdrawalSavingMax);
        }

        /// <summary>既定＝侵攻×2.0・保持×1.0・撤退×0.5・前進深度重み0.5・塹壕節約上限0.4・撤退節約上限0.5。</summary>
        public static SupplyModeParams Default =>
            new SupplyModeParams(2.0f, 1.0f, 0.5f, 0.5f, 0.4f, 0.5f);
    }

    /// <summary>
    /// 作戦様式別補給消費の純ロジック（CRV-3 #1366・兵站）。作戦様式によって補給消費が大きく違う＝
    /// 侵攻（攻勢）は前進・展開・弾薬消費が激しく補給を大量に食い（×2.0）、保持（防御）は塹壕に拠って
    /// 中程度（×1.0）、撤退は戦線を畳んで少ない（×0.5）＝攻める側は守る側の何倍も兵站を要する。
    /// 「侵攻は補給を大量に食い保持は中程度・撤退は少ない＝攻める側は守る側の何倍も兵站を要する
    /// 作戦様式別の非対称」を式に出す。
    /// <see cref="SupplyRules"/>（補給線が回廊で繋がるか＝面の到達・ZOC遮断）とは別＝こちらは作戦様式別の消費倍率。
    /// <see cref="LogisticsRules"/>（所有星系の連結成分から版図の一体化度を出す＝国力割引）とも別＝
    /// こちらは作戦様式（攻勢/防御）による補給消費の非対称。
    /// <see cref="HomelandResistanceRules"/>（侵攻深度による補給線負担）とは OffensiveBurden で整合させる
    /// （攻勢は前進が深いほど補給負担が増す）。MobilizationRules（戦時動員＝軍需/民需の生産倍率＝供給側）
    /// とも別＝こちらは前線の作戦様式による消費側。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SupplyModeRules
    {
        /// <summary>
        /// 作戦様式ごとの補給消費倍率（侵攻×2.0・保持×1.0・撤退×0.5＝既定）。攻める側は守る側の
        /// 何倍も兵站を要する非対称をそのまま倍率で表す。
        /// </summary>
        public static float ConsumptionMultiplier(SupplyMode mode, SupplyModeParams p)
        {
            switch (mode)
            {
                case SupplyMode.侵攻: return p.offensiveMultiplier;
                case SupplyMode.保持: return p.defensiveMultiplier;
                case SupplyMode.撤退: return p.retreatMultiplier;
                default: return p.defensiveMultiplier;
            }
        }

        public static float ConsumptionMultiplier(SupplyMode mode)
            => ConsumptionMultiplier(mode, SupplyModeParams.Default);

        /// <summary>
        /// 実際の補給消費（0..1）。基礎消費（0..1）×様式倍率×作戦強度（0..1）＝作戦様式と強度で実消費が決まる。
        /// 同じ部隊でも侵攻なら2倍、撤退なら半分の補給を食う。強度0なら静止＝消費0。
        /// </summary>
        public static float SupplyConsumption(float baseConsumption, SupplyMode mode, float intensity,
            SupplyModeParams p)
        {
            float baseC = Mathf.Clamp01(baseConsumption);
            float inten = Mathf.Clamp01(intensity);
            float mult = ConsumptionMultiplier(mode, p);
            return Mathf.Clamp01(baseC * mult * inten);
        }

        public static float SupplyConsumption(float baseConsumption, SupplyMode mode, float intensity)
            => SupplyConsumption(baseConsumption, mode, intensity, SupplyModeParams.Default);

        /// <summary>
        /// 攻勢の補給負担（0..1）。攻勢は前進が深いほど補給負担が増す＝部隊規模（0..1）に、前進深度
        /// （0..1）が（1＋深度×前進深度重み）で乗算して増幅＝侵攻倍率を掛ける。深く前進するほど補給線が
        /// 伸びて兵站が重くなる（HomelandResistanceRules.SupplyLineStrain と整合）。
        /// </summary>
        public static float OffensiveBurden(float forceSize, float advanceDepth, SupplyModeParams p)
        {
            float force = Mathf.Clamp01(forceSize);
            float depth = Mathf.Clamp01(advanceDepth);
            float amplified = force * (1f + depth * p.advanceDepthWeight);
            // 攻勢様式の重さ（侵攻倍率を基準の保持倍率で割って正規化）を掛けて攻める側の非対称を出す
            float offensiveWeight = p.defensiveMultiplier > 0.0001f
                ? p.offensiveMultiplier / p.defensiveMultiplier
                : p.offensiveMultiplier;
            return Mathf.Clamp01(amplified * offensiveWeight * 0.5f);
        }

        public static float OffensiveBurden(float forceSize, float advanceDepth)
            => OffensiveBurden(forceSize, advanceDepth, SupplyModeParams.Default);

        /// <summary>
        /// 防御の補給節約後倍率（0..保持倍率）。防御（保持）は塹壕・備蓄で補給を節約できる＝守る側の兵站優位。
        /// 保持倍率に（1−塹壕化×塹壕節約上限）を掛けて消費を抑える。塹壕化が高いほど安く守れる。
        /// 様式が保持でなければ節約は効かない（その様式の素の倍率を返す）。
        /// </summary>
        public static float DefensiveEconomy(SupplyMode mode, float entrenchment, SupplyModeParams p)
        {
            float baseMult = ConsumptionMultiplier(mode, p);
            if (mode != SupplyMode.保持) return baseMult;
            float ent = Mathf.Clamp01(entrenchment);
            return Mathf.Max(0f, baseMult * (1f - ent * p.entrenchmentSavingMax));
        }

        public static float DefensiveEconomy(SupplyMode mode, float entrenchment)
            => DefensiveEconomy(mode, entrenchment, SupplyModeParams.Default);

        /// <summary>
        /// 撤退の補給節約後倍率（0..撤退倍率）。整然とした撤退は消費を抑える＝撤退倍率に
        /// （1−撤退秩序×撤退節約上限）を掛ける。秩序ある撤退ほど物資放棄が少なく安く下がれる
        /// （潰走は別＝物資放棄で逆に高くつくが、ここは整然撤退の節約のみ扱う）。
        /// 様式が撤退でなければ節約は効かない（その様式の素の倍率を返す）。
        /// </summary>
        public static float RetreatSavings(SupplyMode mode, float withdrawalOrder, SupplyModeParams p)
        {
            float baseMult = ConsumptionMultiplier(mode, p);
            if (mode != SupplyMode.撤退) return baseMult;
            float order = Mathf.Clamp01(withdrawalOrder);
            return Mathf.Max(0f, baseMult * (1f - order * p.withdrawalSavingMax));
        }

        public static float RetreatSavings(SupplyMode mode, float withdrawalOrder)
            => RetreatSavings(mode, withdrawalOrder, SupplyModeParams.Default);

        /// <summary>
        /// 攻撃側と防御側の補給消費比（攻める側が守る側の何倍を要するか＝兵站の非対称）。
        /// 攻撃側倍率÷防御側倍率。既定（侵攻×2.0 vs 保持×1.0）なら2.0＝攻める側は守る側の倍の兵站を要する。
        /// 防御側倍率が0なら攻撃側倍率をそのまま返す（ゼロ割回避）。
        /// </summary>
        public static float AttackerDefenderRatio(SupplyMode attackerMode, SupplyMode defenderMode,
            SupplyModeParams p)
        {
            float atk = ConsumptionMultiplier(attackerMode, p);
            float def = ConsumptionMultiplier(defenderMode, p);
            if (def <= 0.0001f) return atk;
            return atk / def;
        }

        public static float AttackerDefenderRatio(SupplyMode attackerMode, SupplyMode defenderMode)
            => AttackerDefenderRatio(attackerMode, defenderMode, SupplyModeParams.Default);

        /// <summary>
        /// 様式ごとに備蓄でどれだけ作戦を継続できるか（0..1＝備蓄/様式倍率を正規化した継続性）。
        /// 同じ備蓄（0..1）でも侵攻は早く尽き（÷2.0）、撤退は長く保つ（÷0.5）＝攻勢は早く尽きる。
        /// 様式倍率で備蓄を割ることで「消費が重い様式ほど継続が短い」を表す。
        /// </summary>
        public static float SustainabilityByMode(SupplyMode mode, float supplyStock, SupplyModeParams p)
        {
            float stock = Mathf.Clamp01(supplyStock);
            float mult = ConsumptionMultiplier(mode, p);
            if (mult <= 0.0001f) return 1f; // 消費しない様式は尽きない
            return Mathf.Clamp01(stock / mult);
        }

        public static float SustainabilityByMode(SupplyMode mode, float supplyStock)
            => SustainabilityByMode(mode, supplyStock, SupplyModeParams.Default);

        /// <summary>
        /// 補給消費が供給能力を超えて兵站が破綻したか＝補給消費（0..1）が供給能力（0..1）×閾値を
        /// 上回ったら true。攻勢で消費が膨らみ供給が追いつかないと攻勢終末点で兵站が破綻する。
        /// 閾値1.0なら供給能力ちょうどで破綻、1未満なら余裕を見て早めに破綻判定。
        /// </summary>
        public static bool IsLogisticallyOverextended(float supplyConsumption, float supplyCapacity,
            float threshold = 1.0f)
        {
            float consumption = Mathf.Clamp01(supplyConsumption);
            float capacity = Mathf.Clamp01(supplyCapacity);
            float thr = Mathf.Max(0f, threshold);
            return consumption > capacity * thr;
        }
    }
}
