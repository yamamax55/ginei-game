using UnityEngine;

namespace Ginei
{
    /// <summary>銃後（国民）の戦争支持＝世論を動かす調整係数。</summary>
    public readonly struct HomeFrontMoraleParams
    {
        /// <summary>平時の戦争支持の出発点（victory/casualty/hardship が無いときの基準・0..1）。</summary>
        public readonly float baseSupport;
        /// <summary>戦勝報道が支持を押し上げる強さ（戦勝規模1で baseSupport にこの分だけ上乗せ）。</summary>
        public readonly float victoryScale;
        /// <summary>損害報道が支持を削る強さ（損害1×感受性1でこの分だけ低下）。</summary>
        public readonly float casualtyScale;
        /// <summary>生活苦が支持を削る強さ（生活苦1でこの分だけ低下）。</summary>
        public readonly float hardshipScale;
        /// <summary>長期化による厭戦の蓄積速度（戦争継続時間あたり・per duration）。</summary>
        public readonly float wearinessRate;
        /// <summary>宣伝・報道統制が支持を維持できる上限（強度1×統制1でこの分だけ底上げ）。</summary>
        public readonly float propagandaScale;
        /// <summary>支持低下が反戦運動へ転化する強さ（不支持1×不満1でこの分まで高まる）。</summary>
        public readonly float antiwarScale;
        /// <summary>戦争支持が体制安定を支える比率（支持1でこの分の安定・0..1）。</summary>
        public readonly float stabilityScale;
        /// <summary>これを下回ると銃後崩壊とみなす既定の戦争支持閾値（0..1）。</summary>
        public readonly float collapseThreshold;

        public HomeFrontMoraleParams(float baseSupport, float victoryScale, float casualtyScale,
                                     float hardshipScale, float wearinessRate, float propagandaScale,
                                     float antiwarScale, float stabilityScale, float collapseThreshold)
        {
            this.baseSupport = Mathf.Clamp01(baseSupport);
            this.victoryScale = Mathf.Max(0f, victoryScale);
            this.casualtyScale = Mathf.Max(0f, casualtyScale);
            this.hardshipScale = Mathf.Max(0f, hardshipScale);
            this.wearinessRate = Mathf.Max(0f, wearinessRate);
            this.propagandaScale = Mathf.Max(0f, propagandaScale);
            this.antiwarScale = Mathf.Max(0f, antiwarScale);
            this.stabilityScale = Mathf.Clamp01(stabilityScale);
            this.collapseThreshold = Mathf.Clamp01(collapseThreshold);
        }

        /// <summary>既定＝基準0.5/戦勝0.5/損害0.4/生活苦0.3/厭戦0.02/宣伝0.5/反戦0.8/安定1/崩壊閾値0.25。</summary>
        public static HomeFrontMoraleParams Default =>
            new HomeFrontMoraleParams(0.5f, 0.5f, 0.4f, 0.3f, 0.02f, 0.5f, 0.8f, 1f, 0.25f);
    }

