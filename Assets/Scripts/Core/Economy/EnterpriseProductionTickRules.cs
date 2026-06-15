namespace Ginei
{
    /// <summary>企業の生産結果（FIRMPROD-6・#2084）。実産出＋稼働率＋ボトルネック投入。</summary>
    public struct ProductionResult
    {
        public float plannedOutput;     // 計画産出
        public float realizedOutput;    // 実産出（投入制約後）
        public float utilization;       // 稼働率（実/計画）
        public bool inputConstrained;   // 投入不足で減産したか
        public ProductionInput binding; // ボトルネックの投入（inputConstrained のとき有効）
    }

    /// <summary>
    /// 企業の投入制約つき生産の暦境界オーケストレータ（FIRMPROD-6・#2084 配線・純ロジック）。
    /// 計画産出→投入制約→実産出＋稼働率＋ボトルネックを <see cref="ProductionResult"/> で返す薄い窓口。
    /// 投入の消費は <see cref="Consume"/> で在庫から引く（原材料→物資／エネルギー→燃料 にマップ）。test-first。
    /// </summary>
    public static class EnterpriseProductionTickRules
    {
        /// <summary>計画産出から投入制約つきの生産結果を計算（資本財は呼び側が可用量を渡す＝既定は潤沢）。</summary>
        public static ProductionResult Produce(float plannedOutput, float availMaterials, float availEnergy, float availCapital)
        {
            float realized = EnterpriseProductionRules.RealizedOutput(plannedOutput, availMaterials, availEnergy, availCapital);
            var binding = ProductionConstraintRules.BindingInput(plannedOutput, availMaterials, availEnergy, availCapital, out bool constrained);
            return new ProductionResult
            {
                plannedOutput = plannedOutput,
                realizedOutput = realized,
                utilization = EnterpriseProductionRules.CapacityUtilization(realized, plannedOutput),
                inputConstrained = constrained,
                binding = binding,
            };
        }

        /// <summary>企業（<see cref="Enterprise"/>）の計画産出を使った生産結果。</summary>
        public static ProductionResult Produce(Enterprise e, float availMaterials, float availEnergy, float availCapital)
            => Produce(EnterpriseRules.Output(e), availMaterials, availEnergy, availCapital);

        /// <summary>
        /// 実産出ぶんの物的投入を在庫から消費（原材料→物資／エネルギー→燃料 にマップ）。資本財は在庫管理外。
        /// </summary>
        public static void Consume(ResourceStockpile stock, float realizedOutput)
        {
            if (stock == null) return;
            stock.Add(ResourceType.物資, -EnterpriseProductionRules.InputConsumed(realizedOutput, ProductionInput.原材料));
            stock.Add(ResourceType.燃料, -EnterpriseProductionRules.InputConsumed(realizedOutput, ProductionInput.エネルギー));
        }
    }
}
