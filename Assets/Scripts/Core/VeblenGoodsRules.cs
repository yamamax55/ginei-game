using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 地位財（Veblen財）の純データ（VEBL-1 #1593・純データ）。宝飾・名馬・宮殿のように
    /// 「高くて他人が買えない」ことに価値がある財。希少性・威信付与を持つ。
    /// 逆需要曲線（価格が上がるほど需要が増える）の解決は <see cref="VeblenGoodsRules"/> が扱う。
    /// </summary>
    [Serializable]
    public struct StatusGood
    {
        /// <summary>基準価格（0..1 正規化＝この財の高価さ。負にはならない）。</summary>
        public float basePrice;
        /// <summary>排他性／希少性（0..1＝1ほど誰も持てない＝威信プレミアムが高い）。</summary>
        public float exclusivity;
        /// <summary>威信付与（0..1＝持つことで得られる地位の量）。</summary>
        public float prestigeValue;

        public StatusGood(float basePrice, float exclusivity, float prestigeValue)
        {
            this.basePrice = Mathf.Clamp01(basePrice);
            this.exclusivity = Mathf.Clamp01(exclusivity);
            this.prestigeValue = Mathf.Clamp01(prestigeValue);
        }
    }

    /// <summary>地位財の調整値（逆需要の強さ・誇示・希少プレミアム・スノッブ効果・最適価格の高さ）。top-level。</summary>
    public readonly struct VeblenGoodsParams
    {
        /// <summary>逆需要の強さ（価格1段の上昇が需要を増やす効き＝大きいほど高さが価値）。</summary>
        public readonly float veblenStrength;
        /// <summary>需要の基礎（価格0でも残る最低需要）。</summary>
        public readonly float baseDemand;
        /// <summary>誇示価値の係数（高価格×人目につく度の効き）。</summary>
        public readonly float conspicuousScale;
        /// <summary>希少プレミアムの効き（exclusivity を威信プレミアムに写す強さ）。</summary>
        public readonly float exclusivityScale;
        /// <summary>スノッブ効果の強さ（普及率が価値を削る効き＝みなが持つと地位財でなくなる）。</summary>
        public readonly float snobStrength;
        /// <summary>最適（高）価格の下限（地位財は高くあるべき＝最適価格はこの値以上）。</summary>
        public readonly float optimalFloor;

        public VeblenGoodsParams(float veblenStrength, float baseDemand, float conspicuousScale,
            float exclusivityScale, float snobStrength, float optimalFloor)
        {
            this.veblenStrength = Mathf.Max(0f, veblenStrength);
            this.baseDemand = Mathf.Clamp01(baseDemand);
            this.conspicuousScale = Mathf.Max(0f, conspicuousScale);
            this.exclusivityScale = Mathf.Max(0f, exclusivityScale);
            this.snobStrength = Mathf.Clamp01(snobStrength);
            this.optimalFloor = Mathf.Clamp01(optimalFloor);
        }

        /// <summary>
        /// 既定＝逆需要強さ0.8・基礎需要0.1・誇示係数1・希少効き1・スノッブ強さ0.8・最適価格下限0.6。
        /// 高価格そのものが需要を生むが、買い手の財力で頭打ちになる程度に調整。
        /// </summary>
        public static VeblenGoodsParams Default => new VeblenGoodsParams(0.8f, 0.1f, 1f, 1f, 0.8f, 0.6f);
    }

    /// <summary>
    /// 地位財と顕示的消費（conspicuous consumption・VEBL-1 #1593・ヴェブレン『有閑階級の理論』参考・純ロジック test-first）。
    /// 普通の財は安いほど売れる（右下がり需給＝<see cref="MarketRules"/>）が、地位財（宝飾・名馬・宮殿）は
    /// 「高くて他人が買えない」ことに価値がある＝高価格そのものが需要を生む逆需要曲線を持つ。
    /// 普及すると（みなが持つと）地位財でなくなる（スノッブ効果）。
    /// 分担：通常財の需給価格は <see cref="MarketRules"/>、模倣・追随の社会動学は <see cref="EmulationRules"/>（同EPIC）、
    /// 国家が授ける名誉の経済は <see cref="HonorsRules"/>（栄典）。ここは「価格が上がるほど需要が増える地位財」専用。
    /// 調整値は <see cref="VeblenGoodsParams"/> に集約（既定 <see cref="VeblenGoodsParams.Default"/>）。
    /// </summary>
    public static class VeblenGoodsRules
    {
        /// <summary>
        /// 地位財の逆需要（VEBL-1 #1593）：価格が高いほど需要が増える（普通財と逆）。
        /// 需要＝baseDemand＋veblenStrength×price×statusSensitivity を、買い手の財力で頭打ちにする
        /// （価格が高すぎると誰も買えない＝statusSensitivity が財力の代理＝高価格でも一定で飽和）。
        /// 戻り値は 0..1 の正規化需要。<see cref="MarketRules.ClearingPrice"/>（右下がり）とは逆の挙動。
        /// </summary>
        public static float VeblenDemand(float price, float statusSensitivity, VeblenGoodsParams p)
        {
            float pr = Mathf.Clamp01(price);
            float sens = Mathf.Clamp01(statusSensitivity);
            // 価格が高いほど需要が増す（逆需要）。地位への感応度が乗数＝財力なき層は反応しない。
            float lift = p.veblenStrength * pr * sens;
            // 財力で頭打ち：感応度が低いほど高価格で早く飽和（買えない）＝(1-sens) が抑制。
            float ceiling = Mathf.Lerp(p.baseDemand, 1f, sens);
            return Mathf.Clamp01(Mathf.Min(p.baseDemand + lift, ceiling));
        }

        /// <summary>
        /// 誇示価値（conspicuous value・#1593）：高価格×人目につく度で誇示の価値が出る（見られてこそ地位財）。
        /// visibility0（誰も見ない）なら高価でも誇示価値は0、visibility1（公然）で最大。価格に比例。
        /// </summary>
        public static float ConspicuousValue(float price, float visibility, VeblenGoodsParams p)
        {
            float pr = Mathf.Clamp01(price);
            float vis = Mathf.Clamp01(visibility);
            return Mathf.Clamp01(pr * vis * p.conspicuousScale);
        }

        /// <summary>
        /// 希少プレミアム（#1593）：希少なほど高い威信プレミアム（誰も持てないことが価値）。
        /// exclusivity を非線形（^1.5）に効かせ、極端な希少は跳ねる。0..1 を返す。
        /// </summary>
        public static float ExclusivityPremium(float exclusivity, VeblenGoodsParams p)
        {
            float ex = Mathf.Clamp01(exclusivity);
            return Mathf.Clamp01(Mathf.Pow(ex, 1.5f) * p.exclusivityScale);
        }

        /// <summary>
        /// スノッブ効果（snob effect・#1593）：みなが持つと価値が下がる（普及で地位財でなくなる）。
        /// 残る価値倍率＝1−snobStrength×adoptionRate。普及率0で1（誰も持たない＝最高）、
        /// 1で(1−snobStrength)（みなが持つ＝凡庸化）。地位財は希少ゆえに価値を持つことを表す。
        /// </summary>
        public static float SnobEffect(float adoptionRate, VeblenGoodsParams p)
        {
            float adopt = Mathf.Clamp01(adoptionRate);
            return Mathf.Clamp01(1f - p.snobStrength * adopt);
        }

        /// <summary>
        /// 地位シグナルの強さ（#1593）：財力に見合った地位の発信力＝価格×希少×財力で決まる。
        /// 財力（ownerWealth）に釣り合わぬ高価財は身の丈に合わず信号にならない＝price と ownerWealth の
        /// 釣り合い（min）が効く＝背伸びは見透かされる。希少なほど信号は強い。0..1 を返す。
        /// </summary>
        public static float StatusSignal(float price, float exclusivity, float ownerWealth, VeblenGoodsParams p)
        {
            float pr = Mathf.Clamp01(price);
            float ex = Mathf.Clamp01(exclusivity);
            float wealth = Mathf.Clamp01(ownerWealth);
            // 価格と財力の釣り合い＝背伸びした高価財は信号を弱める（min で抑える）。
            float afford = Mathf.Min(pr, wealth);
            float premium = ExclusivityPremium(ex, p);
            return Mathf.Clamp01(afford * (0.5f + 0.5f * premium) * (0.5f + 0.5f * wealth));
        }

        /// <summary>
        /// 需要を最大化する地位財の最適（高）価格（#1593）：地位財は高くあるべき。
        /// 感応度が高く買い手に財力（affordability）があるほど価格を高く設定できる＝optimalFloor〜1。
        /// 通常財の最適価格（限界費用近辺）と逆＝高さそのものが需要を呼ぶ。0..1 を返す。
        /// </summary>
        public static float OptimalVeblenPrice(float statusSensitivity, float affordability, VeblenGoodsParams p)
        {
            float sens = Mathf.Clamp01(statusSensitivity);
            float afford = Mathf.Clamp01(affordability);
            // 感応度×財力が高いほど高値へ。地位財ゆえ下限（optimalFloor）以上は確保する。
            float target = Mathf.Lerp(p.optimalFloor, 1f, sens * afford);
            return Mathf.Clamp01(Mathf.Max(p.optimalFloor, target));
        }

        /// <summary>
        /// 地位財判定（#1593）：価格弾力性が正（右上がり＝値上げで需要増）なら地位財（Veblen財）。
        /// 普通の財は弾力性が負（右下がり）＝安いほど売れる。0以上で true。
        /// </summary>
        public static bool IsVeblenGood(float priceElasticity)
        {
            return priceElasticity > 0f;
        }

        /// <summary>
        /// 模倣品の希釈（counterfeit dilution・#1593）：模倣品の流通が本物の威信を薄める。
        /// 本物の実効威信＝genuineExclusivity×(1−counterfeitShare)＝偽物が出回るほど「誰も持てない」が崩れる。
        /// counterfeitShare1（市場が偽物だらけ）で威信は0に近づく。0..1 を返す。
        /// </summary>
        public static float CounterfeitDilution(float genuineExclusivity, float counterfeitShare)
        {
            float genuine = Mathf.Clamp01(genuineExclusivity);
            float fake = Mathf.Clamp01(counterfeitShare);
            return Mathf.Clamp01(genuine * (1f - fake));
        }
    }
}
