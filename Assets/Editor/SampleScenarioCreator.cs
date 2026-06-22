using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// テスト用 Battle デモのサンプル提督(AdmiralData)・会戦(ScenarioData)アセットをワンクリック生成するエディタ拡張。
    /// メニュー「Ginei/Create Sample Scenarios」から実行する。
    /// ScenarioData は BattleSetup が拾えるよう Resources 配下に、AdmiralData は Assets/Data/Admirals に作成する。
    ///
    /// 【現状反映（このデモの狙い）】現行 AdmiralData/ScenarioData の機能を一通り触れるデモにする：
    ///   - 多勢力 FactionData（Empire/Alliance）で色・敵対判定を駆動（無ければ enum で後方互換）。
    ///   - 構造化姓名 #523（FullName/EpithetName）／専用旗艦 #旗艦名（signatureShipName）。
    ///   - 人物アーキタイプ（覇王 isKaiser／半身 isRightHand／魔術師 isMagician）。
    ///   - 性格 #2311（CommanderPersonality）／得意戦型 #2307（CombatSpecialty）／武名 #2304（fame）。
    ///   - 特技・戦法 #特技（TalentCatalog の id＋格）。
    ///   - 多様な勝利条件（殲滅／時間防衛）と増援 #2182（reinforcementDelay）。
    /// 既存の同名提督は能力チューニングを尊重（上書きしない）し、新しいメタデータ（姓名・性格・特技・旗艦等）と
    /// 階級(#14)は再生成で更新する。シナリオは内容を更新する。
    /// </summary>
    public static class SampleScenarioCreator
    {
        private const string AdmiralDir = "Assets/Data/Admirals";
        private const string ScenarioDir = "Assets/Resources";
        private const string FactionDir = "Assets/Resources/Factions";

        [MenuItem("Ginei/Create Sample Scenarios")]
        public static void CreateSamples()
        {
            EnsureFolder(AdmiralDir);
            EnsureFolder(ScenarioDir);

            // 多勢力 FactionData（あれば色・敵対判定・階級名がこれに従う。無ければ enum で従来動作＝後方互換）
            FactionData fEmpire = LoadFaction("Empire");
            FactionData fAlliance = LoadFaction("Alliance");

            // ── 提督ロスター ──
            // 能力 0-100。得意陣形＝個性（攻撃型:紡錘陣／機動型:鶴翼陣／防御型:円陣・方陣）。
            // 階級 tier（#14 既定ラダー：中将7/大将8/上級大将9/元帥10）。梯団の必要階級（軍団=大将8/軍集団=元帥10）と噛み合う配分。
            AdmiralData reinhard = Admiral(new Prof
            {
                file = "Reinhard", name = "ラインハルト", faction = Faction.帝国,
                lead = 98, atk = 95, def = 88, mob = 92, ope = 70, intel = 90, str = 10000,
                pref = Formation.紡錘陣, rank = 10, // 元帥
                given = "ラインハルト", particle = "フォン", family = "ローエングラム", call = "ラインハルト",
                ship = "ブリュンヒルト", kaiser = true,
                personality = CommanderPersonality.果敢, specialty = CombatSpecialty.会戦,
                fame = 95, humility = 40, ambition = 98, loyalty = 1f,
                talents = new[] { T("鬼神", TalentGrade.特), T("神算", TalentGrade.上) },
            });
            AdmiralData kircheis = Admiral(new Prof
            {
                file = "Kircheis", name = "キルヒアイス", faction = Faction.帝国,
                lead = 92, atk = 90, def = 85, mob = 88, ope = 80, intel = 88, str = 9000,
                pref = Formation.紡錘陣, rank = 9, // 上級大将
                given = "ジークフリード", family = "キルヒアイス", call = "キルヒアイス",
                ship = "バルバロッサ", rightHand = true,
                personality = CommanderPersonality.冷静, specialty = CombatSpecialty.会戦,
                fame = 80, humility = 88, ambition = 50, loyalty = 1f,
                talents = new[] { T("用兵", TalentGrade.上), T("鉄壁", TalentGrade.中) },
            });
            AdmiralData mittermeyer = Admiral(new Prof
            {
                file = "Mittermeyer", name = "ミッターマイヤー", faction = Faction.帝国,
                lead = 90, atk = 88, def = 82, mob = 98, ope = 78, intel = 80, str = 9000,
                pref = Formation.鶴翼陣, rank = 8, // 大将
                given = "ウォルフガング", family = "ミッターマイヤー", epithet = "疾風", call = "ミッターマイヤー",
                ship = "ベイオウルフ",
                personality = CommanderPersonality.果敢, specialty = CombatSpecialty.機動戦,
                fame = 80, humility = 75, ambition = 70, loyalty = 1f,
                talents = new[] { T("疾風", TalentGrade.上), T("突貫", TalentGrade.中) },
            });
            AdmiralData reuental = Admiral(new Prof
            {
                file = "Reuental", name = "ロイエンタール", faction = Faction.帝国,
                lead = 91, atk = 90, def = 84, mob = 90, ope = 80, intel = 85, str = 9000,
                pref = Formation.紡錘陣, rank = 8, // 大将
                given = "オスカー", particle = "フォン", family = "ロイエンタール", call = "ロイエンタール",
                ship = "トリスタン",
                personality = CommanderPersonality.冷静, specialty = CombatSpecialty.会戦,
                fame = 80, humility = 55, ambition = 85, loyalty = 0.7f, // 高功名心×やや低忠誠＝下剋上の芽
                talents = new[] { T("鬼神", TalentGrade.上), T("急襲", TalentGrade.中) },
            });

            AdmiralData yang = Admiral(new Prof
            {
                file = "Yang", name = "ヤン", faction = Faction.同盟,
                lead = 96, atk = 88, def = 90, mob = 80, ope = 85, intel = 99, str = 10000,
                pref = Formation.円陣, rank = 7, // 中将（軍団司令には階級不足＝ゲート体感用）
                given = "ウェンリー", family = "ヤン", epithet = "魔術師", call = "ヤン",
                ship = "ヒューベリオン", magician = true,
                personality = CommanderPersonality.慎重, specialty = CombatSpecialty.防衛,
                fame = 88, humility = 80, ambition = 10, loyalty = 1f,
                talents = new[] { T("神算", TalentGrade.神), T("不動", TalentGrade.上) },
            });
            AdmiralData bucock = Admiral(new Prof
            {
                file = "Bucock", name = "ビュコック", faction = Faction.同盟,
                lead = 90, atk = 82, def = 88, mob = 75, ope = 88, intel = 85, str = 9000,
                pref = Formation.方陣, rank = 10, // 元帥
                given = "アレクサンドル", family = "ビュコック", call = "ビュコック",
                personality = CommanderPersonality.堅実, specialty = CombatSpecialty.防衛,
                fame = 82, humility = 90, ambition = 30, loyalty = 1f,
                talents = new[] { T("鉄壁", TalentGrade.上), T("不動", TalentGrade.中) },
            });
            AdmiralData attenborough = Admiral(new Prof
            {
                file = "Attenborough", name = "アッテンボロー", faction = Faction.同盟,
                lead = 85, atk = 84, def = 80, mob = 88, ope = 75, intel = 82, str = 8000,
                pref = Formation.鶴翼陣, rank = 7, // 中将
                given = "ダスティ", family = "アッテンボロー", call = "アッテンボロー",
                personality = CommanderPersonality.果敢, specialty = CombatSpecialty.機動戦,
                fame = 60, humility = 70, ambition = 65, loyalty = 1f,
                talents = new[] { T("急襲", TalentGrade.中) },
            });
            AdmiralData uranff = Admiral(new Prof
            {
                file = "Uranff", name = "ウランフ", faction = Faction.同盟,
                lead = 86, atk = 83, def = 82, mob = 84, ope = 80, intel = 80, str = 8500,
                pref = Formation.横陣, rank = 8, // 大将
                given = "ウランフ", family = "ウランフ", call = "ウランフ",
                personality = CommanderPersonality.堅実, specialty = CombatSpecialty.会戦,
                fame = 62, humility = 72, ambition = 55, loyalty = 1f,
                talents = new[] { T("用兵", TalentGrade.中) },
            });

            // 1) 兵力差：帝国2(ラインハルト＋キルヒアイス) vs 同盟1(ヤン)＝局所優勢の体感（ランチェスター）
            CreateScenario("ヴァンフリート星域会戦", VictoryCondition.殲滅, new List<ScenarioData.FleetEntry>
            {
                Entry(reinhard, Faction.帝国, fEmpire, new Vector2(5f, 2f), Formation.紡錘陣, 1),
                Entry(kircheis, Faction.帝国, fEmpire, new Vector2(5f, -2f), Formation.横陣, 2),
                Entry(yang, Faction.同盟, fAlliance, new Vector2(-5f, 0f), Formation.鶴翼陣, 13),
            });

            // 2) 多対多・配置違い：帝国3 vs 同盟3（編制管理UIの組み替えを試せるよう各勢力2軍団を割り当て #147）
            CreateScenario("アムリッツァ星域会戦", VictoryCondition.殲滅, new List<ScenarioData.FleetEntry>
            {
                Entry(reinhard, Faction.帝国, fEmpire, new Vector2(6f, 3f), Formation.紡錘陣, 1, corps: "親衛軍団"),
                Entry(mittermeyer, Faction.帝国, fEmpire, new Vector2(6f, 0f), Formation.横陣, 3, corps: "帝国主力軍団"),
                Entry(reuental, Faction.帝国, fEmpire, new Vector2(6f, -3f), Formation.横陣, 4, corps: "帝国主力軍団"),
                Entry(yang, Faction.同盟, fAlliance, new Vector2(-6f, 3f), Formation.鶴翼陣, 13, corps: "イゼルローン方面軍"),
                Entry(bucock, Faction.同盟, fAlliance, new Vector2(-6f, 0f), Formation.横陣, 5, corps: "同盟本隊"),
                Entry(attenborough, Faction.同盟, fAlliance, new Vector2(-6f, -3f), Formation.方陣, 14, corps: "同盟本隊"),
            });

            // 3) 拮抗・2vs2（配置・陣形違い）
            CreateScenario("回廊の戦い", VictoryCondition.殲滅, new List<ScenarioData.FleetEntry>
            {
                Entry(mittermeyer, Faction.帝国, fEmpire, new Vector2(4f, 2f), Formation.紡錘陣, 3),
                Entry(reuental, Faction.帝国, fEmpire, new Vector2(4f, -2f), Formation.紡錘陣, 4),
                Entry(yang, Faction.同盟, fAlliance, new Vector2(-4f, 2f), Formation.鶴翼陣, 13),
                Entry(uranff, Faction.同盟, fAlliance, new Vector2(-4f, -2f), Formation.横陣, 10),
            });

            // 4) 関ヶ原型（#817）：同盟は名目兵力で優るが、調略済み・低忠誠の艦隊を抱える＝「戦う前に決まる戦い」。
            //    アッテンボロー隊（小早川型・調略済み）は劣勢を見て寝返り、ウランフ隊（毛利型・低忠誠）は静観する。
            CreateScenario("調略の会戦", VictoryCondition.殲滅, new List<ScenarioData.FleetEntry>
            {
                Entry(reinhard, Faction.帝国, fEmpire, new Vector2(6f, 2f), Formation.紡錘陣, 1),
                Entry(mittermeyer, Faction.帝国, fEmpire, new Vector2(6f, -2f), Formation.横陣, 3),
                Entry(yang, Faction.同盟, fAlliance, new Vector2(-6f, 0f), Formation.鶴翼陣, 13),                                       // 義に殉じる主力（三成・大谷型）
                Entry(attenborough, Faction.同盟, fAlliance, new Vector2(-6f, 4f), Formation.横陣, 14, loyalty: 0.25f, intrigue: 0.7f), // 小早川型＝劣勢で寝返る
                Entry(uranff, Faction.同盟, fAlliance, new Vector2(-6f, -4f), Formation.方陣, 10, loyalty: 0.45f, intrigue: 0.2f),       // 毛利型＝山上で静観
            });

            // 5) 時間防衛＋増援（#2182）：同盟ヤン隊が帝国の攻勢を120秒耐え抜けば勝利。帝国はラインハルト本隊を30秒遅れで投入（時間差圧力）。
            CreateScenario("イゼルローン回廊の死守", VictoryCondition.時間防衛, 120f, null, new List<ScenarioData.FleetEntry>
            {
                Entry(yang, Faction.同盟, fAlliance, new Vector2(-5f, 1f), Formation.円陣, 13),
                Entry(attenborough, Faction.同盟, fAlliance, new Vector2(-5f, -1f), Formation.鶴翼陣, 14),
                Entry(mittermeyer, Faction.帝国, fEmpire, new Vector2(5f, 2f), Formation.紡錘陣, 3),
                Entry(reuental, Faction.帝国, fEmpire, new Vector2(5f, -2f), Formation.横陣, 4),
                Entry(reinhard, Faction.帝国, fEmpire, new Vector2(7f, 0f), Formation.紡錘陣, 1, reinforcementDelay: 30f), // 時間差の本隊投入
            }, objectiveFaction: Faction.同盟);

            // 6) 要塞攻略戦（#78）：攻め手＝同盟（プレイヤーが同盟を選べば攻城側）。帝国のイゼルローン要塞を
            //    制限時間内に制圧（コア破壊 or 全砲台沈黙）すれば勝利。要塞は駐留艦隊(キルヒアイス隊)が守る。
            //    要塞主砲を避けて懐に潜り込み、砲台を削って死角から攻める攻防。objectiveFaction=帝国(守備＝要塞所有)。
            CreateScenario("イゼルローン要塞攻略戦", VictoryCondition.要塞攻略, 240f, null, new List<ScenarioData.FleetEntry>
            {
                Entry(kircheis, Faction.帝国, fEmpire, new Vector2(2.5f, 0f), Formation.円陣, 2),      // 要塞駐留艦隊（守備）
                Entry(yang, Faction.同盟, fAlliance, new Vector2(-9f, 2f), Formation.鶴翼陣, 13),       // 攻城（プレイヤー想定）
                Entry(attenborough, Faction.同盟, fAlliance, new Vector2(-9f, -2f), Formation.横陣, 14),
            }, objectiveFaction: Faction.帝国, fortresses: new List<ScenarioData.FortressEntry>
            {
                Fortress(Faction.帝国, fEmpire, new Vector2(0f, 0f), "イゼルローン要塞", 12, 14000),
            });

            // 7) 要塞防衛戦（#78）：守り手＝帝国（プレイヤーが帝国を選べば守備側）。同盟の攻勢から自要塞を
            //    制限時間(180秒)守り切れば勝利。要塞が陥落すると敗北。攻撃側(同盟)はAIが要塞へ寄せる(攻城)。
            //    objectiveFaction=帝国(守備＝要塞所有)。
            CreateScenario("ガイエスブルク要塞防衛戦", VictoryCondition.要塞防衛, 180f, null, new List<ScenarioData.FleetEntry>
            {
                Entry(reuental, Faction.帝国, fEmpire, new Vector2(2.5f, 1.5f), Formation.円陣, 4),     // 守備（プレイヤー想定）
                Entry(mittermeyer, Faction.帝国, fEmpire, new Vector2(2.5f, -1.5f), Formation.方陣, 3),
                Entry(yang, Faction.同盟, fAlliance, new Vector2(-9f, 2f), Formation.鶴翼陣, 13),        // 攻勢（AIが要塞へ寄せる）
                Entry(bucock, Faction.同盟, fAlliance, new Vector2(-9f, 0f), Formation.横陣, 5),
                Entry(attenborough, Faction.同盟, fAlliance, new Vector2(-9f, -2f), Formation.紡錘陣, 14),
            }, objectiveFaction: Faction.帝国, fortresses: new List<ScenarioData.FortressEntry>
            {
                Fortress(Faction.帝国, fEmpire, new Vector2(0f, 0f), "ガイエスブルク要塞", 12, 14000),
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool factionsMissing = fEmpire == null || fAlliance == null;
            string note = factionsMissing
                ? " ※FactionData(Empire/Alliance)が未生成のため enum 動作（色/敵対は既定）。多勢力の色・敵対を効かせるには先に『Ginei/Create Faction Data』を実行してください。"
                : "";
            Debug.Log("Ginei: サンプルシナリオ7本と提督アセットを生成しました（殲滅×4／時間防衛＋増援×1／要塞攻略×1／要塞防衛×1 #78）。" +
                "現状機能を反映＝多勢力FactionData／構造化姓名#523／専用旗艦／アーキタイプ(覇王/半身/魔術師)／性格#2311／得意戦型#2307／武名#2304／特技#特技／増援#2182。" +
                "※既存の Admirals/*.asset は能力を尊重しつつ姓名・性格・特技・旗艦・階級を再生成で更新します。" + note);
        }

        /// <summary>提督の生成プロファイル（任意フィールドは既定＝後方互換）。</summary>
        private struct Prof
        {
            public string file, name;
            public Faction faction;
            public int lead, atk, def, mob, ope, intel, str;
            public Formation pref;
            public int rank;
            // 構造化姓名（#523）
            public string given, middle, family, particle, epithet, call;
            public int regnal;
            // 専用旗艦・アーキタイプ
            public string ship;
            public bool kaiser, rightHand, magician;
            // 采配・人物（#2311/#2307/#2304）
            public CommanderPersonality personality;
            public CombatSpecialty specialty;
            public int fame, humility, ambition;
            public float loyalty; // personalLoyalty（0以下は 1 とみなす）
            // 特技（#特技）
            public Talent[] talents;
        }

        private static Talent T(string id, TalentGrade grade) => new Talent(id, grade);

        private static FactionData LoadFaction(string fileName)
            => AssetDatabase.LoadAssetAtPath<FactionData>($"{FactionDir}/{fileName}.asset");

        private static ScenarioData.FleetEntry Entry(AdmiralData admiral, Faction faction, FactionData factionData,
            Vector2 pos, Formation formation, int fleetNumber = 0, string fleetName = "", string corps = null,
            string armyGroup = null, float loyalty = 1f, float intrigue = 0f, float reinforcementDelay = 0f)
        {
            // #147 デモ：未指定なら勢力ごとの既定梯団を割り当て（番号付き艦隊のみ。表示確認用）。
            if (corps == null) corps = faction == Faction.帝国 ? "帝国主力軍団" : "同盟主力軍団";
            if (armyGroup == null) armyGroup = faction == Faction.帝国 ? "ローエングラム軍集団" : "自由惑星同盟軍";
            bool hasNum = fleetNumber > 0;
            return new ScenarioData.FleetEntry
            {
                admiral = admiral,
                faction = faction,
                factionData = factionData,        // 多勢力：割り当てると enum より優先（色・敵対判定）
                spawnPosition = pos,
                formation = formation,
                fleetNumber = fleetNumber,         // #146：0=未指定（従来どおり提督名のみ）
                fleetName = fleetName,
                corps = hasNum ? corps : "",       // #147：番号なし艦隊には梯団を付けない
                armyGroup = hasNum ? armyGroup : "",
                loyalty = loyalty,                 // #817：旗幟（1/0＝従来動作）
                intrigue = intrigue,
                reinforcementDelay = reinforcementDelay, // #2182：>0で時間差投入
            };
        }

        private static AdmiralData Admiral(Prof p)
        {
            string path = $"{AdmiralDir}/{p.file}.asset";
            AdmiralData a = AssetDatabase.LoadAssetAtPath<AdmiralData>(path);
            bool isNew = a == null;
            if (isNew)
            {
                a = ScriptableObject.CreateInstance<AdmiralData>();
                // 能力チューニングは新規作成時のみ設定（既存は尊重）。
                a.admiralName = p.name;
                a.faction = p.faction;
                a.leadership = p.lead;
                a.attack = p.atk;
                a.defense = p.def;
                a.mobility = p.mob;
                a.operation = p.ope;
                a.intelligence = p.intel;
                a.baseStrength = p.str;
                a.hasPreferredFormation = true;   // #104：得意陣形を割り当てる
                a.preferredFormation = p.pref;
            }

            // ── 新メタデータは新規/既存とも再生成で更新（idempotent。能力は触らない）──
            a.rankTier = p.rank;                  // #14：階級（指揮可能規模 RANKCMD-1 の出所）
            // #523：構造化姓名（任意）。未指定要素は空＝admiralName へフォールバック（後方互換）。
            a.givenName = p.given ?? "";
            a.middleName = p.middle ?? "";
            a.familyName = p.family ?? "";
            a.nobleParticle = p.particle ?? "";
            a.epithet = p.epithet ?? "";
            a.callName = p.call ?? "";
            a.regnalNumber = p.regnal;
            a.signatureShipName = p.ship ?? "";   // #旗艦名：専用旗艦
            // 人物アーキタイプ
            a.isKaiser = p.kaiser;
            a.isRightHand = p.rightHand;
            a.isMagician = p.magician;
            // 采配・人物（#2311/#2307/#2304）
            a.personality = p.personality;
            a.specialty = p.specialty;
            a.fame = p.fame;
            a.humility = p.humility;
            a.ambition = p.ambition;
            a.personalLoyalty = p.loyalty <= 0f ? 1f : p.loyalty;
            // #特技：所持特技（置換＝再実行でも重複しない）
            a.talents = p.talents != null ? new List<Talent>(p.talents) : new List<Talent>();

            if (isNew) AssetDatabase.CreateAsset(a, path);
            else EditorUtility.SetDirty(a);
            return a;
        }

        private static void CreateScenario(string scenarioName, VictoryCondition vc,
            List<ScenarioData.FleetEntry> fleets)
            => CreateScenario(scenarioName, vc, 180f, null, fleets);

        private static void CreateScenario(string scenarioName, VictoryCondition vc,
            float timeLimit, AdmiralData targetAdmiral, List<ScenarioData.FleetEntry> fleets,
            Faction objectiveFaction = Faction.同盟, List<ScenarioData.FortressEntry> fortresses = null)
        {
            string path = $"{ScenarioDir}/{scenarioName}.asset";
            ScenarioData s = AssetDatabase.LoadAssetAtPath<ScenarioData>(path);
            bool isNew = s == null;
            if (isNew) s = ScriptableObject.CreateInstance<ScenarioData>();

            s.scenarioName = scenarioName;
            s.victoryCondition = vc;
            s.objectiveFaction = objectiveFaction;
            s.timeLimit = timeLimit;
            s.targetAdmiral = targetAdmiral;
            s.fleets = fleets;
            s.fortresses = fortresses ?? new List<ScenarioData.FortressEntry>(); // #78：要塞（無ければ空＝再生成で消える）

            if (isNew) AssetDatabase.CreateAsset(s, path);
            else EditorUtility.SetDirty(s);
        }

        /// <summary>要塞エントリ（#78）。位置・規模（砲台数/耐久）・主砲の有無を指定。</summary>
        private static ScenarioData.FortressEntry Fortress(Faction faction, FactionData factionData,
            Vector2 pos, string name, int turretCount = 12, int coreStrength = 14000, bool mainCannon = true)
        {
            return new ScenarioData.FortressEntry
            {
                faction = faction,
                factionData = factionData,
                position = pos,
                fortressName = name,
                turretCount = turretCount,
                coreStrength = coreStrength,
                hasMainCannon = mainCannon,
            };
        }

        /// <summary>"Assets/A/B" を順に作成（既存はスキップ）。</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
