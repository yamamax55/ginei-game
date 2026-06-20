using UnityEngine;

namespace Ginei
{
    /// <summary>経済的強制の梯子の段階＝通商妨害→経済制裁→金融封鎖と圧力を強める。</summary>
    public enum CoercionStep
    {
        通商妨害,
        経済制裁,
        金融封鎖,
    }

    /// <summary>経済的強制の調整係数。</summary>
    public readonly struct EconomicCoercionParams
    {
        /// <summary>通商妨害（関税・輸入制限）段階の基礎圧力（最弱）。</summary>
        public readonly float tradeHarassPressure;
        /// <summary>経済制裁（取引禁止）段階の基礎圧力（中）。</summary>
        public readonly float sanctionPressure;
        /// <summary>金融封鎖（資産凍結・決済網排除）段階の基礎圧力（最強）。</summary>
        public readonly float financialBlockadePressure;
        /// <summary>強制側の自国への跳ね返り係数（段階が上がるほど大きい・自国の交易依存度に掛かる）。</summary>
        public readonly float selfCostScale;
        /// <summary>現段階が効かないとみなす効果の閾値（これ未満なら次段へ上げる圧力）。</summary>
        public readonly float ineffectiveThreshold;
        /// <summary>金融封鎖で「経済的に締め上げた」とみなす圧力の既定閾値。</summary>
        public readonly float strangulationThreshold;

        public EconomicCoercionParams(float tradeHarassPressure, float sanctionPressure, float financialBlockadePressure,
            float selfCostScale, float ineffectiveThreshold, float strangulationThreshold)
        {
            this.tradeHarassPressure = Mathf.Clamp01(tradeHarassPressure);
            this.sanctionPressure = Mathf.Clamp01(sanctionPressure);
            this.financialBlockadePressure = Mathf.Clamp01(financialBlockadePressure);
            this.selfCostScale = Mathf.Max(0f, selfCostScale);
            this.ineffectiveThreshold = Mathf.Clamp01(ineffectiveThreshold);
            this.strangulationThreshold = Mathf.Clamp01(strangulationThreshold);
        }

        /// <summary>既定＝通商妨害0.3・経済制裁0.6・金融封鎖0.9・自己コスト係数0.5・無効閾値0.3・締め上げ閾値0.7。</summary>
        public static EconomicCoercionParams Default => new EconomicCoercionParams(0.3f, 0.6f, 0.9f, 0.5f, 0.3f, 0.7f);
    }

