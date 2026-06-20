using UnityEngine;

namespace Ginei
{
    /// <summary>要衝価値の調整係数（重み・前線減衰・要衝閾値）。ctor で全値をクランプする。</summary>
    public readonly struct ChokepointParams
    {
        /// <summary>希少性（迂回路の少なさ）の重み。</summary>
        public readonly float scarcityWeight;
        /// <summary>経済流量の重み。</summary>
        public readonly float economyWeight;
        /// <summary>前線への近さの重み。</summary>
        public readonly float frontlineWeight;
        /// <summary>前線距離の減衰スケール。この距離で前線価値が半減する（大きいほど遠くまで価値が残る）。</summary>
        public readonly float frontlineFalloff;
        /// <summary>戦略的要衝とみなす総合価値の閾値（0..1）。</summary>
        public readonly float criticalThreshold;

        public ChokepointParams(float scarcityWeight, float economyWeight, float frontlineWeight,
                                float frontlineFalloff, float criticalThreshold)
        {
            this.scarcityWeight = Mathf.Max(0f, scarcityWeight);
            this.economyWeight = Mathf.Max(0f, economyWeight);
            this.frontlineWeight = Mathf.Max(0f, frontlineWeight);
            this.frontlineFalloff = Mathf.Max(0.01f, frontlineFalloff);
            this.criticalThreshold = Mathf.Clamp01(criticalThreshold);
        }

        /// <summary>既定＝希少性0.5・経済0.2・前線0.3／前線減衰2.0／要衝閾値0.7。</summary>
        public static ChokepointParams Default => new ChokepointParams(0.5f, 0.2f, 0.3f, 2f, 0.7f);
    }

    /// <summary>
    /// 要衝価値の純ロジック＝回廊の戦略価値を点数化する <b>AI・自動配備の判断材料</b>。
    /// 迂回路の有無（唯一の道＝最大）・経済流量・前線への近さを重み付き合成して 0..1 の総合価値を返し、
    /// 守備配分の優先度（価値高×手薄×脅威大が最優先）まで導く。イゼルローン回廊型
    /// （迂回路0・前線直結・全交易が通る）が満点になる設計。盤面（<see cref="GalaxyMap"/> 等）には
    /// 依存せず plain 引数で受ける＝迂回路本数の算出は呼び出し側（<see cref="GalaxyPathfinder"/> 等）の責務。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ChokepointValueRules
    {
        /// <summary>
        /// 希少性価値（0..1）＝迂回路の少なさ。0本（唯一の道）で最大1、1本で0.5、2本で1/3…と
        /// 本数で逓減する（1/(1+本数)）。負数は0本として扱う。
        /// </summary>
        public static float ScarcityValue(int alternativeRoutes)
        {
            int routes = Mathf.Max(0, alternativeRoutes);
            return 1f / (1f + routes);
        }

        /// <summary>経済価値（0..1）＝この回廊を通る交易流量。範囲外はクランプ。</summary>
        public static float EconomicValue(float tradeFlow)
        {
            return Mathf.Clamp01(tradeFlow);
        }

        /// <summary>
        /// 前線価値（0..1）＝前線への近さ。距離0（前線直結）で最大1、距離=falloff で0.5、
        /// 遠いほど逓減する（falloff/(falloff+距離)）。負距離は0として扱う。
        /// </summary>
        public static float FrontlineValue(float distanceToFront, float falloff)
        {
            float d = Mathf.Max(0f, distanceToFront);
            float f = Mathf.Max(0.01f, falloff);
            return f / (f + d);
        }

        /// <summary>
        /// 総合価値（0..1）＝希少性・経済・前線の重み付き平均（重み合計で正規化）。
        /// 重み合計が0なら0。イゼルローン型（迂回路0・流量1・距離0）で満点1になる。
        /// </summary>
        public static float TotalValue(int alternativeRoutes, float tradeFlow, float distanceToFront, ChokepointParams p)
        {
            float weightSum = p.scarcityWeight + p.economyWeight + p.frontlineWeight;
            if (weightSum <= 0f) return 0f;
            float sum = p.scarcityWeight * ScarcityValue(alternativeRoutes)
                      + p.economyWeight * EconomicValue(tradeFlow)
                      + p.frontlineWeight * FrontlineValue(distanceToFront, p.frontlineFalloff);
            return Mathf.Clamp01(sum / weightSum);
        }

        /// <summary>既定係数での総合価値（0..1）。</summary>
        public static float TotalValue(int alternativeRoutes, float tradeFlow, float distanceToFront)
            => TotalValue(alternativeRoutes, tradeFlow, distanceToFront, ChokepointParams.Default);

        /// <summary>
        /// 守備配分の優先度（0..1）＝総合価値×（手薄さ＋脅威の平均）。currentGarrison は守備充足度0..1
        /// （1=満員）、threat は脅威度0..1。価値が高く・手薄で・脅威が大きい回廊ほど最優先＝自動配備の並べ替えキー。
        /// </summary>
        public static float GarrisonPriority(float value, float currentGarrison, float threat)
        {
            float v = Mathf.Clamp01(value);
            float gap = 1f - Mathf.Clamp01(currentGarrison);
            float t = Mathf.Clamp01(threat);
            return Mathf.Clamp01(v * 0.5f * (gap + t));
        }

        /// <summary>戦略的要衝か＝総合価値が閾値以上（AIの重点防衛・攻略目標の判定）。</summary>
        public static bool IsCriticalChokepoint(float value, float threshold)
        {
            return Mathf.Clamp01(value) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="ChokepointParams.criticalThreshold"/>）での要衝判定。</summary>
        public static bool IsCriticalChokepoint(float value)
            => IsCriticalChokepoint(value, ChokepointParams.Default.criticalThreshold);
    }
}
