using System.Collections.Generic;
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
        public static string battleLabel;              // 会戦ウィンドウの見出し用（戦っている場所名・WIN-3 #2570）

        // 旗幟（#817 関ヶ原型）：国家状態由来の基準忠誠/調略浸透を会戦へ運ぶ（既定 1/0＝従来動作）
        public static float loyaltyA = 1f, loyaltyB = 1f;
        public static float intrigueA = 0f, intrigueB = 0f;
        public static float qualityA = 1f, qualityB = 1f; // 軍の質の戦闘力倍率（C4・ForceQualityRules。既定1.0＝従来動作）

        // 初期配置（守備=中央/攻撃=侵攻方向）：星系/惑星の所有者を守備側として中央へ、攻撃側を侵攻方向へ寄せる。
        // hasDefender=false（既定）なら従来の左右対称配置（回廊上の相互衝突など守備側が定まらない会戦）。
        public static bool hasDefender;          // 守備側が定義されているか
        public static Faction defenderFaction;   // 守備側＝係争星系/惑星の所有勢力
        public static Vector2 approachDir;        // 攻撃側が侵攻してきた方向（係争地→攻撃側起点の単位ベクトル・戦略座標）

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

        // 守備隊の引き継ぎ（#131 第4段・戦略の惑星→戦術アリーナ）。maxGarrison 0＝守備隊なし＝従来動作。
        public static int planetMaxGarrison;        // 守備隊の最大（名）
        public static float planetGarrisonRatio = 1f; // 突入時の守備隊残り割合(0..1)
        public static float planetGarrisonMorale = 1f; // 突入時の守備隊士気(0..1)

        // ===== システムビュー（戦闘中でなくても星系をダブルクリックで戦術マップへ入る・恒星系の閲覧）=====
        public static bool IsSystemView;        // この受け渡しが非戦闘のシステムビューか
        public static int systemViewId;         // 入場する星系ID
        public static string systemViewName;    // 表示名
        public static Faction systemViewOwner;  // 星系の所有勢力

        // ===== 回廊要塞の攻略戦＝戦術潜行（FortressUnit 戦・#40 次スライス／#76-78 と接続）=====
        // 戦略マップの回廊要塞（Fortress）に阻まれた艦隊が、自動解決でなく実会戦（FortressUnit）へ潜行する。
        // 戻ったとき GalaxyView が fortressSysA/B で回廊の Fortress を引き、結果を反映する。
        public static bool IsFortressSiege;        // この受け渡しが回廊要塞の攻略戦か
        public static int fortressSysA, fortressSysB; // 封鎖回廊（無向・戻ってから Fortress を引く）
        public static string fortressName;         // 要塞名（表示・会戦見出し）
        public static Faction fortressOwner;       // 守備側＝要塞所有勢力
        public static Faction fortressAttacker;    // 攻撃側＝潜行してきた勢力
        public static int fortressAttackerStrength; // 攻撃側の戦略兵力（合計）
        public static float fortressGarrisonStrength; // 守備戦力（砲台数/コア耐久のスケール根拠）
        public static float fortressShieldIntegrity = 1f; // シールド健全度（戦術規模の素地）
        public static bool fortressHasMainCannon = true;  // 主砲（トールハンマー）を備えるか

        // 出力：要塞攻略の結果（戦術会戦の帰結）
        public static bool fortressResolved;            // 要塞攻略の結果が書き込まれたか
        public static bool fortressResultCaptured;      // 戦術会戦で要塞を制圧（陥落）させたか
        public static int fortressResultAttackerSurvivor; // 攻撃側の残存戦略兵力

        // 攻城の戦術マップでの進捗を戦略へ書き戻す（戻ったとき GalaxyView が惑星へ反映）
        public static bool siegeResolved;        // 攻城結果が書き込まれた
        public static float siegeResultDefense;  // 残った制空権の割合(0..1)
        public static float siegeResultInvasion; // 侵略値の割合(0..1)
        public static bool siegeResultCaptured;  // 戦術マップで占領まで至ったか
        public static float siegeResultGarrison = 1f;  // 残った守備隊割合(0..1)
        public static float siegeResultMorale = 1f;    // 残った守備隊士気(0..1)
        public static bool siegeResultSurrendered;     // 四面楚歌で降伏して落ちたか

        /// <summary>戦術マップでの攻城進捗を結果として書き込む（割合・占領フラグ）。</summary>
        public static void SetSiegeResult(float defenseRatio, float invasionRatio, bool captured)
        {
            siegeResultDefense = Mathf.Clamp01(defenseRatio);
            siegeResultInvasion = Mathf.Clamp01(invasionRatio);
            siegeResultCaptured = captured;
            siegeResolved = true;
        }

        /// <summary>守備隊（割合・士気・降伏）も含めて攻城結果を書き込む（#131 第4段）。</summary>
        public static void SetSiegeResult(float defenseRatio, float invasionRatio, bool captured,
                                          float garrisonRatio, float garrisonMorale, bool surrendered)
        {
            SetSiegeResult(defenseRatio, invasionRatio, captured);
            siegeResultGarrison = Mathf.Clamp01(garrisonRatio);
            siegeResultMorale = Mathf.Clamp01(garrisonMorale);
            siegeResultSurrendered = surrendered;
        }

        /// <summary>
        /// 惑星攻城を戦術マップへ予約する（惑星中心・攻城艦隊が包囲・首飾り射程の外まで接近）。
        /// </summary>
        public static void QueuePlanetSiege(int systemId, string name, Faction owner, float defenseRatio,
            float invasionRatio, Faction besieger, int strength, string returnScene,
            Planet.SiegeTargetKind kind = Planet.SiegeTargetKind.惑星,
            int maxGarrison = 0, float garrisonRatio = 1f, float garrisonMorale = 1f)
        {
            IsPlanetSiege = true;
            IsSystemView = false;
            IsFortressSiege = false;
            planetSystemId = systemId;
            planetName = name;
            planetOwner = owner;
            planetKind = kind;
            planetDefenseRatio = defenseRatio;
            planetInvasionRatio = invasionRatio;
            besiegerFaction = besieger;
            besiegerStrength = strength;
            // 守備隊（#131 第4段）。maxGarrison 0＝守備隊なし＝従来動作。
            planetMaxGarrison = Mathf.Max(0, maxGarrison);
            planetGarrisonRatio = Mathf.Clamp01(garrisonRatio);
            planetGarrisonMorale = Mathf.Clamp01(garrisonMorale);
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
            IsFortressSiege = false;
            systemViewId = systemId;
            systemViewName = name;
            systemViewOwner = owner;
            BattleHandoff.returnScene = returnScene;
            Pending = true;
            Resolved = false;
            siegeResolved = false;
        }

        /// <summary>
        /// 回廊要塞への攻略戦（戦術潜行）を予約する（#40 次スライス）。守備側＝要塞所有勢力、攻撃側＝潜行勢力。
        /// 守備戦力/シールドから戦術側（FortressUnit）の砲台数・コア耐久がスケールされ、戻ったとき
        /// <paramref name="sysA"/>/<paramref name="sysB"/> で回廊の <see cref="Fortress"/> を引いて結果を反映する。
        /// </summary>
        public static void QueueFortressSiege(int sysA, int sysB, string name, Faction owner, Faction attacker,
            int attackerStrength, float garrisonStrength, float shieldIntegrity, bool hasMainCannon, string returnScene)
        {
            IsFortressSiege = true;
            IsPlanetSiege = false;
            IsSystemView = false;
            fleets.Clear();
            fortressSysA = sysA; fortressSysB = sysB;
            fortressName = string.IsNullOrEmpty(name) ? "要塞" : name;
            fortressOwner = owner;
            fortressAttacker = attacker;
            fortressAttackerStrength = Mathf.Max(0, attackerStrength);
            fortressGarrisonStrength = Mathf.Max(0f, garrisonStrength);
            fortressShieldIntegrity = Mathf.Clamp01(shieldIntegrity);
            fortressHasMainCannon = hasMainCannon;
            admiralA = admiralB = null; // 攻撃側は臨時提督（stale な提督を持ち込まない）
            BattleHandoff.returnScene = returnScene;
            fortressResolved = false;
            Pending = true;
            Resolved = false;
        }

        /// <summary>要塞攻略戦の結果（制圧したか・攻撃側の残存戦略兵力）を書き込む（BattleManager から）。</summary>
        public static void SetFortressResult(bool captured, int attackerSurvivor)
        {
            fortressResultCaptured = captured;
            fortressResultAttackerSurvivor = Mathf.Max(0, attackerSurvivor);
            fortressResolved = true;
        }

        /// <summary>2つの戦略艦隊から実会戦を予約する。</summary>
        public static void Queue(StrategicFleet a, StrategicFleet b, string returnScene)
        {
            if (a == null || b == null) return;
            IsPlanetSiege = false;
            IsSystemView = false;
            IsFortressSiege = false;
            factionA = a.faction; strengthA = a.strength; fleetIdA = a.id; admiralA = null;
            factionB = b.faction; strengthB = b.strength; fleetIdB = b.id; admiralB = null;
            loyaltyA = loyaltyB = 1f;   // 旗幟は既定＝完全忠誠（戦略側が国家状態から上書きする・#817）
            intrigueA = intrigueB = 0f;
            qualityA = qualityB = 1f;   // 軍の質は既定＝1.0（戦略側が補給/練度から上書きする・C4）
            hasDefender = false;        // 回廊上の相互衝突は守備側を定めない（左右対称＝従来動作）
            BattleHandoff.returnScene = returnScene;
            Pending = true;
            Resolved = false;
        }

        // ===== 複数艦隊での会戦（接敵内容を会戦へ正しく渡す）=====
        // 回廊で複数の戦略艦隊が交戦したとき、1対1へ畳まず**全艦隊**を会戦へ持ち込むための明細。
        // 1隊＝1旗艦として Battle シーンに湧かせ、結果は勝者側へ按分して書き戻す。空＝従来の A/B 2隊モデル。

        /// <summary>会戦に持ち込む1艦隊ぶんの明細（戦略艦隊→戦術旗艦の対応）。</summary>
        public struct HandoffFleet
        {
            public Faction faction;
            public int strategicStrength;  // 戦略兵力（×StrengthScale で baseStrength）
            public AdmiralData admiral;    // 任意（無ければ臨時提督）
            public int fleetId;            // 戦略艦隊ID（書き戻し用）
            public float loyalty;          // 旗幟（#817）基準忠誠
            public float intrigue;         // 旗幟（#817）調略浸透
            public float quality;          // 軍の質（C4）戦闘力倍率
            public bool sideA;             // 陣営A=true / 陣営B=false
        }

        /// <summary>会戦に持ち込む全艦隊の明細（空＝従来の A/B 2隊モデル）。</summary>
        public static readonly List<HandoffFleet> fleets = new List<HandoffFleet>();

        /// <summary>複数艦隊モードか（明細が積まれていれば true）。</summary>
        public static bool IsMultiFleet => fleets.Count > 0;

        /// <summary>
        /// 複数艦隊の会戦を予約する（接敵内容を会戦へ）。<paramref name="entries"/> の各艦隊を会戦へ湧かせる。
        /// 代表（A/B の faction/fleetId）は通知・暫定勝者・後方互換のために保持し、戦力は陣営合計を入れる。
        /// </summary>
        public static void QueueMulti(List<HandoffFleet> entries, Faction repA, Faction repB,
            int repFleetIdA, int repFleetIdB, string returnScene)
        {
            IsPlanetSiege = false;
            IsSystemView = false;
            IsFortressSiege = false;
            fleets.Clear();
            int sa = 0, sb = 0;
            if (entries != null)
                for (int i = 0; i < entries.Count; i++)
                {
                    fleets.Add(entries[i]);
                    if (entries[i].sideA) sa += entries[i].strategicStrength;
                    else sb += entries[i].strategicStrength;
                }
            factionA = repA; factionB = repB;
            strengthA = sa; strengthB = sb;
            fleetIdA = repFleetIdA; fleetIdB = repFleetIdB;
            admiralA = admiralB = null;
            loyaltyA = loyaltyB = 1f;
            intrigueA = intrigueB = 0f;
            qualityA = qualityB = 1f;
            hasDefender = false;        // 既定は守備側なし（呼び出し側が必要なら直後に設定する）
            BattleHandoff.returnScene = returnScene;
            Pending = true;
            Resolved = false;
        }

        /// <summary>
        /// 守備側（係争星系/惑星の所有勢力）と攻撃側の侵攻方向を設定する（<see cref="QueueMulti"/>/<see cref="Queue"/> の直後に呼ぶ）。
        /// これで <c>BattleSetup</c> が守備側を中央・攻撃側を侵攻方向へ配置する。回廊の相互衝突など守備側が無い会戦では呼ばない。
        /// </summary>
        public static void SetDefender(Faction defender, Vector2 attackerApproachDir)
        {
            hasDefender = true;
            defenderFaction = defender;
            approachDir = attackerApproachDir.sqrMagnitude > 1e-6f ? attackerApproachDir.normalized : Vector2.right;
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
            IsFortressSiege = false;
            siegeResolved = false;
            fortressResolved = false;
            hasDefender = false;
            admiralA = admiralB = null;
            fleets.Clear();
        }

        // ===== スナップショット（WIN-3 #2570 複数同時会戦）=====
        // BattleHandoff は static 単一スロットのため、複数会戦を同時に扱うには各会戦が自分の受け渡し内容を
        // 退避（Capture）して保持し、ロード時・結果書き戻し時に復元（Restore）する必要がある。
        // BattleDirector がロードを直列化する間や、各 BattleManager の決着時に使う。

        /// <summary>BattleHandoff の全状態のスナップショット（会戦ごとに保持・復元する）。</summary>
        public sealed class State
        {
            public bool Pending, Resolved;
            public Faction factionA, factionB;
            public int strengthA, strengthB;
            public AdmiralData admiralA, admiralB;
            public int fleetIdA, fleetIdB;
            public string returnScene;
            public string battleLabel;
            public float loyaltyA, loyaltyB, intrigueA, intrigueB, qualityA, qualityB;
            public bool hasDefender;
            public Faction defenderFaction;
            public Vector2 approachDir;
            public bool sideAWon;
            public int survivorStrength;
            public bool IsPlanetSiege;
            public int planetSystemId;
            public string planetName;
            public Faction planetOwner;
            public Planet.SiegeTargetKind planetKind;
            public float planetDefenseRatio, planetInvasionRatio;
            public Faction besiegerFaction;
            public int besiegerStrength;
            public int planetMaxGarrison;
            public float planetGarrisonRatio, planetGarrisonMorale;
            public bool IsSystemView;
            public int systemViewId;
            public string systemViewName;
            public Faction systemViewOwner;
            public bool IsFortressSiege;
            public int fortressSysA, fortressSysB;
            public string fortressName;
            public Faction fortressOwner, fortressAttacker;
            public int fortressAttackerStrength;
            public float fortressGarrisonStrength, fortressShieldIntegrity;
            public bool fortressHasMainCannon;
            public bool fortressResolved, fortressResultCaptured;
            public int fortressResultAttackerSurvivor;
            public bool siegeResolved;
            public float siegeResultDefense, siegeResultInvasion;
            public bool siegeResultCaptured;
            public float siegeResultGarrison, siegeResultMorale;
            public bool siegeResultSurrendered;
            public readonly List<HandoffFleet> fleets = new List<HandoffFleet>();
        }

        /// <summary>現在の static 状態をスナップショットへ退避する。</summary>
        public static State Capture()
        {
            var s = new State
            {
                Pending = Pending, Resolved = Resolved,
                factionA = factionA, factionB = factionB,
                strengthA = strengthA, strengthB = strengthB,
                admiralA = admiralA, admiralB = admiralB,
                fleetIdA = fleetIdA, fleetIdB = fleetIdB,
                returnScene = returnScene, battleLabel = battleLabel,
                loyaltyA = loyaltyA, loyaltyB = loyaltyB,
                intrigueA = intrigueA, intrigueB = intrigueB,
                qualityA = qualityA, qualityB = qualityB,
                hasDefender = hasDefender, defenderFaction = defenderFaction, approachDir = approachDir,
                sideAWon = sideAWon, survivorStrength = survivorStrength,
                IsPlanetSiege = IsPlanetSiege, planetSystemId = planetSystemId, planetName = planetName,
                planetOwner = planetOwner, planetKind = planetKind,
                planetDefenseRatio = planetDefenseRatio, planetInvasionRatio = planetInvasionRatio,
                besiegerFaction = besiegerFaction, besiegerStrength = besiegerStrength,
                planetMaxGarrison = planetMaxGarrison, planetGarrisonRatio = planetGarrisonRatio,
                planetGarrisonMorale = planetGarrisonMorale,
                IsSystemView = IsSystemView, systemViewId = systemViewId, systemViewName = systemViewName,
                systemViewOwner = systemViewOwner,
                IsFortressSiege = IsFortressSiege, fortressSysA = fortressSysA, fortressSysB = fortressSysB,
                fortressName = fortressName, fortressOwner = fortressOwner, fortressAttacker = fortressAttacker,
                fortressAttackerStrength = fortressAttackerStrength,
                fortressGarrisonStrength = fortressGarrisonStrength, fortressShieldIntegrity = fortressShieldIntegrity,
                fortressHasMainCannon = fortressHasMainCannon,
                fortressResolved = fortressResolved, fortressResultCaptured = fortressResultCaptured,
                fortressResultAttackerSurvivor = fortressResultAttackerSurvivor,
                siegeResolved = siegeResolved, siegeResultDefense = siegeResultDefense,
                siegeResultInvasion = siegeResultInvasion, siegeResultCaptured = siegeResultCaptured,
                siegeResultGarrison = siegeResultGarrison, siegeResultMorale = siegeResultMorale,
                siegeResultSurrendered = siegeResultSurrendered,
            };
            s.fleets.AddRange(fleets);
            return s;
        }

        /// <summary>スナップショットを static 状態へ復元する。</summary>
        public static void Restore(State s)
        {
            if (s == null) return;
            Pending = s.Pending; Resolved = s.Resolved;
            factionA = s.factionA; factionB = s.factionB;
            strengthA = s.strengthA; strengthB = s.strengthB;
            admiralA = s.admiralA; admiralB = s.admiralB;
            fleetIdA = s.fleetIdA; fleetIdB = s.fleetIdB;
            returnScene = s.returnScene; battleLabel = s.battleLabel;
            loyaltyA = s.loyaltyA; loyaltyB = s.loyaltyB;
            intrigueA = s.intrigueA; intrigueB = s.intrigueB;
            qualityA = s.qualityA; qualityB = s.qualityB;
            hasDefender = s.hasDefender; defenderFaction = s.defenderFaction; approachDir = s.approachDir;
            sideAWon = s.sideAWon; survivorStrength = s.survivorStrength;
            IsPlanetSiege = s.IsPlanetSiege; planetSystemId = s.planetSystemId; planetName = s.planetName;
            planetOwner = s.planetOwner; planetKind = s.planetKind;
            planetDefenseRatio = s.planetDefenseRatio; planetInvasionRatio = s.planetInvasionRatio;
            besiegerFaction = s.besiegerFaction; besiegerStrength = s.besiegerStrength;
            planetMaxGarrison = s.planetMaxGarrison; planetGarrisonRatio = s.planetGarrisonRatio;
            planetGarrisonMorale = s.planetGarrisonMorale;
            IsSystemView = s.IsSystemView; systemViewId = s.systemViewId; systemViewName = s.systemViewName;
            systemViewOwner = s.systemViewOwner;
            IsFortressSiege = s.IsFortressSiege; fortressSysA = s.fortressSysA; fortressSysB = s.fortressSysB;
            fortressName = s.fortressName; fortressOwner = s.fortressOwner; fortressAttacker = s.fortressAttacker;
            fortressAttackerStrength = s.fortressAttackerStrength;
            fortressGarrisonStrength = s.fortressGarrisonStrength; fortressShieldIntegrity = s.fortressShieldIntegrity;
            fortressHasMainCannon = s.fortressHasMainCannon;
            fortressResolved = s.fortressResolved; fortressResultCaptured = s.fortressResultCaptured;
            fortressResultAttackerSurvivor = s.fortressResultAttackerSurvivor;
            siegeResolved = s.siegeResolved; siegeResultDefense = s.siegeResultDefense;
            siegeResultInvasion = s.siegeResultInvasion; siegeResultCaptured = s.siegeResultCaptured;
            siegeResultGarrison = s.siegeResultGarrison; siegeResultMorale = s.siegeResultMorale;
            siegeResultSurrendered = s.siegeResultSurrendered;
            fleets.Clear();
            fleets.AddRange(s.fleets);
        }
    }
}
