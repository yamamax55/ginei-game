using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦役の世界状態（<see cref="CampaignState"/>）↔ バージョン付きJSON の変換（FND-2 #495・唯一の窓口）。
    /// SO 参照（FactionData）は名前で落とし、復元時の解決は呼び出し側（`CampaignSaveManager`）に委ねる＝純ロジックで
    /// test-first にできる。`schemaVersion` を持ち、欠落フィールドは JsonUtility が既定値で埋める＝<b>前方互換</b>
    /// （旧セーブの後方互換は `SaveData`/`SaveManager` と同方針）。状態は `GameSettings`（設定）と分離し `CampaignState` に一本化。
    /// </summary>
    public static class CampaignSerializer
    {
        /// <summary>セーブスキーマ版（フィールド構造を変えたら上げる）。</summary>
        public const int SchemaVersion = 1;

        // ===== CampaignState → 平データ =====

        public static CampaignSaveData ToSaveData(CampaignState c)
        {
            var save = new CampaignSaveData { schemaVersion = SchemaVersion };
            if (c == null) return save;

            if (c.map != null)
            {
                for (int i = 0; i < c.map.systems.Count; i++)
                {
                    StarSystem s = c.map.systems[i];
                    if (s == null) continue;
                    var ss = new StarSystemSave
                    {
                        id = s.id,
                        name = s.systemName,
                        posX = s.position.x,
                        posY = s.position.y,
                        owner = (int)s.owner,
                        ownerFactionName = (s.ownerData != null) ? s.ownerData.factionName : "",
                        habitable = s.habitable,
                        isColonized = s.isColonized,
                        systemType = (int)s.systemType,
                        hasPlanet = s.planet != null
                    };
                    if (s.planet != null)
                    {
                        ss.planet = new PlanetSave
                        {
                            systemId = s.planet.systemId,
                            owner = (int)s.planet.owner,
                            orbitalDefense = s.planet.orbitalDefense,
                            maxOrbitalDefense = s.planet.maxOrbitalDefense,
                            invasionProgress = s.planet.invasionProgress,
                            invasionThreshold = s.planet.invasionThreshold
                        };
                    }
                    save.systems.Add(ss);
                }

                for (int i = 0; i < c.map.corridors.Count; i++)
                {
                    Corridor co = c.map.corridors[i];
                    if (co == null) continue;
                    save.corridors.Add(new CorridorSave { aId = co.aId, bId = co.bId, length = co.length, type = (int)co.type });
                }
            }

            if (c.states != null)
            {
                for (int i = 0; i < c.states.Count; i++)
                {
                    FactionState fs = c.states[i];
                    if (fs == null) continue;
                    save.states.Add(new FactionStateSave
                    {
                        faction = (int)fs.faction,
                        inclusiveness = fs.inclusiveness,
                        governmentForm = (int)fs.governmentForm,
                        regimeLegitimacy = fs.regime.legitimacy,
                        regimeCorruption = fs.regime.corruption,
                        regimeVirtue = fs.regime.virtue,
                        polityPopulation = fs.polity.population,
                        polityRulerForce = fs.polity.rulerForce,
                        polityCooperation = fs.polity.cooperation,
                        polityLegitimacy = fs.polity.legitimacy,
                        polityOppression = fs.polity.oppression,
                        orgCohesion = fs.organization.cohesion,
                        orgInstitutionalization = fs.organization.institutionalization,
                        orgLeaderCharisma = fs.organization.leaderCharisma,
                        orgFragmented = fs.organization.fragmented,
                        commHope = fs.community.hope,
                        commRepression = fs.community.repression,
                        commDissent = fs.community.dissent
                    });
                }
            }
            return save;
        }

        // ===== 平データ → CampaignState（SO=ownerData は null のまま＝呼び出し側が名前で解決） =====

        public static CampaignState FromSaveData(CampaignSaveData save)
        {
            var map = new GalaxyMap();
            var state = new CampaignState(map);
            if (save == null) return state;

            for (int i = 0; i < save.systems.Count; i++)
            {
                StarSystemSave ss = save.systems[i];
                if (ss == null) continue;
                var s = new StarSystem(ss.id, ss.name, new Vector2(ss.posX, ss.posY), (Faction)ss.owner)
                {
                    habitable = ss.habitable,
                    isColonized = ss.isColonized,
                    systemType = (SystemType)ss.systemType
                };
                if (ss.hasPlanet && ss.planet != null)
                {
                    s.planet = new Planet
                    {
                        systemId = ss.planet.systemId,
                        owner = (Faction)ss.planet.owner,
                        orbitalDefense = ss.planet.orbitalDefense,
                        maxOrbitalDefense = ss.planet.maxOrbitalDefense,
                        invasionProgress = ss.planet.invasionProgress,
                        invasionThreshold = ss.planet.invasionThreshold
                    };
                }
                map.AddSystem(s);
            }

            for (int i = 0; i < save.corridors.Count; i++)
            {
                CorridorSave co = save.corridors[i];
                if (co == null) continue;
                map.AddCorridor(new Corridor(co.aId, co.bId, co.length, (CorridorType)co.type));
            }

            for (int i = 0; i < save.states.Count; i++)
            {
                FactionStateSave fss = save.states[i];
                if (fss == null) continue;
                var fs = new FactionState((Faction)fss.faction, fss.inclusiveness);
                fs.governmentForm = (GovernmentForm)fss.governmentForm;
                fs.regime.legitimacy = fss.regimeLegitimacy;
                fs.regime.corruption = fss.regimeCorruption;
                fs.regime.virtue = fss.regimeVirtue;
                fs.polity.population = fss.polityPopulation;
                fs.polity.rulerForce = fss.polityRulerForce;
                fs.polity.cooperation = fss.polityCooperation;
                fs.polity.legitimacy = fss.polityLegitimacy;
                fs.polity.oppression = fss.polityOppression;
                fs.organization.cohesion = fss.orgCohesion;
                fs.organization.institutionalization = fss.orgInstitutionalization;
                fs.organization.leaderCharisma = fss.orgLeaderCharisma;
                fs.organization.fragmented = fss.orgFragmented;
                fs.community.hope = fss.commHope;
                fs.community.repression = fss.commRepression;
                fs.community.dissent = fss.commDissent;
                state.states.Add(fs);
            }
            return state;
        }

        // ===== JSON 文字列 =====

        /// <summary>世界状態をJSON文字列へ（バージョン付き）。</summary>
        public static string ToJson(CampaignState c, bool prettyPrint = false)
            => JsonUtility.ToJson(ToSaveData(c), prettyPrint);

        /// <summary>JSON文字列を平データへ復元（空/不正は null）。SO解決前の素の状態。</summary>
        public static CampaignSaveData Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<CampaignSaveData>(json); }
            catch { return null; }
        }

        /// <summary>JSON文字列から世界状態を復元（ownerData=SO は null＝呼び出し側が名前で解決）。空/不正は null。</summary>
        public static CampaignState FromJson(string json)
        {
            CampaignSaveData save = Parse(json);
            return save == null ? null : FromSaveData(save);
        }
    }
}
