using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>月次評定の帰結（TKO-10・#2487）。その月の評定で何が動いたか（昇進・主命の決着/発令）。</summary>
    public readonly struct CouncilOutcome
    {
        /// <summary>主命が決着したか（期限超過の失敗確定）。</summary>
        public readonly bool mandateResolved;
        /// <summary>決着した主命が達成だったか（本評定は期限超過の失敗のみ確定＝通常 false。達成は会戦等で別途 Complete 済み）。</summary>
        public readonly bool mandateSucceeded;
        /// <summary>武勲で昇進したか。</summary>
        public readonly bool promoted;
        /// <summary>評定後の階級 tier。</summary>
        public readonly int newRankTier;
        /// <summary>新たな主命を拝命したか。</summary>
        public readonly bool mandateIssued;
        /// <summary>拝命した新主命（無ければ null）。</summary>
        public readonly SovereignMandate issuedMandate;

        public CouncilOutcome(bool mandateResolved, bool mandateSucceeded, bool promoted, int newRankTier,
            bool mandateIssued, SovereignMandate issuedMandate)
        {
            this.mandateResolved = mandateResolved;
            this.mandateSucceeded = mandateSucceeded;
            this.promoted = promoted;
            this.newRankTier = newRankTier;
            this.mandateIssued = mandateIssued;
            this.issuedMandate = issuedMandate;
        }
    }

    /// <summary>
    /// 月次評定のオーケストレータ（TKO-10・太閤立志伝V「定期的に主君に評価され命を受ける」リズム・#2487・唯一の窓口）。
    /// 暦の<b>月境界</b>で立身出世ループを一括処理する：(1)期限超過の主命を失敗確定（<see cref="SovereignMandateRules"/>）→
    /// (2)保留中の武勲昇進を最大 <see cref="CouncilParams.maxPromotionsPerCouncil"/> 段だけ確定（<see cref="MeritRecordRules.TryApplyPromotion"/>・ペース制御）→
    /// (3)未決の主命が無ければ確率で新主命を発令（<see cref="SovereignMandateRules.Issue"/>）。
    /// <b>数式・状態遷移は委譲し、ここは束ねるだけ</b>（並行系を作らない＝additive）。考課（<see cref="MeritEvaluationRules"/>＝文官/位階の九等評定）とは別軸。
    /// 主人公の <see cref="Person.rankTier"/> と <see cref="MeritRecord.meritPromotionsApplied"/>・主命状態を破壊的に進める。決定論（roll 注入）・test-first。
    /// </summary>
    public static class MonthlyCouncilRules
    {
        /// <summary>評定の調整値。</summary>
        public readonly struct CouncilParams
        {
            /// <summary>1回の評定で確定できる武勲昇進の最大段数（出世のペース制御）。</summary>
            public readonly int maxPromotionsPerCouncil;
            /// <summary>未決の主命が無いとき新主命を発令する確率（0..1）。</summary>
            public readonly float issueChance;

            public CouncilParams(int maxPromotionsPerCouncil, float issueChance)
            {
                this.maxPromotionsPerCouncil = Mathf.Max(1, maxPromotionsPerCouncil);
                this.issueChance = Mathf.Clamp01(issueChance);
            }

            /// <summary>既定＝1段/評定・新主命発令確率0.5。</summary>
            public static CouncilParams Default => new CouncilParams(1, 0.5f);
        }

        /// <summary>
        /// 1ヶ月ぶんの評定を回す。<paramref name="protagonist"/> の武勲（<paramref name="merit"/>）と現主命（<paramref name="active"/>・null可）、
        /// 君主（<paramref name="sovereign"/>）から、昇進・主命の決着/発令を決定論で処理して帰結を返す。
        /// <paramref name="roll"/> は 0..1 を返す乱数源（発令可否＝roll()&lt;issueChance、発令種別＝roll()×種別数）。
        /// <paramref name="newMandateId"/> は新主命に振る id（発令時のみ消費）。
        /// </summary>
        public static CouncilOutcome Hold(
            Person protagonist, MeritRecord merit, FactionData faction,
            SovereignMandate active, Person sovereign, int currentMonth, int newMandateId, Func<float> roll,
            MeritRecordRules.MeritRecordParams meritPrm, SovereignMandateRules.MandateParams mandatePrm,
            CouncilParams councilPrm, PersonRelationGraph relations = null, Func<int, bool> canPromoteTo = null)
        {
            if (protagonist == null)
                return new CouncilOutcome(false, false, false, 0, false, null);

            // (1) 期限超過の主命を失敗確定（達成は会戦等で別途 Complete 済み＝ここでは触れない）。
            bool mandateResolved = false;
            if (SovereignMandateRules.IsOverdue(active, currentMonth))
            {
                float grudge = SovereignMandateRules.Fail(active, mandatePrm);
                if (relations != null) SovereignMandateRules.ApplyOutcomeFavor(relations, active, -grudge);
                mandateResolved = true;
            }

            // (2) 保留中の武勲昇進を最大 maxPromotionsPerCouncil 段だけ確定（ペース制御）。
            //     canPromoteTo（任意）＝その階級に定員空きがあるか＝「上ほど狭き門」のゲート（空きなしは武勲据え置きで保留）。
            bool promoted = false;
            int tier = protagonist.rankTier;
            for (int i = 0; i < councilPrm.maxPromotionsPerCouncil; i++)
            {
                if (!MeritRecordRules.HasPendingPromotion(merit, meritPrm)) break;
                int next = RankSystem.NextRankTier(faction, tier);
                if (next == tier) break; // 最高位
                if (canPromoteTo != null && !canPromoteTo(next)) break; // 定員空きなし＝昇進保留
                if (!MeritRecordRules.TryApplyPromotion(faction, tier, merit, meritPrm, out int applied)) break;
                tier = applied;
                promoted = true;
            }
            protagonist.rankTier = tier;

            // (3) 未決の主命が無ければ確率で新主命を発令。
            bool issued = false;
            SovereignMandate newMandate = null;
            bool hasOpenMandate = SovereignMandateRules.IsOpen(active) && !mandateResolved;
            if (!hasOpenMandate
                && SovereignMandateRules.CanIssue(sovereign, protagonist)
                && roll != null && Mathf.Clamp01(roll()) < councilPrm.issueChance)
            {
                MandateKind kind = PickKind(Mathf.Clamp01(roll != null ? roll() : 0f));
                newMandate = SovereignMandateRules.Issue(newMandateId, sovereign, protagonist, kind, "", currentMonth, mandatePrm);
                issued = newMandate != null;
            }

            return new CouncilOutcome(mandateResolved, false, promoted, tier, issued, newMandate);
        }

        /// <summary>0..1 の roll を主命種別へ写す（6種均等）。</summary>
        public static MandateKind PickKind(float roll01)
        {
            int n = 6; // MandateKind の数
            int idx = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(roll01) * n), 0, n - 1);
            return (MandateKind)idx;
        }
    }
}
