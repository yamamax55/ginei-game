using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 建国神話プリセット(FoundingMythPreset)＋必要な勢力(FactionData)をワンクリック生成するエディタ拡張。
    /// メニュー「Ginei/Create Founding Myth Presets」から実行する。Issue #490(START-1) / #489。
    ///
    /// 生成する4つの出発点：王党派(神授の帝政)／民主派(合議の共和国)／軍閥(実力の支配)／教団(信仰の共同体)。
    /// - イデオロギー/体制/階級は FactionData（ideology・legacyFaction・ranks）に持たせ、プリセットは参照するだけ。
    /// - 各プリセットに「授けられた方舟」スタート（LORE-8 #494）を共通付与する。
    /// - 世界観の2軸（設計↔創発 / 上から↔下から）上の点として各出発点を配置する。
    ///
    /// アセット出力：FactionData は `Assets/Resources/Factions/`、プリセットは `Assets/Resources/FoundingMyths/`。
    /// 既存アセットは壊さない：FactionData は「無ければ作成・あれば流用（GUID/内容を維持）」。
    /// プリセットは本ジェネレータの所有物なので、実行ごとにフィールドを再設定（冪等）する。
    /// 王党派/民主派/軍閥 は FactionDataCreator(#16) が作る Empire/Alliance/Warlord と同一ファイルを共有する。
    /// </summary>
    public static class FoundingMythCreator
    {
        private const string FactionDir = "Assets/Resources/Factions";
        private const string PresetDir = "Assets/Resources/FoundingMyths";

        [MenuItem("Ginei/Create Founding Myth Presets")]
        public static void CreatePresets()
        {
            EnsureFolder(FactionDir);
            EnsureFolder(PresetDir);

            // ── 必要な勢力(FactionData)を用意（無ければ作成・あれば流用）──
            // 王党派/民主派/軍閥 は FactionDataCreator と同名ファイル(Empire/Alliance/Warlord)を共有する。
            FactionData empire = GetOrCreateFaction("Empire", "帝国", new Color(0.9f, 0.2f, 0.2f),
                "王党派（専制・帝政）", Faction.帝国,
                Ranks((5, "准将"), (6, "少将"), (7, "中将"), (8, "大将"), (9, "上級大将"), (10, "元帥")));

            FactionData alliance = GetOrCreateFaction("Alliance", "同盟", new Color(0.2f, 0.5f, 0.9f),
                "民主派（共和制）", Faction.同盟,
                Ranks((5, "准将"), (6, "少将"), (7, "中将"), (8, "大将"), (10, "元帥")));

            FactionData warlord = GetOrCreateFaction("Warlord", "軍閥", new Color(0.45f, 0.75f, 0.35f),
                "軍閥（地方領袖）", Faction.同盟,
                Ranks((6, "隊長"), (8, "将軍"), (10, "総帥")));

            // 教団は本プリセット群で新規追加する勢力（神権・信仰共同体）。
            FactionData theocracy = GetOrCreateFaction("Theocracy", "教団", new Color(0.75f, 0.6f, 0.2f),
                "神権政（信仰共同体）", Faction.帝国,
                Ranks((5, "輔祭"), (6, "司祭"), (7, "主教"), (8, "大主教"), (10, "教主")));

            // ── 建国神話プリセットを生成（FactionData を参照しつつ、軸・初期傾向・方舟を設定）──
            FoundingMythPreset royalist = GetOrCreatePreset("Royalist", "王党派 ── 神授の帝政", empire,
                "天より授かりし血統が統べる帝国。秩序は上から与えられ、民はそれに従う。" +
                "結束と規律に厚いが、変革と異論には冷たい。",
                designVsEmergence: -0.8f, authorityAxis: -0.9f,
                expansion: 1.0f, aggression: 1.1f, cohesion: 1.3f, discipline: 1.3f, openness: 0.7f);

            FoundingMythPreset democrat = GetOrCreatePreset("Democrat", "民主派 ── 合議の共和国", alliance,
                "市民の合意が統治を生むと信じる共和国。自由と開明に富むが、" +
                "決断は遅く、結束は脆い。",
                designVsEmergence: 0.3f, authorityAxis: 0.9f,
                expansion: 1.0f, aggression: 0.85f, cohesion: 0.9f, discipline: 0.8f, openness: 1.3f);

            FoundingMythPreset warlordPreset = GetOrCreatePreset("Warlord", "軍閥 ── 実力の支配", warlord,
                "力こそが秩序を定める。実力者が版図を切り取り、勝者が法となる。" +
                "攻勢と拡張に長けるが、内部の結束は常に揺らぐ。",
                designVsEmergence: -0.2f, authorityAxis: -0.3f,
                expansion: 1.2f, aggression: 1.4f, cohesion: 0.75f, discipline: 1.0f, openness: 0.8f);

            FoundingMythPreset cult = GetOrCreatePreset("Theocracy", "教団 ── 信仰の共同体", theocracy,
                "創造主(機神)を崇める神権の共同体。授かりし聖船を奉じ、固く結束する。" +
                "信仰ゆえの不退転を持つが、原理を問う探究を禁忌とする。",
                designVsEmergence: -1.0f, authorityAxis: -0.7f,
                expansion: 1.0f, aggression: 1.0f, cohesion: 1.5f, discipline: 1.2f, openness: 0.6f);

            FoundingMythPreset[] presets = { royalist, democrat, warlordPreset, cult };
            foreach (var p in presets) EditorUtility.SetDirty(p);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Ginei: 建国神話プリセット4種（王党派/民主派/軍閥/教団）を Assets/Resources/FoundingMyths に生成。" +
                      "勢力(FactionData)は Assets/Resources/Factions を流用/追加（教団は新規）。" +
                      "全プリセットに『授けられた方舟』スタート(LORE-8)を共通付与。TitleManager の出発点選択に使う想定。");
        }

        /// <summary>
        /// 勢力アセットを「無ければ作成・あれば流用」。既存は内容を変更しない（GUID/値を維持）。
        /// 新規作成時のみ scalar・階級を設定する。
        /// </summary>
        private static FactionData GetOrCreateFaction(string fileName, string factionName, Color color,
            string ideology, Faction legacy, List<FactionData.RankEntry> ranks)
        {
            string path = $"{FactionDir}/{fileName}.asset";
            FactionData existing = AssetDatabase.LoadAssetAtPath<FactionData>(path);
            if (existing != null) return existing;

            FactionData f = ScriptableObject.CreateInstance<FactionData>();
            f.factionName = factionName;
            f.color = color;
            f.ideology = ideology;
            f.legacyFaction = legacy;
            f.ranks = ranks;
            AssetDatabase.CreateAsset(f, path);
            return f;
        }

        /// <summary>
        /// 建国神話プリセットを生成/更新（本ジェネレータの所有物＝実行ごとにフィールドを再設定）。
        /// 「授けられた方舟」スタートは全プリセット共通の既定値で付与する。
        /// </summary>
        private static FoundingMythPreset GetOrCreatePreset(string fileName, string presetName, FactionData faction,
            string foundingMyth, float designVsEmergence, float authorityAxis,
            float expansion, float aggression, float cohesion, float discipline, float openness)
        {
            string path = $"{PresetDir}/{fileName}.asset";
            FoundingMythPreset existing = AssetDatabase.LoadAssetAtPath<FoundingMythPreset>(path);
            FoundingMythPreset p = existing != null ? existing : ScriptableObject.CreateInstance<FoundingMythPreset>();

            p.presetName = presetName;
            p.foundingMyth = foundingMyth;
            p.factionData = faction;
            p.designVsEmergence = designVsEmergence;
            p.authorityAxis = authorityAxis;
            p.expansionBias = expansion;
            p.aggressionBias = aggression;
            p.cohesionBias = cohesion;
            p.disciplineBias = discipline;
            p.opennessBias = openness;

            // 授けられた方舟スタート（全プリセット共通・LORE-8 #494）。
            p.bestowedArk = true;
            p.startingFleets = 1;
            p.startingSystems = 1;

            if (existing == null) AssetDatabase.CreateAsset(p, path);
            return p;
        }

        /// <summary>(tier, 名称) の並びから階級表を作る。</summary>
        private static List<FactionData.RankEntry> Ranks(params (int tier, string name)[] entries)
        {
            var list = new List<FactionData.RankEntry>();
            if (entries != null)
            {
                foreach (var e in entries) list.Add(new FactionData.RankEntry(e.tier, e.name));
            }
            return list;
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
