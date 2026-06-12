namespace Ginei
{
    /// <summary>
    /// 陸戦/地上軍の梯団（ORBAT-5 #1721・惑星戦 #131 の地上戦力）。現実準拠の階層
    /// 軍 ⊃ 軍団 ⊃ 師団 ⊃ 旅団 ⊃ 連隊 ⊃ 大隊 ⊃ 中隊 ⊃ 小隊 ⊃ 分隊（陸戦隊・装甲擲弾兵）。
    /// 並び順＝規模順（低→高）。宇宙艦隊系（<see cref="EchelonType"/>）と直交＝地上戦専用の段。
    /// </summary>
    public enum GroundEchelonType { 分隊, 小隊, 中隊, 大隊, 連隊, 旅団, 師団, 軍団, 軍 }

    /// <summary>
    /// 地上梯団の標準プロファイル（ORBAT-5 #1721）＝「指揮官階級 tier」と「人員レンジ（名）」を結ぶ値（read-only）。
    /// 出所は <see cref="GroundForceRules.ProfileFor"/>。
    /// </summary>
    public readonly struct GroundProfile
    {
        public readonly GroundEchelonType echelon;
        public readonly int commanderTier;  // 標準指揮官階級 tier（#14 ラダーを下方拡張＝佐官4/尉官以下0-3）
        public readonly int minPersonnel;   // 人員下限（名）
        public readonly int maxPersonnel;   // 人員上限（名・上限なしは int.MaxValue）

        public GroundProfile(GroundEchelonType echelon, int commanderTier, int minPersonnel, int maxPersonnel)
        {
            this.echelon = echelon;
            this.commanderTier = commanderTier;
            this.minPersonnel = minPersonnel;
            this.maxPersonnel = maxPersonnel;
        }

        public bool Contains(int personnel) => personnel >= minPersonnel && personnel <= maxPersonnel;

        public string ScaleText => maxPersonnel == int.MaxValue
            ? $"{minPersonnel:#,0}名〜"
            : $"{minPersonnel:#,0}〜{maxPersonnel:#,0}名";
    }

    /// <summary>
    /// 陸戦/地上軍の編制ロジック（ORBAT-5 #1721・純ロジック・test-first）。地上梯団の指揮官階級・人員規模・親子関係を
    /// 一表で定める（惑星攻城 #131 の地上戦力＝S-AV侵攻・陸戦隊の編制基盤）。宇宙艦隊系の編制とは別系統。
    /// ※データモデル層。実際の地上戦（SiegeArena 等の惑星攻城）への配線は後段。
    /// </summary>
    public static class GroundForceRules
    {
        // 地上梯団の一表（並び順＝GroundEchelonType の整数＝規模順）。指揮官 tier・人員（名）は現実準拠（軍隊の編制）。
        // これが唯一の出所（二重定義しない）。指揮官 tier は >= ゲートの下限。
        private static readonly GroundProfile[] table =
        {
            new GroundProfile(GroundEchelonType.分隊,  0,      8,      12),    // 軍曹〜兵長
            new GroundProfile(GroundEchelonType.小隊,  1,     30,      60),    // 中尉〜軍曹
            new GroundProfile(GroundEchelonType.中隊,  2,     60,     250),    // 少佐〜中尉
            new GroundProfile(GroundEchelonType.大隊,  3,    300,    1000),    // 中佐/少佐
            new GroundProfile(GroundEchelonType.連隊,  4,    500,    5000),    // 大佐/中佐
            new GroundProfile(GroundEchelonType.旅団,  5,   2000,    8000),    // 少将〜大佐
            new GroundProfile(GroundEchelonType.師団,  6,  10000,   20000),    // 中将/少将
            new GroundProfile(GroundEchelonType.軍団,  8,  30000,   60000),    // 大将/中将
            new GroundProfile(GroundEchelonType.軍,    9,  50000, int.MaxValue) // 元帥〜中将
        };

        /// <summary>その地上梯団の標準プロファイル（指揮官階級 tier ＋人員レンジ）。</summary>
        public static GroundProfile ProfileFor(GroundEchelonType echelon) => table[(int)echelon];

        /// <summary>その地上梯団の標準指揮官階級 tier。</summary>
        public static int CommanderTierFor(GroundEchelonType echelon) => ProfileFor(echelon).commanderTier;

        /// <summary>a が b より大きい段か（並び順＝規模順）。</summary>
        public static bool IsLarger(GroundEchelonType a, GroundEchelonType b) => (int)a > (int)b;

        /// <summary>自然な上位梯団（次の段）。最上段（軍）は null。</summary>
        public static GroundEchelonType? ParentOf(GroundEchelonType echelon)
            => echelon == GroundEchelonType.軍 ? (GroundEchelonType?)null : (GroundEchelonType)((int)echelon + 1);
    }
}
