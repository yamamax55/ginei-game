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

        /// <summary>2つの戦略艦隊から実会戦を予約する。</summary>
        public static void Queue(StrategicFleet a, StrategicFleet b, string returnScene)
        {
            if (a == null || b == null) return;
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
            admiralA = admiralB = null;
        }
    }
}
