using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// シーン間で共有されるゲーム設定と戦績を保持するクラス。
    /// DontDestroyOnLoad によって永続化されます。
    /// </summary>
    public class GameSettings : MonoBehaviour
    {
        private static GameSettings instance;
        public static GameSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Object.FindFirstObjectByType<GameSettings>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("GameSettings");
                        instance = go.AddComponent<GameSettings>();
                        if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return instance;
            }
        }

        [Header("設定")]
        public Faction playerFaction = Faction.同盟;
        [Tooltip("プレイヤーが操作する勢力データ（多勢力対応。設定すると enum playerFaction より優先して操作勢力を判定）")]
        public FactionData playerFactionData;
        public string scenarioName = "アスターテ会戦";
        public string selectedAdmiral = "ラインハルト";
        
        [Header("システム設定")]
        [Range(0f, 1f)]
        public float masterVolume = 1f;
        public float defaultTimeScale = 1f;
        public bool alwaysShowGizmos = false;
        [Tooltip("会戦開始時のカメラズーム（大きいほど引いた画。CameraController が Start で参照）")]
        public float cameraStartZoom = 16f;
        [Tooltip("画面端スクロール（#87・CameraController が参照）。マウスが画面端でパン")]
        public bool edgeScrollEnabled = true;

        // システム設定の永続化キー（PlayerPrefs）
        private const string PrefVolume = "Ginei_MasterVolume";
        private const string PrefTimeScale = "Ginei_DefaultTimeScale";
        private const string PrefGizmos = "Ginei_AlwaysShowGizmos";
        private const string PrefZoom = "Ginei_CameraStartZoom";
        private const string PrefEdgeScroll = "Ginei_EdgeScroll";

        [Header("戦績")]
        public Faction winner;
        [Tooltip("勝者勢力名（多勢力対応。3勢力以上では enum winner では表せないためこちらを表示に使う）")]
        public string winnerName;
        public int imperialSunkCount;
        public int allianceSunkCount;
        public int remainingStrength;
        [Tooltip("帝国軍の残存兵力合計")]
        public int imperialRemainingStrength;
        [Tooltip("同盟軍の残存兵力合計")]
        public int allianceRemainingStrength;
        [Tooltip("殊勲提督（勝者側で与ダメージ最大）")]
        public string mvpAdmiral;
        [Tooltip("勝因")]
        public string victoryReason;

        [Tooltip("勢力ごとの戦績（多勢力対応・勢力名キー。3勢力以上の Result 表示に使用）")]
        public List<FactionStat> factionStats = new List<FactionStat>();

        /// <summary>1勢力分の戦績（勢力名キー）。BattleManager が記録し ResultManager が表示する。</summary>
        [System.Serializable]
        public class FactionStat
        {
            public string factionName;   // 勢力名（FactionData.factionName、無ければ enum 名）
            public int initialCount;     // 開始時の旗艦数
            public int remainingCount;   // 残存旗艦数
            public int sunkCount;        // 喪失数（initialCount - remainingCount）
            public int remainingStrength;// 残存兵力合計
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                    LoadPrefs();   // 保存済みのシステム設定を復元
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>システム設定を PlayerPrefs から復元する（無ければ既定値のまま）。</summary>
        public void LoadPrefs()
        {
            masterVolume = PlayerPrefs.GetFloat(PrefVolume, masterVolume);
            defaultTimeScale = PlayerPrefs.GetFloat(PrefTimeScale, defaultTimeScale);
            alwaysShowGizmos = PlayerPrefs.GetInt(PrefGizmos, alwaysShowGizmos ? 1 : 0) == 1;
            cameraStartZoom = PlayerPrefs.GetFloat(PrefZoom, cameraStartZoom);
            edgeScrollEnabled = PlayerPrefs.GetInt(PrefEdgeScroll, edgeScrollEnabled ? 1 : 0) == 1;
            AudioListener.volume = masterVolume;
        }

        /// <summary>システム設定を PlayerPrefs に保存する（設定画面の変更を永続化）。</summary>
        public void SavePrefs()
        {
            PlayerPrefs.SetFloat(PrefVolume, masterVolume);
            PlayerPrefs.SetFloat(PrefTimeScale, defaultTimeScale);
            PlayerPrefs.SetInt(PrefGizmos, alwaysShowGizmos ? 1 : 0);
            PlayerPrefs.SetFloat(PrefZoom, cameraStartZoom);
            PlayerPrefs.SetInt(PrefEdgeScroll, edgeScrollEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 戦績をリセットします。
        /// </summary>
        public void ResetStats()
        {
            imperialSunkCount = 0;
            allianceSunkCount = 0;
            remainingStrength = 0;
            imperialRemainingStrength = 0;
            allianceRemainingStrength = 0;
            mvpAdmiral = "";
            victoryReason = "";
            winnerName = "";
            factionStats.Clear();
        }
    }
}
