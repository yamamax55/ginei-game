using System;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 戦役セーブの平データ（FND-2 #495・バージョン付きJSON）。<see cref="CampaignState"/>（世界状態）を
    /// JsonUtility で直列化できる素のフィールドだけに落とす（ScriptableObject 参照は名前文字列で持ち、復元時に解決）。
    /// 変換は <see cref="CampaignSerializer"/>、ファイル入出力＋SO解決は `CampaignSaveManager`（Unity）。
    /// </summary>
    [Serializable]
    public class CampaignSaveData
    {
        public int schemaVersion = CampaignSerializer.SchemaVersion;
        public List<StarSystemSave> systems = new List<StarSystemSave>();
        public List<CorridorSave> corridors = new List<CorridorSave>();
        public List<FactionStateSave> states = new List<FactionStateSave>();
        public List<PersonSave> people = new List<PersonSave>(); // ネームド人物ロスター（提督/文官・空=後方互換）
        public List<StrategicFleetSave> fleets = new List<StrategicFleetSave>(); // 戦略艦隊（盤面の駒・空=後方互換）
        public List<ProvinceSave> provinces = new List<ProvinceSave>(); // 惑星内政（#109/#759・空=後方互換）
        // 航行中の援軍（#38 C-5・空=後方互換＝旧セーブは援軍なしで読める）。
        public List<ReinforcementSave> reinforcements = new List<ReinforcementSave>();

        // ===== 稟議・決裁（#稟議完成②）=====
        // 進行中の稟議と決裁カードを保存する。無い旧セーブは空＝案件なしで読める（前方互換）。
        /// <summary>税などの稟議在庫（<see cref="RingiDirector"/> の台帳）。</summary>
        public List<PetitionSave> petitions = new List<PetitionSave>();
        /// <summary>編制の稟議在庫（<see cref="FleetRingiDirector"/> の台帳）。</summary>
        public List<PetitionSave> fleetPetitions = new List<PetitionSave>();
        // 稟議台帳の採番済み最大 id と容量打切りの累計（0=旧セーブ＝復元した id と決裁カードから採番を再構成）。
        public int petitionsLastIssuedId;
        public int petitionsDroppedCount;
        public int fleetPetitionsLastIssuedId;
        public int fleetPetitionsDroppedCount;
        /// <summary>決裁カード（未決も決裁済みの履歴も）。</summary>
        public List<DecisionSave> decisions = new List<DecisionSave>();
        // 統一時間（GameClock）。0=未設定（後方互換＝既定クロック）。
        public double clockElapsed;
        public float clockSpeed = 1f;
        // 朝廷の権威（官僚制基盤・名実の乖離）。既定0.35＝旧セーブに欠落していても武家政権相当で復元（後方互換）。
        public float courtAuthority = 0.35f;
        // 主人公の立身出世（一人称・TKO #2477・P1-c）。null=主人公なし＝後方互換（旧セーブは復元時に無視）。
        public ProtagonistCareerSave protagonistCareer;
        // 全提督の会戦成長（ADM-2 #2303・安定キー=admiralName で永続）。空=後方互換。
        public List<AdmiralGrowthSave> admiralGrowth = new List<AdmiralGrowthSave>();
    }

    /// <summary>提督の会戦成長（<see cref="Growth"/>）のセーブ平データ。実行時キー（InstanceID）は不安定なため
    /// 安定キー＝<see cref="AdmiralData.admiralName"/> で持ち、復元時に `ContentDatabase` で解決して `GrowthRegistry` へ戻す。</summary>
    [Serializable]
    public class AdmiralGrowthSave
    {
        public string admiralName;
        public float experience;
        public int archetype;  // (int)GrowthArchetype
    }

    /// <summary>
    /// 主人公の立身出世（一人称・TKO #2477・P1-c #2477）のセーブ平データ。階級・能力・武勲台帳・在席の主命・
    /// 会戦戦果の未処理インボックス・月次ループ状態を保存し、「続きから」で立身出世の進捗を失わない。
    /// 変換は <see cref="ProtagonistCareerSerializer"/>（純ロジック・test-first）、配線は <c>ProtagonistCareerDirector</c>。
    /// </summary>
    [Serializable]
    public class ProtagonistCareerSave
    {
        public bool hasData;
        public int faction;          // (int)Faction
        public int personId;
        public string name;
        public int rankTier;
        public int origin;           // (int)PersonOrigin（出自・既定0=平民＝後方互換）
        // 能力（成長で動くので保存＝基準は AdmiralData だが在席値を継続）
        public int leadership, attack, defense, mobility, operation, intelligence;
        // 武勲台帳（MeritRecord）
        public float meritPoints;
        public int meritExploitCount, meritPromotionsApplied;
        // 会戦で得た提督の成長（Growth・P1-b 永続＝会戦XPを捨てない）。hasGrowth=false なら未蓄積（後方互換）。
        public bool hasGrowth;
        public float growthExperience;
        public int growthArchetype;  // (int)GrowthArchetype
        // 月次ループ状態
        public int lastCouncilMonth;
        public int nextMandateId;
        public float grievance;      // 不満（主命失敗の蓄積・岐路判定 CareerForkRules 用）
        public int fame;             // 武名（ADM-3 #2304・戦功で上がり政界転身の資本に）
        public int pendingPetitions; // 未裁可の具申（TKO-4・月次評定で上官が裁可）
        public bool retired;         // 下野（軍を辞した・TKO-7 岐路）＝月次ループ停止
        public bool politician;      // 政界転身済み（政治家提督・TKO-7 岐路）
        public int ageMonths;        // 主人公の年齢（月）＝加齢/老衰死・継承の駆動（0=未設定＝既定初任年齢）
        // 会戦戦果の未処理インボックス（戦果→武勲は次の月次評定で変換＝保存中に失わない）
        public float pendingBattleDamage;
        public int pendingBattleVictories, pendingBattleCount;
        // 在席の主命（SovereignMandate・無ければ hasMandate=false）
        public bool hasMandate;
        public int mandateId, mandateFaction, mandateIssuerId, mandateAssigneeId;
        public int mandateKind, mandateStatus, mandateIssuedMonth, mandateDueMonth;
        public string mandateObjective;
        // 一代記（TKO-6・生涯イベント）。空=後方互換。
        public int chronicleDropped;
        public List<ChronicleEntrySave> chronicle = new List<ChronicleEntrySave>();
    }

    /// <summary>一代記の一行（TKO-6・<see cref="ChronicleEntry"/>）のセーブ平データ。</summary>
    [Serializable]
    public class ChronicleEntrySave
    {
        public int monthIndex;
        public int kind;      // (int)ChronicleEventKind
        public string note;
    }

    /// <summary>惑星内政（<see cref="Province"/>）のセーブ平データ。安定度/統合/経済/希少資源の継続。
    /// 人口動態/職業/技能の細部（demographics/workforce/skills）はロード後に再構築（マクロ背景＝再安定する）。</summary>
    [Serializable]
    public class ProvinceSave
    {
        public int systemId;
        public string nativeIdeology;
        public int systemType;        // (int)SystemType
        public float population;
        public float wageIndex, livingStandard, foodShortage;
        public bool hasStrategicResource;
        public int strategicResource; // (int)StrategicResourceType
        public float strategicAbundance;
        public float stability, integration;
        public int governancePolicy;  // (int)GovernancePolicy（既定0=民生＝旧セーブ後方互換）
    }

    /// <summary>星系のセーブ平データ。所有 SO は名前で持つ（復元時に Resources/Factions から解決）。</summary>
    [Serializable]
    public class StarSystemSave
    {
        public int id;
        public string name;
        public float posX, posY;
        public int owner;                 // (int)Faction（後方互換）
        public string ownerFactionName;   // FactionData.factionName（多勢力・無ければ空）
        public bool habitable = true;
        public bool isColonized = true;
        public int systemType;            // (int)SystemType
        public bool hasPlanet;
        public PlanetSave planet;         // hasPlanet のときのみ有効
    }

    /// <summary>防衛惑星のセーブ平データ（#131）。</summary>
    [Serializable]
    public class PlanetSave
    {
        public int systemId;
        public int owner;
        public float orbitalDefense, maxOrbitalDefense, invasionProgress, invasionThreshold;
    }

    /// <summary>
    /// 航行中の援軍（ワープイン）のセーブ平データ（#38 C-5）。
    /// 旧セーブにはこのリストが無い＝空で読める（前方互換）＝援軍なしとして復元される。
    /// 到着は<b>絶対 game-秒</b>で持つので、復元時に残り時間を計算し直さなくても整合する。
    /// </summary>
    [Serializable]
    public class ReinforcementSave
    {
        public int systemA, systemB;   // 戦場キー（回廊は両端／星系戦は同じ値）
        public int faction;            // (int)Faction
        public int fleetId;            // 戦略側の艦隊ID（帰投・到着の紐付け）
        public int strength;           // 抽象兵力
        public double dispatchTime;    // 派遣した game-秒
        public double arrivalTime;     // 到着する game-秒（絶対）
    }

    /// <summary>
    /// 稟議1件のセーブ平データ（#稟議完成②）。案件ID・対象・効果・状態・執行済みかを保つ。
    /// enum は int（JsonUtility 安全・前方互換）。
    /// </summary>
    [Serializable]
    public class PetitionSave
    {
        public int id;
        public string title = "";
        public int faction;        // (int)Faction
        public int box;            // (int)BoxKind
        public string regionKey = "";
        public int origin;         // (int)PetitionOrigin
        public int drafterId;
        public int addresseeId;
        public string effectKey = "";
        public int status;         // (int)PetitionStatus（執行済もここに入る）
        public int carrierId;
        public bool distorted;
        public bool vindicated;
    }

    /// <summary>
    /// 決裁カード1件のセーブ平データ（#稟議完成②）。
    /// <b>効果を適用済みか</b>（<see cref="PendingDecision.applied"/>）と、対応する稟議・摩擦も保存する
    /// ＝ロード後に同じ案件がもう一度効かない／稟議との対応を失わない。
    /// </summary>
    [Serializable]
    public class DecisionSave
    {
        public int id;
        public string title = "";
        public string body = "";
        public string imageKey = "";
        public int severity;       // (int)DecisionSeverity
        public int source;         // (int)DecisionSource
        public List<string> choices = new List<string>();
        public int defaultChoiceIndex;
        public string effectKey = "";
        public int status;         // (int)DecisionStatus
        public float elapsed;      // 提示からの経過 game-秒（残り期限の復元に要る）
        public int chosenIndex = -1;
        public bool applied;       // 効果を適用済みか（二重執行の防止）
        public bool meterApplied;  // 勝敗メーターへ反映済みか
        public int outcome;        // (int)PetitionActionOutcome
        public string resultDetail = "";
        public int petitionId;     // 対応する稟議（0=なし）
        // 誰が出して誰が決めるか・何を対象にするか（#67・提案対象の固定）
        public int proposerId;
        public string proposerName = "";
        public int deciderId;
        public string deciderName = "";
        public string authorityBasis = "";
        public string targetKey = "";   // PetitionTarget.Encode（空＝勢力全体）
        public bool escalated;          // 上申中（決裁権者の返事待ち）
        public float friction;     // 官僚の摩擦（執行忠実度の材料）
    }

    /// <summary>回廊のセーブ平データ。</summary>
    [Serializable]
    public class CorridorSave
    {
        public int aId, bId;
        public float length;
        public int type; // (int)CorridorType

        // ===== 回廊要塞（#40 C-7）=====
        // 旧セーブには無いフィールド＝JsonUtility は欠落を既定値で埋めるので hasFortress=false のまま読める
        // ＝要塞なしの通商回廊として復元される（前方互換）。新セーブだけが要塞を持ち帰る。
        public bool hasFortress;          // false＝要塞なし（フェザーン型／旧セーブ）
        public float fortGarrison;        // 守備戦力
        public float fortShield;          // 反射シールド健全度 0..1
        public float fortMainGun;         // 主砲威力
        public bool fortControlsCorridor; // 回廊を扼しているか（陥落後は false）
        public int fortOwner;             // (int)Faction
        public string fortName;           // 表示名
        // 駐留艦隊のID名簿（#40 駐留艦隊）。施設の守備値 fortGarrison とは<b>別勘定</b>で、
        // 空/欠落（旧セーブ）＝駐留なし＝従来どおり施設の守備値だけが効く。
        public List<int> fortGarrisonFleetIds;
    }

    /// <summary>勢力の国家状態のセーブ平データ（王朝/統治体/組織/共同体＋統治スタイル）。</summary>
    [Serializable]
    public class FactionStateSave
    {
        public int faction;
        public float inclusiveness;
        public int governmentForm; // (int)GovernmentForm（政体形態 #117・既定0=首長制＝旧セーブ前方互換）
        // Regime
        public float regimeLegitimacy, regimeCorruption, regimeVirtue;
        // Polity
        public int polityPopulation, polityRulerForce;
        public float polityCooperation, polityLegitimacy, polityOppression;
        // Organization
        public float orgCohesion, orgInstitutionalization, orgLeaderCharisma;
        public bool orgFragmented;
        // Community
        public float commHope, commRepression;
        public bool commDissent;
        // 財政（在席フロー＝全永続化方針で保存）：国庫/税率/予算分野配分/形式債務。
        public float treasury, taxRate;
        public float budgetMilitary, budgetShipbuilding, budgetAdministration, budgetWelfare, budgetResearch, budgetDiplomacy;
        public float fiscalDebt;
        // 政治（政党・議席・衆参/知事選の日程・直近の開票・選出された首相/知事）。
        // hasPolitics=false（旧セーブ含む）は politics を読まない＝null のまま復元し、次の年次で初期化する。
        // JsonUtility は null のクラスを空の既定値で書くので、必ずこの旗で有無を判定する。
        public bool hasPolitics;
        public PoliticsState politics;
    }

    /// <summary>戦略艦隊（盤面の駒）のセーブ平データ。回廊上の精密位置（私有）は保存せず、停泊星系に再構築（移動中は目的地へ再ワープ）。</summary>
    [Serializable]
    public class StrategicFleetSave
    {
        public int id;
        public int faction;             // (int)Faction
        public int strength;
        public float supply, warpSpeed, sublightFactor;
        public int currentSystemId;     // 停泊星系（移動中は出発元）
        public int destinationSystemId; // 移動中の目的地（0以下=停泊）
        public bool moving;             // 移動中だったか（ロードで再ワープ）
        public bool engaged;            // 交戦固着
        // 艦隊ごとの艦艇数（隻）。旧セーブにはこのフィールドが無い＝0 で読まれるので、
        // 復元時に FleetShipCountRules.EnsureInitialized が兵力から導出して埋める（後方互換）。
        public int shipCount;
        // 上の値が確定値か（旧セーブは false＝兵力から導出）。true なら 0 も「全滅」として保持する。
        public bool shipCountSet;

        // ── 編制と司令官（#軍団列・#指揮官列）──
        // ★いずれも「実値+1」で持つ。JsonUtility は旧セーブに無いフィールドを 0 で埋めるため、
        // 生の id をそのまま入れると「0 番の軍団／0 番の人物に所属」と誤読される。
        // 0＝無所属／未任命（＝旧セーブの既定）、1 以上＝実値+1、と決めておけば旧セーブが必ず安全側に落ちる。
        public int corpsIdPlus1;
        public string corpsName;
        public bool isCorpsFlagship;
        public int armyGroupIdPlus1;
        public string armyGroupName;
        /// <summary>司令官の人物ID+1（0＝未任命）。名前は保存せず、人物ロスターから引き直す。</summary>
        public int commanderPersonIdPlus1;
    }

    /// <summary>ネームド人物（<see cref="Person"/>）の平データ（軍人/文民ロスターの永続化）。enum は int で持つ（JsonUtility 安全・前方互換）。</summary>
    [System.Serializable]
    public class PersonSave
    {
        public int id;
        public string name;
        public int faction;          // (int)Faction
        public int role;             // (int)PersonRole
        public int rankTier;
        public int sex;              // (int)Sex
        public bool isPolitician;
        public bool isSovereign;
        public int financialTrait;   // (int)FinancialTrait
        public float wealth;
        public int birthYear, deathYear;
        public int captiveStatus;    // (int)CaptiveStatus
        public int heldBy;           // (int)Faction
        // 経歴・学歴・在役（#155/#156/#530/#SCHOOL-AGE）
        public int hammockNumber, graduationYear, schoolId, examRank;
        public int militaryDegree;   // (int)MilitaryDegree
        public int examDegree;       // (int)ExamDegree
        public int schoolPostingUntilYear, warCollegeRank;
        public int serviceStatus;    // (int)ServiceStatus
        // 能力（軍才/文才/技術才）
        public int leadership, attack, defense, mobility, operation, intelligence;
        public int research, engineering, planning, production;
        // 官僚制（位階・考課・官僚制基盤）。既定は無位/未評定＝旧セーブ後方互換（欠落フィールドは初期化値を保持）。
        public int courtRank = (int)CourtRank.無位;  // (int)CourtRank（既定=無位＝0=正一位の誤復元を防ぐ）
        public bool hasMerit;                         // 考課記録の有無（false=未評定＝merit は null 復元）
        public int meritEvaluations;
        public float meritCumulative;
        public int meritConsecutiveTop, meritConsecutivePoor;
        public float meritIntegrity = 0.7f;
        public int meritLastRating = (int)MeritRating.中中; // (int)MeritRating（hasMerit のときのみ有効）
    }
}
