using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 外交アクション（制裁／対外援助／講和条項）の<b>発令判断</b> AI の薄いオーケストレータ（外交EPIC #189・純ロジック）。
    /// 「AIがいつ制裁／援助を出すか・どれだけの賠償を求めるか」だけを閾値ベース・決定論で判断する。
    /// 効果値の計算は二重実装せず既存窓口へ<b>委譲</b>：
    /// 制裁の経済打撃＝<see cref="DiplomaticEffectRules.SanctionEconomicHit(float,float)"/>／
    /// 援助の opinion 上昇＝<see cref="DiplomaticEffectRules.AidOpinionGain(float,float)"/>／
    /// 賠償額＝<see cref="ReparationsRules.AnnualPayment(float,float)"/>。
    /// 状態（opinion・国力・経済規模・困窮度・戦況）は<b>引数で受け取り読み取りのみ</b>＝ここでは状態を変えない。
    /// 宣戦／講和／同盟の判断は <see cref="DiplomacyAiRules"/> が担い、本系統は経済的外交手段（制裁／援助／賠償）に特化。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DiplomaticActionAiRules
    {
        /// <summary>外交アクション発令判断の調整値。</summary>
        public struct ActionAiParams
        {
            /// <summary>これ以下の関係で制裁を検討（険悪・既定負値）。</summary>
            public float sanctionOpinionThreshold;
            /// <summary>制裁を課すのに要する国力優位（自/相手 がこれ以上＝報復に耐えうる）。</summary>
            public float sanctionPowerRatio;
            /// <summary>これ以上の関係で援助を検討（友好）。</summary>
            public float aidOpinionThreshold;
            /// <summary>援助に値する相手の最低困窮度（0..1・雪中の炭の入口）。</summary>
            public float aidNeedThreshold;
            /// <summary>援助を出すのに要する自国の最低余裕（国庫等）。</summary>
            public float aidSurplusThreshold;
            /// <summary>賠償率の上限（鶏を殺さない＝経済絞殺を避ける上限・0..1）。</summary>
            public float maxReparationsBurden;

            public ActionAiParams(float sanctionOpinionThreshold, float sanctionPowerRatio,
                float aidOpinionThreshold, float aidNeedThreshold, float aidSurplusThreshold,
                float maxReparationsBurden)
            {
                this.sanctionOpinionThreshold = sanctionOpinionThreshold;
                this.sanctionPowerRatio = sanctionPowerRatio;
                this.aidOpinionThreshold = aidOpinionThreshold;
                this.aidNeedThreshold = Mathf.Clamp01(aidNeedThreshold);
                this.aidSurplusThreshold = aidSurplusThreshold;
                this.maxReparationsBurden = Mathf.Clamp01(maxReparationsBurden);
            }

            /// <summary>既定＝制裁関係−30/制裁国力比1.0/援助関係40/困窮入口0.3/余裕0/賠償上限0.6。</summary>
            public static ActionAiParams Default => new ActionAiParams(-30f, 1.0f, 40f, 0.3f, 0f, 0.6f);
        }

        // ===== 制裁の発令判断 =====

        /// <summary>
        /// 経済制裁を課すべきか＝関係が険悪（≤閾値）かつ国力優位（自/相手≥比＝返り血・報復に耐えうる）。
        /// 効果額（経済打撃）は <see cref="DiplomaticEffectRules.SanctionEconomicHit(float,float)"/> へ委譲＝ここは可否のみ。
        /// </summary>
        public static bool ShouldSanction(float opinion, float ownPower, float targetPower, ActionAiParams p)
        {
            if (opinion > p.sanctionOpinionThreshold) return false;
            return ownPower >= Mathf.Max(0f, targetPower) * p.sanctionPowerRatio;
        }

        /// <summary>既定 Params で制裁の可否を返す簡易窓口。</summary>
        public static bool ShouldSanction(float opinion, float ownPower, float targetPower)
            => ShouldSanction(opinion, ownPower, targetPower, ActionAiParams.Default);

        // ===== 対外援助の発令判断 =====

        /// <summary>
        /// 対外援助を出すべきか＝関係が友好（≥閾値）かつ相手が困窮（≥困窮入口）かつ自国に余裕（≥余裕閾値）。
        /// 効果額（opinion 上昇）は <see cref="DiplomaticEffectRules.AidOpinionGain(float,float)"/> へ委譲＝ここは可否のみ。
        /// </summary>
        public static bool ShouldGiveAid(float opinion, float recipientNeed, float ownSurplus, ActionAiParams p)
        {
            if (opinion < p.aidOpinionThreshold) return false;
            if (Mathf.Clamp01(recipientNeed) < p.aidNeedThreshold) return false;
            return ownSurplus >= p.aidSurplusThreshold;
        }

        /// <summary>既定 Params で援助の可否を返す簡易窓口。</summary>
        public static bool ShouldGiveAid(float opinion, float recipientNeed, float ownSurplus)
            => ShouldGiveAid(opinion, recipientNeed, ownSurplus, ActionAiParams.Default);

        // ===== 講和条項（賠償額）の提示 =====

        /// <summary>
        /// 講和時に求める賠償額（年間取り分）。戦況(warScore 0..1)を賠償率(burden)へ写し、
        /// 鶏を殺さない上限（<see cref="ActionAiParams.maxReparationsBurden"/>）でクランプしてから
        /// <see cref="ReparationsRules.AnnualPayment(float,float)"/> へ委譲（ラッファー曲線＝取り立てすぎは取り分が減る）。
        /// 戦況が芳しくない（warScore≤0）なら賠償なし＝0。負の経済規模は委譲先で0扱い。
        /// </summary>
        public static float ProposedReparations(float warScore, float loserEconomy, ActionAiParams p)
        {
            float burden = Mathf.Min(Mathf.Clamp01(warScore), p.maxReparationsBurden);
            if (burden <= 0f) return 0f;
            return ReparationsRules.AnnualPayment(burden, loserEconomy);
        }

        /// <summary>既定 Params で賠償額を返す簡易窓口。</summary>
        public static float ProposedReparations(float warScore, float loserEconomy)
            => ProposedReparations(warScore, loserEconomy, ActionAiParams.Default);
    }
}
