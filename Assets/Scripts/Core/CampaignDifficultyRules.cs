namespace Ginei
{
    /// <summary>キャンペーンの難易度（新規戦役の設定画面で選ぶ）。</summary>
    public enum CampaignDifficulty { 易しい, 普通, 難しい }

    /// <summary>
    /// キャンペーン難易度の純ロジック（設定画面の選択 → 盤面/勝敗への反映・test-first・唯一の窓口）。
    /// 難易度は2つに効かせる：①勝敗しきい値（<see cref="CampaignVictoryRules.CampaignVictoryParams"/>）＝易しいほど
    /// プレイヤーは少ない支配で勝て・敵は多く握らないと勝てない／②開始戦力の傾き（易しい＝自軍強め・敵弱め）。
    /// 数値ロジックはここに集約し、`GalaxyView`（盤面/勝敗）と `StrategyMapWindow`（進捗バーのしきい値）が読む。
    /// </summary>
    public static class CampaignDifficultyRules
    {
        /// <summary>
        /// 難易度ごとの勝敗しきい値。**制覇勝利は難易度を問わず「すべての星系（惑星）を占領」＝dominationFraction=1.0（全制圧）**。
        /// 難易度は敵制覇（敗北）しきい値 rivalDominationFraction と開始戦力で差をつける（勝利条件は据え置き全制圧）。
        /// </summary>
        public static CampaignVictoryRules.CampaignVictoryParams VictoryParams(CampaignDifficulty d)
        {
            switch (d)
            {
                case CampaignDifficulty.易しい: return new CampaignVictoryRules.CampaignVictoryParams(1.0f, 0.8f);
                case CampaignDifficulty.難しい: return new CampaignVictoryRules.CampaignVictoryParams(1.0f, 0.6f);
                default: return new CampaignVictoryRules.CampaignVictoryParams(1.0f, 0.7f);
            }
        }

        /// <summary>プレイヤー艦隊の開始戦力倍率（易しい＝強い／難しい＝弱い）。基準は1.0。</summary>
        public static float PlayerStrengthFactor(CampaignDifficulty d)
        {
            switch (d)
            {
                case CampaignDifficulty.易しい: return 1.3f;
                case CampaignDifficulty.難しい: return 0.8f;
                default: return 1f;
            }
        }

        /// <summary>敵対勢力の開始戦力倍率（易しい＝弱い／難しい＝強い）。基準は1.0。</summary>
        public static float EnemyStrengthFactor(CampaignDifficulty d)
        {
            switch (d)
            {
                case CampaignDifficulty.易しい: return 0.8f;
                case CampaignDifficulty.難しい: return 1.25f;
                default: return 1f;
            }
        }

        /// <summary>表示用の短い説明（設定画面の補足）。</summary>
        public static string Describe(CampaignDifficulty d)
        {
            switch (d)
            {
                case CampaignDifficulty.易しい: return "自軍強め・敵弱め／全星系を占領で勝利";
                case CampaignDifficulty.難しい: return "自軍弱め・敵強め／全星系を占領で勝利";
                default: return "互角／全星系を占領で勝利";
            }
        }
    }
}
