using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 国全体の労働市場ロジック（#1957 LABM・純ロジック・唯一の窓口）。労働供給（生産年齢人口×労働参加率）と労働需要
    /// （企業 #1022 の雇用枠）をマッチングして就業者・失業率を測り、景気（GDP需給ギャップ #1951）・インフレ（#1945）・
    /// 支持（#113）へ波及させる：LAB-1 労働力人口／LAB-2 就業と求人／LAB-3 失業率と種類／LAB-4 オークンの法則／
    /// LAB-5 フィリップス曲線。既存の <see cref="OccupationRules"/>(#110)・<see cref="EnterpriseRules"/>(#1022)・
    /// <see cref="GdpRules"/>(#1951)・<see cref="MonetaryPolicyRules"/>(#1945) へ接続（read-only/接続のみ）。マクロ近似。test-first。
    /// </summary>
    public static class LaborRules
    {
        /// <summary>既定の労働参加率（生産年齢人口のうち働く意思のある割合）。</summary>
        public const float DefaultParticipationRate = 0.65f;

        /// <summary>自然失業率（摩擦的＋構造的＝好況でも残る最低限の失業）。</summary>
        public const float NaturalUnemploymentRate = 0.04f;

        /// <summary>オークン係数（失業率1ポイント超過＝GDPギャップ何%か。既定2.0）。</summary>
        public const float OkunCoefficient = 2f;

        /// <summary>フィリップス曲線の感応度（失業ギャップ1ポイント＝インフレ何ポイント動くか）。</summary>
        public const float PhillipsSensitivity = 0.5f;

        /// <summary>高失業の不満が満タンになる失業率の超過幅（自然失業率比＝この超過で支持ペナルティ最大）。</summary>
        public const float DiscontentScale = 0.2f;

        // ===== LAB-1 労働力人口と労働参加率 =====

        /// <summary>労働力人口＝生産年齢人口×労働参加率（就業者＋失業者＝働く意思のある人）。</summary>
        public static float LaborForce(float workingAge, float participationRate)
            => Mathf.Max(0f, workingAge) * Mathf.Clamp01(participationRate);

        /// <summary>労働参加率＝労働力人口(就業＋失業)/生産年齢人口（逆算）。生産年齢0以下は0。</summary>
        public static float ParticipationRate(float employed, float unemployed, float workingAge)
            => workingAge <= 0f ? 0f : (Mathf.Max(0f, employed) + Mathf.Max(0f, unemployed)) / workingAge;

        /// <summary>星系群から生産年齢人口を集計（<see cref="OccupationRules.WorkingAge"/> #110 を合算）。</summary>
        public static float AggregateWorkingAge(IReadOnlyList<Province> provinces)
        {
            if (provinces == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < provinces.Count; i++)
                if (provinces[i] != null) sum += OccupationRules.WorkingAge(provinces[i]);
            return sum;
        }

        // ===== LAB-2 就業者と求人 =====

        /// <summary>企業群の雇用枠＝就業者の合計（<see cref="Enterprise.employees"/> #1022 を合算＝労働需要）。</summary>
        public static float AggregateJobs(IReadOnlyList<Enterprise> enterprises)
        {
            if (enterprises == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < enterprises.Count; i++)
                if (enterprises[i] != null) sum += Mathf.Max(0f, enterprises[i].employees);
            return sum;
        }

        /// <summary>就業者＝労働力人口と求人（雇用枠）の小さい方（求人が余れば全員就業＝人手不足）。</summary>
        public static float Employed(float laborForce, float jobsAvailable)
            => Mathf.Min(Mathf.Max(0f, laborForce), Mathf.Max(0f, jobsAvailable));

        /// <summary>失業者＝労働力人口−就業者（求職中で職に就けない人）。負にならない。</summary>
        public static float Unemployed(float laborForce, float employed)
            => Mathf.Max(0f, Mathf.Max(0f, laborForce) - Mathf.Max(0f, employed));

        // ===== LAB-3 失業率と種類 =====

        /// <summary>失業率＝失業者/労働力人口（0..1）。労働力0以下は0。</summary>
        public static float UnemploymentRate(float unemployed, float laborForce)
            => laborForce <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, unemployed) / laborForce);

        /// <summary>就業率＝就業者/労働力人口（0..1）。</summary>
        public static float EmploymentRate(float employed, float laborForce)
            => laborForce <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, employed) / laborForce);

        /// <summary>循環的失業＝実際の失業率−自然失業率（不況ぶん。マイナスは過熱＝人手不足）。</summary>
        public static float CyclicalUnemployment(float actualRate, float naturalRate)
            => actualRate - naturalRate;

        /// <summary>完全雇用か＝失業率が自然失業率以下（循環的失業がゼロ＝景気は天井）。</summary>
        public static bool IsFullEmployment(float rate, float naturalRate)
            => rate <= naturalRate + 1e-6f;

        // ===== LAB-4 オークンの法則 =====

        /// <summary>
        /// オークンの法則：失業率からGDP需給ギャップを推定＝−k(u−u*)。失業が自然失業率を超えるほどGDPは潜在を下回る（不況）。
        /// <see cref="GdpRules.OutputGap"/>（#1951 GDP-3）と同じ符号（プラス＝過熱・マイナス＝不況）。
        /// </summary>
        public static float OutputGapFromUnemployment(float unemploymentRate, float naturalRate, float okun)
            => -okun * (unemploymentRate - naturalRate);

        /// <summary>オークンの法則の逆：GDP需給ギャップから失業率を推定＝u* − gap/k。ギャップがプラス（過熱）ほど失業は自然率を下回る。okun0以下は自然率。</summary>
        public static float UnemploymentFromGap(float outputGap, float naturalRate, float okun)
            => okun <= 0f ? naturalRate : Mathf.Max(0f, naturalRate - outputGap / okun);

        // ===== LAB-5 フィリップス曲線 =====

        /// <summary>
        /// フィリップス曲線：失業率からインフレ率を推定＝期待インフレ−s(u−u*)。失業が自然失業率を下回る（人手不足）と
        /// 賃金・物価が上がる（インフレ↑）＝中央銀行（#1945 CB-2）の政策ジレンマ。
        /// </summary>
        public static float PhillipsInflation(float unemploymentRate, float naturalRate, float expectedInflation, float sensitivity)
            => expectedInflation - sensitivity * (unemploymentRate - naturalRate);

        /// <summary>
        /// 高失業の支持ペナルティ（0..1）＝失業率が自然失業率をどれだけ超えたかを <see cref="DiscontentScale"/> で正規化。
        /// 失業は国民の不満（支持 #113 低下）の源。実効値パターン（基準非破壊）。
        /// </summary>
        public static float SupportPenalty(float unemploymentRate, float naturalRate)
            => Mathf.Clamp01(Mathf.Max(0f, unemploymentRate - naturalRate) / DiscontentScale);
    }
}
