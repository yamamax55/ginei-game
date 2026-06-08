using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// サンプルの提督(AdmiralData)・会戦(ScenarioData)アセットをワンクリック生成するエディタ拡張。
    /// メニュー「Ginei/Create Sample Scenarios」から実行する。
    /// ScenarioData は BattleSetup が拾えるよう Resources 配下に、AdmiralData は Assets/Data/Admirals に作成する。
    /// 既存の同名アセットがあれば再利用（提督は上書きしない）し、シナリオは内容を更新する。
    /// </summary>
    public static class SampleScenarioCreator
    {
        private const string AdmiralDir = "Assets/Data/Admirals";
        private const string ScenarioDir = "Assets/Resources";

        [MenuItem("Ginei/Create Sample Scenarios")]
        public static void CreateSamples()
        {
            EnsureFolder(AdmiralDir);
            EnsureFolder(ScenarioDir);

            // 提督（無ければ作成・あれば再利用）。能力は 0-100。得意陣形＝個性（攻撃型:紡錘陣／機動型:鶴翼陣／防御型:円陣・方陣）。
            AdmiralData reinhard = GetOrCreateAdmiral("Reinhard", "ラインハルト", Faction.帝国, 98, 95, 88, 92, 70, 90, 10000, Formation.紡錘陣);
            AdmiralData kircheis = GetOrCreateAdmiral("Kircheis", "キルヒアイス", Faction.帝国, 92, 90, 85, 88, 80, 88, 9000, Formation.紡錘陣);
            AdmiralData mittermeyer = GetOrCreateAdmiral("Mittermeyer", "ミッターマイヤー", Faction.帝国, 90, 88, 82, 98, 78, 80, 9000, Formation.鶴翼陣);
            AdmiralData reuental = GetOrCreateAdmiral("Reuental", "ロイエンタール", Faction.帝国, 91, 90, 84, 90, 80, 85, 9000, Formation.紡錘陣);

            AdmiralData yang = GetOrCreateAdmiral("Yang", "ヤン", Faction.同盟, 96, 88, 90, 80, 85, 99, 10000, Formation.円陣);
            AdmiralData bucock = GetOrCreateAdmiral("Bucock", "ビュコック", Faction.同盟, 90, 82, 88, 75, 88, 85, 9000, Formation.方陣);
            AdmiralData attenborough = GetOrCreateAdmiral("Attenborough", "アッテンボロー", Faction.同盟, 85, 84, 80, 88, 75, 82, 8000, Formation.鶴翼陣);
            AdmiralData uranff = GetOrCreateAdmiral("Uranff", "ウランフ", Faction.同盟, 86, 83, 82, 84, 80, 80, 8500, Formation.横陣);

            // 1) 兵力差：帝国2(ラインハルト＋キルヒアイス) vs 同盟1(ヤン)
            CreateScenario("ヴァンフリート星域会戦", new List<ScenarioData.FleetEntry>
            {
                Entry(reinhard, Faction.帝国, new Vector2(5f, 2f), Formation.紡錘陣, 1),
                Entry(kircheis, Faction.帝国, new Vector2(5f, -2f), Formation.横陣, 2),
                Entry(yang, Faction.同盟, new Vector2(-5f, 0f), Formation.鶴翼陣, 13),
            });

            // 2) 多対多・配置違い：帝国3 vs 同盟3
            CreateScenario("アムリッツァ星域会戦", new List<ScenarioData.FleetEntry>
            {
                Entry(reinhard, Faction.帝国, new Vector2(6f, 3f), Formation.紡錘陣, 1),
                Entry(mittermeyer, Faction.帝国, new Vector2(6f, 0f), Formation.横陣, 3),
                Entry(reuental, Faction.帝国, new Vector2(6f, -3f), Formation.横陣, 4),
                Entry(yang, Faction.同盟, new Vector2(-6f, 3f), Formation.鶴翼陣, 13),
                Entry(bucock, Faction.同盟, new Vector2(-6f, 0f), Formation.横陣, 5),
                Entry(attenborough, Faction.同盟, new Vector2(-6f, -3f), Formation.方陣, 14),
            });

            // 3) 拮抗・2vs2（配置・陣形違い）
            CreateScenario("回廊の戦い", new List<ScenarioData.FleetEntry>
            {
                Entry(mittermeyer, Faction.帝国, new Vector2(4f, 2f), Formation.紡錘陣, 3),
                Entry(reuental, Faction.帝国, new Vector2(4f, -2f), Formation.紡錘陣, 4),
                Entry(yang, Faction.同盟, new Vector2(-4f, 2f), Formation.鶴翼陣, 13),
                Entry(uranff, Faction.同盟, new Vector2(-4f, -2f), Formation.横陣, 10),
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Ginei: サンプルシナリオ3本と提督アセットを生成しました（Resources 配下にシナリオ／艦隊番号付き #146）。");
        }

        private static ScenarioData.FleetEntry Entry(AdmiralData admiral, Faction faction, Vector2 pos, Formation formation,
            int fleetNumber = 0, string fleetName = "")
        {
            return new ScenarioData.FleetEntry
            {
                admiral = admiral,
                faction = faction,
                spawnPosition = pos,
                formation = formation,
                fleetNumber = fleetNumber,  // #146：0=未指定（従来どおり提督名のみ）
                fleetName = fleetName
            };
        }

        private static AdmiralData GetOrCreateAdmiral(string fileName, string admiralName, Faction faction,
            int leadership, int attack, int defense, int mobility, int operation, int intelligence, int baseStrength,
            Formation preferred)
        {
            string path = $"{AdmiralDir}/{fileName}.asset";
            AdmiralData existing = AssetDatabase.LoadAssetAtPath<AdmiralData>(path);
            if (existing != null) return existing; // 既存は尊重（上書きしない）

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
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        private static void CreateScenario(string scenarioName, List<ScenarioData.FleetEntry> fleets)
        {
            string path = $"{ScenarioDir}/{scenarioName}.asset";
            ScenarioData s = AssetDatabase.LoadAssetAtPath<ScenarioData>(path);
            bool isNew = false;
            if (s == null)
            {
                s = ScriptableObject.CreateInstance<ScenarioData>();
                isNew = true;
            }
            s.scenarioName = scenarioName;
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
