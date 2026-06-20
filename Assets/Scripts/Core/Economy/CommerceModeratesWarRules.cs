using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// やわらかな商業（doux commerce）の調整係数（モンテスキュー型）。マジックナンバー禁止＝集約。
    /// </summary>
    public readonly struct CommerceModeratesWarParams
    {
        /// <summary>相互依存が戦争忌避へ寄与する重み（交易で結ばれた国ほど戦争を避ける）。</summary>
        public readonly float warReluctanceWeight;
        /// <summary>商業が習俗を洗練・温和化する速度（1秒あたりの温和度上昇）。</summary>
        public readonly float refinementRate;
        /// <summary>交易破壊が厭戦を加速させる重み（商人が早く和平を望む）。</summary>
        public readonly float wearinessAccelWeight;
        /// <summary>平和の継続が相互依存を深める好循環の重み（商業平和の配当）。</summary>
        public readonly float peaceDividendWeight;
        /// <summary>相互依存の武器化（制裁・封鎖）が摩擦を生む重み（諸刃）。</summary>
        public readonly float tradeWarWeight;
        /// <summary>商業共和国とみなす商業水準の閾値。</summary>
        public readonly float commercialRepublicThreshold;

        public CommerceModeratesWarParams(float warReluctanceWeight, float refinementRate, float wearinessAccelWeight,
            float peaceDividendWeight, float tradeWarWeight, float commercialRepublicThreshold)
        {
            this.warReluctanceWeight = Mathf.Max(0f, warReluctanceWeight);
            this.refinementRate = Mathf.Max(0f, refinementRate);
            this.wearinessAccelWeight = Mathf.Max(0f, wearinessAccelWeight);
            this.peaceDividendWeight = Mathf.Max(0f, peaceDividendWeight);
            this.tradeWarWeight = Mathf.Max(0f, tradeWarWeight);
            this.commercialRepublicThreshold = Mathf.Clamp01(commercialRepublicThreshold);
        }

        /// <summary>既定＝戦争忌避重み0.8・温和化速度0.05/秒・厭戦加速0.5・平和配当0.3・貿易戦争0.6・共和国閾値0.5。</summary>
        public static CommerceModeratesWarParams Default => new CommerceModeratesWarParams(
            0.8f, 0.05f, 0.5f,
            0.3f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 通商と温和政治の純ロジック（MONT-6 #1453・モンテスキュー『法の精神』のやわらかな商業＝doux commerce）。
    /// 商業は人々を平和へ向かわせる＝交易で結ばれた国は互いを必要とするので戦争を避け（<see cref="WarReluctance"/>）、
    /// 戦争が交易を破壊するほど厭戦が加速する（<see cref="WarWearinessAcceleration"/>）。商業は野蛮な習俗を洗練し
    /// 温和にする（<see cref="MoresRefinement"/>）が、専制政体は自由な商人を恐れて商業を抑圧し温和化が働かない
    /// （<see cref="DespotismCommerceSuppression"/>）。平和が続くほど交易が深まる好循環（<see cref="CommercialPeaceDividend"/>）と、
    /// 経済的相互依存の武器化が生む摩擦（<see cref="TradeWarRisk"/>）の諸刃も扱う。
    /// <b>分担</b>：<see cref="TradeRules"/>（交易の利得分配）／<see cref="WarGoalRules"/>（厭戦の実体＝ここは加速係数を渡す）／
    /// <see cref="SanctionsRules"/>（経済的強制の実体）／<see cref="GovernmentPrincipleRules"/>（専制の政体原理）とは別＝
    /// モンテスキューの「商業は平和を促す」を式にする層。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CommerceModeratesWarRules
    {
        /// <summary>
        /// 経済的相互依存（0..1）＝交易量(0..1)×相互依存(0..1)。
        /// 互いを必要とする度合い＝量だけでなく代替不能性（mutualReliance）が掛かる。
        /// </summary>
        public static float Interdependence(float tradeVolume, float mutualReliance)
        {
            return Mathf.Clamp01(tradeVolume) * Mathf.Clamp01(mutualReliance);
        }

        /// <summary>
        /// 戦争忌避度（0..1）＝相互依存×戦争忌避重み。交易で結ばれた国ほど戦争を避けたがる
        /// （戦争は交易相手を失う＝開戦の機会費用が高い）。
        /// </summary>
        public static float WarReluctance(float interdependence, CommerceModeratesWarParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(interdependence) * p.warReluctanceWeight);
        }

        public static float WarReluctance(float interdependence)
            => WarReluctance(interdependence, CommerceModeratesWarParams.Default);

        /// <summary>
        /// 習俗の洗練量（時間積分の1tickぶん）＝商業水準(0..1)×温和化速度×dt。
        /// 商業が野蛮な習俗を和らげ温和にする＝商業文化が荒々しさを削る。専制下では
        /// <see cref="DespotismCommerceSuppression"/> を掛けて減衰させる想定。
        /// </summary>
        public static float MoresRefinement(float commerceLevel, float dt, CommerceModeratesWarParams p)
        {
            return Mathf.Clamp01(commerceLevel) * p.refinementRate * Mathf.Max(0f, dt);
        }

        public static float MoresRefinement(float commerceLevel, float dt)
            => MoresRefinement(commerceLevel, dt, CommerceModeratesWarParams.Default);

        /// <summary>
        /// 厭戦の加速係数（≥1）＝1＋交易量(0..1)×交易破壊(0..1)×加速重み。
        /// 戦争が交易を破壊するほど（warDisruption が大きいほど）商人が早く和平を望み厭戦が加速する。
        /// <see cref="WarGoalRules.WarWeariness"/> に乗じる係数として使う。
        /// </summary>
        public static float WarWearinessAcceleration(float tradeVolume, float warDisruption, CommerceModeratesWarParams p)
        {
            float t = Mathf.Clamp01(tradeVolume);
            float d = Mathf.Clamp01(warDisruption);
            return 1f + t * d * p.wearinessAccelWeight;
        }

        public static float WarWearinessAcceleration(float tradeVolume, float warDisruption)
            => WarWearinessAcceleration(tradeVolume, warDisruption, CommerceModeratesWarParams.Default);

        /// <summary>
        /// 専制による商業抑圧の温和化倍率（0..1）。専制度(0..1)が高いほど自由な商人を恐れて商業を抑圧し、
        /// 商業による温和化が働かなくなる＝1−専制度。<see cref="MoresRefinement"/> などに乗じる係数。
        /// </summary>
        public static float DespotismCommerceSuppression(float despotismLevel)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(despotismLevel));
        }

        /// <summary>
        /// 商業平和の配当（0..1）＝相互依存×（1＋平和継続×配当重み）を丸めた値。
        /// 平和が続くほど（peaceDuration 0..1）交易が深まり、さらに平和を強める好循環。
        /// </summary>
        public static float CommercialPeaceDividend(float interdependence, float peaceDuration, CommerceModeratesWarParams p)
        {
            float inter = Mathf.Clamp01(interdependence);
            float peace = Mathf.Clamp01(peaceDuration);
            return Mathf.Clamp01(inter * (1f + peace * p.peaceDividendWeight));
        }

        public static float CommercialPeaceDividend(float interdependence, float peaceDuration)
            => CommercialPeaceDividend(interdependence, peaceDuration, CommerceModeratesWarParams.Default);

        /// <summary>
        /// 貿易戦争リスク（0..1・諸刃）＝相互依存×経済的強制(0..1)×貿易戦争重み。
        /// 相互依存は平和を促すが、それが武器化される（制裁・封鎖）と逆に摩擦を生む
        /// （<see cref="SanctionsRules"/> と接続）。依存が深いほど強制の痛打が大きい。
        /// </summary>
        public static float TradeWarRisk(float interdependence, float economicCoercion, CommerceModeratesWarParams p)
        {
            float inter = Mathf.Clamp01(interdependence);
            float coer = Mathf.Clamp01(economicCoercion);
            return Mathf.Clamp01(inter * coer * p.tradeWarWeight);
        }

        public static float TradeWarRisk(float interdependence, float economicCoercion)
            => TradeWarRisk(interdependence, economicCoercion, CommerceModeratesWarParams.Default);

        /// <summary>
        /// 商業が栄え温和な共和的国家か＝商業水準(0..1)が閾値以上、かつ専制でない
        /// （despotismLevel が閾値未満）。専制下では商業が栄えても温和な共和国にはならない。
        /// </summary>
        public static bool IsCommercialRepublic(float commerceLevel, float despotismLevel, CommerceModeratesWarParams p)
        {
            return Mathf.Clamp01(commerceLevel) >= p.commercialRepublicThreshold
                && Mathf.Clamp01(despotismLevel) < p.commercialRepublicThreshold;
        }

        public static bool IsCommercialRepublic(float commerceLevel, float despotismLevel)
            => IsCommercialRepublic(commerceLevel, despotismLevel, CommerceModeratesWarParams.Default);
    }
}
