using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 会戦シナリオの定義データ（ScriptableObject）。
    /// 出撃する艦隊の一覧（提督・陣営・配置・陣形）を保持する。
    /// BattleSetup がこれを読み、艦隊プレハブを生成・配置する。
    /// </summary>
    [CreateAssetMenu(fileName = "NewScenario", menuName = "Ginei/Scenario Data")]
    public class ScenarioData : ScriptableObject
    {
        [Header("シナリオ情報")]
        [Tooltip("GameSettings.scenarioName と一致させる名前")]
        public string scenarioName = "アスターテ会戦";

        [Header("出撃艦隊")]
        [Tooltip("この会戦に登場する全艦隊のエントリ（帝国・同盟を混在させてよい）")]
        public List<FleetEntry> fleets = new List<FleetEntry>();

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
        }
    }
}
