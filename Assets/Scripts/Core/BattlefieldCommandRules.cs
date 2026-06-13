using System.Collections.Generic;

namespace Ginei
{
    /// <summary>臨時指揮の後任候補（#147 拡張・会戦の臨時指揮）。id・階級tier・先任順位（小さいほど先任）。</summary>
    public struct CommandCandidate
    {
        public int id;
        public int rankTier;
        public int seniority; // 先任順位（同階級の序列・小さいほど上位）
        public CommandCandidate(int id, int rankTier, int seniority)
        {
            this.id = id; this.rankTier = rankTier; this.seniority = seniority;
        }
    }

    /// <summary>
    /// 会戦中の臨時指揮（acting/brevet command）の純ロジック（#147 拡張・史実準拠）。
    /// 指揮官が戦死/離脱したら、<b>必要階級を満たさなくても</b>先任の次席が臨時で指揮を継承する（階級不足でも就ける）。
    /// 後任選定は「最上位階級→先任→id」。戦闘終了で `ActingCommandLedger.Clear` により正規人事へ戻す（臨時の解除）。test-first。
    /// </summary>
    public static class BattlefieldCommandRules
    {
        /// <summary>
        /// 臨時指揮の後任を選ぶ：最上位階級→先任(seniority小)→id小。候補なしは id=-1。
        /// <b>通常の階級ゲート（RequiredTier）は問わない</b>＝臨時指揮は階級不足でも就ける。
        /// </summary>
        public static CommandCandidate SelectActingSuccessor(IReadOnlyList<CommandCandidate> available)
        {
            var best = new CommandCandidate(-1, int.MinValue, int.MaxValue);
            if (available == null) return best;
            for (int i = 0; i < available.Count; i++)
            {
                var c = available[i];
                if (c.id < 0) continue;
                bool better = c.rankTier > best.rankTier
                    || (c.rankTier == best.rankTier && c.seniority < best.seniority)
                    || (c.rankTier == best.rankTier && c.seniority == best.seniority && c.id < best.id);
                if (better) best = c;
            }
            return best;
        }

        /// <summary>後任がいるか。</summary>
        public static bool HasSuccessor(IReadOnlyList<CommandCandidate> available)
            => SelectActingSuccessor(available).id >= 0;
    }
}
