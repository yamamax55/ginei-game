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
    }
}
