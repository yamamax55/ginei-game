using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力(FactionData)アセットをワンクリック生成するエディタ拡張。
    /// メニュー「Ginei/Create Faction Data」から実行する。
    /// 帝国・同盟の2勢力に加え、多勢力化の検証用に3勢力目（自治領）も作成する。
    /// アセットは `Assets/Resources/Factions/` に作成（既存は色などを更新、参照は維持）。
    ///
    /// 既定では「異勢力＝敵」（FactionData.IsHostileTo）なので、3勢力目は帝国・同盟の双方と
    /// 敵対する。味方関係を作りたい場合は各アセットの nonHostileFactions に相手を追加する。
    /// </summary>
    public static class FactionDataCreator
    {
        private const string FactionDir = "Assets/Resources/Factions";

        [MenuItem("Ginei/Create Faction Data")]
        public static void CreateFactions()
        {
            EnsureFolder(FactionDir);

            // 色は FactionColor の既定（帝国=赤／同盟=青）に合わせる。3勢力目は緑。
            GetOrCreateFaction("Empire", "帝国", new Color(0.9f, 0.2f, 0.2f), "王党派（帝国）", Faction.帝国);
            GetOrCreateFaction("Alliance", "同盟", new Color(0.2f, 0.5f, 0.9f), "民主派（同盟）", Faction.同盟);
            // 多勢力化の検証用 3 勢力目（既定で帝国・同盟の双方と敵対）
            GetOrCreateFaction("Autonomous", "自治領", new Color(0.3f, 0.8f, 0.4f), "中立軍閥", Faction.帝国);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Ginei: FactionData アセット（帝国/同盟/自治領）を Assets/Resources/Factions に生成しました。" +
                      "ScenarioData の各エントリや FleetStrength.factionData に割り当てて使います。");
        }

        private static FactionData GetOrCreateFaction(string fileName, string factionName, Color color, string ideology, Faction legacy)
        {
            string path = $"{FactionDir}/{fileName}.asset";
            FactionData existing = AssetDatabase.LoadAssetAtPath<FactionData>(path);
            FactionData f = existing != null ? existing : ScriptableObject.CreateInstance<FactionData>();

            f.factionName = factionName;
            f.color = color;
            f.ideology = ideology;
            f.legacyFaction = legacy;

            if (existing == null) AssetDatabase.CreateAsset(f, path);
            else EditorUtility.SetDirty(f);
            return f;
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
