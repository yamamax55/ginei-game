using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ドラッグストアのロジック（業種細分化・小売 #2017 の調剤併設サブ業種・#2025・純ロジック・唯一の窓口）：調剤収益＝処方箋枚数×技術料（DRG-1）／
    /// PB/NB混合の粗利（DRG-2）／食品を安く売る集客（ロスリーダー・DRG-3）／利益（DRG-4）。
    /// 高粗利の医薬・化粧品（化学#2024）で稼ぎ、低粗利の食品で客を呼ぶ二層構造＝小売#2017の業態特化。マクロ近似。test-first。
    /// </summary>
    public static class DrugstoreRules
    {
        /// <summary>調剤収益＝処方箋枚数×調剤技術料（公定の調剤報酬＝薬以外の技術料が利益源）。</summary>
        public static float DispensingRevenue(int prescriptions, float feePerPrescription)
            => Mathf.Max(0, prescriptions) * Mathf.Max(0f, feePerPrescription);

        /// <summary>混合粗利＝売上×(PB比率×PB粗利率+(1−PB比率)×NB粗利率)（PB＝自主企画は高粗利）。</summary>
        public static float BlendedGrossProfit(float sales, float pbShare, float pbMargin, float nbMargin)
        {
            float pb = Mathf.Clamp01(pbShare);
            return Mathf.Max(0f, sales) * (pb * Mathf.Clamp01(pbMargin) + (1f - pb) * Mathf.Clamp01(nbMargin));
        }

        /// <summary>食品ロスリーダー集客＝基礎来店×(1+値引き深度×集客感応度)（食品を安く売り来店を増やす）。</summary>
        public static float FoodLossLeaderTraffic(float baseTraffic, float foodDiscountDepth, float trafficSensitivity)
            => Mathf.Max(0f, baseTraffic) * (1f + Mathf.Max(0f, foodDiscountDepth) * Mathf.Max(0f, trafficSensitivity));

        /// <summary>ドラッグストア利益＝粗利−固定費。</summary>
        public static float DrugstoreProfit(float grossProfit, float fixedCost)
            => grossProfit - Mathf.Max(0f, fixedCost);
    }
}
