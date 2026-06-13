namespace Ginei
{
    /// <summary>キャンペーンの決着（継続/勝利/敗北）。プレイヤー視点の戦略的帰結。</summary>
    public enum CampaignOutcome { 継続, 勝利, 敗北 }

    /// <summary>
    /// キャンペーンの勝敗条件の純ロジック（遊べる縦スライスの核・test-first・唯一の窓口）。
    /// 戦略マップ（<see cref="GalaxyMap"/>）の星系所有から、プレイヤー勢力の<b>制覇（支配率）・制圧（敵排除）・滅亡</b>を判定する。
    /// ＝これまでのシミュ（政体/軍政/財政/会戦）が「勝つための手段」として意味を持つための目標。`GalaxyView` が年次で評価する。
    /// </summary>
    public static class CampaignVictoryRules
    {
        /// <summary>勝敗の調整値。</summary>
        public readonly struct CampaignVictoryParams
        {
            /// <summary>この支配率（所有星系/全星系）以上で制覇勝利（0..1）。</summary>
            public readonly float dominationFraction;

            public CampaignVictoryParams(float dominationFraction)
            {
                this.dominationFraction = UnityEngine.Mathf.Clamp01(dominationFraction);
            }

            /// <summary>既定＝6割の星系を支配で勝利。</summary>
            public static CampaignVictoryParams Default => new CampaignVictoryParams(0.6f);
        }

        /// <summary>全星系数。</summary>
        public static int TotalSystems(GalaxyMap map)
            => map == null || map.systems == null ? 0 : map.systems.Count;

        /// <summary>その勢力が所有する星系数。</summary>
        public static int OwnedCount(GalaxyMap map, Faction faction)
        {
            if (map == null || map.systems == null) return 0;
            int n = 0;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s != null && s.owner == faction) n++;
            }
            return n;
        }

        /// <summary>その勢力の支配率（所有/全。星系0は0）。</summary>
        public static float OwnedFraction(GalaxyMap map, Faction faction)
        {
            int total = TotalSystems(map);
            return total <= 0 ? 0f : (float)OwnedCount(map, faction) / total;
        }

        /// <summary>プレイヤー以外（敵対勢力）が所有する星系が残っているか。</summary>
        public static bool RivalSystemsRemain(GalaxyMap map, Faction player)
        {
            if (map == null || map.systems == null) return false;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s != null && s.owner != player) return true;
            }
            return false;
        }

        /// <summary>
        /// プレイヤー視点の決着を判定する：
        /// 星系を1つも持たない＝敗北／敵対所有が残っていない（全制圧）or 支配率が閾値以上＝勝利／ほかは継続。
        /// </summary>
        public static CampaignOutcome Evaluate(GalaxyMap map, Faction player, CampaignVictoryParams prm)
        {
            int total = TotalSystems(map);
            if (total <= 0) return CampaignOutcome.継続; // 盤面未構築

            if (OwnedCount(map, player) == 0) return CampaignOutcome.敗北; // 滅亡
            if (!RivalSystemsRemain(map, player)) return CampaignOutcome.勝利; // 全制圧
            if (OwnedFraction(map, player) >= prm.dominationFraction) return CampaignOutcome.勝利; // 制覇
            return CampaignOutcome.継続;
        }

        /// <summary>既定パラメータ版。</summary>
        public static CampaignOutcome Evaluate(GalaxyMap map, Faction player)
            => Evaluate(map, player, CampaignVictoryParams.Default);
    }
}
