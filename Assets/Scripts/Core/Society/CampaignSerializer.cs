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
                    var cs = new CorridorSave { aId = co.aId, bId = co.bId, length = co.length, type = (int)co.type };
                    // #40：回廊要塞は所有・守備・シールドまで保存する（積み残すとロードで封鎖が消える）。
                    if (co.fortress != null)
                    {
                        cs.hasFortress = true;
                        cs.fortGarrison = co.fortress.garrisonStrength;
                        cs.fortShield = co.fortress.shieldIntegrity;
                        cs.fortMainGun = co.fortress.mainGunPower;
                        cs.fortControlsCorridor = co.fortress.controlsCorridor;
                        cs.fortOwner = (int)co.fortress.owner;
                        cs.fortName = co.fortress.fortressName;
                        // 駐留艦隊はIDだけ保存する（艦隊の実体は fleets 側にあるので二重に持たない）。
                        cs.fortGarrisonFleetIds = co.fortress.garrisonFleetIds == null
                            ? new System.Collections.Generic.List<int>()
                            : new System.Collections.Generic.List<int>(co.fortress.garrisonFleetIds);
                    }
                    save.corridors.Add(cs);
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
                        hasForeignDoctrine = true,
                        foreignDoctrine = (int)fs.foreignDoctrine,
                        daoValue = Mathf.Clamp(fs.daoValue, -1f, 1f),
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
                        budgetEducation = fs.budget != null ? fs.budget.education : 0f,
                        fiscalDebt = fs.fiscal != null ? fs.fiscal.debt : 0f,
                        hasPolitics = fs.politics != null,
                        politics = fs.politics,
                        hasCivilService = fs.civilService != null,
                        civilService = fs.civilService,
                        hasEducation = fs.education != null,
                        education = fs.education,
                        hasTalentDevelopment = fs.talentDevelopment != null,
                        talentDevelopment = fs.talentDevelopment,
                        hasShipDesigns = fs.shipDesigns != null,
                        shipDesigns = fs.shipDesigns
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
                var corridor = new Corridor(co.aId, co.bId, co.length, (CorridorType)co.type);
                // #40：hasFortress=false（旧セーブ含む）は要塞なし＝フェザーン型の自由通行で復元する。
                if (co.hasFortress)
                {
                    corridor.fortress = new Fortress(co.fortGarrison, co.fortMainGun, co.fortShield, co.fortControlsCorridor)
                    {
                        owner = (Faction)co.fortOwner,
                        fortressName = string.IsNullOrEmpty(co.fortName) ? "要塞" : co.fortName,
                        // JsonUtility は欠落したリストを null で返すので必ず作り直す。
                        // 旧セーブはここが空になり、施設の守備値（fortGarrison）だけが効く＝従来動作。
                        garrisonFleetIds = co.fortGarrisonFleetIds == null
                            ? new System.Collections.Generic.List<int>()
                            : new System.Collections.Generic.List<int>(co.fortGarrisonFleetIds),
                    };
                }
                map.AddCorridor(corridor);
            }

            for (int i = 0; i < save.states.Count; i++)
            {
                FactionStateSave fss = save.states[i];
                if (fss == null) continue;
                var fs = new FactionState((Faction)fss.faction, fss.inclusiveness);
                fs.governmentForm = (GovernmentForm)fss.governmentForm;
                // 旧セーブは主義情報を持たない＝中立・王道覇道0から開始し、既定enum 0の保守と誤認しない。
                fs.foreignDoctrine = fss.hasForeignDoctrine
                    ? (ForeignDoctrine)fss.foreignDoctrine
                    : ForeignDoctrine.中立;
                fs.daoValue = fss.hasForeignDoctrine ? Mathf.Clamp(fss.daoValue, -1f, 1f) : 0f;
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
                    fs.budget.education = fss.budgetEducation;
                }
                if (fs.fiscal != null) fs.fiscal.debt = fss.fiscalDebt;
                // 政治は旗が立っているときだけ戻す（旧セーブは null＝次の年次で初期化）。読込では選挙をしない。
                if (fss.hasPolitics && fss.politics != null)
                {
                    fs.politics = fss.politics;
                    ElectionCycleRules.NormalizeLoaded(fs.politics);
                }
                // 省内職位の人事台帳も旗が立っているときだけ戻す（旧セーブは null＝空の台帳）。読込では任命も解任もしない。
                if (fss.hasCivilService && fss.civilService != null)
                {
                    fs.civilService = fss.civilService;
                    CivilServicePostRules.NormalizeLoaded(fs.civilService);
                }
                // 教育は新規戦役にも必ずある。旧セーブ（旗なし）は FactionState の基準状態を維持する。
                if (fss.hasEducation && fss.education != null)
                {
                    fs.education = fss.education;
                    EducationAnnualRules.NormalizeLoaded(fs.education);
                }
                // 育成台帳は読込だけで進行・修了させず、欠落一覧と採番だけを正規化する。
                if (fss.hasTalentDevelopment && fss.talentDevelopment != null)
                {
                    fs.talentDevelopment = fss.talentDevelopment;
                    TalentDevelopmentRules.NormalizeLoaded(fs.talentDevelopment);
                }
                // 設計台帳は読込だけで登録・現役化を起こさず、欠落配列と採番だけを正規化する。
                if (fss.hasShipDesigns && fss.shipDesigns != null)
                {
                    fs.shipDesigns = fss.shipDesigns;
                    ShipDesignRules.NormalizeLoaded(fs.shipDesigns);
                }
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
                isRoyal = p.isRoyal, isSpecialForces = p.isSpecialForces, isFreeAgent = p.isFreeAgent,
                financialTrait = (int)p.financialTrait, wealth = p.wealth,
                birthYear = p.birthYear, deathYear = p.deathYear,
                captiveStatus = (int)p.captiveStatus, heldBy = (int)p.heldBy,
                spouseId = p.spouseId, motherId = p.motherId, fatherId = p.fatherId,
                recessiveTalent = p.recessiveTalent,
                hammockNumber = p.hammockNumber, graduationYear = p.graduationYear,
                schoolId = p.schoolId, examRank = p.examRank,
                generationKind = (int)p.generationKind, generationEventId = p.generationEventId,
                generationSeed = p.generationSeed,
                militaryDegree = (int)p.militaryDegree, examDegree = (int)p.examDegree,
                schoolPostingUntilYear = p.schoolPostingUntilYear, warCollegeRank = p.warCollegeRank,
                serviceStatus = (int)p.serviceStatus,
                leadership = p.leadership, attack = p.attack, defense = p.defense, mobility = p.mobility,
                operation = p.operation, intelligence = p.intelligence,
                research = p.research, engineering = p.engineering, planning = p.planning, production = p.production,
                creed = (int)p.creed, socialOrigin = (int)p.socialOrigin, birthSystemId = p.birthSystemId,
                charisma = p.charisma, constitution = p.constitution,
                hobby = (int)p.hobby, vice = (int)p.vice,
                grievance = p.grievance, loyaltyTargetId = p.loyaltyTargetId,
                popularRenown = p.popularRenown, infamy = p.infamy,
                // 官僚制（位階・考課）
                courtRank = (int)p.courtRank,
                hasMerit = p.merit != null,
                meritEvaluations = p.merit != null ? p.merit.evaluations : 0,
                meritCumulative = p.merit != null ? p.merit.cumulativeScore : 0f,
                meritConsecutiveTop = p.merit != null ? p.merit.consecutiveTop : 0,
                meritConsecutivePoor = p.merit != null ? p.merit.consecutivePoor : 0,
                meritIntegrity = p.merit != null ? p.merit.integrity : 0.7f,
                meritLastRating = p.merit != null ? (int)p.merit.lastRating : (int)MeritRating.中中,
                hiddenTraits = SaveHiddenTraits(p.hiddenTraits)
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
                isRoyal = d.isRoyal, isSpecialForces = d.isSpecialForces, isFreeAgent = d.isFreeAgent,
                financialTrait = (FinancialTrait)d.financialTrait, wealth = d.wealth,
                birthYear = d.birthYear, deathYear = d.deathYear,
                captiveStatus = (CaptiveStatus)d.captiveStatus, heldBy = (Faction)d.heldBy,
                spouseId = d.spouseId, motherId = d.motherId, fatherId = d.fatherId,
                recessiveTalent = d.recessiveTalent,
                hammockNumber = d.hammockNumber, graduationYear = d.graduationYear,
                schoolId = d.schoolId, examRank = d.examRank,
                generationKind = (PersonGenerationKind)d.generationKind,
                generationEventId = d.generationEventId ?? string.Empty, generationSeed = d.generationSeed,
                militaryDegree = (MilitaryDegree)d.militaryDegree, examDegree = (ExamDegree)d.examDegree,
                schoolPostingUntilYear = d.schoolPostingUntilYear, warCollegeRank = d.warCollegeRank,
                serviceStatus = (ServiceStatus)d.serviceStatus,
                leadership = d.leadership, attack = d.attack, defense = d.defense, mobility = d.mobility,
                operation = d.operation, intelligence = d.intelligence,
                research = d.research, engineering = d.engineering, planning = d.planning, production = d.production,
                creed = (Creed)d.creed, socialOrigin = (SocialOrigin)d.socialOrigin,
                birthSystemId = d.birthSystemId, charisma = d.charisma, constitution = d.constitution,
                hobby = (Hobby)d.hobby, vice = (Vice)d.vice,
                grievance = d.grievance, loyaltyTargetId = d.loyaltyTargetId,
                popularRenown = d.popularRenown, infamy = d.infamy,
                hiddenTraits = LoadHiddenTraits(d.hiddenTraits),
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

        private static System.Collections.Generic.List<HiddenTraitSave> SaveHiddenTraits(
            System.Collections.Generic.IEnumerable<HiddenTrait> traits)
        {
            var saved = new System.Collections.Generic.List<HiddenTraitSave>();
            if (traits == null) return saved;
            foreach (HiddenTrait trait in traits)
                saved.Add(new HiddenTraitSave { label = trait.label, concealment = trait.concealment });
            return saved;
        }

        private static System.Collections.Generic.List<HiddenTrait> LoadHiddenTraits(
            System.Collections.Generic.IEnumerable<HiddenTraitSave> traits)
        {
            var loaded = new System.Collections.Generic.List<HiddenTrait>();
            if (traits == null) return loaded;
            foreach (HiddenTraitSave trait in traits)
                if (trait != null) loaded.Add(new HiddenTrait(trait.label, trait.concealment));
            return loaded;
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
                    moving = f.IsMoving, engaged = f.engaged,
                    // 艦隊ごとの艦艇数は必ず確定値で保存する（0＝全滅もそのまま持ち帰る）。
                    shipCount = f.Ships, shipCountSet = true,
                    // 編制と司令官は「実値+1」で保存する（0＝無所属／未任命。旧セーブは 0 で読まれる）。
                    corpsIdPlus1 = f.corpsId + 1, corpsName = f.corpsName, isCorpsFlagship = f.isCorpsFlagship,
                    armyGroupIdPlus1 = f.armyGroupId + 1, armyGroupName = f.armyGroupName,
                    commanderPersonIdPlus1 = f.commanderPersonId + 1,
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
                    strength = d.strength, supply = d.supply, sublightFactor = d.sublightFactor,
                    // 旧セーブは shipCountSet を持たない（＝false）ので兵力から導出して埋める（後方互換）。
                    // 新セーブは確定値なので 0（全滅）もそのまま復元し、艦艇が勝手に復活しない。
                    shipCount = d.shipCountSet ? Mathf.Max(0, d.shipCount)
                                               : FleetShipCountRules.FromStrength(d.strength),
                    shipCountSet = true,
                    // 編制と司令官は「実値+1」で保存してある＝0（旧セーブ・欠落）は -1（無所属／未任命）へ戻す。
                    // 旧セーブが「0番の軍団に所属」「0番の人物が司令」と誤読されないための符号化。
                    corpsId = d.corpsIdPlus1 - 1,
                    corpsName = d.corpsName,
                    isCorpsFlagship = d.isCorpsFlagship,
                    armyGroupId = d.armyGroupIdPlus1 - 1,
                    armyGroupName = d.armyGroupName,
                    commanderPersonId = d.commanderPersonIdPlus1 - 1,
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
                    integration = p.integration,
                    governancePolicy = (int)p.governancePolicy
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
                    integration = d.integration,
                    governancePolicy = System.Enum.IsDefined(typeof(GovernancePolicy), d.governancePolicy)
                        ? (GovernancePolicy)d.governancePolicy
                        : GovernancePolicy.民生
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

        // ===== 航行中の援軍（#38 C-5） =====

        /// <summary>
        /// 援軍台帳を保存データへ（#38）。到着は<b>絶対 game-秒</b>なので、クロックと一緒に往復すれば
        /// 残り時間が保たれる。到着済み・閉鎖済みは持ち越さない（戦場は再開時に張り直す）。
        /// </summary>
        public static void WriteReinforcements(CampaignSaveData save, WarpReinforcementLedger ledger)
        {
            if (save == null) return;
            save.reinforcements.Clear();
            if (ledger == null) return;

            var buf = new System.Collections.Generic.List<WarpReinforcement>();
            ledger.PeekAll(buf);
            for (int i = 0; i < buf.Count; i++)
            {
                WarpReinforcement o = buf[i];
                save.reinforcements.Add(new ReinforcementSave
                {
                    systemA = o.battlefield.systemA,
                    systemB = o.battlefield.systemB,
                    faction = (int)o.faction,
                    fleetId = o.fleetId,
                    strength = o.strength,
                    dispatchTime = o.dispatchTime,
                    arrivalTime = o.arrivalTime,
                });
            }
        }

        /// <summary>
        /// 保存データから援軍台帳を復元する（#38）。リストが無い旧セーブは空の台帳＝援軍なし（前方互換）。
        /// 台帳の現在時刻はクロックへ合わせる＝ロード直後に過去ぶんが一気に到着扱いにならない。
        /// </summary>
        public static WarpReinforcementLedger ReadReinforcements(CampaignSaveData save, GameClock clock)
        {
            var ledger = new WarpReinforcementLedger();
            if (clock != null) ledger.SyncTo(clock.elapsedSeconds);
            if (save == null || save.reinforcements == null) return ledger;

            for (int i = 0; i < save.reinforcements.Count; i++)
            {
                ReinforcementSave r = save.reinforcements[i];
                if (r == null) continue;
                BattlefieldKey key = r.systemA == r.systemB
                    ? BattlefieldKey.System(r.systemA)
                    : BattlefieldKey.Corridor(r.systemA, r.systemB);
                ledger.DispatchAt(key, (Faction)r.faction, r.fleetId, r.strength, r.arrivalTime);
            }
            return ledger;
        }

        // ===== 稟議・決裁（#稟議完成②） =====

        /// <summary>稟議台帳を保存データへ。活性・決着済みの区別は <see cref="Petition.status"/> が持つ。</summary>
        public static void WritePetitions(System.Collections.Generic.List<PetitionSave> into, PetitionLedger ledger)
        {
            if (into == null) return;
            into.Clear();
            if (ledger == null || ledger.items == null) return;

            for (int i = 0; i < ledger.items.Count; i++)
            {
                Petition p = ledger.items[i];
                if (p == null) continue;
                into.Add(new PetitionSave
                {
                    id = p.id, title = p.title ?? "", faction = (int)p.faction,
                    box = (int)p.box, regionKey = p.regionKey ?? "", origin = (int)p.origin,
                    severity = (int)p.severity + 1,
                    drafterId = p.drafterId, addresseeId = p.addresseeId,
                    effectKey = p.effectKey ?? "", status = (int)p.status,
                    carrierId = p.carrierId, distorted = p.distorted, vindicated = p.vindicated,
                });
            }
        }

        /// <summary>保存データから稟議台帳へ戻す（リストが無い旧セーブは空＝案件なし）。</summary>
        public static void ReadPetitions(System.Collections.Generic.List<PetitionSave> from, PetitionLedger ledger)
        {
            if (ledger == null) return;
            ledger.Clear();
            if (from == null) return;

            for (int i = 0; i < from.Count; i++)
            {
                PetitionSave s = from[i];
                if (s == null) continue;
                var p = new Petition(s.id, s.title, (Faction)s.faction, (BoxKind)s.box,
                                     (PetitionOrigin)s.origin, s.effectKey)
                {
                    regionKey = s.regionKey ?? "",
                    severity = s.severity > 0 && System.Enum.IsDefined(typeof(DecisionSeverity), s.severity - 1)
                        ? (DecisionSeverity)(s.severity - 1) : DecisionSeverity.通常,
                    drafterId = s.drafterId,
                    addresseeId = s.addresseeId,
                    status = (PetitionStatus)s.status,
                    carrierId = s.carrierId,
                    distorted = s.distorted,
                    vindicated = s.vindicated,
                };
                ledger.Add(p);
            }
        }

        /// <summary>
        /// 稟議台帳の採番済み最大 id と打切り累計を取り出す（容量で落ちた稟議の id を再利用しない・打切りを silent にしない）。
        /// 容量は設定値のため保存しない。台帳が null なら 0。
        /// </summary>
        public static void WritePetitionLedgerMeta(PetitionLedger ledger, out int lastIssuedId, out int droppedCount)
        {
            lastIssuedId = ledger != null ? ledger.LastIssuedId : 0;
            droppedCount = ledger != null ? ledger.droppedCount : 0;
        }

        /// <summary>
        /// <see cref="ReadPetitions"/> の後に呼び、採番を保存時の最大 id まで予約し打切り累計を戻す。
        /// 読み込み時に容量で落ちた分（容量設定が小さくなった場合）は保存値に加算する。旧セーブ（0）は何も変えない。
        /// </summary>
        public static void ReadPetitionLedgerMeta(PetitionLedger ledger, int lastIssuedId, int droppedCount)
        {
            if (ledger == null) return;
            if (lastIssuedId > 0) ledger.ReserveId(lastIssuedId);
            if (droppedCount > 0) ledger.droppedCount += droppedCount;
        }

        /// <summary>
        /// 決裁カードが指す稟議 id を台帳の採番へ予約する（ロード後に新しい稟議が古いカードの id を再利用しない）。
        /// 読み込んだ稟議自体の id は <see cref="ReadPetitions"/> の <see cref="PetitionLedger.Add"/> が採番へ反映済み。
        /// </summary>
        public static void ReservePetitionIds(DecisionQueue queue, PetitionLedger ledger)
        {
            if (queue == null || queue.items == null || ledger == null) return;
            for (int i = 0; i < queue.items.Count; i++)
            {
                PendingDecision d = queue.items[i];
                if (d != null && d.petitionId > 0) ledger.ReserveId(d.petitionId);
            }
        }

        /// <summary>決裁カードを保存データへ（未決も決裁済みの履歴も・適用済みフラグを含む）。</summary>
        public static void WriteDecisions(CampaignSaveData save, DecisionQueue queue)
        {
            if (save == null) return;
            save.decisions.Clear();
            if (queue == null || queue.items == null) return;

            for (int i = 0; i < queue.items.Count; i++)
            {
                PendingDecision d = queue.items[i];
                if (d == null) continue;
                var rec = new DecisionSave
                {
                    id = d.id, title = d.title ?? "", body = d.body ?? "", imageKey = d.imageKey ?? "",
                    severity = (int)d.severity, source = (int)d.source,
                    defaultChoiceIndex = d.defaultChoiceIndex, effectKey = d.effectKey ?? "",
                    status = (int)d.status, elapsed = d.elapsed, chosenIndex = d.chosenIndex,
                    applied = d.applied, meterApplied = d.meterApplied,
                    outcome = (int)d.outcome, resultDetail = d.resultDetail ?? "",
                    petitionId = d.petitionId, friction = d.friction,
                    proposerId = d.proposerId, proposerName = d.proposerName ?? "",
                    deciderId = d.deciderId, deciderName = d.deciderName ?? "",
                    authorityBasis = d.authorityBasis ?? "", targetKey = d.targetKey ?? "",
                    escalated = d.escalated,
                };
                if (d.choices != null) rec.choices.AddRange(d.choices);
                save.decisions.Add(rec);
            }
        }

        /// <summary>
        /// 保存データから決裁キューを復元する（リストが無い旧セーブは空＝カードなし）。
        /// <b>適用済みフラグごと戻す</b>ので、ロード後に同じ案件を裁可しても効果は二度出ない。
        /// </summary>
        public static DecisionQueue ReadDecisions(CampaignSaveData save)
        {
            var queue = new DecisionQueue();
            if (save == null || save.decisions == null) return queue;

            for (int i = 0; i < save.decisions.Count; i++)
            {
                DecisionSave s = save.decisions[i];
                if (s == null) continue;
                var d = new PendingDecision(s.id, s.title, (DecisionSeverity)s.severity,
                                            (DecisionSource)s.source, s.effectKey, s.defaultChoiceIndex, s.body)
                {
                    imageKey = s.imageKey ?? "",
                    status = (DecisionStatus)s.status,
                    elapsed = s.elapsed,
                    chosenIndex = s.chosenIndex,
                    applied = s.applied,
                    meterApplied = s.meterApplied,
                    outcome = (PetitionActionOutcome)s.outcome,
                    resultDetail = s.resultDetail ?? "",
                    petitionId = s.petitionId,
                    friction = s.friction,
                    proposerId = s.proposerId, proposerName = s.proposerName ?? "",
                    deciderId = s.deciderId, deciderName = s.deciderName ?? "",
                    authorityBasis = s.authorityBasis ?? "", targetKey = s.targetKey ?? "",
                    escalated = s.escalated,
                };
                if (s.choices != null) d.choices.AddRange(s.choices);
                queue.Enqueue(d);
            }
            return queue;
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
