using UnityEngine;

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
        public static Planet.SiegeTargetKind planetKind; // 攻城対象の種別（惑星/要塞/コロニー・PB-6 表示用）
        public static float planetDefenseRatio; // 制空権の残り割合(0..1)＝接近限界リングの根拠
        public static float planetInvasionRatio; // 侵略値の割合(0..1)＝突入時に引き継ぐ
        public static Faction besiegerFaction;  // 攻城側（突入する艦隊）
        public static int besiegerStrength;      // 攻城側の戦略兵力

        // ===== システムビュー（戦闘中でなくても星系をダブルクリックで戦術マップへ入る・恒星系の閲覧）=====
        public static bool IsSystemView;        // この受け渡しが非戦闘のシステムビューか
        public static int systemViewId;         // 入場する星系ID
        public static string systemViewName;    // 表示名
        public static Faction systemViewOwner;  // 星系の所有勢力

        // 攻城の戦術マップでの進捗を戦略へ書き戻す（戻ったとき GalaxyView が惑星へ反映）
        public static bool siegeResolved;        // 攻城結果が書き込まれた
        public static float siegeResultDefense;  // 残った制空権の割合(0..1)
        public static float siegeResultInvasion; // 侵略値の割合(0..1)
        public static bool siegeResultCaptured;  // 戦術マップで占領まで至ったか

        /// <summary>戦術マップでの攻城進捗を結果として書き込む（割合・占領フラグ）。</summary>
        public static void SetSiegeResult(float defenseRatio, float invasionRatio, bool captured)
        {
            siegeResultDefense = Mathf.Clamp01(defenseRatio);
            siegeResultInvasion = Mathf.Clamp01(invasionRatio);
            siegeResultCaptured = captured;
            siegeResolved = true;
        }

        /// <summary>
        /// 惑星攻城を戦術マップへ予約する（惑星中心・攻城艦隊が包囲・首飾り射程の外まで接近）。
        /// </summary>
        public static void QueuePlanetSiege(int systemId, string name, Faction owner, float defenseRatio,
            float invasionRatio, Faction besieger, int strength, string returnScene,
            Planet.SiegeTargetKind kind = Planet.SiegeTargetKind.惑星)
        {
            IsPlanetSiege = true;
            IsSystemView = false;
            planetSystemId = systemId;
            planetName = name;
            planetOwner = owner;
            planetKind = kind;
            planetDefenseRatio = defenseRatio;
            planetInvasionRatio = invasionRatio;
            besiegerFaction = besieger;
            besiegerStrength = strength;
            BattleHandoff.returnScene = returnScene;
            siegeResolved = false;
            Pending = true;
            Resolved = false;
        }

        /// <summary>
        /// 非戦闘のシステムビューを予約する（星系をダブルクリック→戦術マップで恒星系を閲覧）。
        /// 戦闘判定は行わず、Backspace で戦略マップへ戻る。
        /// </summary>
        public static void QueueSystemView(int systemId, string name, Faction owner, string returnScene)
        {
            IsSystemView = true;
            IsPlanetSiege = false;
            systemViewId = systemId;
            systemViewName = name;
            systemViewOwner = owner;
            BattleHandoff.returnScene = returnScene;
            Pending = true;
            Resolved = false;
            siegeResolved = false;
        }

        /// <summary>2つの戦略艦隊から実会戦を予約する。</summary>
        public static void Queue(StrategicFleet a, StrategicFleet b, string returnScene)
        {
            if (a == null || b == null) return;
            IsPlanetSiege = false;
            IsSystemView = false;
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
            IsSystemView = false;
            siegeResolved = false;
            admiralA = admiralB = null;
        }
    }
}
