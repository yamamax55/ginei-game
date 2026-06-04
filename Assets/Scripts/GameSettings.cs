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
        public string scenarioName = "アスターテ会戦";
        public string selectedAdmiral = "ラインハルト";
        
        [Header("システム設定")]
        [Range(0f, 1f)]
        public float masterVolume = 1f;
        public float defaultTimeScale = 1f;
        public bool alwaysShowGizmos = false;

        [Header("戦績")]
        public Faction winner;
        public int imperialSunkCount;
        public int allianceSunkCount;
        public int remainingStrength;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 戦績をリセットします。
        /// </summary>
        public void ResetStats()
        {
            imperialSunkCount = 0;
            allianceSunkCount = 0;
            remainingStrength = 0;
        }
    }
}
