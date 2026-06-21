using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍団の撤退判断と敗北の代償（士気・練度低下＋将帥の声望/政治的代償）の数値を集約する純ロジック
    /// （static・test-first）。Game 層（<see cref="BattlefieldCommandManager"/>）／戦略層（<see cref="StrategyRules"/>）は
    /// 本ルールへ判断を委譲し、配線だけを持つ。
    ///
    /// 「敗色濃厚なら軍団長が安全に撤退判断」＝有能・冷静な軍団長ほど早めに崩れる前に整然と退く（statesman）。
    /// 高慢/功名心の強い軍団長は粘りがち（撤退閾値が下がる＝より追い詰められるまで退かない）。
    /// 盤面非依存・plain 引数・乱数なし・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class CorpsRetreatRules
    {
        // ── 軍団の撤退判断 ──

        /// <summary>軍団が撤退を検討し始める既定の兵力比（残/総。0.35＝3.5割で総退却を考える）。</summary>
        public const float DefaultRetreatStrengthRatio = 0.35f;

        /// <summary>統率が撤退閾値を引き上げる重み（有能なほど早めに整然と退く＝閾値が上がる）。</summary>
        public const float LeadershipRetreatWeight = 0.15f;

        /// <summary>功名心が撤退閾値を引き下げる重み（高慢/功名心が強いほど粘る＝閾値が下がる）。</summary>
        public const float AmbitionHoldWeight = 0.15f;

        /// <summary>
        /// 軍団の実効的な撤退兵力しきい値（0..1）。基準 <see cref="DefaultRetreatStrengthRatio"/> に対し、
        /// 統率（0..100）が高いほど引き上げ（早めに退く）、功名心 ambition（0..100）が高いほど引き下げる（粘る）。
        /// 例：凡将＝基準どおり／名将＝高めに（崩れる前に退く）／猪武者＝低く（最後まで踏みとどまる）。
        /// </summary>
        public static float RetreatThreshold(int leadership, int ambition, float baseRatio)
        {
            float lead = Mathf.Clamp01(leadership / 100f);
            float amb = Mathf.Clamp01(ambition / 100f);
            float thr = baseRatio
                + (lead - 0.5f) * 2f * LeadershipRetreatWeight   // 統率50で±0、100で+weight、0で−weight
                - (amb - 0.5f) * 2f * AmbitionHoldWeight;        // 功名心が高いほど閾値↓（粘る）
            return Mathf.Clamp(thr, 0.05f, 0.9f);
        }

        /// <summary>既定基準版。</summary>
        public static float RetreatThreshold(int leadership, int ambition)
            => RetreatThreshold(leadership, ambition, DefaultRetreatStrengthRatio);

        /// <summary>
        /// 軍団長が「整然とした総退却」を命じるべきか。条件＝(1) 軍団の兵力比が実効しきい値を下回る、
        /// または (2) 軍団長が敗走（routed）＝指揮が崩れた。どちらかで coordinated retreat を発令する
        /// （バラバラに各個撃破される前に、軍団ごと撤退線へ後退する）。
        /// </summary>
        public static bool ShouldOrderRetreat(float corpsStrengthRatio, bool commanderRouted,
            int leadership, int ambition, float baseRatio)
        {
            if (commanderRouted) return true;
            float ratio = Mathf.Clamp01(corpsStrengthRatio);
            return ratio < RetreatThreshold(leadership, ambition, baseRatio);
        }

        /// <summary>既定基準版。</summary>
        public static bool ShouldOrderRetreat(float corpsStrengthRatio, bool commanderRouted,
            int leadership, int ambition)
            => ShouldOrderRetreat(corpsStrengthRatio, commanderRouted, leadership, ambition,
                DefaultRetreatStrengthRatio);

        // ── 敗北の代償（士気・練度低下） ──

        /// <summary>敗北時に失う士気の既定割合（最大 maxMorale のこの割合ぶん減る）。</summary>
        public const float DefaultMoralePenaltyRatio = 0.35f;

        /// <summary>敗北で削られる練度（XP）の既定割合（残存 XP のこの割合ぶん失う＝錬度の希釈に相当）。</summary>
        public const float DefaultVeterancyPenaltyRatio = 0.2f;

        /// <summary>
        /// 敗北で失う士気量（≥0）。最大士気 × <paramref name="ratio"/>（敗北の度合い severity 0..1 でスケール）。
        /// 完敗ほど（severity 1）大きく、辛勝（severity 小）なら軽い。Game 側は <c>ApplyMoraleDelta(-本値)</c> で適用。
        /// </summary>
        public static float MoraleLossOnDefeat(float maxMorale, float severity, float ratio)
        {
            float m = Mathf.Max(0f, maxMorale);
            float sev = Mathf.Clamp01(severity);
            return m * Mathf.Clamp01(ratio) * sev;
        }

        /// <summary>既定割合版。</summary>
        public static float MoraleLossOnDefeat(float maxMorale, float severity)
            => MoraleLossOnDefeat(maxMorale, severity, DefaultMoralePenaltyRatio);

        /// <summary>
        /// 敗北後の練度（XP）。残存 XP から <paramref name="ratio"/>×severity ぶん失う（規律の乱れ・古参の喪失）。
        /// 戻り値は更新後 XP（≥0）。<see cref="VeterancyRules"/> の希釈とは別系統＝敗走そのものの錬度低下。
        /// </summary>
        public static float VeterancyAfterDefeat(float xp, float severity, float ratio)
        {
            float x = Mathf.Max(0f, xp);
            float loss = Mathf.Clamp01(ratio) * Mathf.Clamp01(severity);
            return Mathf.Max(0f, x * (1f - loss));
        }

        /// <summary>既定割合版。</summary>
        public static float VeterancyAfterDefeat(float xp, float severity)
            => VeterancyAfterDefeat(xp, severity, DefaultVeterancyPenaltyRatio);

        // ── 敗北の代償（兵力＝撤退に伴う追加損耗） ──

        /// <summary>撤退に伴う組織的損耗の既定割合（生存兵力からさらにこの割合×severity ぶん欠ける）。</summary>
        public const float DefaultWithdrawalAttritionRatio = 0.1f;

        /// <summary>
        /// 撤退で帰還する兵力（≥1）。生存兵力 survivors から、撤退の混乱で <paramref name="ratio"/>×severity ぶん
        /// さらに欠ける（しんがり・落伍）。完敗ほど目減りが大きい。最低1（全滅させない＝原隊へ帰還させる）。
        /// </summary>
        public static int SurvivorsAfterWithdrawal(int survivors, float severity, float ratio)
        {
            int s = Mathf.Max(0, survivors);
            float keep = 1f - Mathf.Clamp01(ratio) * Mathf.Clamp01(severity);
            return Mathf.Max(1, Mathf.RoundToInt(s * keep));
        }

        /// <summary>既定割合版。</summary>
        public static int SurvivorsAfterWithdrawal(int survivors, float severity)
            => SurvivorsAfterWithdrawal(survivors, severity, DefaultWithdrawalAttritionRatio);

        // ── 敗北の度合い（severity） ──

        /// <summary>
        /// 敗北の度合い（0..1）＝敗者の残存兵力が乏しく・勝者との戦力差が大きいほど大きい。
        /// 完膚なきまでの敗北なら1に近く、僅差の負けなら小さい。声望/士気/練度のスケールに使う。
        /// = clamp01( (1 − 敗者残/敗者元) を主に、勝者比でも増す )。
        /// </summary>
        public static float DefeatSeverity(int loserSurvivor, int loserInitial, int winnerStrength)
        {
            float init = Mathf.Max(1, loserInitial);
            float lossFrac = Mathf.Clamp01(1f - Mathf.Max(0, loserSurvivor) / init);
            // 勝者が圧倒的（敗者元比で大きい）ほど屈辱的＝severity を底上げ。
            float dominance = Mathf.Clamp01(Mathf.Max(0, winnerStrength) / init - 0.5f); // 勝者が敗者元の1.5倍超で加算
            return Mathf.Clamp01(0.5f * lossFrac + 0.5f * Mathf.Max(lossFrac, dominance));
        }

        // ── 将帥の声望・政治的代償 ──

        /// <summary>
        /// 敗北による将帥の声望失墜後の声望（0..1）＝<see cref="CommandReputationRules.ReputationDecay"/> へ委譲。
        /// recentDefeat に severity を渡す＝完敗ほど大きく失墜する。AdmiralData 等に声望フィールドが無い設計では、
        /// 本値の算出に頼らず通知（声望低下のアナウンス）で表現してよい。
        /// </summary>
        public static float ReputationAfterDefeat(float reputation, float severity)
            => CommandReputationRules.ReputationDecay(reputation, severity);
    }
}
