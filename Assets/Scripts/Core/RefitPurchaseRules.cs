using UnityEngine;

namespace Ginei
{
    /// <summary>戦力の調達方法（#1068）。改装＝既存艦の延命／新造＝新規建造／購入＝他勢力から取得。</summary>
    public enum AcquisitionMethod
    {
        改装,
        新造,
        購入
    }

    /// <summary>改装/復元と建造vs購入の調整係数（#1068）。</summary>
    public readonly struct RefitPurchaseParams
    {
        /// <summary>改装の基本コスト係数（性能向上幅1あたりの基準費用）。</summary>
        public readonly float refitBaseCost;
        /// <summary>艦齢が改装コストを押し上げる強さ（古い艦体ほど改装が高くつく）。</summary>
        public readonly float ageCostPenalty;
        /// <summary>改装で届く性能上限の最大値（新造＝1.0に対して、若い艦が改装で届く上限）。</summary>
        public readonly float maxRefitCeiling;
        /// <summary>改装上限が艦齢で目減りする強さ（老朽艦は改装しても新造に及ばない）。</summary>
        public readonly float ceilingAgeDecay;
        /// <summary>改装上限の下限（どんな老朽艦でも改装すればこれだけは出せる）。</summary>
        public readonly float minRefitCeiling;
        /// <summary>建造vs購入で時間を金に換算する重み（時間1あたりの機会費用）。</summary>
        public readonly float timeValueWeight;
        /// <summary>購入の依存リスク係数（外国供給依存度1あたりのリスク）。</summary>
        public readonly float dependencyWeight;
        /// <summary>改装か新造かの分岐となる艦齢（これを超えると新造が得になりやすい）。</summary>
        public readonly float refitReplaceBreakAge;

        public RefitPurchaseParams(float refitBaseCost, float ageCostPenalty, float maxRefitCeiling,
                                   float ceilingAgeDecay, float minRefitCeiling, float timeValueWeight,
                                   float dependencyWeight, float refitReplaceBreakAge)
        {
            this.refitBaseCost = Mathf.Max(0f, refitBaseCost);
            this.ageCostPenalty = Mathf.Max(0f, ageCostPenalty);
            this.maxRefitCeiling = Mathf.Clamp01(maxRefitCeiling);
            this.ceilingAgeDecay = Mathf.Max(0f, ceilingAgeDecay);
            this.minRefitCeiling = Mathf.Clamp01(minRefitCeiling);
            this.timeValueWeight = Mathf.Max(0f, timeValueWeight);
            this.dependencyWeight = Mathf.Clamp01(dependencyWeight);
            this.refitReplaceBreakAge = Mathf.Clamp01(refitReplaceBreakAge);
        }

        /// <summary>既定＝改装基本0.5・艦齢ペナルティ1.0・改装上限上0.85・上限減衰0.5・上限下限0.4・時間重み0.6・依存重み0.7・改装新造分岐艦齢0.5。</summary>
        public static RefitPurchaseParams Default =>
            new RefitPurchaseParams(0.5f, 1.0f, 0.85f, 0.5f, 0.4f, 0.6f, 0.7f, 0.5f);
    }

