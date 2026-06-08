namespace Ginei
{
    /// <summary>
    /// 戦略マップの回廊戦闘を実会戦（Battleシーン）に渡すための受け渡し（C-3 実会戦差し替えの土台）。
    /// 戦略側が2勢力の戦力をここに積んで Battle シーンへ遷移し、BattleManager が勝敗・残存兵力を
    /// 書き戻して戻る。戻った後 StrategyRules.ApplyHandoffResult で戦略状態へ反映する。
    /// シーンをまたぐため static（プロセス内で同時に1件のみ＝同時に複数の実会戦は扱わない簡易版）。
    /// </summary>
    public static class BattleHandoff
    {
        /// <summary>戦略の抽象兵力 → 戦術の基準兵力(baseStrength) への倍率。残存はこの逆算で戦略へ戻す。</summary>
        public const int StrengthScale = 40;

        public static bool Pending;   // 実会戦を予約済み（Battleシーンはこれを見て遭遇から生成）
        public static bool Resolved;  // Battleシーンが結果を書き込んだ

        // 入力：2勢力の戦力
        public static Faction factionA, factionB;
        public static int strengthA, strengthB;
        public static AdmiralData admiralA, admiralB;  // 任意（演出用・無ければ既定提督）
        public static int fleetIdA, fleetIdB;          // 戦略側の艦隊ID（戻ってから紐付ける）
        public static string returnScene = "Strategy";

        // 出力：結果
        public static bool sideAWon;
        public static int survivorStrength;

        // ===== 惑星攻城モード（戦略マップで惑星に到着→戦術マップへ突入・#131 PB-1/PB-5）=====
        public static bool IsPlanetSiege;     // この受け渡しが惑星攻城か（false＝通常の回廊会戦）
        public static int planetSystemId;     // 攻める惑星の星系ID
        public static string planetName;      // 表示名
        public static Faction planetOwner;    // 惑星の所有勢力（守備側）
        public static float planetDefenseRatio; // 制空権の残り割合(0..1)＝接近限界リングの根拠
        public static Faction besiegerFaction;  // 攻城側（突入する艦隊）
        public static int besiegerStrength;      // 攻城側の戦略兵力

        /// <summary>
        /// 惑星攻城を戦術マップへ予約する（惑星中心・攻城艦隊が包囲・首飾り射程の外まで接近）。
        /// </summary>
        public static void QueuePlanetSiege(int systemId, string name, Faction owner, float defenseRatio,
            Faction besieger, int strength, string returnScene)
        {
            IsPlanetSiege = true;
            planetSystemId = systemId;
            planetName = name;
            planetOwner = owner;
            planetDefenseRatio = defenseRatio;
            besiegerFaction = besieger;
            besiegerStrength = strength;
            BattleHandoff.returnScene = returnScene;
            Pending = true;
            Resolved = false;
        }

        /// <summary>2つの戦略艦隊から実会戦を予約する。</summary>
        public static void Queue(StrategicFleet a, StrategicFleet b, string returnScene)
        {
            if (a == null || b == null) return;
            IsPlanetSiege = false;
            factionA = a.faction; strengthA = a.strength; fleetIdA = a.id; admiralA = null;
            factionB = b.faction; strengthB = b.strength; fleetIdB = b.id; admiralB = null;
            BattleHandoff.returnScene = returnScene;
            Pending = true;
            Resolved = false;
        }

        /// <summary>Battleシーン側が勝敗と勝者残存兵力を書き込む。</summary>
        public static void SetResult(bool aWon, int survivors)
        {
            sideAWon = aWon;
            survivorStrength = survivors;
            Resolved = true;
        }

        public static void Clear()
        {
            Pending = false;
            Resolved = false;
            IsPlanetSiege = false;
            admiralA = admiralB = null;
        }
    }
}