    /// <summary>
    /// 銃後の士気＝<b>国民の戦争支持・長期戦の世論</b>の純ロジック。戦争を支えるのは前線だけでなく
    /// 銃後（国民）で、戦勝・大義は支持を高め、損害・生活苦・長期化は支持を蝕む。支持が崩れると
    /// 厭戦・反戦運動・体制動揺へ。<see cref="FleetMorale"/>（軍＝前線の士気）とは別＝こちらは銃後
    /// （国民）の戦争支持。既存の <c>PoliticsState</c>/<c>FactionState</c>（政党支持率・国家状態）とも
    /// 別レイヤー＝<b>戦争世論</b>そのものを扱う。盤面非依存の plain 引数のみ・乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HomeFrontMoraleRules
    {
        /// <summary>
        /// 国民の戦争支持（0..1）＝基準＋戦勝報道による上昇−損害ショック−生活苦。
        /// 大義（戦勝報道）が支えとなり、損害・困窮が支持を削る合算。
        /// </summary>
        public static float WarSupport(float victoryNews, float casualtyNews, float economicHardship,
                                       HomeFrontMoraleParams p)
        {
            float support = p.baseSupport
                + p.victoryScale * Mathf.Clamp01(victoryNews)
                - p.casualtyScale * Mathf.Clamp01(casualtyNews)
                - p.hardshipScale * Mathf.Clamp01(economicHardship);
            return Mathf.Clamp01(support);
        }

        public static float WarSupport(float victoryNews, float casualtyNews, float economicHardship)
            => WarSupport(victoryNews, casualtyNews, economicHardship, HomeFrontMoraleParams.Default);

        /// <summary>
        /// 戦勝報道による支持上昇（0..1）＝戦勝規模×戦勝係数。
        /// 大勝の報道は銃後を熱狂させ戦争支持を押し上げる（規模はクランプ）。
        /// </summary>
        public static float VictoryBoost(float victoryMagnitude, HomeFrontMoraleParams p)
        {
            return Mathf.Clamp01(p.victoryScale * Mathf.Clamp01(victoryMagnitude));
        }

        public static float VictoryBoost(float victoryMagnitude)
            => VictoryBoost(victoryMagnitude, HomeFrontMoraleParams.Default);

        /// <summary>
        /// 損害が支持を削る量（0..1）＝損害×感受性×損害係数。
        /// 同じ損害でも国民の損害感受性が高いほど支持は強く削られる（戦死報道のショック）。
        /// </summary>
        public static float CasualtyShock(float casualties, float casualtySensitivity, HomeFrontMoraleParams p)
        {
            return Mathf.Clamp01(p.casualtyScale * Mathf.Clamp01(casualties) * Mathf.Clamp01(casualtySensitivity));
        }

        public static float CasualtyShock(float casualties, float casualtySensitivity)
            => CasualtyShock(casualties, casualtySensitivity, HomeFrontMoraleParams.Default);

        /// <summary>
        /// 長期化による厭戦の蓄積（0..1）＝戦争継続時間×厭戦速度。
        /// 戦争が長引くほど銃後の厭戦気分が積み上がる（支持を下げる側の係数）。
        /// </summary>
        public static float WarWearinessGrowth(float warDuration, HomeFrontMoraleParams p)
        {
            return Mathf.Clamp01(p.wearinessRate * Mathf.Max(0f, warDuration));
        }

        public static float WarWearinessGrowth(float warDuration)
            => WarWearinessGrowth(warDuration, HomeFrontMoraleParams.Default);

        /// <summary>
        /// 宣伝・報道統制で維持できる支持（0..1）＝宣伝強度×報道統制×宣伝係数。
        /// 強い宣伝も統制された報道環境が無ければ効かない（両方が要る積）。
        /// </summary>
        public static float PropagandaEffect(float propagandaIntensity, float mediaControl, HomeFrontMoraleParams p)
        {
            return Mathf.Clamp01(p.propagandaScale * Mathf.Clamp01(propagandaIntensity) * Mathf.Clamp01(mediaControl));
        }

        public static float PropagandaEffect(float propagandaIntensity, float mediaControl)
            => PropagandaEffect(propagandaIntensity, mediaControl, HomeFrontMoraleParams.Default);

        /// <summary>
        /// 反戦運動の高まり（0..1）＝不支持(1−戦争支持)×不満×反戦係数。
        /// 戦争支持が低いほど、かつ社会の不満が大きいほど反戦運動は強まる。
        /// </summary>
        public static float AntiwarMovement(float warSupport, float dissent, HomeFrontMoraleParams p)
        {
            float disaffection = 1f - Mathf.Clamp01(warSupport);
            return Mathf.Clamp01(p.antiwarScale * disaffection * Mathf.Clamp01(dissent));
        }

        public static float AntiwarMovement(float warSupport, float dissent)
            => AntiwarMovement(warSupport, dissent, HomeFrontMoraleParams.Default);

        /// <summary>
        /// 戦争支持が支える体制の安定（0..1）＝戦争支持×安定係数。
        /// 銃後が戦争を支持している間は体制も揺るがない（支持が崩れると安定も崩れる）。
        /// </summary>
        public static float RegimeStabilityFromSupport(float warSupport, HomeFrontMoraleParams p)
        {
            return Mathf.Clamp01(p.stabilityScale * Mathf.Clamp01(warSupport));
        }

        public static float RegimeStabilityFromSupport(float warSupport)
            => RegimeStabilityFromSupport(warSupport, HomeFrontMoraleParams.Default);

        /// <summary>
        /// 銃後が崩壊しつつあるか＝戦争支持が崩壊閾値を下回る（true＝厭戦が体制動揺へ）。
        /// </summary>
        public static bool IsHomeFrontCollapsing(float warSupport, float threshold)
        {
            return Mathf.Clamp01(warSupport) < Mathf.Clamp01(threshold);
        }

        public static bool IsHomeFrontCollapsing(float warSupport)
            => IsHomeFrontCollapsing(warSupport, HomeFrontMoraleParams.Default.collapseThreshold);
    }
}
