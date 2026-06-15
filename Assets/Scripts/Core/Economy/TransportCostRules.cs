using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 輸送コスト係数の調整値（CNTR-1 #1611）。回廊コストを距離・規格化・混雑から
    /// 合成するときの重み。<see cref="Default"/> で既定値。top-level（クラス外）の純データ。
    /// </summary>
    public readonly struct TransportCostParams
    {
        /// <summary>規格化が効かせる最大割引率（standardization=1 でこの割合だけ安くなる）。</summary>
        public readonly float standardizationDiscount;

        /// <summary>混雑が効かせる最大割増率（congestion=1 でこの割合だけ高くなる）。</summary>
        public readonly float congestionSurcharge;

        /// <summary>実効距離・連結重みの写像の感度（大きいほどコスト差が距離・重みに強く出る）。</summary>
        public readonly float weightSharpness;

        /// <summary>採算判定の既定しきい値（このコスト以下なら経済的）。</summary>
        public readonly float economicalThreshold;

        public TransportCostParams(float standardizationDiscount, float congestionSurcharge,
            float weightSharpness, float economicalThreshold)
        {
            this.standardizationDiscount = standardizationDiscount;
            this.congestionSurcharge = congestionSurcharge;
            this.weightSharpness = weightSharpness;
            this.economicalThreshold = economicalThreshold;
        }

        /// <summary>既定パラメータ（規格化で最大70%割引・混雑で最大100%割増・感度2・採算しきい値0.6）。</summary>
        public static TransportCostParams Default =>
            new TransportCostParams(0.7f, 1.0f, 2f, 0.6f);
    }

    /// <summary>
    /// 輸送コスト係数の純ロジック（CNTR-1 #1611・レビンソン『コンテナ物語』参考）。
    /// 回廊（エッジ）ごとの輸送コストを連続値化し、版図一体化の評価を
    /// 「つながっているか否か」の二値から「どれだけ安く運べるか」の連続値へ拡張する。
    /// コンテナ革命は輸送コストを劇的に下げて経済地理を書き換えた＝距離・規格・混雑で決まる
    /// 回廊コストが、版図の<b>実効的な一体化度（加重連結）</b>を左右する。
    /// 分担：<see cref="LogisticsRules"/>＝所有星系の連結成分（つながっているか否かの二値の一体化度）／
    /// TransshipmentRules（同 EPIC）＝ハブ経由の積み替え／<see cref="Corridor"/>＝回廊データ（length/type）。
    /// 本クラスは<b>回廊ごとの連続コストによる加重一体化</b>を式に出す。乱数なし決定論。test-first。
    /// </summary>
    public static class TransportCostRules
    {
        /// <summary>
        /// 回廊コスト＝距離 ×（規格化で割引）×（混雑で割増）。
        /// distance(0..1)が基準、standardization(0..1)で安く、congestion(0..1)で高くなる。
        /// 規格化が進み混雑が低い回廊ほど安く運べる（コンテナ化の効能）。
        /// </summary>
        public static float CorridorCost(float distance, float standardization, float congestion,
            TransportCostParams p)
        {
            float d = Mathf.Clamp01(distance);
            float std = Mathf.Clamp01(standardization);
            float cong = Mathf.Clamp01(congestion);
            float discount = 1f - p.standardizationDiscount * std;   // 規格化で割引
            float surcharge = 1f + p.congestionSurcharge * cong;     // 混雑で割増
            return d * discount * surcharge;
        }

        /// <summary>既定パラメータ版の回廊コスト。</summary>
        public static float CorridorCost(float distance, float standardization, float congestion)
            => CorridorCost(distance, standardization, congestion, TransportCostParams.Default);

        /// <summary>
        /// コストを実効距離へ写す（安い回廊は近い）。感度 weightSharpness で非線形に強調する。
        /// コストが低いほど実効距離が縮む＝経済地理上は隣り合う。
        /// </summary>
        public static float EffectiveDistance(float corridorCost, TransportCostParams p)
        {
            float c = Mathf.Max(0f, corridorCost);
            return Mathf.Pow(c, p.weightSharpness);
        }

        /// <summary>既定パラメータ版の実効距離。</summary>
        public static float EffectiveDistance(float corridorCost)
            => EffectiveDistance(corridorCost, TransportCostParams.Default);

        /// <summary>
        /// 連結の重み 0..1＝コストが低いほど高い。<see cref="LogisticsRules"/> の二値連結を連続化する重み。
        /// コスト0で重み1（自由に運べる）、コストが上がるほど重みが減る。
        /// </summary>
        public static float ConnectionWeight(float corridorCost, TransportCostParams p)
        {
            float c = Mathf.Max(0f, corridorCost);
            float w = 1f - Mathf.Pow(c, p.weightSharpness);
            return Mathf.Clamp01(w);
        }

        /// <summary>既定パラメータ版の連結重み。</summary>
        public static float ConnectionWeight(float corridorCost)
            => ConnectionWeight(corridorCost, TransportCostParams.Default);

        /// <summary>
        /// 加重一体化度 0..1＝回廊重みの平均。安い回廊が多いほど一体化が強い。
        /// totalWeight は各回廊の <see cref="ConnectionWeight"/> の総和、edgeCount は回廊数。
        /// 二値の連結成分（LogisticsRules）に対し、コストで重み付けした実効一体化を出す。
        /// </summary>
        public static float WeightedCohesion(float totalWeight, int edgeCount)
        {
            if (edgeCount <= 0) return 0f;
            return Mathf.Clamp01(totalWeight / edgeCount);
        }

        /// <summary>
        /// 経路上の回廊コスト合計（安い回廊を辿るほど総コストが小さい）。
        /// null/空は0（運ぶ回廊が無い）。負のコストは0へ丸める。手書きループ。
        /// </summary>
        public static float CostToTraverse(float[] corridorCosts)
        {
            if (corridorCosts == null || corridorCosts.Length == 0) return 0f;
            float total = 0f;
            for (int i = 0; i < corridorCosts.Length; i++)
            {
                total += Mathf.Max(0f, corridorCosts[i]);
            }
            return total;
        }

        /// <summary>
        /// 規格化前後のコスト低減率 0..1＝コンテナ革命の効果。
        /// beforeCost に規格化 afterStandardization(0..1) を効かせ、どれだけ安くなったかを割合で返す。
        /// 規格化が進むほど低減率が上がる（規格が揃うほど積み替えが消えて安くなる）。
        /// </summary>
        public static float ContainerizationGain(float beforeCost, float afterStandardization,
            TransportCostParams p)
        {
            float before = Mathf.Max(0f, beforeCost);
            if (before <= 0f) return 0f;
            float std = Mathf.Clamp01(afterStandardization);
            float after = before * (1f - p.standardizationDiscount * std);
            float gain = (before - after) / before;
            return Mathf.Clamp01(gain);
        }

        /// <summary>既定パラメータ版のコンテナ化低減率。</summary>
        public static float ContainerizationGain(float beforeCost, float afterStandardization)
            => ContainerizationGain(beforeCost, afterStandardization, TransportCostParams.Default);

        /// <summary>
        /// 通行プレミアム＝迂回路が少ないほど高い（チョークポイントの上乗せ）。
        /// alternativeCount は代替経路の本数。0本なら最大の上乗せ（唯一の道）、本数が増えるほど薄まる。
        /// </summary>
        public static float ChokepointPremium(float corridorCost, int alternativeCount)
        {
            float c = Mathf.Max(0f, corridorCost);
            int alt = Mathf.Max(0, alternativeCount);
            float scarcity = 1f / (1f + alt);   // 迂回路が少ないほど1へ
            return c * scarcity;
        }

        /// <summary>
        /// 採算に乗る回廊か＝コストがしきい値以下なら経済的。
        /// 安い回廊だけを版図の実効連結に数えたいときの足切り。
        /// </summary>
        public static bool IsEconomical(float corridorCost, float threshold)
        {
            return Mathf.Max(0f, corridorCost) <= Mathf.Max(0f, threshold);
        }

        /// <summary>既定しきい値版の採算判定。</summary>
        public static bool IsEconomical(float corridorCost)
            => IsEconomical(corridorCost, TransportCostParams.Default.economicalThreshold);
    }
}
