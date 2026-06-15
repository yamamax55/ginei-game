using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 資本の純ロジック（ピケティ r>g・#917・唯一の窓口）。資本収益率 r が成長率 g を上回る分だけ富が集中し（<see cref="ConcentrationDrift"/>）、
    /// 累進資本課税で集中を押し下げ（<see cref="CapitalTaxEffect"/>）、格差が広がるほど反乱圧力が増す（<see cref="InequalityUnrest"/>）。
    /// 係数は #106・実効値（基準フィールド非破壊＝Tick 以外は元 state を書き換えない）。値は常に Clamp。test-first。
    /// </summary>
    public static class CapitalRules
    {
        /// <summary>調整値（マジックナンバー回避）。`Default` を既定に使う。</summary>
        [System.Serializable]
        public struct Params
        {
            [Tooltip("r-g 1ポイントあたりの集中ドリフト速度（per 単位時間）")]
            public float driftRate;

            [Tooltip("累進資本課税 1ポイントあたりの集中低下量")]
            public float taxReductionRate;

            [Tooltip("集中が反乱圧力に効き始める閾値（0..1）")]
            public float unrestThreshold;

            [Tooltip("閾値超過分→反乱圧力の感度")]
            public float unrestScale;

            public static Params Default => new Params
            {
                driftRate = 0.5f,
                taxReductionRate = 0.5f,
                unrestThreshold = 0.5f,
                unrestScale = 1.5f,
            };
        }

        /// <summary>集中ドリフト＝(r−g) に比例した集中の増減量（正＝r>g で集中↑・負＝g>r で集中↓）。state は書き換えない。</summary>
        public static float ConcentrationDrift(CapitalState s, float dt, Params p)
        {
            if (s == null) return 0f;
            float gap = s.capitalReturn - s.growthRate;
            return gap * Mathf.Max(0f, p.driftRate) * Mathf.Max(0f, dt);
        }

        /// <summary>累進資本課税の集中低下効果＝累進率に比例して下がる集中量（非負）。state は書き換えない。</summary>
        public static float CapitalTaxEffect(float progressiveRate, Params p)
            => Mathf.Max(0f, Mathf.Clamp01(progressiveRate) * Mathf.Max(0f, p.taxReductionRate));

        /// <summary>1tick の集中更新＝ドリフト−累進課税効果。`wealthConcentration` を 0..1 にクランプして書き戻す。</summary>
        public static void Tick(CapitalState s, float progressiveRate, float dt, Params p)
        {
            if (s == null) return;
            float delta = ConcentrationDrift(s, dt, p) - CapitalTaxEffect(progressiveRate, p) * Mathf.Max(0f, dt);
            s.wealthConcentration = Mathf.Clamp01(s.wealthConcentration + delta);
        }

        /// <summary>格差→反乱圧力（0..1）。閾値を超えた集中分だけ線形に増える。内部勢力#113・反乱#109 の火種。</summary>
        public static float InequalityUnrest(float wealthConcentration)
            => InequalityUnrest(wealthConcentration, Params.Default);

        /// <summary>格差→反乱圧力（0..1）。`unrestThreshold` 超過分×`unrestScale` をクランプ。</summary>
        public static float InequalityUnrest(float wealthConcentration, Params p)
        {
            float over = Mathf.Clamp01(wealthConcentration) - Mathf.Clamp01(p.unrestThreshold);
            if (over <= 0f) return 0f;
            return Mathf.Clamp01(over * Mathf.Max(0f, p.unrestScale));
        }
    }
}
