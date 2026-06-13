using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍神＝数値の限界を超えて成長する提督の基盤（#軍神・上杉謙信型）。
    /// 孟子「天時・地利・人和」が<b>揃った</b>とき、軍神型（<see cref="AdmiralData.isTranscendent"/>）に限り
    /// 能力上限（<see cref="AdmiralData.MaxStatValue"/>=100）を超える。並の提督は何があっても100で頭打ち。
    /// 成長曲線の数式は <see cref="GrowthRules"/> を流用し（上限だけ引き上げる＝二重実装しない）、
    /// 一時バフの三密 <see cref="FocusRules"/>(#872) とは別軸（こちらは限界突破と登場の希少性）。実効値パターン・test-first。
    /// </summary>
    public static class TenchijinRules
    {
        // 整合（孟子の序列＝人和＞地利＞天時）を重みにした幾何平均（全要素必要＝一つ0で整合0）。
        public const float WeightHeaven = 0.25f; // 天の時
        public const float WeightEarth = 0.30f;  // 地の利
        public const float WeightPerson = 0.45f; // 人の和（最重視）

        /// <summary>天地人が「揃った」とみなす既定の整合しきい値（高め＝揃うのは稀）。</summary>
        public const float AlignmentThreshold = 0.8f;

        /// <summary>軍神が100を超えて到達できる最大の上乗せ点（整合最大時）。</summary>
        public const int TranscendStatBonus = 20;

        /// <summary>軍神の絶対上限（=100+上乗せ）。これ以上には決して伸びない。</summary>
        public const int TranscendCeiling = AdmiralData.MaxStatValue + TranscendStatBonus;

        /// <summary>天地人の整合度（0..1）。重み付き幾何平均＝一つでも欠けると崩れる（揃ってこそ）。</summary>
        public static float Alignment(Tenchijin t)
        {
            float h = Mathf.Clamp01(t.heaven);
            float e = Mathf.Clamp01(t.earth);
            float p = Mathf.Clamp01(t.person);
            if (h <= 0f || e <= 0f || p <= 0f) return 0f; // 一つでも欠ければ天地人は揃わない
            return Mathf.Clamp01(Mathf.Pow(h, WeightHeaven) * Mathf.Pow(e, WeightEarth) * Mathf.Pow(p, WeightPerson));
        }

        /// <summary>境界比較の許容誤差（pow を分けて掛けるため、等価要素でも僅かに誤差が出る＝しきい値ちょうどを取りこぼさない）。</summary>
        public const float AlignmentEpsilon = 1e-4f;

        /// <summary>天地人が揃ったか（整合がしきい値以上・浮動小数の境界を許容）。</summary>
        public static bool IsAligned(Tenchijin t, float threshold = AlignmentThreshold)
            => Alignment(t) >= threshold - AlignmentEpsilon;

        /// <summary>
        /// この提督・この天地人での能力上限。軍神型は 100 + round(上乗せ×整合) を 100..<see cref="TranscendCeiling"/> でクランプ。
        /// 並の提督・整合0なら 100（=従来）。
        /// </summary>
        public static int EffectiveCeiling(bool isTranscendent, float alignment)
        {
            if (!isTranscendent) return AdmiralData.MaxStatValue;
            int ceiling = AdmiralData.MaxStatValue + Mathf.RoundToInt(TranscendStatBonus * Mathf.Clamp01(alignment));
            return Mathf.Clamp(ceiling, AdmiralData.MaxStatValue, TranscendCeiling);
        }

        /// <summary>提督と天地人から能力上限を解く。</summary>
        public static int EffectiveCeiling(AdmiralData admiral, Tenchijin t)
            => EffectiveCeiling(admiral != null && admiral.isTranscendent, Alignment(t));

        /// <summary>
        /// 限界突破込みの実効能力＝基準 + 成長ボーナス（<see cref="GrowthRules"/> を上限指定で流用）。
        /// 軍神＋天地人が揃えば100超、並の提督・整合不足なら100で頭打ち（基準非破壊）。
        /// </summary>
        public static int EffectiveStat(int baseStat, Growth growth, AdmiralData admiral, Tenchijin t)
        {
            int ceiling = EffectiveCeiling(admiral, t);
            int bonus = GrowthRules.EffectiveStatBonus(growth, baseStat, ceiling);
            return Mathf.Clamp(baseStat, 0, ceiling) + bonus;
        }

        /// <summary>100を超えた分（軍神の超越量・0以上）。並の提督は常に0。</summary>
        public static int TranscendenceAmount(int baseStat, Growth growth, AdmiralData admiral, Tenchijin t)
            => Mathf.Max(0, EffectiveStat(baseStat, growth, admiral, t) - AdmiralData.MaxStatValue);

        /// <summary>
        /// 軍神（限界突破型）が登場・顕現する確率（天地人が揃って初めて＞0）。
        /// しきい値で0、整合最大で1へ線形（揃うほど現れやすい＝稀有）。
        /// </summary>
        public static float EmergenceLikelihood(float alignment, float threshold = AlignmentThreshold)
        {
            if (alignment < threshold) return 0f;
            if (threshold >= 1f) return alignment >= 1f ? 1f : 0f;
            return Mathf.Clamp01((alignment - threshold) / (1f - threshold));
        }

        /// <summary>軍神が登場するか（決定論＝roll∈[0,1) を外から注入）。天地人が揃わなければ現れない。</summary>
        public static bool Emerges(float alignment, float roll, float threshold = AlignmentThreshold)
            => roll < EmergenceLikelihood(alignment, threshold);
    }
}
