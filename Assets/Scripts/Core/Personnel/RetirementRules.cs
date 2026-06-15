using UnityEngine;

namespace Ginei
{
    /// <summary>軍人の在職状態（現役→予備役→退役の一方向ライフサイクル）。</summary>
    public enum ServiceStatus
    {
        現役,
        予備役,
        退役,
    }

    /// <summary>
    /// 軍人の退役ライフサイクルの純ロジック（#530-536・米軍＋明治日本軍モデル・test-first）。
    /// 階級別の<b>停年（定年退役）</b>と<b>アップ・オア・アウト</b>（昇進停滞で予備役編入）を、年齢と階級 tier から
    /// 決定論的に解決する。最上位（元帥 tier）は<b>終身</b>＝停年の例外。予備役/退役者は年齢上限内なら戦時召集できる。
    /// 在役年数で恩給係数を出す。基準値非破壊（実効値パターン）。<see cref="RankSystem"/> は read-only 参照。
    /// </summary>
    public static class RetirementRules
    {
        /// <summary>退役判定の調整値（停年・在級上限・召集年齢上限・元帥 tier）。</summary>
        public readonly struct RetireParams
        {
            public readonly int retireAgeTier5;   // tier5（准将級）の停年
            public readonly int retireAgeTier6;   // tier6（少将級）の停年
            public readonly int retireAgeTier7;   // tier7（中将級）の停年
            public readonly int retireAgeTier8;   // tier8（大将級）の停年
            public readonly int maxYearsInGrade;  // 在級年数の上限（超で予備役＝アップ・オア・アウト）
            public readonly int recallMaxAge;     // 戦時召集の年齢上限
            public readonly int marshalTier;      // 終身扱いの最上位 tier（元帥）
            public readonly int fullPensionYears; // 満額恩給に要する在役年数

            public RetireParams(int retireAgeTier5, int retireAgeTier6, int retireAgeTier7, int retireAgeTier8,
                int maxYearsInGrade, int recallMaxAge, int marshalTier, int fullPensionYears)
            {
                this.retireAgeTier5 = retireAgeTier5;
                this.retireAgeTier6 = retireAgeTier6;
                this.retireAgeTier7 = retireAgeTier7;
                this.retireAgeTier8 = retireAgeTier8;
                this.maxYearsInGrade = Mathf.Max(1, maxYearsInGrade);
                this.recallMaxAge = recallMaxAge;
                this.marshalTier = marshalTier;
                this.fullPensionYears = Mathf.Max(1, fullPensionYears);
            }

            /// <summary>既定＝停年 tier5:50/tier6:54/tier7:58/tier8:62、在級上限5年、召集上限65歳、元帥tier10、満額恩給30年。</summary>
            public static RetireParams Default => new RetireParams(50, 54, 58, 62, 5, 65, 10, 30);
        }

        /// <summary>
        /// 階級別の停年（定年退役年齢）。低 tier ほど早く退く（米軍 grade-based mandatory retirement）。
        /// tier5 以下は tier5 の停年、tier8 以上（元帥含む）は tier8 の停年へ丸める（停年の上限）。
        /// </summary>
        public static int MandatoryRetirementAge(int rankTier, RetireParams p)
        {
            if (rankTier <= 5) return p.retireAgeTier5;
            switch (rankTier)
            {
                case 6: return p.retireAgeTier6;
                case 7: return p.retireAgeTier7;
                default: return p.retireAgeTier8; // 8 以上は大将級の停年
            }
        }

        /// <summary>年齢が停年に達したか（終身＝元帥 tier は対象外で常に false）。</summary>
        public static bool ShouldRetireByAge(int age, int rankTier, RetireParams p)
        {
            if (IsMarshalTenure(rankTier, p)) return false; // 元帥は終身
            return age >= MandatoryRetirementAge(rankTier, p);
        }

        /// <summary>
        /// アップ・オア・アウト：在級年数が上限を超えたら（昇進停滞）予備役へ編入すべきか。
        /// 元帥 tier は対象外（終身＝据え置き）。
        /// </summary>
        public static bool ShouldUpOrOut(int yearsInGrade, int rankTier, RetireParams p)
        {
            if (IsMarshalTenure(rankTier, p)) return false;
            return yearsInGrade > p.maxYearsInGrade;
        }

        /// <summary>最上位（元帥 tier）は終身＝停年/アップ・オア・アウトの例外か。</summary>
        public static bool IsMarshalTenure(int rankTier, RetireParams p)
        {
            return rankTier >= p.marshalTier;
        }

        /// <summary>予備役/退役者を戦時召集できるか（現役は対象外。年齢上限内のみ）。</summary>
        public static bool CanRecall(ServiceStatus status, int age, RetireParams p)
        {
            if (status == ServiceStatus.現役) return false; // 既に現役
            return age <= p.recallMaxAge;
        }

        /// <summary>
        /// 在役年数に応じた恩給係数（0..1）。満額在役年数で 1.0、それ未満は比例、超過しても 1.0 でクランプ。
        /// 在役年数が負なら 0。
        /// </summary>
        public static float PensionFactor(int yearsOfService, RetireParams p)
        {
            if (yearsOfService <= 0) return 0f;
            return Mathf.Clamp01((float)yearsOfService / p.fullPensionYears);
        }
    }
}
