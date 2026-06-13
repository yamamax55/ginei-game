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
        // 統一時間（GameClock）。0=未設定（後方互換＝既定クロック）。
        public double clockElapsed;
        public float clockSpeed = 1f;
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

    /// <summary>回廊のセーブ平データ。</summary>
    [Serializable]
    public class CorridorSave
    {
        public int aId, bId;
        public float length;
        public int type; // (int)CorridorType
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
    }
}
