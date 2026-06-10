using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 全 ScriptableObject の索引＝データアクセスの唯一の窓口（FND-1 #496・#495）。起動時に `Resources` を一度だけ走査して
    /// 勢力(<see cref="FactionData"/>)・会戦(<see cref="ScenarioData"/>)・提督(<see cref="AdmiralData"/>) を名前で引けるようにし、
    /// 散在する `Resources.Load`/`LoadAll`（`BattleSetup`/`TitleManager`/`CampaignSaveManager`/`ScenarioData.Resolve`）を集約する。
    /// テスト用に手動登録（`Register*`）＋`Clear` を提供＝索引ロジックは EditMode で担保できる（Resources 走査を回避）。
    /// 新規コードは `Resources.Load`/`FindObjectsByType` を増やさずここを使う（CLAUDE.md 既存例外は維持）。
    /// </summary>
    public static class ContentDatabase
    {
        private static readonly Dictionary<string, FactionData> factions = new Dictionary<string, FactionData>();
        private static readonly Dictionary<string, ScenarioData> scenarios = new Dictionary<string, ScenarioData>();
        private static readonly Dictionary<string, AdmiralData> admirals = new Dictionary<string, AdmiralData>();
        private static bool built;

        /// <summary>索引が構築済みか（手動登録でも true）。</summary>
        public static bool IsBuilt => built;

        /// <summary>索引を破棄する（テスト初期化・再読込）。</summary>
        public static void Clear()
        {
            factions.Clear();
            scenarios.Clear();
            admirals.Clear();
            built = false;
        }

        /// <summary>必要なら `Resources` を走査して索引を構築する（一度だけ）。手動登録済みなら走査しない。</summary>
        public static void EnsureBuilt()
        {
            if (built) return;
            BuildFromResources();
            built = true;
        }

        /// <summary>明示的に `Resources` から索引を構築し直す。</summary>
        public static void BuildFromResources()
        {
            factions.Clear(); scenarios.Clear(); admirals.Clear();
            IndexFactions(Resources.LoadAll<FactionData>("Factions"));
            IndexScenarios(Resources.LoadAll<ScenarioData>(""));
            // 提督アセットは Resources 外（Assets/Data/Admirals）に置く方針＝ScenarioData 等から直接参照される。
            // Resources 配下に置かれた分があれば拾う（任意）。
            IndexAdmirals(Resources.LoadAll<AdmiralData>(""));
            built = true;
        }

        // ===== 引き（名前→SO） =====

        public static FactionData FactionByName(string name)
        {
            EnsureBuilt();
            return (name != null && factions.TryGetValue(name, out var f)) ? f : null;
        }

        public static ScenarioData ScenarioByName(string name)
        {
            EnsureBuilt();
            return (name != null && scenarios.TryGetValue(name, out var s)) ? s : null;
        }

        public static AdmiralData AdmiralByName(string name)
        {
            EnsureBuilt();
            return (name != null && admirals.TryGetValue(name, out var a)) ? a : null;
        }

        public static IReadOnlyList<FactionData> AllFactions()
        {
            EnsureBuilt();
            return new List<FactionData>(factions.Values);
        }

        public static IReadOnlyList<ScenarioData> AllScenarios()
        {
            EnsureBuilt();
            return new List<ScenarioData>(scenarios.Values);
        }

        // ===== 手動登録（テスト・動的追加） =====

        public static void RegisterFaction(FactionData f)
        {
            if (f != null && !string.IsNullOrEmpty(f.factionName)) { factions[f.factionName] = f; built = true; }
        }

        public static void RegisterScenario(ScenarioData s)
        {
            if (s != null && !string.IsNullOrEmpty(s.scenarioName)) { scenarios[s.scenarioName] = s; built = true; }
        }

        public static void RegisterAdmiral(AdmiralData a)
        {
            if (a != null && !string.IsNullOrEmpty(a.admiralName)) { admirals[a.admiralName] = a; built = true; }
        }

        // ===== 内部索引化 =====

        private static void IndexFactions(FactionData[] arr)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++) RegisterFaction(arr[i]);
        }

        private static void IndexScenarios(ScenarioData[] arr)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++) RegisterScenario(arr[i]);
        }

        private static void IndexAdmirals(AdmiralData[] arr)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++) RegisterAdmiral(arr[i]);
        }
    }
}
