using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 決裁<b>前</b>に出す判断材料（作業票⑤）。費用・対象・期待効果・実行時期を、
    /// 決める前に読める形で1本にまとめる。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item>確かな値（いま国庫からいくら引かれるか等）と、<b>不確かな見込み</b>を書き分ける。
    ///   官僚の執行忠実度で値切られるので、承認しても満額は効かない＝「見込み」と明示する。</item>
    ///   <item>盤面が分かるときは<b>実際の対象</b>（造船所の数・守りが薄い惑星・交戦相手）を出す。
    ///   分からないときは「対象は執行時に決まります」と書く＝架空の対象を書かない。</item>
    ///   <item>実行時期＝即時か、時間がかかるか（建艦は完成待ち）。</item>
    /// </list>
    /// </summary>
    public static class PetitionBriefingRules
    {
        /// <summary>その決裁の判断材料（複数行）。効果キーが未対応なら短い既定文。</summary>
        public static string Brief(string effectKey, PetitionActionContext ctx)
        {
            switch (effectKey)
            {
                case "mil.mobilize": return MobilizeBrief(ctx);
                case "mil.offensive": return OffensiveBrief(ctx);
                case "mil.defend": return DefendBrief(ctx);
                case "diplo.ceasefire": return CeasefireBrief(ctx);
                case "tax.cut": return TaxBrief(ctx, down: true);
                case "tax.hike": return TaxBrief(ctx, down: false);
                default: return "費用：不明　対象：執行時に決まります　実行：即時（見込み）";
            }
        }

        private static string MobilizeBrief(PetitionActionContext ctx)
        {
            int yards = ctx != null && ctx.shipyards != null ? ctx.shipyards.Count : 0;
            float unit = ShipyardRules.Cost(ShipClass.巡航艦, ShipRole.戦闘艦);
            float full = unit * PetitionActionRules.MobilizeOrdersFull;
            string treasury = TreasuryLine(ctx);
            return $"費用：最大 {full:0}（1隻 {unit:0}×{PetitionActionRules.MobilizeOrdersFull}隻・国庫から即時）{treasury}\n"
                 + $"対象：自勢力の造船所 {yards} か所へ発注\n"
                 + "期待効果（見込み）：完成した艦から順に艦艇プールへ入る。<b>その場では増えない</b>\n"
                 + "実行：発注は即時／艦の完成は造船所の進捗しだい";
        }

        private static string OffensiveBrief(PetitionActionContext ctx)
        {
            int idle = 0;
            if (ctx != null && ctx.fleets != null && ctx.fleets.fleets != null)
                for (int i = 0; i < ctx.fleets.fleets.Count; i++)
                {
                    StrategicFleet f = ctx.fleets.fleets[i];
                    if (f == null || f.faction != ctx.faction) continue;
                    if (f.engaged || f.IsOnCorridor || f.warpingAsReinforcement) continue;
                    if (f.strength > 0) idle++;
                }
            return "費用：国庫（戦費）\n"
                 + $"対象：手の空いている自軍艦隊 {idle} 隊のうち最大 {PetitionActionRules.OffensiveFleetsFull} 隊\n"
                 + "期待効果（見込み）：最も近い敵星系へ進撃を命じる。会戦になるかは相手しだい\n"
                 + "実行：命令は即時／到着は航路の長さしだい";
        }

        private static string DefendBrief(PetitionActionContext ctx)
        {
            int weak = 0;
            if (ctx != null && ctx.map != null && ctx.map.systems != null)
                for (int i = 0; i < ctx.map.systems.Count; i++)
                {
                    StarSystem s = ctx.map.systems[i];
                    if (s?.planet == null || s.planet.owner != ctx.faction) continue;
                    if (s.planet.maxOrbitalDefense - s.planet.orbitalDefense > 0.01f) weak++;
                }
            return $"費用：最大 {PetitionActionRules.DefendCostFull:0}（国庫から即時）{TreasuryLine(ctx)}\n"
                 + $"対象：制空が削られた自勢力の惑星 {weak} 個のうち、最も削られた1つ\n"
                 + $"期待効果（見込み）：制空を最大 +{PetitionActionRules.DefendRestoreFull:0} 回復（攻城戦で実際に消費される値）\n"
                 + "実行：即時";
        }

        private static string CeasefireBrief(PetitionActionContext ctx)
        {
            string enemy = "（交戦相手なし）";
            if (ctx != null && ctx.diplomacy != null)
            {
                string me = ctx.faction.ToString();
                var all = (Faction[])System.Enum.GetValues(typeof(Faction));
                for (int i = 0; i < all.Length; i++)
                {
                    string other = all[i].ToString();
                    if (other == me) continue;
                    if (ctx.diplomacy.Status(me, other) == DiplomacyState.DiplomaticStatus.交戦) { enemy = other; break; }
                }
            }
            return "費用：なし（版図の主張は取り下げる）\n"
                 + $"対象：交戦中の相手 {enemy}\n"
                 + "期待効果（見込み）：交戦→平時。<b>相手の厭戦が足りなければ拒否される</b>\n"
                 + "実行：即時（相手が受ければ）";
        }

        private static string TaxBrief(PetitionActionContext ctx, bool down)
        {
            float step = PetitionEffects.TaxStepFull * 100f;
            FactionState fs = ctx?.State();
            string now = fs != null ? $"（現在 {fs.taxRate * 100f:0}%）" : "";
            return down
                ? $"費用：歳入が細る　対象：税率{now}\n期待効果（見込み）：税率 −{step:0}pt（官僚の執行忠実度ぶん値切られる）\n実行：即時"
                : $"費用：民心が削れる　対象：税率{now}\n期待効果（見込み）：税率 +{step:0}pt（官僚の執行忠実度ぶん値切られる）\n実行：即時";
        }

        private static string TreasuryLine(PetitionActionContext ctx)
        {
            FactionState fs = ctx?.State();
            return fs != null ? $"／国庫 {fs.treasury:0}" : "";
        }
    }
}
