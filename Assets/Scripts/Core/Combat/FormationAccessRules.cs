namespace Ginei
{
    /// <summary>
    /// 陣形の使用可否の純ロジック（軍神専用ゲートの単一窓口）。
    /// 車懸かり（旋回突撃）は軍神（`AdmiralData.isTranscendent`＝上杉謙信型）のみ使える。
    /// それ以外の陣形は誰でも使える（後方互換）。`FleetCommander.ChangeFormation`・
    /// `FormationDoctrineRules`・`CommandMenu` がこの窓口で判定する（二重実装しない）。test-first。
    /// </summary>
    public static class FormationAccessRules
    {
        /// <summary>軍神だけが使える陣形か（現状＝車懸かりのみ）。</summary>
        public static bool IsTranscendentOnly(Formation f)
        {
            return f == Formation.車懸かり;
        }

        /// <summary>その提督能力（軍神フラグ）でこの陣形を使えるか。</summary>
        public static bool CanUse(Formation f, bool isTranscendent)
        {
            if (IsTranscendentOnly(f)) return isTranscendent;
            return true;
        }
    }
}
