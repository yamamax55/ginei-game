using UnityEngine;

namespace Ginei
{
    /// <summary>経済制裁の調整係数（対象勢力への経済的圧力）。</summary>
    public readonly struct EconomicSanctionParams
    {
        /// <summary>対象分野の比重（対象分野×この係数＝範囲の素地。1で素直）。</summary>
        public readonly float sectorWeight;
        /// <summary>包囲（制裁参加国×国際支持）の効き。大きいほど少数の参加国でも包囲が固まる。</summary>
        public readonly float coalitionWeight;
        /// <summary>第三国貿易が抜け穴を広げる強さ（迂回の容易さ）。</summary>
        public readonly float loopholeWeight;
        /// <summary>制裁する側の返り血（対象国貿易×範囲）に掛かるコスト係数。</summary>
        public readonly float senderCostWeight;
        /// <summary>適応（耐性×時間）の上限。これ以上は効きが鈍らない床を残す。</summary>
        public readonly float adaptationCap;

        public EconomicSanctionParams(float sectorWeight, float coalitionWeight, float loopholeWeight,
            float senderCostWeight, float adaptationCap)
        {
            this.sectorWeight = Mathf.Clamp(sectorWeight, 0f, 4f);
            this.coalitionWeight = Mathf.Clamp(coalitionWeight, 0f, 4f);
            this.loopholeWeight = Mathf.Clamp01(loopholeWeight);
            this.senderCostWeight = Mathf.Clamp(senderCostWeight, 0f, 4f);
            this.adaptationCap = Mathf.Clamp01(adaptationCap);
        }

        /// <summary>既定＝分野比重1.0・包囲1.0・抜け穴0.8・返り血1.0・適応上限0.8。</summary>
        public static EconomicSanctionParams Default => new EconomicSanctionParams(1f, 1f, 0.8f, 1f, 0.8f);
    }

