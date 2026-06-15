namespace Ginei
{
    /// <summary>
    /// 政治家の素養データ（政治家システム基盤・#159 GOV-6 / 目安箱 政治家箱 #1296 と接続）。
    /// ネームド人物（<see cref="Person"/>＝<see cref="PersonVocation.政治家"/>）に紐づく <b>政治固有のパラメータ</b>。
    /// 軍才/文才（<see cref="Person"/> の能力）とは別軸＝政治家は<b>民意と票</b>で生き死にする（game-introduction）。
    /// 解決は <see cref="PoliticianRules"/>（static）が唯一の窓口。基準値非破壊（実効値パターン）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    /// <remarks>
    /// <see cref="Person"/> に直接フィールドを足さず<b>別管理</b>する（<see cref="PersonVocationRules"/> と同じ方針）＝
    /// 政治家でない人物に政治パラメータを持たせない。<see cref="personId"/> でロスターと結合する。
    /// </remarks>
    [System.Serializable]
    public class PoliticianProfile
    {
        /// <summary>紐づく人物（<see cref="Person.id"/>）。</summary>
        public int personId;

        /// <summary>民意＝大衆人気（0..1・既定0.5）。世論の追い風/逆風で増減し、党員票（広い支持基盤）の源。</summary>
        public float popularity = 0.5f;

        /// <summary>弁舌＝演説・説得力（0..100・既定50）。人気を増幅し、世論を動かす（大衆動員）。</summary>
        public int oratory = 50;

        /// <summary>党内基盤＝派閥・領袖との結びつき（0..1・既定0.5）。議員票（党内の票）の源（GOV-7 #165）。</summary>
        public float partyStanding = 0.5f;

        /// <summary>清廉さ（0..100・既定50）。高いほどスキャンダルに強く、党内の談合で信頼される。</summary>
        public int integrity = 50;

        /// <summary>地盤・票田（地方箱の regionKey＝方面/星系。空＝全国区＝特定の票田を持たない）。</summary>
        public string homeRegionKey = "";

        /// <summary>スキャンダルの累積（0..1・既定0＝清廉）。実効人気を削り、時間で忘れられる（<see cref="PoliticianRules.TickYear"/>）。</summary>
        public float scandalLevel = 0f;

        public PoliticianProfile() { }

        public PoliticianProfile(int personId)
        {
            this.personId = personId;
        }
    }
}
