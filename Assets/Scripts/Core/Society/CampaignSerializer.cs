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
                        commDissent = fs.community.dissent,
                        treasury = fs.treasury,
                        taxRate = fs.taxRate,
                        budgetMilitary = fs.budget != null ? fs.budget.military : 0f,
                        budgetShipbuilding = fs.budget != null ? fs.budget.shipbuilding : 0f,
                        budgetAdministration = fs.budget != null ? fs.budget.administration : 0f,
                        budgetWelfare = fs.budget != null ? fs.budget.welfare : 0f,
                        budgetResearch = fs.budget != null ? fs.budget.research : 0f,
                        budgetDiplomacy = fs.budget != null ? fs.budget.diplomacy : 0f,
                        fiscalDebt = fs.fiscal != null ? fs.fiscal.debt : 0f
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
                fs.treasury = fss.treasury;
                fs.taxRate = fss.taxRate;
                if (fs.budget != null)
                {
                    fs.budget.military = fss.budgetMilitary;
                    fs.budget.shipbuilding = fss.budgetShipbuilding;
                    fs.budget.administration = fss.budgetAdministration;
                    fs.budget.welfare = fss.budgetWelfare;
                    fs.budget.research = fss.budgetResearch;
                    fs.budget.diplomacy = fss.budgetDiplomacy;
                }
                if (fs.fiscal != null) fs.fiscal.debt = fss.fiscalDebt;
                state.states.Add(fs);
            }
            return state;
        }

        // ===== ネームド人物ロスター（提督/文官）の往復 =====

        /// <summary><see cref="Person"/> → 平データ（全永続フィールド・enum は int）。</summary>
        public static PersonSave PersonToSave(Person p)
        {
            if (p == null) return null;
            return new PersonSave
            {
                id = p.id, name = p.name, faction = (int)p.faction, role = (int)p.role,
                rankTier = p.rankTier, sex = (int)p.sex,
                isPolitician = p.isPolitician, isSovereign = p.isSovereign,
                financialTrait = (int)p.financialTrait, wealth = p.wealth,
                birthYear = p.birthYear, deathYear = p.deathYear,
                captiveStatus = (int)p.captiveStatus, heldBy = (int)p.heldBy,
                hammockNumber = p.hammockNumber, graduationYear = p.graduationYear,
                schoolId = p.schoolId, examRank = p.examRank,
                militaryDegree = (int)p.militaryDegree, examDegree = (int)p.examDegree,
                schoolPostingUntilYear = p.schoolPostingUntilYear, warCollegeRank = p.warCollegeRank,
                serviceStatus = (int)p.serviceStatus,
                leadership = p.leadership, attack = p.attack, defense = p.defense, mobility = p.mobility,
                operation = p.operation, intelligence = p.intelligence,
                research = p.research, engineering = p.engineering, planning = p.planning, production = p.production,
                // 官僚制（位階・考課）
                courtRank = (int)p.courtRank,
                hasMerit = p.merit != null,
                meritEvaluations = p.merit != null ? p.merit.evaluations : 0,
                meritCumulative = p.merit != null ? p.merit.cumulativeScore : 0f,
                meritConsecutiveTop = p.merit != null ? p.merit.consecutiveTop : 0,
                meritConsecutivePoor = p.merit != null ? p.merit.consecutivePoor : 0,
                meritIntegrity = p.merit != null ? p.merit.integrity : 0.7f,
                meritLastRating = p.merit != null ? (int)p.merit.lastRating : (int)MeritRating.中中
            };
        }

        /// <summary>平データ → <see cref="Person"/>（往復）。</summary>
        public static Person PersonFromSave(PersonSave d)
        {
            if (d == null) return null;
            var p = new Person(d.id, d.name, (Faction)d.faction, (PersonRole)d.role)
            {
                rankTier = d.rankTier, sex = (Sex)d.sex,
                isPolitician = d.isPolitician, isSovereign = d.isSovereign,
                financialTrait = (FinancialTrait)d.financialTrait, wealth = d.wealth,
                birthYear = d.birthYear, deathYear = d.deathYear,
                captiveStatus = (CaptiveStatus)d.captiveStatus, heldBy = (Faction)d.heldBy,
                hammockNumber = d.hammockNumber, graduationYear = d.graduationYear,
                schoolId = d.schoolId, examRank = d.examRank,
                militaryDegree = (MilitaryDegree)d.militaryDegree, examDegree = (ExamDegree)d.examDegree,
                schoolPostingUntilYear = d.schoolPostingUntilYear, warCollegeRank = d.warCollegeRank,
                serviceStatus = (ServiceStatus)d.serviceStatus,
                leadership = d.leadership, attack = d.attack, defense = d.defense, mobility = d.mobility,
                operation = d.operation, intelligence = d.intelligence,
                research = d.research, engineering = d.engineering, planning = d.planning, production = d.production,
                courtRank = (CourtRank)d.courtRank
            };
            // 考課記録（OfficialMerit）は hasMerit のときのみ復元（未評定は null＝後方互換）。
            if (d.hasMerit)
                p.merit = new OfficialMerit(d.id, d.meritIntegrity)
                {
                    evaluations = d.meritEvaluations,
                    cumulativeScore = d.meritCumulative,
                    consecutiveTop = d.meritConsecutiveTop,
                    consecutivePoor = d.meritConsecutivePoor,
                    lastRating = (MeritRating)d.meritLastRating
                };
            return p;
        }

        /// <summary>人物ロスターを保存データへ書き込む（既存 people をクリアして詰め直す）。null は無視。</summary>
        public static void WritePeople(CampaignSaveData save, System.Collections.Generic.IEnumerable<Person> people)
        {
            if (save == null) return;
            save.people.Clear();
            if (people == null) return;
            foreach (Person p in people)
                if (p != null) save.people.Add(PersonToSave(p));
        }

        /// <summary>保存データから人物ロスターを復元する（空/null は空リスト）。</summary>
        public static System.Collections.Generic.List<Person> ReadPeople(CampaignSaveData save)
        {
            var list = new System.Collections.Generic.List<Person>();
            if (save == null || save.people == null) return list;
            for (int i = 0; i < save.people.Count; i++)
            {
                Person p = PersonFromSave(save.people[i]);
                if (p != null) list.Add(p);
            }
            return list;
        }

        // ===== 戦略艦隊（盤面の駒）の往復 =====

        /// <summary>戦略艦隊レジストリを保存データへ書き込む（既存 fleets をクリア）。回廊上の精密位置は保存しない。</summary>
        public static void WriteFleets(CampaignSaveData save, StrategicFleetRegistry reg)
        {
            if (save == null) return;
            save.fleets.Clear();
            if (reg == null || reg.fleets == null) return;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null) continue;
                save.fleets.Add(new StrategicFleetSave
                {
                    id = f.id, faction = (int)f.faction, strength = f.strength,
                    supply = f.supply, warpSpeed = f.warpSpeed, sublightFactor = f.sublightFactor,
                    currentSystemId = f.currentSystemId, destinationSystemId = f.destinationSystemId,
                    moving = f.IsMoving, engaged = f.engaged
                });
            }
        }

        /// <summary>保存データから戦略艦隊レジストリを復元する（停泊星系に再構築・移動中は目的地へ再ワープ）。</summary>
        public static StrategicFleetRegistry ReadFleets(CampaignSaveData save, GalaxyMap map)
        {
            var reg = new StrategicFleetRegistry(map);
            if (save == null || save.fleets == null) return reg;
            for (int i = 0; i < save.fleets.Count; i++)
            {
                StrategicFleetSave d = save.fleets[i];
                if (d == null) continue;
                var f = new StrategicFleet(d.id, d.currentSystemId, (Faction)d.faction, d.warpSpeed)
                {
                    strength = d.strength, supply = d.supply, sublightFactor = d.sublightFactor
                };
                if (d.moving && d.destinationSystemId > 0 && map != null) f.WarpTo(map, d.destinationSystemId);
                f.engaged = d.engaged;
                reg.Add(f);
            }
            return reg;
        }

        // ===== 惑星内政（Province）の往復（#109/#759） =====

        /// <summary>惑星内政（systemId→Province）を保存データへ書き込む（既存 provinces をクリア）。
        /// 人口動態/職業/技能の細部（demographics/workforce/skills）は保存せずロード後に再構築（マクロ背景＝再安定する）。</summary>
        public static void WriteProvinces(CampaignSaveData save, System.Collections.Generic.Dictionary<int, Province> provinces)
        {
            if (save == null) return;
            save.provinces.Clear();
            if (provinces == null) return;
            foreach (var kv in provinces)
            {
                Province p = kv.Value;
                if (p == null) continue;
                save.provinces.Add(new ProvinceSave
                {
                    systemId = p.systemId,
                    nativeIdeology = p.nativeIdeology,
                    systemType = (int)p.systemType,
                    population = p.population,
                    wageIndex = p.wageIndex,
                    livingStandard = p.livingStandard,
                    foodShortage = p.foodShortage,
                    hasStrategicResource = p.hasStrategicResource,
                    strategicResource = (int)p.strategicResource,
                    strategicAbundance = p.strategicAbundance,
                    stability = p.stability,
                    integration = p.integration
                });
            }
        }

        /// <summary>保存データから惑星内政（systemId→Province）を復元する（空/null は空辞書）。
        /// demographics/workforce/skills は null のまま（ロード後に再構築＝後方互換）。</summary>
        public static System.Collections.Generic.Dictionary<int, Province> ReadProvinces(CampaignSaveData save)
        {
            var dict = new System.Collections.Generic.Dictionary<int, Province>();
            if (save == null || save.provinces == null) return dict;
            for (int i = 0; i < save.provinces.Count; i++)
            {
                ProvinceSave d = save.provinces[i];
                if (d == null) continue;
                var p = new Province
                {
                    systemId = d.systemId,
                    nativeIdeology = d.nativeIdeology ?? "",
                    systemType = (SystemType)d.systemType,
                    population = d.population,
                    wageIndex = d.wageIndex <= 0f ? 1f : d.wageIndex,
                    livingStandard = d.livingStandard,
                    foodShortage = d.foodShortage,
                    hasStrategicResource = d.hasStrategicResource,
                    strategicResource = (StrategicResourceType)d.strategicResource,
                    strategicAbundance = d.strategicAbundance,
                    stability = d.stability,
                    integration = d.integration
                };
                dict[p.systemId] = p;
            }
            return dict;
        }

        // ===== 統一時間（GameClock） =====

        /// <summary>クロックを保存データへ。</summary>
        public static void WriteClock(CampaignSaveData save, GameClock clock)
        {
            if (save == null || clock == null) return;
            save.clockElapsed = clock.elapsedSeconds;
            save.clockSpeed = clock.speed;
        }

        /// <summary>保存データからクロックを復元（新規 GameClock を返す）。</summary>
        public static GameClock ReadClock(CampaignSaveData save)
        {
            var clock = new GameClock();
            if (save != null)
            {
                clock.elapsedSeconds = save.clockElapsed;
                clock.speed = save.clockSpeed <= 0f ? 1f : save.clockSpeed;
            }
            return clock;
        }

        // ===== JSON 文字列 =====

        /// <summary>世界状態をJSON文字列へ（バージョン付き）。</summary>
        public static string ToJson(CampaignState c, bool prettyPrint = false)
            => JsonUtility.ToJson(ToSaveData(c), prettyPrint);

        /// <summary>世界状態＋人物ロスターをJSON文字列へ（人物を同梱して保存する版）。</summary>
        public static string ToJson(CampaignState c, System.Collections.Generic.IEnumerable<Person> people, bool prettyPrint = false)
        {
            CampaignSaveData save = ToSaveData(c);
            WritePeople(save, people);
            return JsonUtility.ToJson(save, prettyPrint);
        }

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
