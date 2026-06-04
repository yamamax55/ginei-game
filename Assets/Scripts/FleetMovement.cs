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

        [Tooltip("敗走時の移動速度・回頭速度の倍率")]
        public float routedMobilityRatio = 0.5f;

        [Header("デバッグ用")]
        [Tooltip("現在の速度")]
        public float currentSpeed = 0f;

        private Vector2 targetPosition;
        private bool isMoving = false;

        // 到着時の向き指定（null=指定なし＝従来通り）。到着後その場で回頭する。
        private float? arrivalFacing = null;
        private bool isOrientingAtArrival = false;

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
            if (isMoving || currentSpeed > 0 || isOrientingAtArrival)
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

            // 実効速度の計算（提督機動・士気・交戦による補正）
            float mobilityFactor = GetMobilityFactor();
            float effectiveRotationSpeed = rotationSpeed * mobilityFactor;
            float effectiveMaxSpeed = maxSpeed * mobilityFactor;

            // 1. 到着判定
            if (isMoving && distance < arriveDistance)
            {
                isMoving = false;
                // 到着時の向き指定があれば、その場回頭フェーズへ
                if (arrivalFacing.HasValue) isOrientingAtArrival = true;
            }

            // 2. 回頭処理（目標方向が極小のときは回頭せず現在の向きを維持）
            float currentAngle = transform.eulerAngles.z;
            float nextAngle = currentAngle;
            float angleDiff = 0f;
            if (distance >= arriveDistance)
            {
                float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, effectiveRotationSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0, 0, nextAngle);
                angleDiff = Mathf.Abs(Mathf.DeltaAngle(nextAngle, targetAngle));
            }

            // 3. 速度制御 (慣性)
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

            // 5. 到着後の向き合わせ（指定があれば、実効回頭速度でその場回頭してから停止）
            if (isOrientingAtArrival && !isMoving)
            {
                float orientRotSpeed = rotationSpeed * mobilityFactor;
                float cur = transform.eulerAngles.z;
                float next = Mathf.MoveTowardsAngle(cur, arrivalFacing.Value, orientRotSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0, 0, next);

                if (Mathf.Abs(Mathf.DeltaAngle(next, arrivalFacing.Value)) < 0.5f)
                {
                    isOrientingAtArrival = false;
                    arrivalFacing = null;
                }
            }
        }

        /// <summary>
        /// 提督機動・士気・交戦状態による機動性の補正倍率を返す。
        /// </summary>
        private float GetMobilityFactor()
        {
            float factor = 1.0f;

            // 提督の機動能力による補正（機動50で1.0倍, 100で1.5倍, 0で0.5倍）
            if (strength != null && strength.admiralData != null)
            {
                factor *= 1.0f + (strength.admiralData.mobility - 50) / 100f;
            }

            // 士気による補正（敗走時は専用倍率を適用し、段階的な士気補正と二重に掛けない）
            if (moraleComponent != null)
            {
                factor *= moraleComponent.IsRouted
                    ? routedMobilityRatio
                    : moraleComponent.GetMoraleFactor();
            }

            // 交戦中は機動性を低下
            if (weapon != null && weapon.IsInCombat)
            {
                factor *= weapon.combatMobilityRatio;
            }

            return factor;
        }

        /// <summary>
        /// 外部から目標地点を設定。到着時の向き(z角)を指定すると、到着後その場で回頭してから停止する。
        /// </summary>
        /// <param name="pos">目標のワールド座標</param>
        /// <param name="facingAngleZ">到着時に向ける z 角度（null=指定なし＝従来通り）。基準は正面=Transform.up、z角=Atan2(dir.y,dir.x)*Rad2Deg-90。</param>
        public void SetDestination(Vector2 pos, float? facingAngleZ = null)
        {
            targetPosition = pos;
            isMoving = true;
            arrivalFacing = facingAngleZ;
            isOrientingAtArrival = false;
        }

        /// <summary>
        /// 前進せず、その場で指定座標の方向へ回頭する（交戦中に敵を向いて射界を維持するため）。
        /// </summary>
        /// <param name="pos">向きたいワールド座標</param>
        public void FaceTarget(Vector2 pos)
        {
            // 前進は停止（残速度は MoveProcess 側で自然減速）。
            // targetPosition を自位置にしておくことで、MoveProcess 側の回頭は
            // 「目標方向が極小ならスキップ」のガードに掛かり、回頭の競合を防ぐ。
            isMoving = false;
            isOrientingAtArrival = false; // 交戦時の射界維持を優先（到着回頭は中断）
            arrivalFacing = null;
            targetPosition = transform.position;

            Vector2 direction = pos - (Vector2)transform.position;
            // 目標が近すぎる場合は回頭しない（向きが暴れるのを防ぐ）
            if (direction.magnitude < arriveDistance) return;

            float effectiveRotationSpeed = rotationSpeed * GetMobilityFactor();
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float nextAngle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetAngle, effectiveRotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, 0, nextAngle);
        }
    }
}


