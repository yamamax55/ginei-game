using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 会戦シナリオの定義データ（ScriptableObject）。
    /// 出撃する艦隊の一覧（提督・陣営・配置・陣形）を保持する。
    /// BattleSetup がこれを読み、艦隊プレハブを生成・配置する。
    /// </summary>
    /// <summary>
    /// 会戦の勝利条件。BattleManager が毎チェックで評価する。
    /// どの条件でも「片陣営の旗艦全滅（殲滅）」は常に終了条件として働く。
    /// </summary>
    public enum VictoryCondition
    {
        殲滅,      // 敵旗艦を全滅させた側が勝利（従来動作）
        時間防衛,  // 防衛側(objectiveFaction)が timeLimit 秒生き残れば勝利
        旗艦撃破,  // 敵VIP旗艦(targetAdmiral)を撃破で勝利。時間切れまで生存なら守備側勝利
        護衛       // 護衛対象(targetAdmiral)を timeLimit 秒守り切れば勝利。喪失で敗北
    }

    [CreateAssetMenu(fileName = "NewScenario", menuName = "Ginei/Scenario Data")]
    public class ScenarioData : ScriptableObject
    {
        [Header("シナリオ情報")]
        [Tooltip("GameSettings.scenarioName と一致させる名前")]
        public string scenarioName = "アスターテ会戦";

        [Header("勝利条件")]
        [Tooltip("この会戦の勝利条件。殲滅=従来。どの条件でも片陣営の旗艦全滅は常に決着する")]
        public VictoryCondition victoryCondition = VictoryCondition.殲滅;

        [Tooltip("時間防衛で『防衛する陣営』（時間防衛のみ使用）")]
        public Faction objectiveFaction = Faction.同盟;

        [Tooltip("制限時間（秒）。時間防衛/旗艦撃破/護衛で使用。0以下で無制限")]
        public float timeLimit = 180f;

        [Tooltip("旗艦撃破/護衛で対象となるVIP提督（このAdmiralDataを持つ旗艦が撃破/喪失されると決着）")]
        public AdmiralData targetAdmiral;

        [Header("出撃艦隊")]
        [Tooltip("この会戦に登場する全艦隊のエントリ（帝国・同盟を混在させてよい）")]
        public List<FleetEntry> fleets = new List<FleetEntry>();

        /// <summary>現在の会戦で使用中のシナリオ（BattleSetup が解決時に設定）。BattleManager 等が参照する。</summary>
        public static ScenarioData ActiveScenario { get; set; }

        /// <summary>
        /// scenarioName 一致で Resources から ScenarioData を解決する（BattleSetup と同じ規則）。
        /// ActiveScenario が未設定のときのフォールバック用。
        /// </summary>
        public static ScenarioData Resolve(string name)
        {
            ScenarioData[] all = Resources.LoadAll<ScenarioData>("");
            foreach (var s in all)
            {
                if (s != null && s.scenarioName == name) return s;
            }
            return Resources.Load<ScenarioData>(name);
        }

        /// <summary>
        /// 1艦隊分の出撃定義。
        /// </summary>
        [System.Serializable]
        public class FleetEntry
        {
            [Tooltip("提督データ（能力値・兵力・名前の元）")]
            public AdmiralData admiral;

            [Tooltip("所属陣営")]
            public Faction faction;

            [Tooltip("生成位置（XY平面）")]
            public Vector2 spawnPosition;

            [Tooltip("初期陣形")]
            public Formation formation = Formation.横陣;
        }
    }
}