    /// <summary>
    /// 改装/復元＋建造vs購入の純ロジック（#1068）。古い艦を改装して延命するか、新造するか、他勢力から購入するか＝
    /// 戦力調達の三択の損得を式に出す。「改装は安いが限界がある（古い器の限界）・新造は高いが自由・購入は速いが依存」を
    /// 数値化する。新規建造そのものの能力・コストは <see cref="ShipyardRules"/>（新造）、武装の設計は
    /// <see cref="ArmamentDesignRules"/>（設計）、経年劣化の進み方は <see cref="ShipAgingRules"/>（艦齢）、
    /// 購入契約の供給保証は <see cref="SupplyContractRules"/>（購入契約）が担い、ここは「どの調達法を選ぶか」の経済だけを扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RefitPurchaseRules
    {
        /// <summary>
        /// 改装コスト（>=0）。性能向上幅に基本コストを掛け、古い艦体ほど割増（船体の限界＝<see cref="ShipAgingRules"/> と接続）。
        /// 艦齢0で基準、艦齢1で (1+ageCostPenalty) 倍。同じ向上幅でも老朽艦は高くつく。
        /// </summary>
        public static float RefitCost(float hullAge, float performanceGain, RefitPurchaseParams p)
        {
            float age = Mathf.Clamp01(hullAge);
            float gain = Mathf.Clamp01(performanceGain);
            float ageMult = 1f + age * p.ageCostPenalty;
            return p.refitBaseCost * gain * ageMult;
        }

        public static float RefitCost(float hullAge, float performanceGain)
            => RefitCost(hullAge, performanceGain, RefitPurchaseParams.Default);

        /// <summary>
        /// 改装で届く性能上限（minRefitCeiling..maxRefitCeiling）。新造＝1.0 に対し、改装は元の艦体に縛られるので 1.0 に届かない。
        /// 若い艦体ほど上限が高く、老朽艦は改装しても新造に及ばない＝古い器の限界。
        /// </summary>
        public static float RefitPerformanceCeiling(float hullAge, RefitPurchaseParams p)
        {
            float age = Mathf.Clamp01(hullAge);
            float ceiling = p.maxRefitCeiling - age * p.ceilingAgeDecay;
            return Mathf.Clamp(ceiling, p.minRefitCeiling, p.maxRefitCeiling);
        }

        public static float RefitPerformanceCeiling(float hullAge)
            => RefitPerformanceCeiling(hullAge, RefitPurchaseParams.Default);

        /// <summary>
        /// 建造か購入かの決定値（正＝建造有利／負＝購入有利／0＝拮抗）。
        /// 建造の実効コスト＝建造費＋建造時間×時間重み×urgency、購入の実効コスト＝購入価格＋納期×時間重み×urgency。
        /// 急ぐ（urgency 大）ほど時間が重く効き、速い方（短納期）が有利＝急ぐなら買う・安くしたいなら造る。
        /// 戻り値＝購入の実効コスト−建造の実効コスト（建造が安いほど正＝建造有利）。
        /// </summary>
        public static float BuildVsBuyDecision(float buildCost, float buildTime, float purchasePrice,
                                               float deliveryTime, float urgency, RefitPurchaseParams p)
        {
            float u = Mathf.Clamp01(urgency);
            float bCost = Mathf.Max(0f, buildCost);
            float bTime = Mathf.Max(0f, buildTime);
            float pPrice = Mathf.Max(0f, purchasePrice);
            float dTime = Mathf.Max(0f, deliveryTime);

            float buildEffective = bCost + bTime * p.timeValueWeight * u;
            float buyEffective = pPrice + dTime * p.timeValueWeight * u;
            return buyEffective - buildEffective;
        }

        public static float BuildVsBuyDecision(float buildCost, float buildTime, float purchasePrice,
                                               float deliveryTime, float urgency)
            => BuildVsBuyDecision(buildCost, buildTime, purchasePrice, deliveryTime, urgency,
                                  RefitPurchaseParams.Default);

        /// <summary>
        /// 購入の依存リスク（0..1）。他勢力からの供給に頼るほど供給を握られる＝有事に切られる脆さ。
        /// 外国供給依存度（自前調達できない割合）に依存重みを掛けて出す（<see cref="SupplyContractRules"/> と接続）。
        /// 0＝完全自給で依存なし、1×重み＝最大依存。
        /// </summary>
        public static float PurchaseDependency(float foreignSupplier, RefitPurchaseParams p)
        {
            float f = Mathf.Clamp01(foreignSupplier);
            return Mathf.Clamp01(f * p.dependencyWeight);
        }

        public static float PurchaseDependency(float foreignSupplier)
            => PurchaseDependency(foreignSupplier, RefitPurchaseParams.Default);

        /// <summary>
        /// 改装か新造かの損得（正＝改装が得／負＝新造が得／0＝拮抗）。
        /// 改装は安く済むが、老朽艦ほど艦齢ペナルティで価値が目減りする（古い艦体に金をかけても新造に及ばない）。
        /// 若い艦は改装が得・老朽艦は新造が得＝艦齢が分岐点（refitReplaceBreakAge）。
        /// 戻り値＝（新造コスト−改装の実効コスト）。改装が割安なほど正＝改装有利。
        /// </summary>
        public static float RefitVsReplaceValue(float hullAge, float refitCost, float newBuildCost,
                                                RefitPurchaseParams p)
        {
            float age = Mathf.Clamp01(hullAge);
            float rCost = Mathf.Max(0f, refitCost);
            float nCost = Mathf.Max(0f, newBuildCost);
            // 艦齢が分岐点を超えた分だけ改装の実効コストを割増（老朽艦への改装は割に合わない）。
            float overAge = Mathf.Max(0f, age - p.refitReplaceBreakAge);
            float refitEffective = rCost * (1f + overAge * p.ageCostPenalty);
            return nCost - refitEffective;
        }

        public static float RefitVsReplaceValue(float hullAge, float refitCost, float newBuildCost)
            => RefitVsReplaceValue(hullAge, refitCost, newBuildCost, RefitPurchaseParams.Default);

        /// <summary>
        /// 最適調達法の推奨（#1068 三択の裁定）。
        /// 急ぐ（urgency 高）なら速い<see cref="AcquisitionMethod.購入"/>、
        /// 予算が乏しく艦体が若いなら安い<see cref="AcquisitionMethod.改装"/>、
        /// それ以外（艦が老朽・予算に余裕）なら自由な<see cref="AcquisitionMethod.新造"/>。
        /// 「改装は安いが限界がある・新造は高いが自由・購入は速いが依存」を状況で選ぶ。
        /// </summary>
        public static AcquisitionMethod AcquisitionRecommendation(float urgency, float budget, float hullAge,
                                                                  RefitPurchaseParams p)
        {
            float u = Mathf.Clamp01(urgency);
            float b = Mathf.Clamp01(budget);
            float age = Mathf.Clamp01(hullAge);

            // 急務は時間優先＝購入（依存リスクは負っても速さを買う）。
            if (u >= 0.6f) return AcquisitionMethod.購入;
            // 予算が乏しく艦体がまだ若い＝改装で延命するのが得（分岐点未満）。
            if (b < 0.5f && age <= p.refitReplaceBreakAge) return AcquisitionMethod.改装;
            // それ以外＝新造（自由に造れる）。
            return AcquisitionMethod.新造;
        }

        public static AcquisitionMethod AcquisitionRecommendation(float urgency, float budget, float hullAge)
            => AcquisitionRecommendation(urgency, budget, hullAge, RefitPurchaseParams.Default);
    }
}
