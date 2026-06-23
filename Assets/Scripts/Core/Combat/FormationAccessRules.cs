namespace Ginei
{
    /// <summary>
    /// 陣形の使用可否の純ロジック（軍神/天才専用ゲートの単一窓口）。
    /// 限界突破した英傑（`AdmiralData.isTranscendent`）のみが使える伝説の陣形を制限する：
    /// 車懸かり（旋回突撃・上杉謙信型）／八陣（石兵八陣の防御迷宮・諸葛亮型）。
    /// それ以外の陣形は誰でも使える（後方互換）。`FleetCommander.ChangeFormation`・
    /// `FormationDoctrineRules`・`CommandMenu` がこの窓口で判定する（二重実装しない）。test-first。
    /// </summary>
    public static class FormationAccessRules
    {
        /// <summary>限界突破した英傑だけが使える伝説の陣形か（車懸かり／八陣）。</summary>
        public static bool IsTranscendentOnly(Formation f)
        {
            return f == Formation.車懸かり || f == Formation.八陣;
        }

        /// <summary>その提督能力（軍神/天才フラグ）でこの陣形を使えるか。</summary>
        public static bool CanUse(Formation f, bool isTranscendent)
        {
            if (IsTranscendentOnly(f)) return isTranscendent;
            return true;
        }
    }
}
