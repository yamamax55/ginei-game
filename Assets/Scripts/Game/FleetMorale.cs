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

        [Tooltip("敗走から回復を始めるまでの『交戦が無い』継続時間（秒）")]
        public float routedRecoveryDelay = 5f;

        public bool IsRouted => morale <= 0;

        private FleetStrength strength;
        private FleetWeapon weapon;
        private FleetSustainment sustainment; // 継戦（ORBAT-4・任意・既定で挙動不変）
        private TextMesh moraleLabel;
        private float lastCombatTime;   // 直近に交戦していた時刻（敗走回復の待機判定用）

        private void Awake()
        {
            strength = GetComponent<FleetStrength>();
            weapon = GetComponent<FleetWeapon>();
        }

        private void Start()
        {
            // 継戦（ORBAT-4）は Squadron.Awake が付与する＝Awake 順に依存しないよう Start で取得（無ければ null＝挙動不変）。
            sustainment = GetComponent<FleetSustainment>();
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
            // 日本語フォントは FontProvider に集約（Unity6 の Arial.ttf 禁止対応も一元化）
            Font jaFont = FontProvider.JapaneseFont;
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
                // 参謀補完を反映した実効統率を使用（基準値は非破壊）
                maxMorale = Mathf.Max(1f, strength.admiralData.EffectiveLeadership);
                morale = maxMorale;
            }
        }

        private void UpdateMorale()
        {
            bool inCombat = (weapon != null && weapon.IsInCombat);
            if (inCombat) lastCombatTime = Time.time;

            if (IsRouted)
            {
                // 敗走中：交戦が routedRecoveryDelay 秒途切れたら回復を開始（士気>0で敗走解除）
                if (!inCombat && Time.time - lastCombatTime >= routedRecoveryDelay)
                {
                    ChangeMorale(recoveryRate * Time.deltaTime);
                }
                return;
            }

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
            if (maxMorale <= 0) return ApplySustainment(1.0f);

            // 士気が低いほど(閾値以下で)ペナルティが発生する簡易モデル
            // 例: 士気が最大値の30%以下から低下し始め、0で0.5倍になる
            float ratio = morale / maxMorale;
            float factor = (ratio > 0.3f) ? 1.0f : Mathf.Lerp(0.5f, 1.0f, ratio / 0.3f);
            return ApplySustainment(factor);
        }

        /// <summary>継戦ペナルティ（ORBAT-4・任意）を乗せる。コンポーネント無し/opt-in OFF/継戦OK なら 1.0 倍＝挙動不変。</summary>
        private float ApplySustainment(float factor)
            => sustainment != null ? factor * sustainment.EffectiveFactor : factor;
    }
}
