using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 海運会社のロジック（東証33業種「海運業」・#2024・純ロジック・唯一の窓口）。運賃市況の乱高下で稼ぐ：運賃市況＝貨物需要/
    /// 船腹供給で決まる（SHP-1）／船腹過剰＝供給過剰で運賃暴落（SHP-2）／航海採算＝運賃−燃料−用船料（SHP-3）／戦時リスク割増＝
    /// 通商破壊で運賃/保険が跳ねる（SHP-4・#94/#95・海上保険 #1982）。市場（#179）・通商破壊（#94/#95）へ接続（read-only/接続のみ）。
    /// マクロ近似（個船 micro は持たない）。test-first。
    /// </summary>
    public static class ShippingRules
    {
        // ===== SHP-1 運賃市況 =====

        /// <summary>運賃率＝基準運賃×(貨物需要/船腹供給)（需要超過＝逼迫で運賃高騰、供給超過＝暴落＝バルチック指数型の乱高下）。供給0以下は超高。</summary>
        public static float FreightRate(float cargoDemand, float fleetSupply, float baseRate)
            => fleetSupply <= 0f ? 999999f : Mathf.Max(0f, baseRate) * (Mathf.Max(0f, cargoDemand) / fleetSupply);

        // ===== SHP-2 船腹過剰 =====

        /// <summary>船腹過剰率＝(船腹供給−貨物需要)/貨物需要（プラスは供給過剰＝運賃暴落の元凶）。需要0以下は0。</summary>
        public static float OvercapacityRatio(float fleetSupply, float cargoDemand)
            => cargoDemand <= 0f ? 0f : Mathf.Max(0f, fleetSupply - cargoDemand) / cargoDemand;

        // ===== SHP-3 航海採算 =====

        /// <summary>航海利益＝運賃収入−燃料費−用船料（運賃市況が燃料/用船を上回れば黒字）。</summary>
        public static float VoyageProfit(float freightRevenue, float fuelCost, float charterCost)
            => freightRevenue - Mathf.Max(0f, fuelCost) - Mathf.Max(0f, charterCost);

        // ===== SHP-4 戦時リスク割増 =====

        /// <summary>戦時リスク割増＝基準運賃×襲撃確率×割増係数（通商破壊 #94/#95 のリスクで運賃・海上保険 #1982 が跳ねる）。</summary>
        public static float WarRiskSurcharge(float baseRate, float raidProbability, float surchargeFactor)
            => Mathf.Max(0f, baseRate) * Mathf.Clamp01(raidProbability) * Mathf.Max(0f, surchargeFactor);
    }
}
