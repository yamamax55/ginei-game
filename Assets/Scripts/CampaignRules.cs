using System.Collections.Generic;

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
