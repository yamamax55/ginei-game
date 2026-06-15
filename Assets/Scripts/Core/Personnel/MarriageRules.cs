using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 政略結婚・血縁外交の純ロジック（PDX-2 #647・唯一の窓口）。
    /// 婚姻による同盟結束の底上げ(<see cref="AllianceBond"/>)・請求権の世代減衰(<see cref="ClaimInheritance"/>)・
    /// 婚姻が生む関係値ボーナス(<see cref="MarriageOpinionBonus"/>)を集約する。データは <see cref="MarriageAlliance"/>。
    /// 細かな家系管理は持たず（タイクン化回避）、高位の帰結（結束・請求・好感）を扱う。
    /// 実効値パターン（基準非破壊・ローカルに倍率計算）。純ロジック・test-first。
    /// </summary>
    public static class MarriageRules
    {
        /// <summary>政略結婚の調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct MarriageParams
        {
            public readonly float baseBond;        // 婚姻同盟の基準結束 0..1
            public readonly float claimBondWeight; // 請求権が結束へ寄与する重み（利害の一致）
            public readonly float inheritanceDecay; // 1世代あたりの請求権減衰率 0..1
            public readonly float opinionBonus;    // 婚姻による関係値（opinion）加算

            public MarriageParams(float baseBond, float claimBondWeight, float inheritanceDecay, float opinionBonus)
            {
                this.baseBond = Mathf.Clamp01(baseBond);
                this.claimBondWeight = Mathf.Max(0f, claimBondWeight);
                this.inheritanceDecay = Mathf.Clamp01(inheritanceDecay);
                this.opinionBonus = Mathf.Max(0f, opinionBonus);
            }

            /// <summary>既定＝基準結束0.5・請求重み0.3・世代減衰0.5・関係+20。</summary>
            public static MarriageParams Default => new MarriageParams(0.5f, 0.3f, 0.5f, 20f);
        }

        /// <summary>
        /// 婚姻による同盟結束 0..1＝基準結束＋請求権×重み。請求権が強いほど利害が一致し結束が高まる。
        /// 自己婚姻（同じ家同士・null/空の片側）は同盟にならず結束0。
        /// </summary>
        public static float AllianceBond(MarriageAlliance m, MarriageParams p)
        {
            if (m == null) return 0f;
            if (string.IsNullOrEmpty(m.houseA) || string.IsNullOrEmpty(m.houseB)) return 0f;
            if (m.houseA == m.houseB) return 0f;
            float claim = Mathf.Clamp01(m.claimStrength);
            return Mathf.Clamp01(p.baseBond + claim * p.claimBondWeight);
        }

        /// <summary>既定パラメータで同盟結束を算出。</summary>
        public static float AllianceBond(MarriageAlliance m) => AllianceBond(m, MarriageParams.Default);

        /// <summary>
        /// 請求権の世代継承＝generations 世代ぶん減衰した請求権強度 0..1。
        /// generations=0 は当代＝減衰なし。各世代で (1-decay) 倍に希薄化（遠い血縁ほど名分が弱まる）。
        /// 負の世代数は0へ丸める。
        /// </summary>
        public static float ClaimInheritance(float claimStrength, int generations, MarriageParams p)
        {
            float claim = Mathf.Clamp01(claimStrength);
            int gen = Mathf.Max(0, generations);
            float retained = Mathf.Pow(1f - p.inheritanceDecay, gen);
            return Mathf.Clamp01(claim * retained);
        }

        /// <summary>既定パラメータで請求権の世代継承を算出。</summary>
        public static float ClaimInheritance(float claimStrength, int generations)
            => ClaimInheritance(claimStrength, generations, MarriageParams.Default);

        /// <summary>婚姻が生む関係値（opinion）ボーナス（外交 #189 の OpinionFactors.marriageTie 相当）。</summary>
        public static float MarriageOpinionBonus(MarriageParams p) => Mathf.Max(0f, p.opinionBonus);

        /// <summary>既定パラメータで婚姻ボーナスを返す。</summary>
        public static float MarriageOpinionBonus() => MarriageOpinionBonus(MarriageParams.Default);
    }
}
