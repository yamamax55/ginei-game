using UnityEngine;

namespace Ginei
{
    /// <summary>調達様式（供給契約の組み方）。</summary>
    public enum ProcurementMode
    {
        /// <summary>長期契約＝価格を固定し安定供給を得るが柔軟性を失う。</summary>
        長期契約,
        /// <summary>スポット＝都度調達。柔軟だが市場の価格変動に晒される。</summary>
        スポット,
        /// <summary>混合＝長期契約とスポットを併用する中庸。</summary>
        混合
    }

    /// <summary>供給契約の調整係数（マジックナンバー禁止＝集約）。</summary>
    public readonly struct SupplyContractParams
    {
        /// <summary>長期契約による価格安定の基礎度（被覆率1.0で価格がどれだけ安定するか）。</summary>
        public readonly float stabilityBase;
        /// <summary>長期契約が生む柔軟性喪失の強さ（被覆率が高いほど需要変動に追従できない）。</summary>
        public readonly float rigidityWeight;
        /// <summary>契約破棄ペナルティの単価（残存期間×価値×これ＝違約金の重み）。</summary>
        public readonly float breachUnitCost;
        /// <summary>破棄が大国相手のとき外交余波を増幅する係数（制裁/戦争へ発展する圧）。</summary>
        public readonly float falloutWeight;

        public SupplyContractParams(float stabilityBase, float rigidityWeight, float breachUnitCost, float falloutWeight)
        {
            this.stabilityBase = Mathf.Clamp01(stabilityBase);
            this.rigidityWeight = Mathf.Clamp01(rigidityWeight);
            this.breachUnitCost = Mathf.Max(0f, breachUnitCost);
            this.falloutWeight = Mathf.Max(0f, falloutWeight);
        }

        /// <summary>既定＝安定基礎0.9・硬直重み0.8・違約単価1.0・外交余波重み1.5。</summary>
        public static SupplyContractParams Default =>
            new SupplyContractParams(0.9f, 0.8f, 1.0f, 1.5f);
    }