    /// <summary>
    /// 経済的強制の梯子の純ロジック（#1397・限定戦争）。経済を武器にした強制は通商妨害（関税・輸入制限）→
    /// 経済制裁（取引禁止）→金融封鎖（資産凍結・決済網からの排除）と段階的に圧力を強め、各段階で相手への
    /// 打撃と自国への跳ね返りが増す。強力な手段ほど多国間協調を要し（一国の制裁は抜け穴だらけ）、第三国・
    /// 世界経済への波及（巻き込み）も大きい。現段階で効かなければ梯子を一段上げる。
    /// <see cref="SanctionsRules"/>（制裁の実体＝実効ペナルティと抜け穴）・<see cref="BlockadeRules"/>（物理封鎖）・
    /// <see cref="DebtDiplomacyRules"/>（債務外交）・<see cref="ReserveCurrencyRules"/>（基軸通貨＝金融封鎖の武器）
    /// とは別＝それらを段階の梯子として統合するエスカレーション層。多国間協調は
    /// <see cref="CollectiveSecurityRules"/> と接続。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EconomicCoercionRules
    {
        /// <summary>
        /// 各段階の経済的圧力（0..1）＝段階の基礎圧力×強度(0..1)。
        /// 通商妨害＜経済制裁＜金融封鎖と段階的に強い（基礎圧力で段階差を表す）。
        /// </summary>
        public static float CoercionPressure(CoercionStep step, float intensity, EconomicCoercionParams p)
        {
            float basePressure;
            switch (step)
            {
                case CoercionStep.金融封鎖: basePressure = p.financialBlockadePressure; break;
                case CoercionStep.経済制裁: basePressure = p.sanctionPressure; break;
                default: basePressure = p.tradeHarassPressure; break; // 通商妨害
            }
            return Mathf.Clamp01(basePressure * Mathf.Clamp01(intensity));
        }

        public static float CoercionPressure(CoercionStep step, float intensity)
            => CoercionPressure(step, intensity, EconomicCoercionParams.Default);

        /// <summary>
        /// 相手への打撃（0..1）＝経済的圧力×相手の交易依存度(0..1)。
        /// 交易に依存しているほど経済的強制が効く（自給自足の相手には効かない）。
        /// </summary>
        public static float TargetDamage(float coercionPressure, float targetTradeDependence)
        {
            return Mathf.Clamp01(Mathf.Clamp01(coercionPressure) * Mathf.Clamp01(targetTradeDependence));
        }

        /// <summary>
        /// 強制する側の自国への跳ね返り（0..1）＝段階の基礎圧力×自国の交易依存度(0..1)×係数。
        /// 段階が上がるほど跳ね返りが大きく、金融封鎖は自国の金融にも打撃＝諸刃。
        /// </summary>
        public static float SelfCost(CoercionStep step, float ownTradeDependence, EconomicCoercionParams p)
        {
            float basePressure;
            switch (step)
            {
                case CoercionStep.金融封鎖: basePressure = p.financialBlockadePressure; break;
                case CoercionStep.経済制裁: basePressure = p.sanctionPressure; break;
                default: basePressure = p.tradeHarassPressure; break; // 通商妨害
            }
            return Mathf.Clamp01(basePressure * Mathf.Clamp01(ownTradeDependence) * p.selfCostScale);
        }

        public static float SelfCost(CoercionStep step, float ownTradeDependence)
            => SelfCost(step, ownTradeDependence, EconomicCoercionParams.Default);

        /// <summary>
        /// 梯子を一段上げる（通商妨害→経済制裁→金融封鎖の決定論遷移）。
        /// 金融封鎖は最上段＝それ以上は無い（金融封鎖のまま）。
        /// </summary>
        public static CoercionStep NextStep(CoercionStep step)
        {
            switch (step)
            {
                case CoercionStep.通商妨害: return CoercionStep.経済制裁;
                case CoercionStep.経済制裁: return CoercionStep.金融封鎖;
                default: return CoercionStep.金融封鎖; // 最上段
            }
        }

        /// <summary>
        /// エスカレーション判断＝現段階で効かないと次の段階へ上げる（梯子を昇る圧力）。
        /// 相手の抵抗(0..1)が高く、現段階の効果(0..1)が無効閾値を下回るときに昇る。
        /// </summary>
        public static bool EscalationDecision(float targetResistance, float currentStepEffect, EconomicCoercionParams p)
        {
            return Mathf.Clamp01(currentStepEffect) < p.ineffectiveThreshold && Mathf.Clamp01(targetResistance) > 0.5f;
        }

        public static bool EscalationDecision(float targetResistance, float currentStepEffect)
            => EscalationDecision(targetResistance, currentStepEffect, EconomicCoercionParams.Default);

        /// <summary>
        /// 第三国・世界経済への波及（0..1・巻き込み）＝段階の基礎圧力×世界経済の統合度(0..1)。
        /// 金融封鎖など強力な手段ほど第三国・世界経済に波及する（決済網の排除は皆を巻き込む）。
        /// </summary>
        public static float ThirdPartyDisruption(CoercionStep step, float globalIntegration, EconomicCoercionParams p)
        {
            float basePressure;
            switch (step)
            {
                case CoercionStep.金融封鎖: basePressure = p.financialBlockadePressure; break;
                case CoercionStep.経済制裁: basePressure = p.sanctionPressure; break;
                default: basePressure = p.tradeHarassPressure; break; // 通商妨害
            }
            return Mathf.Clamp01(basePressure * Mathf.Clamp01(globalIntegration));
        }

        public static float ThirdPartyDisruption(CoercionStep step, float globalIntegration)
            => ThirdPartyDisruption(step, globalIntegration, EconomicCoercionParams.Default);

        /// <summary>
        /// 多国間協調の要件（0..1）＝強力な段階ほど高い（一国の制裁は抜け穴だらけ）。
        /// 段階の基礎圧力をそのまま協調要件とする＝金融封鎖は広い協調なしでは穴だらけ。
        /// <see cref="CollectiveSecurityRules"/> の制裁参加と接続する想定。
        /// </summary>
        public static float CoalitionRequirement(CoercionStep step, EconomicCoercionParams p)
        {
            switch (step)
            {
                case CoercionStep.金融封鎖: return p.financialBlockadePressure;
                case CoercionStep.経済制裁: return p.sanctionPressure;
                default: return p.tradeHarassPressure; // 通商妨害
            }
        }

        public static float CoalitionRequirement(CoercionStep step)
            => CoalitionRequirement(step, EconomicCoercionParams.Default);

        /// <summary>
        /// 金融封鎖で相手を経済的に締め上げた判定＝段階が金融封鎖で、かつ経済的圧力が閾値以上。
        /// 金融封鎖以外の段階では締め上げにならない（最上段の手段だけが扼殺たりうる）。
        /// </summary>
        public static bool IsFinancialStrangulation(CoercionStep step, float coercionPressure, float threshold)
        {
            return step == CoercionStep.金融封鎖 && Mathf.Clamp01(coercionPressure) >= Mathf.Clamp01(threshold);
        }

        public static bool IsFinancialStrangulation(CoercionStep step, float coercionPressure)
            => IsFinancialStrangulation(step, coercionPressure, EconomicCoercionParams.Default.strangulationThreshold);
    }
}
