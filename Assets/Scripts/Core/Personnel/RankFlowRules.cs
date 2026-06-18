using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 勢力ごとの階級ピラミッド（<see cref="RankDistribution"/>）の単一ストア（TKO-13 Game配線・#2490・Core static）。
    /// 軍の階級別人数を勢力単位で保持し、観測（軍 M／執務机）・定員ゲート（昇進律速）・年次フロー（<see cref="RankFlowRules"/>）が読む。
    /// <see cref="FleetPool"/>（勢力の総艦艇）と同じく勢力独立の静的在庫＝並行レジストリを作らない。純データ・test-first。
    /// </summary>
    public static class MilitaryRankRegistry
    {
        private static readonly Dictionary<Faction, RankDistribution> map = new Dictionary<Faction, RankDistribution>();

        /// <summary>勢力の分布を得る（無ければ生成して登録）。</summary>
        public static RankDistribution Get(Faction faction)
        {
            if (!map.TryGetValue(faction, out var d))
            {
                d = new RankDistribution();
                map[faction] = d;
            }
            return d;
        }

        /// <summary>登録済みか。</summary>
        public static bool Has(Faction faction) => map.ContainsKey(faction);

        /// <summary>分布を差し替える（初期ピラミッドの種付け等）。</summary>
        public static void Set(Faction faction, RankDistribution distribution)
        {
            if (distribution != null) map[faction] = distribution;
        }

        /// <summary>登録済みの勢力一覧（観測用）。</summary>
        public static IEnumerable<Faction> Factions => map.Keys;

        /// <summary>全消去（戦役リセット用）。</summary>
        public static void Clear() => map.Clear();
    }

    /// <summary>
    /// 階級ピラミッドの年次フローの純ロジック（TKO-13 Game配線・#2490・唯一の窓口）。<b>徴募（最下級へ流入）→ 損耗（退役/戦死で流出）→
    /// 定員空きへ昇進（下から上へ）</b>を回し、分布を理想ピラミッド（<see cref="RankDistributionRules.PyramidTarget"/>）へ近づける。
    /// 個体に降りず人数の流れだけを扱う（マクロ集約＝PERF）。<b>昇進は上位の定員空き内でのみ起き</b>＝「上ほど狭き門」を生む
    /// （個人昇進の定員ゲート <see cref="RankDistributionRules.Vacancy"/> と同じ原理）。決定論・test-first。
    /// </summary>
    public static class RankFlowRules
    {
        /// <summary>フローの調整値。</summary>
        public readonly struct FlowParams
        {
            /// <summary>各段から上位の空き内で昇進を試みる割合（0..1）。</summary>
            public readonly float promoteRate;
            /// <summary>各段から毎年抜ける割合（退役/戦死・0..1）。</summary>
            public readonly float attritionRate;

            public FlowParams(float promoteRate, float attritionRate)
            {
                this.promoteRate = UnityEngine.Mathf.Clamp01(promoteRate);
                this.attritionRate = UnityEngine.Mathf.Clamp01(attritionRate);
            }

            /// <summary>既定＝昇進25%/年・損耗8%/年。</summary>
            public static FlowParams Default => new FlowParams(0.25f, 0.08f);
        }

        /// <summary>最下級（二等兵）へ新兵を流入させる。</summary>
        public static void Recruit(RankDistribution d, int intake)
        {
            if (d == null || intake <= 0) return;
            d.Add(MilitaryGrade.二等兵, intake);
        }

        /// <summary>各階級から一定割合が抜ける（退役/戦死）。流出した総数を返す。</summary>
        public static int Attrition(RankDistribution d, float rate)
        {
            if (d == null) return 0;
            float r = UnityEngine.Mathf.Clamp01(rate);
            int total = 0;
            for (int i = 0; i < d.counts.Length; i++)
            {
                int leave = UnityEngine.Mathf.RoundToInt(d.counts[i] * r);
                if (leave <= 0) continue;
                d.counts[i] -= leave;
                total += leave;
            }
            return total;
        }

        /// <summary>
        /// 下から上へ、上位の定員空き内で昇進させる（各段から最大 <paramref name="promoteRate"/> 割）。
        /// 上が詰まっていれば（空き0）昇進しない＝ピラミッドが律速する。移動した総数を返す。
        /// </summary>
        public static int PromoteByVacancy(RankDistribution d, int[] target, float promoteRate)
        {
            if (d == null || target == null) return 0;
            float r = UnityEngine.Mathf.Clamp01(promoteRate);
            int moved = 0;
            // 下（二等兵）から上へ＝下の昇進が上の段を埋め、その段の昇進は次段の空きを見る。
            for (int g = 0; g < d.counts.Length - 1; g++)
            {
                int above = g + 1;
                int vacancy = UnityEngine.Mathf.Max(0, target[above] - d.counts[above]);
                if (vacancy <= 0) continue;
                int eligible = UnityEngine.Mathf.RoundToInt(d.counts[g] * r);
                int up = UnityEngine.Mathf.Min(eligible, vacancy);
                if (up <= 0) continue;
                d.counts[g] -= up;
                d.counts[above] += up;
                moved += up;
            }
            return moved;
        }

        /// <summary>
        /// 1年ぶんのフロー：損耗（空きを開ける）→ 徴募（最下級を満たす）→ 定員空きへ昇進（下から上へ）。
        /// 目標ピラミッドは現員総数から都度算出（<paramref name="extraIntake"/> はPOP/予算由来の追加新兵）。
        /// </summary>
        public static void TickYear(RankDistribution d, int extraIntake,
            RankDistributionRules.PyramidParams pyramid, FlowParams flow)
        {
            if (d == null) return;
            Attrition(d, flow.attritionRate);
            Recruit(d, UnityEngine.Mathf.Max(0, extraIntake));
            int[] target = RankDistributionRules.PyramidTarget(RankDistributionRules.TotalForce(d), pyramid);
            PromoteByVacancy(d, target, flow.promoteRate);
        }
    }
}
