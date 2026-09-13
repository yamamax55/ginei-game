namespace Ginei
{
    /// <summary>
    /// 戦略艦隊の「所属軍団」「司令官」を画面へ出すときの文言（純ロジック・test-first）。
    ///
    /// <b>なぜ Core にあるか</b>：この2つは誤解を生みやすく、誤解の中身が仕様そのものだから。
    /// <list type="bullet">
    ///   <item><see cref="StrategicFleet.isCorpsFlagship"/> は「<b>軍団の指揮を担う艦隊</b>」であって
    ///   旗艦の有無ではない。艦隊にはどれも旗艦（司令の乗艦）がある。単に「旗艦」と出すと
    ///   「他の艦隊には旗艦が無い」と読まれるので、<b>軍団旗艦</b>と出す。</item>
    ///   <item>司令官は<b>実際に任命された人物</b>の名前だけを出す。解決できないときは
    ///   <see cref="Unassigned"/>＝<b>名前を作らない・軍団長で代用しない</b>。</item>
    /// </list>
    /// </summary>
    public static class FleetCommandLabelRules
    {
        /// <summary>軍団に属していない艦隊の表示（独立艦隊）。</summary>
        public const string NoCorps = "―";

        /// <summary>司令官が任命されていない／解決できないときの表示。</summary>
        public const string Unassigned = "未任命";

        /// <summary>軍団の指揮を担う艦隊であることを示す語。「旗艦」単独では使わない。</summary>
        public const string CorpsFlagship = "軍団旗艦";

        /// <summary>
        /// 所属軍団の1行。軍団名が無ければ ID から補い、どちらも無ければ <see cref="NoCorps"/>。
        /// 軍団の指揮を担う艦隊には <see cref="CorpsFlagship"/> を添える
        /// （＝「この艦隊が軍団を率いている」であって「この艦隊にだけ旗艦がある」ではない）。
        /// </summary>
        public static string CorpsLabel(StrategicFleet fleet)
        {
            if (fleet == null) return NoCorps;
            return CorpsLabel(fleet.corpsId, fleet.corpsName, fleet.isCorpsFlagship);
        }

        /// <summary><inheritdoc cref="CorpsLabel(StrategicFleet)"/></summary>
        public static string CorpsLabel(int corpsId, string corpsName, bool isCorpsFlagship)
        {
            string body = !string.IsNullOrEmpty(corpsName)
                ? corpsName
                : (corpsId >= 0 ? "軍団#" + corpsId : "");

            if (string.IsNullOrEmpty(body))
                // 軍団に属していないのに軍団旗艦フラグだけ立っている＝データの不整合。
                // ここで勝手に軍団名を作らず、フラグだけを見せて気づけるようにする。
                return isCorpsFlagship ? CorpsFlagship : NoCorps;

            return isCorpsFlagship ? body + "（" + CorpsFlagship + "）" : body;
        }

        /// <summary>
        /// 司令官の1行。<paramref name="personName"/> が空なら <see cref="Unassigned"/>。
        /// 階級名があれば前置する（階級の解決は <see cref="RankSystem"/> 側の仕事）。
        /// <b>ここで名前を合成しない</b>＝渡された実名だけを出す。
        /// </summary>
        public static string CommanderLabel(string personName, string rankName = null)
        {
            if (string.IsNullOrEmpty(personName)) return Unassigned;
            if (string.IsNullOrEmpty(rankName)) return personName;
            return rankName + " " + personName;
        }

        /// <summary>
        /// 艦隊の呼び名。<b>「・旗艦」を付けない</b>＝軍団旗艦かどうかは
        /// <see cref="CorpsLabel(StrategicFleet)"/> の列で示す（艦隊名に混ぜると旗艦の有無と読まれる）。
        /// </summary>
        public static string FleetTitle(StrategicFleet fleet)
            => fleet == null ? "" : "第" + fleet.id + "艦隊";
    }
}
