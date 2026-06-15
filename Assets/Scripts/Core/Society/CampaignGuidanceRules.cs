namespace Ginei
{
    /// <summary>プレイヤーへの「次の一手」ヒント（戦略マップの行動指針）。`CampaignGuidanceRules` が状況から選ぶ。</summary>
    public enum CampaignHint
    {
        前線へ潜行,   // 交戦中の回廊がある＝今すぐ手動指揮できる戦いがある
        任務を発令,   // 遊休艦隊がある＝攻略任務(C)や編成(B)で動かせる
        領土を広げよ, // 動かせる戦いも遊休もないが敵領が残る＝進軍して攻める
        好機を待て,   // それ以外（敵領なし＝ほぼ決着 等）
    }

    /// <summary>
    /// 戦略キャンペーンの「次の一手」ガイダンス（遊べる縦スライス B＝目的可視化・純ロジック・test-first）。
    /// プレイ中に「自分はどうすべきか」を一行で示すための行動指針を、盤面の単純なシグナル
    /// （交戦中の回廊の有無・遊休艦隊数・敵領の残存）から選ぶ。文言/キーの整形は Game 層（<see cref="StrategyMapWindow"/>）。
    /// 勝率の状態色（勝利目前/守勢）は支配率から Game 側で着色＝ここは<b>行動</b>のみ返す（関心の分離）。
    /// </summary>
    public static class CampaignGuidanceRules
    {
        /// <summary>
        /// 次の一手を選ぶ。優先＝①交戦中の回廊（即・手動指揮）②遊休艦隊（任務/編成）③敵領が残る（進軍）④それ以外。
        /// </summary>
        public static CampaignHint NextAction(bool hasEngagement, int idleFleetCount, bool rivalSystemsRemain)
        {
            if (hasEngagement) return CampaignHint.前線へ潜行;
            if (idleFleetCount > 0) return CampaignHint.任務を発令;
            if (rivalSystemsRemain) return CampaignHint.領土を広げよ;
            return CampaignHint.好機を待て;
        }
    }
}
