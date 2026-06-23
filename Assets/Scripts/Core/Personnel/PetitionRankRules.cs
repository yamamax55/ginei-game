namespace Ginei
{
    /// <summary>具申の区分（階級ゲートの単位）。低位ほど誰でも、高位ほど将官専用。海賊狩りは下級士官専用（上限あり）。</summary>
    public enum PetitionCategory { 建白, 予算, 編制, 作戦, 人事, 政治, 海賊狩り }

    /// <summary>
    /// 具申の階級ゲート（純・決定論）。階級（rankTier）で出せる具申を制限する唯一の窓口。
    /// 尉官/佐官（tier1-4）は自隊レベル（建白・予算）どまり、将官（tier5＝准将以上）から作戦・人事・政治の具申が解禁。
    /// しきい値は <see cref="RankSystem"/> の既定ラダー（准将5/少将6/中将7…）に合わせる。<see cref="ProtagonistDeskOverlay"/> の
    /// ボタン活性と <see cref="ProtagonistCareerDirector"/> の提出ガードが共用。test-first。
    /// </summary>
    public static class PetitionRankRules
    {
        /// <summary>その区分の具申を出せる最低階級 tier。</summary>
        public static int MinTier(PetitionCategory category)
        {
            switch (category)
            {
                case PetitionCategory.建白: return 0; // 誰でも（汎用建白）
                case PetitionCategory.予算: return 0; // 尉官から自隊の予算/増援級は具申できる
                case PetitionCategory.海賊狩り: return 1; // 少尉から（最下級の腕試し）
                case PetitionCategory.編制: return 3; // 少佐級（中堅）から
                case PetitionCategory.作戦: return 5; // 准将＝将官から（根拠地変更/出撃提案）
                case PetitionCategory.人事: return 6; // 少将から（昇進推挙/恩賞）
                default: return 7;                    // 政治＝中将から（講和/同盟/予算配分）
            }
        }

        /// <summary>その区分の具申を出せる最高階級 tier（既定は上限なし）。海賊狩りは下級士官専用ゆえ上限あり。</summary>
        public static int MaxTier(PetitionCategory category)
        {
            switch (category)
            {
                case PetitionCategory.海賊狩り: return 2; // 大尉まで（将官の任ではない＝下級士官の仕事）
                default: return int.MaxValue;
            }
        }

        /// <summary>その階級でこの区分の具申を出せるか（最低〜最高の範囲）。</summary>
        public static bool CanRaise(PetitionCategory category, int rankTier)
            => rankTier >= MinTier(category) && rankTier <= MaxTier(category);
    }
}
