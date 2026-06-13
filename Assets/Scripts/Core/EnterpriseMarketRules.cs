using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 企業の実産出を市場へ繋ぐ＝投入コストと営業利潤（FIRMPROD-5・#2084・純ロジック）。
    /// 投入価格の上昇が利潤を圧迫。産出は市場#179 へ供給し、価格本体は `MarketRules.ClearingPrice`#179 が決める（委譲）。test-first。
    /// </summary>
    public static class EnterpriseMarketRules
    {
        /// <summary>投入コスト＝Σ 係数×価格×実産出（物的3投入＝原材料/エネルギー/資本財）。</summary>
        public static float InputCost(float realizedOutput, float priceMaterials, float priceEnergy, float priceCapital)
        {
            float o = Mathf.Max(0f, realizedOutput);
            return o * (EnterpriseInputRules.InputCoefficient(ProductionInput.原材料) * Mathf.Max(0f, priceMaterials)
                      + EnterpriseInputRules.InputCoefficient(ProductionInput.エネルギー) * Mathf.Max(0f, priceEnergy)
                      + EnterpriseInputRules.InputCoefficient(ProductionInput.資本財) * Mathf.Max(0f, priceCapital));
        }

        /// <summary>売上＝実産出×出荷価格。</summary>
        public static float Revenue(float realizedOutput, float outputPrice)
            => Mathf.Max(0f, realizedOutput) * Mathf.Max(0f, outputPrice);

        /// <summary>営業利潤＝売上−投入コスト−賃金総額（投入高で利潤が圧迫される）。</summary>
        public static float OperatingProfit(float realizedOutput, float outputPrice, float priceMaterials, float priceEnergy, float priceCapital, float wageBill)
            => Revenue(realizedOutput, outputPrice) - InputCost(realizedOutput, priceMaterials, priceEnergy, priceCapital) - Mathf.Max(0f, wageBill);

        /// <summary>市場#179 への供給量＝実産出（価格は `MarketRules.ClearingPrice` が決める）。</summary>
        public static float MarketSupply(float realizedOutput)
            => Mathf.Max(0f, realizedOutput);
    }
}
