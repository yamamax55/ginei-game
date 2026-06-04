using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊の士気を管理するクラス。
    /// 被弾や交戦で低下し、能力に影響を与えます。士気が0になると敗走状態になります。
    /// </summary>
    [RequireComponent(typeof(FleetStrength))]
    public class FleetMorale : MonoBehaviour
    {
        [Header("士気設定")]
        public float morale = 100f;
        public float maxMorale = 100f;

        [Tooltip("非交戦時の自然回復速度 (ポイント/秒)")]
        public float recoveryRate = 0.5f;

        [Tooltip("交戦中の自然低下速度 (ポイント/秒)")]
        public float combatDrainRate = 0.2f;

        [Tooltip("ダメージ100あたりの士気低下量")]
        public float damageDrainFactor = 0.1f;

        public bool IsRouted => morale <= 0;

        private FleetStrength strength;
        private FleetWeapon weapon;
        private TextMesh moraleLabel;

        private void Awake()
        {
            strength = GetComponent<FleetStrength>();
            weapon = GetComponent<FleetWeapon>();
        }

        private void Start()
        {
            InitializeMorale();
            CreateMoraleLabel();
        }

        private void Update()
        {
            UpdateMorale();
            UpdateMoraleLabel();
        }

        private void CreateMoraleLabel()
        {
            // Unity 6 では "Arial.ttf" は廃止され例外を投げるため "LegacyRuntime.ttf" を使う
            Font jaFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if UNITY_EDITOR
            Font customFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/msgothic.ttc");
            if (customFont != null) jaFont = customFont;
#endif
            // プレハブに焼き込まれた既存 "MoraleLabel" があれば再利用（二重生成を防ぐ）
            Transform existingLabel = transform.Find("MoraleLabel");
            GameObject go;
            if (existingLabel != null)
            {
                go = existingLabel.gameObject;
            }
            else
            {
                go = new GameObject("MoraleLabel");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(-0.6f, 0.6f, 0f);
                go.transform.localScale = Vector3.one * 0.15f;
            }

            moraleLabel = go.GetComponent<TextMesh>();
            if (moraleLabel == null) moraleLabel = go.AddComponent<TextMesh>();
            moraleLabel.font = jaFont;
            moraleLabel.anchor = TextAnchor.LowerCenter;
            moraleLabel.alignment = TextAlignment.Center;
            moraleLabel.fontSize = 60;
            moraleLabel.characterSize = 0.4f;

            var mr = go.GetComponent<MeshRenderer>();
            if (jaFont != null) mr.sharedMaterial = jaFont.material;

            moraleLabel.text = "";
        }

        private void UpdateMoraleLabel()
        {
            if (moraleLabel == null) return;

            if (IsRouted)
            {
                moraleLabel.text = "敗走";
                moraleLabel.color = new Color(1f, 0.2f, 0.2f);
            }
            else if (GetMoraleFactor() < 1f)
            {
                moraleLabel.text = "士気低下";
                moraleLabel.color = new Color(1f, 0.85f, 0.1f);
            }
            else
            {
                moraleLabel.text = "";
            }
        }

        private void InitializeMorale()
        {
            if (strength != null && strength.admiralData != null)
            {
                // 最大士気は提督の統率力に依存 (例: 統率と同じ値)。0以下にはしない（ゼロ除算防止）
                maxMorale = Mathf.Max(1f, strength.admiralData.leadership);
                morale = maxMorale;
            }
        }

        private void UpdateMorale()
        {
            if (IsRouted) return;

            bool inCombat = (weapon != null && weapon.IsInCombat);

            if (inCombat)
            {
                // 交戦中による低下
                ChangeMorale(-combatDrainRate * Time.deltaTime);
            }
            else
            {
                // 非交戦中による回復
                ChangeMorale(recoveryRate * Time.deltaTime);
            }
        }

        /// <summary>
        /// ダメージを受けた際の士気低下処理。
        /// </summary>
        public void OnTakeDamage(int damageAmount)
        {
            float drain = damageAmount * damageDrainFactor;
            ChangeMorale(-drain);
        }

        private void ChangeMorale(float amount)
        {
            morale = Mathf.Clamp(morale + amount, 0, maxMorale);
        }

        /// <summary>
        /// 現在の士気による能力補正倍率を取得します。
        /// </summary>
        /// <returns>1.0 (正常) 〜 0.5 (低士気) 等</returns>
        public float GetMoraleFactor()
        {
            // 最大士気が未設定(0以下)なら補正なし（ゼロ除算によるNaN防止）
            if (maxMorale <= 0) return 1.0f;

            // 士気が低いほど(閾値以下で)ペナルティが発生する簡易モデル
            // 例: 士気が最大値の30%以下から低下し始め、0で0.5倍になる
            float ratio = morale / maxMorale;
            if (ratio > 0.3f) return 1.0f;
            
            return Mathf.Lerp(0.5f, 1.0f, ratio / 0.3f);
        }
    }
}