    /// <summary>
    /// 供給契約の純ロジック（契約管理 EPIC #1006・DIP-2 接続）。
    /// 「長期契約は安定を買い柔軟性を売る＝破棄は商売を超えて外交になる」を式に出す：長期契約の被覆率が
    /// 高いほど価格は安定するが（<see cref="ContractPriceStability"/>）、契約外の分だけスポット市場の
    /// 変動に晒され（<see cref="SpotExposure"/>）、固定数量ゆえ需要変動へ追従できず余っても買い続ける
    /// 柔軟性喪失を負う（<see cref="FlexibilityLoss"/>）。契約破棄は残存期間×価値の違約金がかかり
    /// （<see cref="BreachPenalty"/>）、相手が大国なら制裁/戦争のリスクとして外交へ波及する
    /// （<see cref="BreachDiplomaticFallout"/>）。需要が安定し価格が荒れるなら長期契約・需要が荒れるなら
    /// スポットが最適となる（<see cref="OptimalContractMix"/>）。
    /// <see cref="TreatyRules"/>（条約一般＝同盟/不可侵/通商の opinion・違約判定）の<b>商業版</b>であり、
    /// 破棄の外交余波は <see cref="DiplomacyRules"/>（#189 外交＝opinion 修正・状態遷移）へ波及させる
    /// 入力値（opinion ペナルティ）を返すにとどめ、状態遷移そのものは委譲する。スポット価格の生成・
    /// 需給均衡は MarketRules（市場の clearing price）が持ち、ここは契約の組み方と破棄の力学のみを扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SupplyContractRules
    {
        /// <summary>
        /// 価格の安定性（0..1）＝長期契約の被覆率が高いほど価格変動に強い。
        /// 被覆率1.0で stabilityBase に到達し、契約のない分はそのまま市場変動に晒される。
        /// </summary>
        public static float ContractPriceStability(float contractCoverage, SupplyContractParams p)
        {
            float cov = Mathf.Clamp01(contractCoverage);
            return cov * p.stabilityBase;
        }

        /// <summary>既定 Params で価格安定性を返す簡易窓口。</summary>
        public static float ContractPriceStability(float contractCoverage) =>
            ContractPriceStability(contractCoverage, SupplyContractParams.Default);

        /// <summary>
        /// スポット価格への曝露（0..1）＝契約していない分（1−被覆率）が市場のボラティリティに晒される。
        /// 全量を長期契約で固めれば曝露0、全量スポットならボラティリティそのもの。
        /// </summary>
        public static float SpotExposure(float contractCoverage, float spotPriceVolatility)
        {
            float uncovered = 1f - Mathf.Clamp01(contractCoverage);
            return uncovered * Mathf.Clamp01(spotPriceVolatility);
        }

        /// <summary>
        /// 柔軟性の喪失（0..1）＝長期契約は固定数量ゆえ需要変動に対応できず、余っても買い続ける。
        /// 被覆率が高いほど硬直し、rigidityWeight が硬直の上限を決める。
        /// </summary>
        public static float FlexibilityLoss(float contractCoverage, SupplyContractParams p)
        {
            float cov = Mathf.Clamp01(contractCoverage);
            return cov * p.rigidityWeight;
        }

        /// <summary>既定 Params で柔軟性喪失を返す簡易窓口。</summary>
        public static float FlexibilityLoss(float contractCoverage) =>
            FlexibilityLoss(contractCoverage, SupplyContractParams.Default);

        /// <summary>
        /// 契約破棄のペナルティ（違約金）＝残存期間×契約価値×単価。残りが長いほど破棄は高くつく。
        /// 契約価値・残存期間は非負にクランプ。
        /// </summary>
        public static float BreachPenalty(float contractValue, float contractRemainingTerm, SupplyContractParams p)
        {
            float value = Mathf.Max(0f, contractValue);
            float term = Mathf.Max(0f, contractRemainingTerm);
            return value * term * p.breachUnitCost;
        }

        /// <summary>既定 Params で違約金を返す簡易窓口。</summary>
        public static float BreachPenalty(float contractValue, float contractRemainingTerm) =>
            BreachPenalty(contractValue, contractRemainingTerm, SupplyContractParams.Default);

        /// <summary>
        /// 破棄の外交的余波（opinion ペナルティの大きさ・非負）＝契約価値×相手国力×外交余波重み。
        /// 大国との契約を破れば制裁/戦争のリスクとして外交へ波及する＝この値を <see cref="DiplomacyRules"/> の
        /// opinion 引き下げ（および casus belli の火種）に充てる。状態遷移そのものは DiplomacyRules へ委譲。
        /// 弱小国相手（国力≈0）なら余波はほぼゼロ＝商売の範囲に収まる。
        /// </summary>
        public static float BreachDiplomaticFallout(float contractValue, float counterpartyPower, SupplyContractParams p)
        {
            float value = Mathf.Max(0f, contractValue);
            float power = Mathf.Clamp01(counterpartyPower);
            return value * power * p.falloutWeight;
        }

        /// <summary>既定 Params で外交余波を返す簡易窓口。</summary>
        public static float BreachDiplomaticFallout(float contractValue, float counterpartyPower) =>
            BreachDiplomaticFallout(contractValue, counterpartyPower, SupplyContractParams.Default);

        /// <summary>
        /// 最適な長期契約比率（0..1）＝需要が安定し価格が荒れるなら長期契約を厚く・需要が荒れるなら
        /// スポットを厚く。価格ボラが高いほど固定したい（長期契約↑）、需要ボラが高いほど縛られたくない
        /// （長期契約↓）。priceVolatility×(1−demandVolatility) で「固めたい度合い」を返す。
        /// </summary>
        public static float OptimalContractMix(float demandVolatility, float priceVolatility)
        {
            float dem = Mathf.Clamp01(demandVolatility);
            float price = Mathf.Clamp01(priceVolatility);
            return Mathf.Clamp01(price * (1f - dem));
        }

        /// <summary>
        /// 推奨調達様式＝最適比率から離散化する。長期契約が厚ければ <see cref="ProcurementMode.長期契約"/>、
        /// 薄ければ <see cref="ProcurementMode.スポット"/>、中庸なら <see cref="ProcurementMode.混合"/>。
        /// </summary>
        public static ProcurementMode RecommendMode(float demandVolatility, float priceVolatility)
        {
            float mix = OptimalContractMix(demandVolatility, priceVolatility);
            if (mix >= 0.66f) return ProcurementMode.長期契約;
            if (mix <= 0.33f) return ProcurementMode.スポット;
            return ProcurementMode.混合;
        }
    }
}
