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
        /// scenarioName 一致で ScenarioData を解決する（BattleSetup と同じ規則）。ActiveScenario 未設定時のフォールバック。
        /// 索引は <see cref="ContentDatabase"/> に集約（FND-1 #496）＝散在する Resources 走査を一本化。見つからなければ Resources 直読みで保険。
        /// </summary>
        public static ScenarioData Resolve(string name)
        {
            ScenarioData s = ContentDatabase.ScenarioByName(name);
            if (s != null) return s;
            return Resources.Load<ScenarioData>(name); // 名前=アセット名の保険（DB 未索引の直置き対応）
        }

        /// <summary>
        /// 1艦隊分の出撃定義。
        /// </summary>
        [System.Serializable]
        public class FleetEntry
        {
            [Tooltip("提督データ（能力値・兵力・名前の元）")]
            public AdmiralData admiral;

            [Tooltip("所属陣営（FactionData 未指定時に使う旧 enum。FactionData があればそちらを優先）")]
            public Faction faction;

            [Tooltip("所属勢力データ（多勢力対応。割り当てると enum faction より優先され、敵対判定・色がこれに従う）")]
            public FactionData factionData;

            [Tooltip("生成位置（XY平面）")]
            public Vector2 spawnPosition;

            [Tooltip("初期陣形")]
            public Formation formation = Formation.横陣;

            [Tooltip("この艦隊の基準兵力＝艦艇数（RANKCMD-1 #1711。兵力は艦隊が持つ）。0＝未指定＝提督側 baseStrength へフォールバック（非推奨・後方互換）")]
            public int baseStrength = 0;

            [Tooltip("艦隊番号（#146。0＝未指定＝従来どおり提督名のみ表示）")]
            public int fleetNumber = 0;

            [Tooltip("艦隊の固有名（任意。無ければ「第N艦隊」）")]
            public string fleetName = "";

            [Tooltip("所属軍団名（#147。空＝梯団なし）")]
            public string corps = "";

            [Tooltip("所属軍集団名（#147。空＝軍団直属/なし。corps の上位）")]
            public string armyGroup = "";

            [Tooltip("自軍への忠誠 0..1（#817 関ヶ原型。1＝従来動作＝必ず戦う。低いと趨勢次第で静観・寝返り）")]
            [Range(0f, 1f)]
            public float loyalty = 1f;

            [Tooltip("敵の調略の浸透 0..1（#817。高いほど劣勢時に寝返りやすい）")]
            [Range(0f, 1f)]
            public float intrigue = 0f;

            [Tooltip("増援の到着遅延（秒・game-time。0＝開戦時から在場＝従来動作。>0で戦場端から時間差投入 #2182）")]
            public float reinforcementDelay = 0f;
        }
    }
}
