using UnityEngine;

namespace Ginei
{
    /// <summary>不満の動態の調整値（PER-PROPS A3）。</summary>
    public readonly struct GrievanceParams
    {
        /// <summary>冷遇1回あたりの不満増（点）。</summary>
        public readonly float slightGain;
        /// <summary>厚遇・時間での減衰（点/年）。</summary>
        public readonly float decayPerYear;
        /// <summary>これ以上で離反を検討（0..100）。</summary>
        public readonly int defectThreshold;
        /// <summary>功名心が離反傾性へ乗る重み（0..1）。</summary>
        public readonly float ambitionWeight;

        public GrievanceParams(float slightGain, float decayPerYear, int defectThreshold, float ambitionWeight)
        {
            this.slightGain = slightGain;
            this.decayPerYear = decayPerYear;
            this.defectThreshold = defectThreshold;
            this.ambitionWeight = ambitionWeight;
        }

        /// <summary>既定＝冷遇+15／年-8減衰／離反検討60／功名重み0.5。</summary>
        public static GrievanceParams Default => new GrievanceParams(15f, 8f, 60, 0.5f);
    }

    /// <summary>
    /// 人物の不満（grievance 0..100）の動態（PER-PROPS A3）。冷遇・降格・恩賞不足で溜まり、厚遇・時間で減る。
    /// <b>静的な忠誠</b>（<see cref="AllegianceDriftRules"/> の personalLoyalty）に対し、<b>蓄積する遺恨の記憶</b>を別軸に持ち、
    /// 高い不満×高い功名心が離反・出奔の傾性になる。離反の実行判定は <see cref="AllegianceDriftRules.WouldDefect"/> へ
    /// 橋渡し（ここは不満値と離反傾性 0..1 を返すだけ＝再実装しない）。決定論・test-first。
    /// </summary>
    public static class PersonGrievanceRules
    {
        /// <summary>冷遇 slights 回ぶん不満を積む（0..100 でクランプ）。</summary>
        public static int Accumulate(int current, int slights, GrievanceParams p)
            => Mathf.Clamp(current + Mathf.RoundToInt(Mathf.Max(0, slights) * p.slightGain), 0, 100);

        public static int Accumulate(int current, int slights) => Accumulate(current, slights, GrievanceParams.Default);

        /// <summary>years 年ぶん不満を減衰（0..100 でクランプ）。厚遇・時間で和らぐ。</summary>
        public static int Decay(int current, float years, GrievanceParams p)
            => Mathf.Clamp(current - Mathf.RoundToInt(Mathf.Max(0f, years) * p.decayPerYear), 0, 100);

        public static int Decay(int current, float years) => Decay(current, years, GrievanceParams.Default);

        /// <summary>離反傾性 0..1＝不満を正規化し、功名心で増幅（功名の士ほど不満が離反に直結）。</summary>
        public static float DefectionPropensity(int grievance, int ambition, GrievanceParams p)
        {
            float g = Mathf.Clamp01(grievance / 100f);
            float a = Mathf.Clamp01(ambition / 100f) * Mathf.Clamp01(p.ambitionWeight);
            return Mathf.Clamp01(g * (1f + a));
        }

        public static float DefectionPropensity(int grievance, int ambition)
            => DefectionPropensity(grievance, ambition, GrievanceParams.Default);

        /// <summary>離反を検討すべき不満水準に達したか（しきい値以上）。実行は AllegianceDriftRules へ。</summary>
        public static bool ShouldConsiderDefection(int grievance, GrievanceParams p) => grievance >= p.defectThreshold;

        public static bool ShouldConsiderDefection(int grievance) => ShouldConsiderDefection(grievance, GrievanceParams.Default);
    }
}
