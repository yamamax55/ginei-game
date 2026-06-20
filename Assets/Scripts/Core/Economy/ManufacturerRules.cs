using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// メーカー（製造業）のロジック（#2016・純ロジック・唯一の窓口）。汎用企業（#1022 労働/資本で産出）に製造変換を足す：
    /// 原材料と製造＝材料がボトルネック（MFG-1）／歩留まり・品質＝不良品の損失（MFG-2）／研究開発→製品力（MFG-3）／
    /// ブランド→価格プレミアム（MFG-4）／経験曲線・利潤＝作るほど単価が下がる（MFG-5）。原材料は資源（#92/#178）、製品は
    /// 市場（#179）・建艦（#884）へ接続（read-only/接続のみ）。マクロ近似（工場ラインの micro は持たない）。test-first。
    /// </summary>
    public static class ManufacturerRules
    {
        /// <summary>既定の歩留まり。</summary>
        public const float DefaultYieldRate = 0.9f;

        /// <summary>経験曲線の学習率（累積生産が倍増するごとに単価が乗じる係数＝90%曲線）。</summary>
        public const float DefaultLearningRate = 0.9f;

        /// <summary>ブランドの価格プレミアム上限（ブランド力1.0で何割増しまで）。</summary>
        public const float DefaultBrandPremiumMax = 0.5f;

        // ===== MFG-1 原材料と製造（投入産出） =====

        /// <summary>原材料で作れる製品数の上限＝原材料/1単位投入量（材料がボトルネック）。投入量0以下は0。</summary>
        public static float ProducibleUnits(float rawMaterials, float unitMaterialInput)
            => unitMaterialInput <= 0f ? 0f : Mathf.Max(0f, rawMaterials) / unitMaterialInput;

        /// <summary>実際の製造数＝目標生産と原材料上限の小さい方（材料が足りなければ作れない）。</summary>
        public static float ManufacturedOutput(float targetOutput, float rawMaterials, float unitMaterialInput)
            => Mathf.Min(Mathf.Max(0f, targetOutput), ProducibleUnits(rawMaterials, unitMaterialInput));

        /// <summary>原材料費＝製造数×1単位原材料費。</summary>
        public static float MaterialCost(float units, float unitMaterialCost)
            => Mathf.Max(0f, units) * Mathf.Max(0f, unitMaterialCost);

        // ===== MFG-2 歩留まり・品質 =====

        /// <summary>良品数＝総産出×歩留まり（売れるのは良品だけ）。</summary>
        public static float GoodUnits(float grossOutput, float yieldRate)
            => Mathf.Max(0f, grossOutput) * Mathf.Clamp01(yieldRate);

        /// <summary>不良品数＝総産出×(1−歩留まり)。</summary>
        public static float DefectUnits(float grossOutput, float yieldRate)
            => Mathf.Max(0f, grossOutput) * (1f - Mathf.Clamp01(yieldRate));

        /// <summary>不良品の損失＝不良品数×1単位原価（作ったが売れず原価ぶん損）。</summary>
        public static float DefectLossCost(float defectUnits, float unitCost)
            => Mathf.Max(0f, defectUnits) * Mathf.Max(0f, unitCost);

        // ===== MFG-3 研究開発→製品力 =====

        /// <summary>R&Dによる生産性係数＝1＋研究開発水準×1段あたり寄与（R&Dで生産性・製品力が上がる）。</summary>
        public static float RdProductivityFactor(float rdLevel, float gainPerLevel)
            => 1f + Mathf.Max(0f, rdLevel) * Mathf.Max(0f, gainPerLevel);

        /// <summary>R&D投資で歩留まりを改善＝min(上限, 歩留まり＋投資×改善率)（基準フィールドは非破壊＝新値を返す）。</summary>
        public static float ImproveYield(float yieldRate, float rdInvestment, float improveRatePerSpend, float maxYield)
            => Mathf.Min(Mathf.Clamp01(maxYield), Mathf.Clamp01(yieldRate) + Mathf.Max(0f, rdInvestment) * Mathf.Max(0f, improveRatePerSpend));

        // ===== MFG-4 ブランド→価格プレミアム =====

        /// <summary>ブランドの価格プレミアム率＝ブランド力×上限（強いブランドほど高く売れる）。</summary>
        public static float BrandPremium(float brandStrength, float maxPremium)
            => Mathf.Clamp01(brandStrength) * Mathf.Max(0f, maxPremium);

        /// <summary>ブランド価格＝基準価格×(1＋ブランドプレミアム)。</summary>
        public static float BrandedPrice(float basePrice, float brandStrength, float maxPremium)
            => Mathf.Max(0f, basePrice) * (1f + BrandPremium(brandStrength, maxPremium));

        // ===== MFG-5 経験曲線・利潤 =====

        /// <summary>
        /// 経験曲線の単価＝基準単価×学習率^(累積/基準の対数2)（累積生産が倍増するごとに学習率を乗じる＝作るほど安くなる）。
        /// 累積が基準未満は基準単価（負の学習はしない）。基準・学習率不正は基準単価。
        /// </summary>
        public static float LearningCurveUnitCost(float baseUnitCost, float cumulativeOutput, float referenceOutput, float learningRate)
        {
            float b = Mathf.Max(0f, baseUnitCost);
            if (referenceOutput <= 0f || learningRate <= 0f) return b;
            float ratio = Mathf.Max(1f, cumulativeOutput / referenceOutput);
            float doublings = Mathf.Log(ratio) / Mathf.Log(2f);
            return b * Mathf.Pow(learningRate, doublings);
        }

        /// <summary>1単位の利潤＝価格−単価（負もありうる）。</summary>
        public static float UnitProfit(float price, float unitCost) => price - unitCost;

        /// <summary>製造利潤＝良品数×(価格−単価)（売れる良品ぶんだけ稼ぐ）。</summary>
        public static float ManufacturingProfit(float goodUnits, float price, float unitCost)
            => Mathf.Max(0f, goodUnits) * UnitProfit(price, unitCost);
    }
}
