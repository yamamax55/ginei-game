namespace Ginei
{
    /// <summary>
    /// 武功の物語化＝動的異名の純ロジック（CDR-4 #2314）。静的な `epithet` に対し、武功記録
    /// （連勝・通算戦歴・武名 fame#2304）から二つ名を動的に解決する＝「常勝」「不敗」。列伝#785 の素地。
    /// 純ロジック（決定論）・test-first。
    /// </summary>
    public static class EpithetRules
    {
        /// <summary>「不敗」に要する無敗の勝利数。</summary>
        public const int UndefeatedWins = 10;
        /// <summary>「常勝」に要する通算勝利数。</summary>
        public const int EverVictoriousWins = 20;
        /// <summary>「歴戦」に要する武名。</summary>
        public const int VeteranFame = 90;

        /// <summary>
        /// 武功記録から異名を解決する（無ければ ""）。優先＝不敗（無敗かつ十分な勝利）＞常勝（通算勝利）＞歴戦（高武名）。
        /// </summary>
        public static string ResolveEpithet(int wins, int losses, int fame)
        {
            if (losses <= 0 && wins >= UndefeatedWins) return "不敗";
            if (wins >= EverVictoriousWins) return "常勝";
            if (fame >= VeteranFame) return "歴戦";
            return "";
        }

        /// <summary>異名を得たか（解決結果が非空）。</summary>
        public static bool HasEarnedEpithet(int wins, int losses, int fame)
            => !string.IsNullOrEmpty(ResolveEpithet(wins, losses, fame));
    }
}
