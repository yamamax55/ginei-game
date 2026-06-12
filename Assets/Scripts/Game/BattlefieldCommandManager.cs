using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 会戦中の臨時指揮（#147 拡張・史実準拠）の配線（Battle シーンに自動生成・手置き不要）。
    /// 軍団（`FleetStrength.corpsName`）ごとに生存旗艦から「最上位階級→先任」の指揮官を解決し（`BattlefieldCommandRules`）、
    /// <b>軍団長が戦死/離脱したら下位（階級不足でも）が臨時で継承</b>して通知する。
    /// 戦闘終了（Battle シーン離脱＝本オブジェクト破棄）で `ActingCommandLedger.Clear`＝臨時指揮を解いて正規人事へ戻す。
    /// ※艦隊内（副提督→提督）の継承は `CommandStaffRules`#885 が担う。本クラスは軍団以上の臨時指揮を扱う。
    /// </summary>
    public class BattlefieldCommandManager : MonoBehaviour
    {
        [Tooltip("臨時指揮を解決する間隔（秒）")]
        public float resolveInterval = 1.0f;

        private float nextResolveTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Battle") return;
            if (FindAnyObjectByType<BattlefieldCommandManager>() != null) return;
            new GameObject("BattlefieldCommandManager").AddComponent<BattlefieldCommandManager>();
        }

        private void Awake() => ActingCommandLedger.Clear(); // 新しい会戦＝臨時指揮をリセット

        private void OnDestroy() => ActingCommandLedger.Clear(); // 戦闘終了（シーン離脱）＝正規人事へ戻す

        private void Update()
        {
            if (Time.time < nextResolveTime) return;
            nextResolveTime = Time.time + Mathf.Max(0.1f, resolveInterval);
            ResolveActingCommands();
        }

        /// <summary>軍団ごとに生存旗艦から臨時指揮官を解決し、交代があれば通知する。</summary>
        private void ResolveActingCommands()
        {
            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            if (flagships == null) return;

            // 軍団（corpsName）ごとに生存旗艦を集める。
            var groups = new Dictionary<string, List<FleetStrength>>();
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength f = flagships[i];
                if (f == null || !f.IsAlive || string.IsNullOrEmpty(f.corpsName)) continue;
                string key = f.faction + "/" + f.corpsName;
                if (!groups.TryGetValue(key, out var list)) { list = new List<FleetStrength>(); groups[key] = list; }
                list.Add(f);
            }
            if (groups.Count == 0) return;

            int requiredTier = CommandCapacityRules.CommanderTierFor(EchelonType.軍団);
            foreach (var kv in groups)
            {
                var list = kv.Value;
                var candidates = new List<CommandCandidate>(list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    int tier = list[i].admiralData != null ? list[i].admiralData.rankTier : 0;
                    candidates.Add(new CommandCandidate(list[i].GetInstanceID(), tier, 0));
                }

                var acting = BattlefieldCommandRules.SelectActingSuccessor(candidates);
                if (acting.id < 0) continue;

                int prev = ActingCommandLedger.ActingFor(kv.Key);
                ActingCommandLedger.Record(kv.Key, acting.id, acting.id);

                if (prev != -1 && prev != acting.id)
                {
                    // 上官（軍団長）を失い、先任の下位が臨時で指揮を継承。
                    FleetStrength newCmd = FindById(list, acting.id);
                    string name = (newCmd != null && newCmd.admiralData != null) ? newCmd.admiralData.admiralName : "次席";
                    bool underRank = acting.rankTier < requiredTier;
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                        $"{kv.Key} の指揮を {name} が継承{(underRank ? "（階級不足ながら臨時）" : "（臨時）")}");
                }
            }
        }

        private static FleetStrength FindById(List<FleetStrength> list, int instanceId)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].GetInstanceID() == instanceId) return list[i];
            return null;
        }
    }
}
