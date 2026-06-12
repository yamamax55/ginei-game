using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦役の世界状態の純ロジック（社会シミュ ↔ 地理盤面の統合・最上層）。盤面に存在する勢力ごとに
    /// <see cref="FactionState"/> を用意して時間を進め、版図の一体化度（<see cref="LogisticsRules"/>・物流 #844）で
    /// 実効安定度を割り引く＝散在/分断された版図は国家を栄えさせにくい。これまでの全モジュール
    /// （関ヶ原/継承/合意/希望/王朝/物流）を盤面の上で一つに回す。test-first。
    /// </summary>
    public static class CampaignRules
    {
        /// <summary>課税ベースの人口あたり係数（人口×これ×安定度＝経済規模の代理。S5）。</summary>
        public const float EconomyPerCapita = 1e-5f;
        /// <summary>高税の負担が民心(希望)を蝕む速さ（/戦略秒。S5）。</summary>
        public const float TaxBurdenDriftRate = 0.05f;

        /// <summary>
        /// 課税ベース＝人口 × <see cref="EconomyPerCapita"/> × 実効安定度（安定した大国ほど課税ベースが大きい）。
        /// </summary>
        public static float EconomyBase(FactionState s)
        {
            if (s == null || s.polity == null) return 0f;
            float pop = s.polity.population > 0f ? s.polity.population : 0f;
            return pop * EconomyPerCapita * FactionStateRules.Stability(s);
        }

        /// <summary>
        /// 財政の時間進行（S5・縦スライス）：各勢力の税収を国庫へ加算し、高税の負担で民心(希望)を蝕む。
        /// 既存の <see cref="Tick"/>（社会連鎖）とは別系統＝盤面側が両方を回す。null/dt&lt;=0 は無効。
        /// </summary>
        public static void TickEconomy(CampaignState c, float dt)
        {
            if (c == null || dt <= 0f) return;
            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                // 税収を国庫へ（課税ベース×税率）
                s.treasury += FiscalRules.TaxRevenue(EconomyBase(s), s.taxRate) * dt;
                // 高税の負担で民心(希望=支持)が蝕まれる（状態を直接進める＝盤面の進行）
                if (s.community != null)
                {
                    float burden = FiscalRules.TaxBurdenPenalty(s.taxRate) * TaxBurdenDriftRate * dt;
                    s.community.hope = Mathf.Clamp01(s.community.hope - burden);
                }
            }
        }

        /// <summary>
        /// 財政を <b>1 game-day ぶん</b>進める（TIME-6 #952＝暦の日境界で1回呼ぶ）。連続版 <see cref="TickEconomy"/> を
        /// 1日の秒数（<paramref name="secondsPerDay"/>）で積分した量と一致＝総量は同じで、離散（日次）に切り替える。
        /// フレームレート非依存・「暦比で同じ帰結」を厳密化する。secondsPerDay&lt;=0 は無効。
        /// </summary>
        public static void TickEconomyDay(CampaignState c, float secondsPerDay)
        {
            if (secondsPerDay <= 0f) return;
            TickEconomy(c, secondsPerDay);
        }

        /// <summary>
        /// 予算の時間進行（国家予算の基盤）：各勢力の歳出総額（<see cref="BudgetRules.Total"/>）を国庫から引く＝
        /// 歳入（<see cref="TickEconomy"/>）の対＝歳出。国庫は赤字（マイナス＝国債相当）を許容し、過剰歳出が可視化される。
        /// budget が空（既定）なら歳出0＝無変化（後方互換）。null/dt&lt;=0 は無効。
        /// </summary>
        public static void TickBudget(CampaignState c, float dt)
        {
            if (c == null || dt <= 0f) return;
            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null || s.budget == null) continue;
                s.treasury -= BudgetRules.Total(s.budget) * dt;
            }
        }

        /// <summary>予算を <b>1 game-day ぶん</b>進める（TIME-6＝暦の日境界で1回）。連続版 <see cref="TickBudget"/> を
        /// 1日の秒数で積分した量と一致（離散化しても暦比で同じ帰結）。secondsPerDay&lt;=0 は無効。</summary>
        public static void TickBudgetDay(CampaignState c, float secondsPerDay)
        {
            if (secondsPerDay <= 0f) return;
            TickBudget(c, secondsPerDay);
        }

        /// <summary>盤面に1星系以上を所有する勢力それぞれに FactionState を用意する（無ければ追加）。</summary>
        public static void EnsureStates(CampaignState c)
        {
            if (c == null || c.map == null) return;
            var owners = new HashSet<Faction>();
            for (int i = 0; i < c.map.systems.Count; i++)
            {
                StarSystem s = c.map.systems[i];
                if (s != null) owners.Add(s.owner);
            }
            foreach (Faction f in owners)
                if (GetState(c, f) == null) c.states.Add(new FactionState(f));
        }

        /// <summary>指定勢力の国家状態を返す（無ければ null）。</summary>
        public static FactionState GetState(CampaignState c, Faction faction)
        {
            if (c == null) return null;
            for (int i = 0; i < c.states.Count; i++)
                if (c.states[i] != null && c.states[i].faction == faction) return c.states[i];
            return null;
        }

        /// <summary>全勢力の国家状態を dt 進める（腐敗→合意→希望の連鎖）。</summary>
        public static void Tick(CampaignState c, float dt)
        {
            if (c == null || dt <= 0f) return;
            for (int i = 0; i < c.states.Count; i++)
                FactionStateRules.Tick(c.states[i], dt);
        }

        /// <summary>実効安定度＝国家の安定度 × 版図の一体化度（散在/分断された版図は割り引かれる）。</summary>
        public static float EffectiveStability(CampaignState c, Faction faction)
        {
            FactionState s = GetState(c, faction);
            if (s == null) return 0f;
            float cohesion = (c.map != null) ? LogisticsRules.CohesionFactor(c.map, faction) : 1f;
            return FactionStateRules.Stability(s) * cohesion;
        }

        /// <summary>暫定優勢勢力＝実効安定度が最も高い勢力（同点や不在は最初の状態の勢力）。</summary>
        public static Faction LeadingFaction(CampaignState c)
        {
            if (c == null || c.states.Count == 0) return Faction.帝国;
            Faction best = c.states[0].faction;
            float bestVal = EffectiveStability(c, best);
            for (int i = 1; i < c.states.Count; i++)
            {
                Faction f = c.states[i].faction;
                float v = EffectiveStability(c, f);
                if (v > bestVal) { bestVal = v; best = f; }
            }
            return best;
        }
    }
}
