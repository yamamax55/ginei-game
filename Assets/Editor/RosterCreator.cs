using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 世界観バイブル(docs/worldbuilding-bible.md)準拠の**オリジナル提督ロスター**と、
    /// 勝利条件・兵力差の異なる**シナリオ群**を生成するエディタ拡張（Issue #18）。
    /// メニュー「Ginei/Create Roster &amp; Scenarios」から実行する。
    ///
    /// - 提督(AdmiralData)は `Assets/Data/Admirals/`、シナリオ(ScenarioData)は `Resources/` に作成。
    /// - 既存の同名提督は尊重（上書きしない）。シナリオは内容を更新。
    /// - 三つ巴・軍閥連合シナリオは FactionData（`Ginei/Create Faction Data` で生成）を参照する。
    ///   未生成でも 2 勢力分は動くが、3 勢力を分けるには先に Faction Data を作ること。
    /// 既存 IP の固有名詞は使わず、バイブル §3 の命名規則に沿ったオリジナル名を用いる。
    /// </summary>
    public static class RosterCreator
    {
        private const string AdmiralDir = "Assets/Data/Admirals";
        private const string ScenarioDir = "Assets/Resources";
        private const string FactionDir = "Assets/Resources/Factions";

        [MenuItem("Ginei/Create Roster & Scenarios")]
        public static void CreateRoster()
        {
            EnsureFolder(AdmiralDir);
            EnsureFolder(ScenarioDir);

            // FactionData（あれば多勢力シナリオで使用。無ければ enum のみ）
            FactionData fEmpire = LoadFaction("Empire");
            FactionData fAlliance = LoadFaction("Alliance");
            FactionData fCommunist = LoadFaction("Communist");
            FactionData fAutonomous = LoadFaction("Autonomous");
            FactionData fWarlord = LoadFaction("Warlord");

            // ── 提督ロスター（能力 0-100：統率/攻撃/防御/機動/運営/情報、基準兵力）──
            // 王党派（帝国系）：攻撃・統率に優れる（得意陣形＝攻撃型は紡錘陣／機動型は鶴翼陣／防御型は方陣）
            // 貴族＋異名「疾風」（FullName=ヴァルダー・フォン・アイゼンベルク／頭上=疾風ヴァルダー）
            AdmiralData walder   = Admiral("Walder", "ヴァルダー", Faction.帝国, 95, 96, 85, 90, 72, 82, 11000, Formation.紡錘陣, 8, // 大将
                new NameParts { given = "ヴァルダー", particle = "フォン", family = "アイゼンベルク", epithet = "疾風", callName = "ヴァルダー" });
            AdmiralData greven   = Admiral("Greven", "グレーヴェン", Faction.帝国, 88, 90, 80, 92, 75, 80, 9000, Formation.鶴翼陣, 7);  // 中将
            AdmiralData rothstein= Admiral("Rothstein", "ロートシュタイン", Faction.帝国, 86, 85, 88, 80, 78, 82, 9000, Formation.方陣, 6); // 少将
            // 世数付き君主（FullName=ブラント三世／頭上=ブラント）
            AdmiralData brandt   = Admiral("Brandt", "ブラント", Faction.帝国, 84, 88, 82, 86, 74, 78, 8500, Formation.紡錘陣, 8, // 大将（君主）
                new NameParts { given = "ブラント", regnal = 3 });

            // 民主派（同盟系）：防御・情報に優れ粘る（防御型は円陣／機動型は鶴翼陣）
            // 平民＋異名が短縮名に付く「ミラクル」（FullName=カーター・グリーン／頭上=ミラクルグリーン・前置詞無し＝平民）
            AdmiralData carter   = Admiral("Carter", "カーター", Faction.同盟, 94, 84, 90, 80, 85, 95, 10500, Formation.円陣, 7, // 中将（ミラクル）
                new NameParts { given = "カーター", family = "グリーン", epithet = "ミラクル", callName = "グリーン" });
            AdmiralData vega     = Admiral("Vega", "ヴェガ", Faction.同盟, 88, 82, 88, 82, 82, 86, 9000, Formation.円陣, 6);  // 少将
            AdmiralData lowell   = Admiral("Lowell", "ロウェル", Faction.同盟, 85, 80, 86, 84, 80, 84, 8500, Formation.方陣, 6); // 少将
            AdmiralData marsh    = Admiral("Marsh", "マーシュ", Faction.同盟, 83, 82, 84, 86, 78, 80, 8500, Formation.鶴翼陣, 5); // 准将

            // 共産主義：運営・統率（集団指揮）に優れる＝密集の方陣。enum は暫定で帝国、実体は FactionData。
            AdmiralData volkov   = Admiral("Volkov", "同志ヴォルコフ", Faction.帝国, 92, 86, 84, 82, 90, 80, 10000, Formation.方陣, 8); // 大将
            AdmiralData raisa    = Admiral("Raisa", "同志ライサ", Faction.帝国, 86, 84, 80, 84, 88, 82, 9000, Formation.方陣, 7);   // 中将

            // 軍閥：玉石混交。機動寄り・能力低め＝鶴翼陣。enum は暫定で同盟。
            AdmiralData dougal   = Admiral("Dougal", "ドゥーガル", Faction.同盟, 82, 85, 76, 88, 70, 74, 8000, Formation.鶴翼陣, 6); // 少将
            AdmiralData morris   = Admiral("Morris", "モリス", Faction.同盟, 80, 82, 78, 84, 72, 76, 7500, Formation.鶴翼陣, 5);   // 准将

            // ── シナリオ群（勝利条件・兵力差の違い）──

            // 1) 殲滅・拮抗 2vs2（王党派 vs 民主派）
            Scenario("黎明の遭遇戦", VictoryCondition.殲滅, Faction.同盟, 0f, null, new List<ScenarioData.FleetEntry>
            {
                Entry(walder, Faction.帝国, fEmpire, new Vector2(5f, 2f), Formation.紡錘陣),
                Entry(greven, Faction.帝国, fEmpire, new Vector2(5f, -2f), Formation.横陣),
                Entry(carter, Faction.同盟, fAlliance, new Vector2(-5f, 2f), Formation.鶴翼陣),
                Entry(vega,   Faction.同盟, fAlliance, new Vector2(-5f, -2f), Formation.横陣),
            });

            // 2) 時間防衛・兵力差（民主派カーターが王党派2部隊の攻勢を120秒耐え抜けば勝利）
            Scenario("孤塁の防衛", VictoryCondition.時間防衛, Faction.同盟, 120f, null, new List<ScenarioData.FleetEntry>
            {
                Entry(walder, Faction.帝国, fEmpire, new Vector2(6f, 2f), Formation.紡錘陣),
                Entry(brandt, Faction.帝国, fEmpire, new Vector2(6f, -2f), Formation.横陣),
                Entry(carter, Faction.同盟, fAlliance, new Vector2(-5f, 0f), Formation.円陣),
            });

            // 3) 旗艦撃破（民主派が王党派VIPヴァルダーを討てば勝利。生存で時間切れなら王党派勝利）
            Scenario("将旗を討て", VictoryCondition.旗艦撃破, Faction.同盟, 180f, walder, new List<ScenarioData.FleetEntry>
            {
                Entry(walder,    Faction.帝国, fEmpire, new Vector2(6f, 0f), Formation.方陣),
                Entry(rothstein, Faction.帝国, fEmpire, new Vector2(6f, -3f), Formation.横陣),
                Entry(carter, Faction.同盟, fAlliance, new Vector2(-6f, 2f), Formation.鶴翼陣),
                Entry(vega,   Faction.同盟, fAlliance, new Vector2(-6f, 0f), Formation.横陣),
                Entry(lowell, Faction.同盟, fAlliance, new Vector2(-6f, -2f), Formation.横陣),
            });

            // 4) 護衛（民主派VIPヴェガを150秒守り切れば勝利。喪失で敗北）
            Scenario("要人護送", VictoryCondition.護衛, Faction.同盟, 150f, vega, new List<ScenarioData.FleetEntry>
            {
                Entry(vega,  Faction.同盟, fAlliance, new Vector2(-4f, 0f), Formation.円陣),
                Entry(marsh, Faction.同盟, fAlliance, new Vector2(-6f, 0f), Formation.横陣),
                Entry(greven, Faction.帝国, fEmpire, new Vector2(5f, 2f), Formation.紡錘陣),
                Entry(brandt, Faction.帝国, fEmpire, new Vector2(5f, -2f), Formation.紡錘陣),
            });

            // 5) 三つ巴・殲滅（王党派 vs 民主派 vs 共産）。FactionData 必須（無いと enum バケツで2極化）。
            Scenario("三国会戦", VictoryCondition.殲滅, Faction.同盟, 0f, null, new List<ScenarioData.FleetEntry>
            {
                Entry(walder, Faction.帝国, fEmpire,    new Vector2(0f, 6f), Formation.紡錘陣),
                Entry(greven, Faction.帝国, fEmpire,    new Vector2(2f, 6f), Formation.横陣),
                Entry(carter, Faction.同盟, fAlliance,  new Vector2(-6f, -3f), Formation.鶴翼陣),
                Entry(vega,   Faction.同盟, fAlliance,  new Vector2(-6f, -1f), Formation.横陣),
                Entry(volkov, Faction.帝国, fCommunist, new Vector2(6f, -3f), Formation.方陣),
                Entry(raisa,  Faction.帝国, fCommunist, new Vector2(6f, -1f), Formation.横陣),
            });

            // 6) 軍閥介入・殲滅（王党派 vs 民主派＋軍閥連合）。軍閥2勢力は #16 で相互非敵対＝共闘。
            Scenario("軍閥介入", VictoryCondition.殲滅, Faction.同盟, 0f, null, new List<ScenarioData.FleetEntry>
            {
                Entry(walder, Faction.帝国, fEmpire,     new Vector2(6f, 2f), Formation.紡錘陣),
                Entry(rothstein, Faction.帝国, fEmpire,  new Vector2(6f, -2f), Formation.横陣),
                Entry(carter, Faction.同盟, fAlliance,   new Vector2(-6f, 3f), Formation.鶴翼陣),
                Entry(dougal, Faction.同盟, fAutonomous, new Vector2(-6f, 0f), Formation.横陣),
                Entry(morris, Faction.同盟, fWarlord,    new Vector2(-6f, -3f), Formation.横陣),
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool factionsMissing = fEmpire == null || fAlliance == null || fCommunist == null || fAutonomous == null || fWarlord == null;
            string note = factionsMissing
                ? " ※一部の FactionData が未生成です。三つ巴/軍閥シナリオを正しく分けるには先に『Ginei/Create Faction Data』を実行してください。"
                : "";
            Debug.Log("Ginei: 提督12名とシナリオ6本（殲滅/時間防衛/旗艦撃破/護衛/三つ巴/軍閥介入）を生成しました。" +
                "#523 命名の実例＝Walder(貴族＋異名 疾風)/Carter(平民＋異名 ミラクル)/Brandt(世数 三世)。" +
                "※既存の Admirals/*.asset は上書きしないため、新フィールドを反映するには該当アセットを削除して再生成してください。" + note);
        }

        private static FactionData LoadFaction(string fileName)
            => AssetDatabase.LoadAssetAtPath<FactionData>($"{FactionDir}/{fileName}.asset");

        private static AdmiralData Admiral(string fileName, string admiralName, Faction faction,
            int leadership, int attack, int defense, int mobility, int operation, int intelligence, int baseStrength,
            Formation preferred, int rankTier, NameParts names = default)
        {
            string path = $"{AdmiralDir}/{fileName}.asset";
            AdmiralData existing = AssetDatabase.LoadAssetAtPath<AdmiralData>(path);
            if (existing != null)
            {
                // 既存の能力等は尊重しつつ、階級(#14)だけは再生成で更新する（指揮可能規模 RANKCMD-1 が階級から出るため）。
                existing.rankTier = rankTier;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AdmiralData a = ScriptableObject.CreateInstance<AdmiralData>();
            a.admiralName = admiralName;
            a.faction = faction;
            a.leadership = leadership;
            a.attack = attack;
            a.defense = defense;
            a.mobility = mobility;
            a.operation = operation;
            a.intelligence = intelligence;
            a.baseStrength = baseStrength;
            a.hasPreferredFormation = true;   // #104：得意陣形を割り当てる
            a.preferredFormation = preferred;
            a.rankTier = rankTier;            // #14：階級（指揮可能規模 RANKCMD-1 の出所）
            // #523：構造化姓名（任意）。未指定の要素は空＝admiralName へフォールバック（後方互換）。
            a.givenName     = names.given    ?? "";
            a.middleName    = names.middle   ?? "";
            a.familyName    = names.family   ?? "";
            a.nobleParticle = names.particle ?? "";
            a.epithet       = names.epithet  ?? "";
            a.callName      = names.callName ?? "";
            a.regnalNumber  = names.regnal;
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        /// <summary>
        /// 提督の構造化姓名（任意・#523）。未指定の要素は admiralName にフォールバックする。
        /// 固有 IP のキャラ名は使わず、汎用的なオリジナル名のみを用いる。
        /// </summary>
        public struct NameParts
        {
            public string given;    // 名（カタカナ）
            public string middle;   // ミドルネーム
            public string family;   // 姓
            public string particle; // 貴族の前置詞（フォン等。平民は空）
            public string epithet;  // 異名（頭上ラベルで短縮名の前に付く）
            public string callName; // 呼称・愛称（短縮表示の最優先）
            public int regnal;      // 世数（0＝無し）
        }

        private static ScenarioData.FleetEntry Entry(AdmiralData admiral, Faction faction, FactionData factionData,
            Vector2 pos, Formation formation)
        {
            return new ScenarioData.FleetEntry
            {
                admiral = admiral,
                faction = faction,
                factionData = factionData,
                spawnPosition = pos,
                formation = formation
            };
        }

        private static void Scenario(string scenarioName, VictoryCondition vc, Faction objectiveFaction,
            float timeLimit, AdmiralData targetAdmiral, List<ScenarioData.FleetEntry> fleets)
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

            if (isNew) AssetDatabase.CreateAsset(s, path);
            else EditorUtility.SetDirty(s);
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
