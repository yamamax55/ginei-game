using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 福祉支出が貧困を減らし民心（希望）を高める純ロジック（逓減効果＝diminishing returns）。
    /// 1人あたり支出からカバー率を導き、平方根で逓減させて貧困削減・希望ボーナスを返す。
    /// （FiscalRules.WelfareHopeBonus を補完するが呼ばない＝独立・自己完結。）
    /// 自己完結＝float のみで他ゲーム型に依存しない。決定論・clamp 済み・test-first。
    /// </summary>
    public static class WelfareProvisionRules
    {
        // ゼロ割り回避用の最小人口（const＝マジックナンバー禁止）。
        private const float Epsilon = 1e-6f;

        /// <summary>福祉モデルの調整値。</summary>
        public readonly struct Params
        {
            /// <summary>1人あたり支出→カバー率への変換効率。</summary>
            public readonly float coverageEfficiency;
            /// <summary>貧困削減の上限割合（0..1）。</summary>
            public readonly float maxPovertyReduction;
            /// <summary>カバー率→希望ボーナスへの重み。</summary>
            public readonly float hopeWeight;

            public Params(float coverageEfficiency, float maxPovertyReduction, float hopeWeight)
            {
                this.coverageEfficiency = coverageEfficiency;
                this.maxPovertyReduction = maxPovertyReduction;
                this.hopeWeight = hopeWeight;
            }

            /// <summary>既定値（変換効率1.0・貧困削減上限0.6・希望重み0.5）。</summary>
            public static Params Default => new Params(1f, 0.6f, 0.5f);
        }

        /// <summary>
        /// カバー率＝clamp01(1人あたり支出 × coverageEfficiency)。人口でゼロ割りしないよう Epsilon でガード。
        /// 支出が増えるほど上がり、人口が増えるほど下がる。
        /// </summary>
        public static float Coverage(float welfareSpend, float population, Params p)
        {
            float perCapita = welfareSpend / Mathf.Max(Epsilon, population);
            return Mathf.Clamp01(perCapita * p.coverageEfficiency);
        }

        /// <summary>カバー率（既定値）。</summary>
        public static float Coverage(float welfareSpend, float population)
            => Coverage(welfareSpend, population, Params.Default);

        /// <summary>貧困削減＝maxPovertyReduction × sqrt(clamp01(カバー率))。平方根で逓減。</summary>
        public static float PovertyReduction(float coverage01, Params p)
            => p.maxPovertyReduction * Mathf.Sqrt(Mathf.Clamp01(coverage01));

        /// <summary>貧困削減（既定値）。</summary>
        public static float PovertyReduction(float coverage01)
            => PovertyReduction(coverage01, Params.Default);

        /// <summary>希望ボーナス＝clamp01(カバー率 × hopeWeight)。</summary>
        public static float HopeBonus(float coverage01, Params p)
            => Mathf.Clamp01(coverage01 * p.hopeWeight);

        /// <summary>希望ボーナス（既定値）。</summary>
        public static float HopeBonus(float coverage01)
            => HopeBonus(coverage01, Params.Default);
    }
}