    /// <summary>
    /// 経済制裁の純ロジック（対象勢力への経済的圧力）。対象分野×深さで制裁範囲を決め、対象の貿易依存に応じて
    /// 経済打撃を出す。第三国の協力（制裁包囲）が高いほど抜け穴（迂回）が塞がり、実効的な制裁効果が残る。
    /// 制裁する側も対象国との貿易を失う（返り血＝コスト）。対象は耐性と時間で適応し効きが鈍る。最後に実効効果と
    /// 適応から政治的圧力（屈服を迫る度合い）を返す。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EconomicSanctionRules
    {
        /// <summary>
        /// 制裁範囲（0..1）。対象分野割合×制裁の深さ×分野比重。広く深い制裁ほど範囲が大きい。
        /// </summary>
        public static float SanctionScope(float targetSectors, float sanctionDepth, EconomicSanctionParams p)
        {
            float sectors = Mathf.Clamp01(targetSectors);
            float depth = Mathf.Clamp01(sanctionDepth);
            return Mathf.Clamp01(sectors * depth * p.sectorWeight);
        }

        public static float SanctionScope(float targetSectors, float sanctionDepth)
            => SanctionScope(targetSectors, sanctionDepth, EconomicSanctionParams.Default);

        /// <summary>
        /// 対象経済への打撃（0..1）。制裁範囲×対象の貿易依存。貿易に頼る経済ほど範囲が同じでも痛手が大きい。
        /// </summary>
        public static float TargetDamage(float sanctionScope, float targetTradeDependency, EconomicSanctionParams p)
        {
            float scope = Mathf.Clamp01(sanctionScope);
            float dep = Mathf.Clamp01(targetTradeDependency);
            return Mathf.Clamp01(scope * dep);
        }

        public static float TargetDamage(float sanctionScope, float targetTradeDependency)
            => TargetDamage(sanctionScope, targetTradeDependency, EconomicSanctionParams.Default);

        /// <summary>
        /// 制裁包囲（0..1）。制裁参加国の規模×国際支持×包囲係数。多くの国が支持を伴って参加するほど包囲が固まる。
        /// </summary>
        public static float CoalitionParticipation(float sanctioningStates, float internationalSupport, EconomicSanctionParams p)
        {
            float states = Mathf.Clamp01(sanctioningStates);
            float support = Mathf.Clamp01(internationalSupport);
            return Mathf.Clamp01(states * support * p.coalitionWeight);
        }

        public static float CoalitionParticipation(float sanctioningStates, float internationalSupport)
            => CoalitionParticipation(sanctioningStates, internationalSupport, EconomicSanctionParams.Default);

        /// <summary>
        /// 抜け穴＝迂回（0..1）。包囲が緩いほど(1-包囲)、第三国との貿易が太いほど迂回路が広がる。包囲が固ければ塞がる。
        /// </summary>
        public static float Loopholes(float coalitionParticipation, float thirdPartyTrade, EconomicSanctionParams p)
        {
            float gap = 1f - Mathf.Clamp01(coalitionParticipation);
            float third = Mathf.Clamp01(thirdPartyTrade);
            return Mathf.Clamp01(gap * third * p.loopholeWeight);
        }

        public static float Loopholes(float coalitionParticipation, float thirdPartyTrade)
            => Loopholes(coalitionParticipation, thirdPartyTrade, EconomicSanctionParams.Default);

        /// <summary>
        /// 実効的な制裁効果（0..1）。経済打撃×(1-抜け穴)。迂回が広いほど打撃が漏れて効果が削がれる。
        /// </summary>
        public static float EffectiveDamage(float targetDamage, float loopholes, EconomicSanctionParams p)
        {
            float dmg = Mathf.Clamp01(targetDamage);
            float leak = Mathf.Clamp01(loopholes);
            return Mathf.Clamp01(dmg * (1f - leak));
        }

        public static float EffectiveDamage(float targetDamage, float loopholes)
            => EffectiveDamage(targetDamage, loopholes, EconomicSanctionParams.Default);

        /// <summary>
        /// 制裁する側のコスト＝返り血（0..1）。自国の対象国貿易×制裁範囲×返り血係数。対象国と深く広く商っていた側ほど
        /// 自らも貿易を失う。
        /// </summary>
        public static float SenderCost(float ownTradeWithTarget, float sanctionScope, EconomicSanctionParams p)
        {
            float own = Mathf.Clamp01(ownTradeWithTarget);
            float scope = Mathf.Clamp01(sanctionScope);
            return Mathf.Clamp01(own * scope * p.senderCostWeight);
        }

        public static float SenderCost(float ownTradeWithTarget, float sanctionScope)
            => SenderCost(ownTradeWithTarget, sanctionScope, EconomicSanctionParams.Default);

        /// <summary>
        /// 対象の適応（0..1）。耐性×制裁継続時間（時間とともに効きが鈍る）に飽和をかけ adaptationCap で頭打ち。
        /// duration は sqrt で逓減（序盤は速く適応、後は緩やかに）。
        /// </summary>
        public static float TargetAdaptation(float targetResilience, float sanctionDuration, EconomicSanctionParams p)
        {
            float res = Mathf.Clamp01(targetResilience);
            float t = Mathf.Sqrt(Mathf.Clamp01(sanctionDuration)); // 0..1 を逓減カーブへ
            return Mathf.Clamp01(res * t * p.adaptationCap);
        }

        public static float TargetAdaptation(float targetResilience, float sanctionDuration)
            => TargetAdaptation(targetResilience, sanctionDuration, EconomicSanctionParams.Default);

        /// <summary>
        /// 政治的圧力（0..1＝屈服を迫る度合い）。実効効果×(1-適応)。適応が進むほど同じ効果でも政治圧力は薄まる。
        /// </summary>
        public static float PoliticalEffect(float effectiveDamage, float targetAdaptation, EconomicSanctionParams p)
        {
            float dmg = Mathf.Clamp01(effectiveDamage);
            float adapt = Mathf.Clamp01(targetAdaptation);
            return Mathf.Clamp01(dmg * (1f - adapt));
        }

        public static float PoliticalEffect(float effectiveDamage, float targetAdaptation)
            => PoliticalEffect(effectiveDamage, targetAdaptation, EconomicSanctionParams.Default);
    }
}
