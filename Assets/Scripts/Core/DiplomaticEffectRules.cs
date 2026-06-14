using UnityEngine;
using DS = Ginei.DiplomacyState;

namespace Ginei
{
    /// <summary>
    /// 外交の中身（条約効果・債務外交・制裁・援助）を合成する薄いオーケストレータ（外交EPIC #189・純ロジック）。
    /// 既存の未配線 Core（<see cref="TreatyRules"/>／<see cref="DebtDiplomacyRules"/>／<see cref="SanctionsRules"/>／
    /// <see cref="ForeignAidRules"/>／<see cref="AllianceDivergenceRules"/>）へ<b>委譲するだけ</b>＝数式を二重実装しない。
    /// 状態（<see cref="DiplomacyState"/>・opinion 等）は<b>引数で受け取り読み取りのみ</b>＝ここでは状態を変えず
    /// 効果値を返す（適用＝opinion 加算等は呼び出し側＝<see cref="DiplomacyRules.AdjustOpinion"/> で行う）。
    /// 乱数なし・決定論。null/欠落は安全に既定値。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DiplomaticEffectRules
    {
        // ===== 条約効果（TreatyRules へ委譲） =====

        /// <summary>締結中の条約が関係値(opinion)に与える効果。<see cref="TreatyRules.OpinionEffect"/> へ委譲。</summary>
        public static float TreatyOpinionDelta(TreatyType type, TreatyRules.TreatyParams p)
            => TreatyRules.OpinionEffect(type, p);

        /// <summary>既定 Params で条約の opinion 効果を返す簡易窓口。</summary>
        public static float TreatyOpinionDelta(TreatyType type)
            => TreatyRules.OpinionEffect(type, TreatyRules.TreatyParams.Default);

        /// <summary>
        /// 外交状態(<see cref="DS.DiplomaticStatus"/>)に対応する条約の opinion 効果。
        /// 同盟/不可侵/属国は対応する <see cref="TreatyType"/> の効果、平時/交戦は0（条約効果なし）。
        /// 状態は引数で受け取り読み取りのみ。
        /// </summary>
        public static float StatusOpinionDelta(DS.DiplomaticStatus status, TreatyRules.TreatyParams p)
        {
            switch (status)
            {
                case DS.DiplomaticStatus.同盟: return TreatyRules.OpinionEffect(TreatyType.同盟, p);
                case DS.DiplomaticStatus.不可侵: return TreatyRules.OpinionEffect(TreatyType.不可侵, p);
                case DS.DiplomaticStatus.属国: return TreatyRules.OpinionEffect(TreatyType.属国, p);
                default: return 0f; // 平時/交戦＝条約効果なし
            }
        }

        public static float StatusOpinionDelta(DS.DiplomaticStatus status)
            => StatusOpinionDelta(status, TreatyRules.TreatyParams.Default);

        // ===== 制裁の経済打撃（SanctionsRules へ委譲） =====

        /// <summary>
        /// 経済制裁が相手経済へ与える打撃量（産出の絶対減少分）＝相手経済規模×実効ペナルティ。
        /// 実効ペナルティは <see cref="SanctionsRules.OutputPenalty"/> へ委譲（強度×抜け穴で減衰）。
        /// 負の経済規模は0扱い（打撃は非負）。
        /// </summary>
        public static float SanctionEconomicHit(float targetEconomy, float sanctionSeverity, float leakage, SanctionsParams p)
        {
            float economy = Mathf.Max(0f, targetEconomy);
            return economy * SanctionsRules.OutputPenalty(sanctionSeverity, leakage, p);
        }

        /// <summary>抜け穴なし・既定 Params で制裁の経済打撃を返す簡易窓口。</summary>
        public static float SanctionEconomicHit(float targetEconomy, float sanctionSeverity)
            => SanctionEconomicHit(targetEconomy, sanctionSeverity, 0f, SanctionsParams.Default);

        // ===== 対外援助→opinion/従属（ForeignAidRules へ委譲） =====

        /// <summary>
        /// 援助による関係値(opinion)上昇。困窮度に応じ何倍も効く（雪中の炭）。
        /// <see cref="ForeignAidRules.OpinionGain"/> へ委譲。
        /// </summary>
        public static float AidOpinionGain(float aidAmount, float recipientNeed, ForeignAidParams p)
            => ForeignAidRules.OpinionGain(aidAmount, recipientNeed, p);

        /// <summary>既定 Params で援助の opinion 上昇を返す簡易窓口（困窮度0＝平時援助）。</summary>
        public static float AidOpinionGain(float aidAmount, float recipientNeed = 0f)
            => ForeignAidRules.OpinionGain(aidAmount, recipientNeed, ForeignAidParams.Default);

        // ===== 債務を外交圧力に（DebtDiplomacyRules へ委譲） =====

        /// <summary>
        /// 債権が握る政治レバレッジ（0..1）＝債務を外交圧力に。<see cref="DebtDiplomacyRules.DebtLeverage"/> へ委譲。
        /// 返せない借金ほど力になる（債務/借り手経済規模が閾値超で線形に増大）。
        /// </summary>
        public static float DebtLeverage(float debtOwed, float debtorEconomy, DebtDiplomacyParams p)
            => DebtDiplomacyRules.DebtLeverage(debtOwed, debtorEconomy, p);

        /// <summary>既定 Params で債務レバレッジを返す簡易窓口。</summary>
        public static float DebtLeverage(float debtOwed, float debtorEconomy)
            => DebtDiplomacyRules.DebtLeverage(debtOwed, debtorEconomy, DebtDiplomacyParams.Default);
    }
}
