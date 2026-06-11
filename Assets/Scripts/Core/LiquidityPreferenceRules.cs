using UnityEngine;

namespace Ginei
{
    /// <summary>流動性選好の調整値（マジックナンバー禁止＝集約）。top-level＝クラス外。</summary>
    public readonly struct LiquidityPreferenceParams
    {
        public readonly float transactionWeight;   // 取引動機の重み（所得比例）
        public readonly float speculativeWeight;    // 投機動機の重み（低金利ほど現金）
        public readonly float precautionaryWeight;  // 予備的動機の重み（不確実性比例）
        public readonly float zlbFloor;             // 名目金利のゼロ下限（これ以下に下げられない）
        public readonly float trapThreshold;        // この金利以下で流動性の罠（無限弾力的）
        public readonly float rateSensitivity;      // 貨幣供給増→均衡金利低下の感度

        public LiquidityPreferenceParams(float transactionWeight, float speculativeWeight, float precautionaryWeight,
                                          float zlbFloor, float trapThreshold, float rateSensitivity)
        {
            this.transactionWeight = Mathf.Clamp01(transactionWeight);
            this.speculativeWeight = Mathf.Clamp01(speculativeWeight);
            this.precautionaryWeight = Mathf.Clamp01(precautionaryWeight);
            this.zlbFloor = Mathf.Clamp01(zlbFloor);
            // 罠の閾値はゼロ下限以上（下限より下では判定できない）
            this.trapThreshold = Mathf.Clamp(trapThreshold, this.zlbFloor, 1f);
            this.rateSensitivity = Mathf.Clamp01(rateSensitivity);
        }

        /// <summary>既定＝取引0.5/投機0.3/予備0.2・ゼロ下限0%・罠閾値5%・供給感度0.5。</summary>
        public static LiquidityPreferenceParams Default =>
            new LiquidityPreferenceParams(0.5f, 0.3f, 0.2f, 0.0f, 0.05f, 0.5f);
    }

    /// <summary>
    /// 流動性選好と金利下限の純ロジック（KEYN-4 #1548・ケインズ『一般理論』参考＝流動性の罠 liquidity trap・唯一の窓口）。
    /// 人は不確実性下で<b>現金（流動性）を保有したがる</b>＝貨幣需要は取引動機（所得比例）＋投機動機（低金利ほど現金）＋
    /// 予備的動機（不確実性比例）から成る。金利が<b>ゼロ下限（ZLB）</b>に達するとこれ以上下げられず、貨幣需要が無限弾力的になる
    /// <b>流動性の罠</b>で金融政策（利下げ）が無力化し、クラウディングアウトが起きないため<b>財政政策だけが有効</b>になる。
    /// 信用創造の窓口＝<see cref="BankRules"/>／財政（国債・PB）の窓口＝<see cref="FiscalRules"/>／有効需要・乗数は同EPIC KEYN の
    /// EffectiveDemandRules（別窓口）。ここは流動性選好＝貨幣需要と金利下限（金融政策の無力化）に特化。決定論・基準値非破壊。test-first。
    /// </summary>
    public static class LiquidityPreferenceRules
    {
        /// <summary>
        /// 貨幣需要 0..1＝取引動機（所得比例）＋投機動機（低金利ほど現金保有＝金利と逆相関）＋予備的動機（不確実性比例）。
        /// 不確実性下で人は現金を持ちたがる＝予備的動機が需要を押し上げる。
        /// </summary>
        public static float LiquidityDemand(float income, float interestRate, float uncertainty, LiquidityPreferenceParams p)
        {
            float y = Mathf.Clamp01(income);
            float r = Mathf.Clamp01(interestRate);
            float u = Mathf.Clamp01(uncertainty);
            float transaction = p.transactionWeight * y;                  // 取引動機＝所得比例
            float speculative = p.speculativeWeight * (1f - r);           // 投機動機＝低金利ほど現金
            float precautionary = p.precautionaryWeight * u;              // 予備的動機＝不確実性比例
            return Mathf.Clamp01(transaction + speculative + precautionary);
        }

        /// <summary>
        /// 投機的貨幣需要 0..1＝低金利ほど債券でなく現金を持つ（金利と逆相関）。
        /// 金利が高ければ債券（利息）を選び、低ければ現金（流動性）を選ぶ。
        /// </summary>
        public static float SpeculativeMoneyDemand(float interestRate)
            => 1f - Mathf.Clamp01(interestRate);

        /// <summary>
        /// 貨幣需給で決まる均衡金利＝貨幣需要が供給を上回るほど高く、供給増で下がる。ただしゼロ下限（ZLB）で打ち止め。
        /// 供給が需要を満たしても、金利は <see cref="LiquidityPreferenceParams.zlbFloor"/> より下には行けない。
        /// </summary>
        public static float MoneyMarketRate(float moneySupply, float liquidityDemand, LiquidityPreferenceParams p)
        {
            float ms = Mathf.Clamp01(moneySupply);
            float ld = Mathf.Clamp01(liquidityDemand);
            // 需要超過（ld−ms）が正なら金利上昇、供給超過なら低下。感度で傾きを調整。
            float raw = (ld - ms) * p.rateSensitivity;
            return ZeroLowerBound(raw, p);
        }

        /// <summary>名目金利のゼロ下限（ZLB）＝これ以下には下げられない（現金を抱えれば負金利を避けられるため）。</summary>
        public static float ZeroLowerBound(float nominalRate, LiquidityPreferenceParams p)
            => Mathf.Max(p.zlbFloor, nominalRate);

        /// <summary>
        /// 流動性の罠の判定＝金利が下限（trapThreshold）近くまで下がり、貨幣需要が無限弾力的になった状態。
        /// ここでは利下げが効かない（金融政策の無力化）。
        /// </summary>
        public static bool IsLiquidityTrap(float interestRate, LiquidityPreferenceParams p)
            => Mathf.Clamp01(interestRate) <= p.trapThreshold;

        /// <summary>
        /// 金融政策（利下げ）の有効度 0..1＝金利が下限に近いほど効かない（無力化）。
        /// 下限近接度 zlbProximity（0=下限から遠い/1=下限すれすれ）が高いほど低下。
        /// </summary>
        public static float MonetaryPolicyEffectiveness(float interestRate, float zlbProximity, LiquidityPreferenceParams p)
        {
            float prox = Mathf.Clamp01(zlbProximity);
            // 下限に近づくほど（prox→1）効果が消える。罠なら完全に無力。
            float baseEff = 1f - prox;
            if (IsLiquidityTrap(interestRate, p)) return 0f;
            return Mathf.Clamp01(baseEff);
        }

        /// <summary>
        /// 財政政策の有効度 0..1＝流動性の罠では財政政策だけが有効（クラウディングアウトが起きない＝金利が動かないので民間投資を締め出さない）。
        /// 罠でなければ部分的にクラウディングアウトされ効果は半減。
        /// </summary>
        public static float FiscalPolicyEffectiveness(bool liquidityTrap)
            => liquidityTrap ? 1f : 0.5f;

        /// <summary>
        /// 現金退蔵圧力 0..1＝不確実性が高く信認（confidence）が低いほど現金を抱え込む（流動性への逃避）。
        /// 不確実性下で人は現金を持ちたがる＝退蔵が貨幣を市場から引き上げる。
        /// </summary>
        public static float HoardingPressure(float uncertainty, float confidence)
        {
            float u = Mathf.Clamp01(uncertainty);
            float c = Mathf.Clamp01(confidence);
            // 不確実性が高く信認が低いほど退蔵（u×(1−c)）。
            return Mathf.Clamp01(u * (1f - c));
        }
    }
}
