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

        private void Awake()
        {
            strength = GetComponent<FleetStrength>();
            weapon = GetComponent<FleetWeapon>();
        }

        private void Start()
        {
            InitializeMorale();
        }

        private void Update()
        {
            UpdateMorale();
        }

        private void InitializeMorale()
        {
            if (strength != null && strength.admiralData != null)
            {
                // 最大士気は提督の統率力に依存 (例: 統率と同じ値)
                maxMorale = strength.admiralData.leadership;
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
            // 士気が低いほど(閾値以下で)ペナルティが発生する簡易モデル
            // 例: 士気が最大値の30%以下から低下し始め、0で0.5倍になる
            float ratio = morale / maxMorale;
            if (ratio > 0.3f) return 1.0f;
            
            return Mathf.Lerp(0.5f, 1.0f, ratio / 0.3f);
        }
    }
}
