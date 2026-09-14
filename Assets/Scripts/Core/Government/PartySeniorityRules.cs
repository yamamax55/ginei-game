using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>当選回数による党内序列の調整値（ゲーム用の党内慣行。現実の公式な資格要件ではない）。</summary>
    public readonly struct PartySeniorityParams
    {
        /// <summary>年功の伸びが半分に達する当選回数（逓減の効き）。</summary>
        public readonly float halfStandingWins;
        /// <summary>これ以上の当選回数は年功を増やさない（頭打ち）。</summary>
        public readonly int maxStandingWins;
        /// <summary>中堅とみなす当選回数（未満は新人）。</summary>
        public readonly int midCareerWins;
        /// <summary>ベテランとみなす当選回数。</summary>
        public readonly int veteranWins;

        public PartySeniorityParams(float halfStandingWins, int maxStandingWins, int midCareerWins, int veteranWins)
        {
            this.halfStandingWins = Mathf.Max(0.1f, halfStandingWins);
            this.maxStandingWins = Mathf.Max(1, maxStandingWins);
            this.midCareerWins = Mathf.Max(1, midCareerWins);
            this.veteranWins = Mathf.Max(this.midCareerWins + 1, veteranWins);
        }

        /// <summary>既定＝3回で半分・12回で頭打ち・3回から中堅・6回からベテラン。</summary>
        public static PartySeniorityParams Default => new PartySeniorityParams(3f, 12, 3, 6);
    }

    /// <summary>当選回数の目安の区分（表示と任命候補の説明用。役職・権限を自動で与えない）。</summary>
    public enum SeniorityTier { 履歴未登録, 新人, 中堅, ベテラン }

    /// <summary>一人の党内序列の素材（国政の当選履歴だけ。知事の当選・党首選の勝利・閣僚任命は数えない）。</summary>
    public struct SeniorityInfo
    {
        public int personId;
        /// <summary>議員の当選履歴（<see cref="LegislatorRecord"/>）が登録されているか。</summary>
        public bool historyRegistered;
        public int nationalWins;
        public int lowerWins;
        public int upperWins;
        public int consecutiveWins;
        public int firstWinYear;
        public int lastWinYear;
        public bool seated;
        public SeniorityTier tier;
        /// <summary>年功（0..1・逓減と頭打ち）。</summary>
        public float standing;
    }

    /// <summary>
    /// 当選回数による党内序列の純ロジック（#165 / #2768）。国政の累積当選（<see cref="LegislatorRosterRules"/> が数えた記録）だけを読み、
    /// 逓減・頭打ちのある「年功」（0..1）にする。年功は総裁選の候補評価・推薦集め・人事の待機順の<b>一要素</b>で、
    /// 政治能力・行政能力・軍階級・法的権限・役職には直結させない（ここは何も書き換えない）。決定論・test-first。
    /// </summary>
    public static class PartySeniorityRules
    {
        /// <summary>当選回数から年功（0..1）：min(回数,上限)/(min(回数,上限)+半減回数) を上限時の値で割って 0..1 にする。</summary>
        public static float Standing(int wins, PartySeniorityParams p)
        {
            if (wins <= 0) return 0f;
            float w = Mathf.Min(wins, p.maxStandingWins);
            float cap = p.maxStandingWins / (p.maxStandingWins + p.halfStandingWins);
            return Mathf.Clamp01(w / (w + p.halfStandingWins) / cap);
        }

        /// <summary>当選回数の目安の区分（履歴が無ければ 履歴未登録）。</summary>
        public static SeniorityTier TierOf(bool historyRegistered, int wins, PartySeniorityParams p)
        {
            if (!historyRegistered) return SeniorityTier.履歴未登録;
            if (wins >= p.veteranWins) return SeniorityTier.ベテラン;
            if (wins >= p.midCareerWins) return SeniorityTier.中堅;
            return SeniorityTier.新人;
        }

        /// <summary>一人の序列の素材（状態は変えない）。</summary>
        public static SeniorityInfo InfoOf(PoliticsState pol, int personId, PartySeniorityParams p)
        {
            var info = new SeniorityInfo { personId = personId };
            LegislatorRecord r = LegislatorRosterRules.Find(pol, personId);
            if (r != null)
            {
                info.historyRegistered = true;
                info.lowerWins = r.TotalLowerWins;
                info.upperWins = r.TotalUpperWins;
                info.nationalWins = r.TotalWins;
                info.consecutiveWins = r.consecutiveWins;
                info.firstWinYear = r.firstWinYear;
                info.lastWinYear = r.lastWinYear;
                info.seated = r.seated;
            }
            info.standing = Standing(info.nationalWins, p);
            info.tier = TierOf(info.historyRegistered, info.nationalWins, p);
            return info;
        }

        /// <summary>
        /// 党内序列（党員を並べる）：国政当選回数の多い順→連続当選の多い順→初当選の早い順（記録なしは後ろ）→人物ID小。
        /// 表示・待機順の説明用で、役職や票を与えない。
        /// </summary>
        public static List<SeniorityInfo> Ranking(PoliticsState pol, Party party, PartySeniorityParams p)
        {
            var list = new List<SeniorityInfo>();
            if (party == null || party.memberIds == null) return list;
            var seen = new HashSet<int>();
            for (int i = 0; i < party.memberIds.Count; i++)
            {
                int id = party.memberIds[i];
                if (id < 0 || !seen.Add(id)) continue;
                list.Add(InfoOf(pol, id, p));
            }
            list.Sort((a, b) =>
            {
                if (a.nationalWins != b.nationalWins) return b.nationalWins.CompareTo(a.nationalWins);
                if (a.consecutiveWins != b.consecutiveWins) return b.consecutiveWins.CompareTo(a.consecutiveWins);
                int fa = a.firstWinYear > 0 ? a.firstWinYear : int.MaxValue;
                int fb = b.firstWinYear > 0 ? b.firstWinYear : int.MaxValue;
                if (fa != fb) return fa.CompareTo(fb);
                return a.personId.CompareTo(b.personId);
            });
            return list;
        }
    }
}
