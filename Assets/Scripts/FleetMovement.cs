using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊の移動を制御するクラス。
    /// 指定された目標地点へ回頭してから、加減速（慣性）を伴って前進します。
    /// 入力は FleetCommander から SetDestination を通じて受け取ります。
    /// </summary>
    public class FleetMovement : MonoBehaviour
    {
        [Header("移動設定")]
        [Tooltip("最大前進速度 (単位/秒)")]
        public float maxSpeed = 5f;

        [Tooltip("加速力 (単位/秒^2)")]
        public float acceleration = 2f;

        [Tooltip("減速力 (単位/秒^2)")]
        public float deceleration = 4f;

        [Tooltip("回頭速度 (度/秒)")]
        public float rotationSpeed = 60f;

        [Tooltip("前進を開始する角度のしきい値 (度)")]
        public float faceThreshold = 10f;

        [Tooltip("目標地点に到達したとみなす距離")]
        public float arriveDistance = 0.5f;

        [Header("デバッグ用")]
        [Tooltip("現在の速度")]
        public float currentSpeed = 0f;

        private Vector2 targetPosition;
        private bool isMoving = false;
        private FleetWeapon weapon;
        private FleetStrength strength;
        private FleetMorale moraleComponent;

        private void Awake()
        {
            weapon = GetComponent<FleetWeapon>();
            strength = GetComponent<FleetStrength>();
            moraleComponent = GetComponent<FleetMorale>();
        }

        private void Update()
        {
            if (isMoving || currentSpeed > 0)
            {
                MoveProcess();
            }
        }

        /// <summary>
        /// 回頭および加減速を伴う移動処理
        /// </summary>
        private void MoveProcess()
        {
            Vector2 currentPos = transform.position;
            Vector2 direction = targetPosition - currentPos;
            float distance = direction.magnitude;

            // 実効速度の計算（交戦中なら低下させる）
            float effectiveRotationSpeed = rotationSpeed;
            float effectiveMaxSpeed = maxSpeed;

            // 提督の機動能力による補正
            if (strength != null && strength.admiralData != null)
            {
                // 機動50で1.0倍, 100で1.5倍, 0で0.5倍
                float mobilityBonus = 1.0f + (strength.admiralData.mobility - 50) / 100f;
                effectiveRotationSpeed *= mobilityBonus;
                effectiveMaxSpeed *= mobilityBonus;
            }

            // 士気による補正
            if (moraleComponent != null)
            {
                float moraleFactor = moraleComponent.GetMoraleFactor();
                effectiveRotationSpeed *= moraleFactor;
                effectiveMaxSpeed *= moraleFactor;
            }

            if (weapon != null && weapon.IsInCombat)
{
                effectiveRotationSpeed *= weapon.combatMobilityRatio;
                effectiveMaxSpeed *= weapon.combatMobilityRatio;
            }

            // 1. 到着判定
            if (isMoving && distance < arriveDistance)
            {
                isMoving = false;
            }

            // 2. 回頭処理
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float currentAngle = transform.eulerAngles.z;
            float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, effectiveRotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, 0, nextAngle);

            // 3. 速度制御 (慣性)
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(nextAngle, targetAngle));
            float targetSpeed = 0f;

            if (isMoving)
            {
                // 正面を向いている場合のみ加速を許可
                if (angleDiff <= faceThreshold)
                {
                    // 簡易制動距離計算
                    float brakingDistance = (currentSpeed * currentSpeed) / (2f * deceleration);
                    
                    if (distance > brakingDistance + arriveDistance)
                    {
                        targetSpeed = effectiveMaxSpeed;
                    }
                    else
                    {
                        targetSpeed = 0f; // 減速モード
                    }
                }
                else
                {
                    // 回頭中（角度がズレている間）は停止または低速
                    targetSpeed = 0f; 
                }
            }
            else
            {
                targetSpeed = 0f;
            }

            // 加減速
            if (currentSpeed < targetSpeed)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.deltaTime);
            }

            // 4. 移動実行
            if (currentSpeed > 0)
            {
                transform.position += transform.up * (currentSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// 外部から目標地点を設定
        /// </summary>
        /// <param name="pos">目標のワールド座標</param>
        public void SetDestination(Vector2 pos)
        {
            targetPosition = pos;
            isMoving = true;
        }
    }
}


