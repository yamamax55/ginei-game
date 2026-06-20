namespace Ginei
{
    /// <summary>
    /// 蔭位（おんい）制の純ロジック（日本の律令制・官僚制基盤・史実参考＝養老令の蔭位表）。
    /// <b>父祖が五位以上なら、その子（三位以上は嫡孫も）は父祖の位階に応じた位階を出身時に与えられる</b>＝
    /// 家柄（門閥）が出発点を決める仕組み。試験（<see cref="CivilEntryRoute.貢挙"/>）による実力登用が振るわず、
    /// 蔭位・譜第が官界を占めた日本の特徴を表す。<see cref="JapaneseCourtRankRules"/> の位階上で解決する。
    /// 純ロジック（非 MonoBehaviour・test-first）。皇親（親王・諸王）は別表ゆえ本実装は諸臣のみ（後方互換）。
    /// </summary>
    public static class OnishikiRules
    {
        /// <summary>蔭位の資格があるか＝父祖が五位以上（貴族）。</summary>
        public static bool IsEligible(CourtRank parentRank) => JapaneseCourtRankRules.IsNobility(parentRank);

        /// <summary>
        /// 蔭位による出身時の位階を返す。資格が無ければ <see cref="CourtRank.無位"/>（＝蔭位では出身できず、
        /// 貢挙や雑任の経路による）。<paramref name="legitimate"/>＝嫡子（false＝庶子は一段低い）。
        /// <paramref name="grandchild"/>＝嫡孫（三位以上のみ蔭が及び、嫡子の位より一階下げ。四位五位の孫は無位）。
        /// </summary>
        public static CourtRank StartingRank(CourtRank parentRank, bool legitimate, bool grandchild = false)
        {
            if (!IsEligible(parentRank)) return CourtRank.無位;

            int s = (int)parentRank;
            CourtRank chakushi, shoshi;
            bool isThirdOrAbove; // 三位以上＝嫡孫にも蔭が及ぶ

            if (s <= (int)CourtRank.従一位)        { chakushi = CourtRank.従五位下; shoshi = CourtRank.正六位上; isThirdOrAbove = true; }
            else if (s <= (int)CourtRank.従二位)   { chakushi = CourtRank.正六位下; shoshi = CourtRank.従六位上; isThirdOrAbove = true; }
            else if (s <= (int)CourtRank.従三位)   { chakushi = CourtRank.従六位上; shoshi = CourtRank.従六位下; isThirdOrAbove = true; }
            else if (s <= (int)CourtRank.正四位下) { chakushi = CourtRank.正七位下; shoshi = CourtRank.従七位上; isThirdOrAbove = false; }
            else if (s <= (int)CourtRank.従四位下) { chakushi = CourtRank.従七位上; shoshi = CourtRank.従七位下; isThirdOrAbove = false; }
            else if (s <= (int)CourtRank.正五位下) { chakushi = CourtRank.正八位下; shoshi = CourtRank.従八位上; isThirdOrAbove = false; }
            else                                   { chakushi = CourtRank.従八位上; shoshi = CourtRank.従八位下; isThirdOrAbove = false; }

            if (grandchild)
            {
                if (!isThirdOrAbove) return CourtRank.無位;          // 四位五位の孫には蔭が及ばない
                return JapaneseCourtRankRules.Previous(chakushi);    // 嫡孫は嫡子の位より一階下げ
            }
            return legitimate ? chakushi : shoshi;
        }

        /// <summary>蔭位で出身できたか（資格があり、与えられる位階が無位でない）。</summary>
        public static bool TryStartingRank(CourtRank parentRank, bool legitimate, out CourtRank start, bool grandchild = false)
        {
            start = StartingRank(parentRank, legitimate, grandchild);
            return start != CourtRank.無位;
        }
    }
}
