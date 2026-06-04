using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊の兵力（耐久力）を管理するクラス。
    /// 0以下になると艦隊が消滅します。
    /// </summary>
    public class FleetStrength : MonoBehaviour
    {
        [Header("兵力設定")]
        [Tooltip("提督データ (割り当てると各能力値が反映されます)")]
        public AdmiralData admiralData;

        [Tooltip("提督名")]
        public string admiralName = "ラインハルト";

        [Tooltip("現在の兵力")]
        public int strength = 10000;

        [Tooltip("最大兵力")]
        public int maxStrength = 10000;

        [Header("陣営設定")]
        public Faction faction;

        private TextMesh strengthDisplay;
        private FleetMorale moraleComponent;

        private void Awake()
        {
            moraleComponent = GetComponent<FleetMorale>();
        }

        private void Start()
        {
            ApplyAdmiralData();
            
            // 艦隊の右下に情報を表示するためのテキストを作成
GameObject textObj = new GameObject("StrengthDisplay");
            textObj.transform.SetParent(transform);
            // 位置を右下付近に変更し、より近くに配置
            textObj.transform.localPosition = new Vector3(0.6f, -0.6f, 0); 
            textObj.transform.localScale = Vector3.one * 0.2f; // サイズ調整

            strengthDisplay = textObj.AddComponent<TextMesh>();
            
            // 日本語フォントの読み込み (文字化け対策)
            Font jaFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); // Fallback
#if UNITY_EDITOR
            // エディタ上では作成したフォントを優先
            Font customJaFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/msgothic.ttc");
            if (customJaFont != null) jaFont = customJaFont;
#endif
            strengthDisplay.font = jaFont;
            if (jaFont != null)
            {
                textObj.GetComponent<MeshRenderer>().sharedMaterial = jaFont.material;
            }

            // 右下へ向かって表示されるようアンカーを調整
            strengthDisplay.anchor = TextAnchor.UpperLeft;
            strengthDisplay.alignment = TextAlignment.Left;
            strengthDisplay.characterSize = 0.5f;
            strengthDisplay.fontSize = 50;
            // 色の設定は FactionColor コンポーネントに一任するため削除

            UpdateDisplay();
        }



        /// <summary>
        /// 提督データから艦隊ステータスを初期化します。
        /// </summary>
        public void ApplyAdmiralData()
        {
            if (admiralData == null) return;

            admiralName = admiralData.admiralName;
            faction = admiralData.faction;

            // 統率によって兵力上限を決定 (baseStrength を基準に補正)
            // 例：統率100で baseStrength * 1.5, 統率0で baseStrength * 0.5
            float leadershipBonus = (admiralData.leadership - 50) / 100f; // -0.5 ~ +0.5
            maxStrength = Mathf.RoundToInt(admiralData.baseStrength * (1.0f + leadershipBonus));
            strength = maxStrength;

            // 陣営色コンポーネントがあれば色を更新
            FactionColor factionColor = GetComponent<FactionColor>();
            if (factionColor != null)
            {
                factionColor.ApplyColors();
            }
        }

        /// <summary>
        /// ダメージを受けます。
        /// </summary>
        /// <param name="rawDamage">元のダメージ量</param>
        public void TakeDamage(int rawDamage)
        {
            // 防御力によるダメージ軽減
            float defenseValue = admiralData != null ? admiralData.defense : 0f;
            // 防御100でダメージ50%カット
            float reduction = 1.0f - Mathf.Clamp(defenseValue / 200f, 0, 0.9f);
            int finalDamage = Mathf.RoundToInt(rawDamage * reduction);

            strength -= finalDamage;
            
            if (moraleComponent != null)
            {
                moraleComponent.OnTakeDamage(finalDamage);
            }

            UpdateDisplay();

            if (strength <= 0)
            {
                Die();
            }
        }

        private void UpdateDisplay()
        {
            if (strengthDisplay != null)
            {
                strengthDisplay.text = $"{admiralName}\n兵力: {Mathf.Max(0, strength)}";
            }
        }

        private void Die()
        {
            Debug.Log($"{admiralName} 提督の艦隊 ({faction}) は壊滅した。");
            Destroy(gameObject);
        }
    }
}

