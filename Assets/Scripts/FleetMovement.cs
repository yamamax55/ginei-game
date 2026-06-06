using System.Collections.Generic;
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

        [Tooltip("後退（向きを保ったまま下がる）時の速度倍率。前進より遅くする")]
        [Range(0f, 1f)]
        public float reverseSpeedRatio = 0.5f;

        [Header("混雑ペナルティ（艦隊接触時の減速）")]
        [Tooltip("艦隊同士が密集・接触したとき、移動・回頭速度を低下させる")]
        public bool enableCongestionPenalty = true;

        [Tooltip("接触判定の半径倍率。1.0=外接円が触れた時点、>1=触れる前から効き始める")]
        public float congestionRadiusScale = 1.1f;

        [Tooltip("重なり量を減速へ変換する強さ。大きいほど少しの重なりで強く減速")]
        public float congestionStrength = 1.0f;

        [Tooltip("混雑時の最小機動倍率（これ以下には遅くならない＝完全停止を防ぐ）")]
        [Range(0f, 1f)]
        public float minCongestionFactor = 0.4f;

        [Tooltip("近接スキャンの間隔（秒）。負荷軽減のため毎フレームは計算しない")]
        public float congestionUpdateInterval = 0.2f;

        [Tooltip("混雑係数の追従速度（倍率/秒）。大きいほど素早く反映、小さいほど滑らか")]
        public float congestionSmoothSpeed = 2.0f;

        [Header("得意陣形ボーナス（#104）")]
        [Tooltip("提督の得意陣形と現在陣形が一致する間の移動・回頭速度の倍率（1.0=実質無効。例:1.15で+15%）。実効値パターン＝基準 maxSpeed/rotationSpeed は非破壊")]
        public float preferredFormationMobilityBonus = 1.15f;

        [Header("デバッグ用")]
        [Tooltip("現在の速度")]
        public float currentSpeed = 0f;

        private Vector2 targetPosition;
        private bool isMoving = false;

        // 到着時の向き指定（null=指定なし＝従来通り）。到着後その場で回頭する。
        private float? arrivalFacing = null;
        private bool isOrientingAtArrival = false;

        // 後退モード（true=回頭せず現在の向きを保ったまま目標へ並進＝射界を維持して下がる）
        private bool isReverse = false;

        private FleetWeapon weapon;
        private FleetStrength strength;
        private FleetMorale moraleComponent;
        private Squadron squadron;

        // 混雑ペナルティの状態（係数は間引き計算＋滑らかに追従）
        private float congestionFactor = 1f;
        private float congestionTarget = 1f;
        private float lastCongestionUpdate = 0f;

        private void Awake()
        {
            weapon = GetComponent<FleetWeapon>();
            strength = GetComponent<FleetStrength>();
            moraleComponent = GetComponent<FleetMorale>();
            squadron = GetComponent<Squadron>();
            // 全艦が同フレームに一斉計算しないよう初回タイミングを分散
            lastCongestionUpdate = -Random.Range(0f, congestionUpdateInterval);
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

            // 後退モードは前方へは進ませない：目標方向から前方成分（現在の向き Transform.up 方向）を
            // 取り除き、後方／横方向の成分だけで移動する。真正面を指定すると移動量ゼロ＝動かない。
            if (isReverse)
            {
                Vector2 up = transform.up;
                float forwardComponent = Vector2.Dot(direction, up);
                if (forwardComponent > 0f) direction -= up * forwardComponent;
                distance = direction.magnitude;
            }

            // 実効速度の計算（提督機動・士気・交戦による補正）
            float mobilityFactor = GetMobilityFactor();
            float effectiveRotationSpeed = rotationSpeed * mobilityFactor;
            float effectiveMaxSpeed = maxSpeed * mobilityFactor;
            // 後退時は前進より遅い上限速度を使う
            float modeMaxSpeed = effectiveMaxSpeed * (isReverse ? reverseSpeedRatio : 1f);

            // 1. 到着判定
            if (isMoving && distance < arriveDistance)
            {
                isMoving = false;
                // 到着時の向き指定があれば、その場回頭フェーズへ
                if (arrivalFacing.HasValue) isOrientingAtArrival = true;
            }

            // 2. 回頭処理（目標方向が極小のときは回頭せず現在の向きを維持）
            // 後退モードでは一切回頭せず現在の向き（射界）を保つ。angleDiff=0 のまま＝加速ガードも通る。
            float currentAngle = transform.eulerAngles.z;
            float nextAngle = currentAngle;
            float angleDiff = 0f;
            if (!isReverse && distance >= arriveDistance)
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
                        targetSpeed = modeMaxSpeed;
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
                // 後退時は回頭せず、目標方向へそのまま並進（向き＝射界を維持して下がる）。
                Vector3 moveDir = transform.up;
                if (isReverse)
                    moveDir = distance > 0.0001f ? (Vector3)(direction / distance) : Vector3.zero;
                transform.position += moveDir * (currentSpeed * Time.deltaTime);
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
            // 参謀補完を反映した実効機動を使用（基準値は非破壊）
            if (strength != null && strength.admiralData != null)
            {
                factor *= 1.0f + (strength.admiralData.EffectiveMobility - 50) / 100f;
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

            // 得意陣形ボーナス：現在陣形が提督の得意陣形と一致する間だけ移動補正（実効値パターン）
            if (strength != null && strength.admiralData != null && squadron != null
                && strength.admiralData.IsPreferredFormation(squadron.currentFormation))
            {
                factor *= Mathf.Max(0.1f, preferredFormationMobilityBonus);
            }

            // 艦隊同士の接触・密集による減速（混雑ペナルティ）
            factor *= GetCongestionFactor();

            return factor;
        }

        /// <summary>
        /// 他の艦隊（部隊）との接触・密集による機動低下倍率を返す（1.0=非接触, minCongestionFactor=最大混雑）。
        /// 近接スキャンは congestionUpdateInterval ごとに間引き、係数は滑らかに追従させる。
        /// </summary>
        private float GetCongestionFactor()
        {
            if (!enableCongestionPenalty || squadron == null || strength == null) return 1f;

            // 重い近接スキャンは一定間隔でのみ実行（Time.time は timeScale 追従＝ポーズ中は進まない）
            if (Time.time - lastCongestionUpdate >= congestionUpdateInterval)
            {
                lastCongestionUpdate = Time.time;
                congestionTarget = ComputeCongestionTarget();
            }

            // 目標倍率へ滑らかに追従（急なカクつき防止・離れると徐々に 1.0 へ戻る）
            congestionFactor = Mathf.MoveTowards(congestionFactor, congestionTarget,
                congestionSmoothSpeed * Time.deltaTime);
            return congestionFactor;
        }

        /// <summary>
        /// 現在の他艦隊との外接円の重なり量から、目標とする混雑倍率を算出する。
        /// </summary>
        private float ComputeCongestionTarget()
        {
            squadron.GetBoundingCircle(out Vector3 myCenter, out float myRadius);

            float congestion = 0f;
            // 全旗艦を対象（陣営問わず接触すれば混雑）。多勢力対応の単一在庫を参照。
            AccumulateOverlap(FleetRegistry.AllFlagships, myCenter, myRadius, ref congestion);

            float t = Mathf.Clamp01(congestion * congestionStrength);
            return Mathf.Lerp(1f, minCongestionFactor, t);
        }

        /// <summary>
        /// 指定旗艦リストの各艦隊と自艦隊の外接円の重なり量（半径和で正規化）を加算する。
        /// 自分自身・退却/破棄済みは除外。陣営問わず接触すれば混雑とみなす。
        /// </summary>
        private void AccumulateOverlap(IReadOnlyList<FleetStrength> flagships,
            Vector3 myCenter, float myRadius, ref float congestion)
        {
            if (flagships == null) return;
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength fs = flagships[i];
                if (fs == null || fs == strength || !fs.IsAlive) continue;

                Squadron other = fs.GetComponent<Squadron>();
                if (other == null) continue;

                other.GetBoundingCircle(out Vector3 oc, out float orad);
                float dist = Vector2.Distance(myCenter, oc);
                float contactDist = (myRadius + orad) * congestionRadiusScale;
                float overlap = contactDist - dist;
                if (overlap <= 0f) continue;

                // 半径和で正規化し、艦隊の大小に依らず 0〜1 程度の寄与にする
                float denom = Mathf.Max(0.0001f, myRadius + orad);
                congestion += overlap / denom;
            }
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
            isReverse = false;
            arrivalFacing = facingAngleZ;
            isOrientingAtArrival = false;
        }

        /// <summary>
        /// 後退で目標地点へ移動する。回頭せず現在の向き（射界）を保ったまま、
        /// reverseSpeedRatio 倍の速度で並進する。戦いながらの離脱に使う。
        /// 前方へは進まない（目標の前方成分は除去し、後方／横成分のみで移動）。
        /// </summary>
        /// <param name="pos">目標のワールド座標</param>
        public void SetReverseDestination(Vector2 pos)
        {
            targetPosition = pos;
            isMoving = true;
            isReverse = true;
            arrivalFacing = null;
            isOrientingAtArrival = false;
        }

        /// <summary>その場で停止する（前進を止め、残速度は自然減速。向きは現状維持）。#85 標準命令で使用。</summary>
        public void Stop()
        {
            isMoving = false;
            isReverse = false;
            isOrientingAtArrival = false;
            arrivalFacing = null;
            targetPosition = transform.position;
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
            isReverse = false;            // その場回頭へ切替（後退モードは解除）
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


