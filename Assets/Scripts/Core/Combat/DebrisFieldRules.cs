using UnityEngine;

namespace Ginei
{
    /// <summary>残骸宙域（会戦後の漂流残骸が作る戦場環境）の調整係数。</summary>
    public readonly struct DebrisFieldParams
    {
        /// <summary>1隻撃沈あたりの残骸蓄積量（激戦度1のとき）。</summary>
        public readonly float accumulationPerShip;
        /// <summary>センサー擾乱の上限（残骸が極大のとき紛れる度合いの天井）。</summary>
        public readonly float maxClutter;
        /// <summary>衝突リスクの係数（残骸×速度に掛ける）。</summary>
        public readonly float collisionScale;
        /// <summary>残骸量→遮蔽の係数（残骸を盾にする利用度）。</summary>
        public readonly float coverScale;
        /// <summary>隠密接近の係数（擾乱×遮蔽に掛ける）。</summary>
        public readonly float approachScale;
        /// <summary>生存者救助の難化係数（残骸×経過時間がこれ倍で効く）。</summary>
        public readonly float rescueDecay;
        /// <summary>サルベージ資源の係数（残骸×回収能力に掛ける）。</summary>
        public readonly float salvageScale;
        /// <summary>ゼロ割回避の微小値。</summary>
        public readonly float epsilon;

        public DebrisFieldParams(float accumulationPerShip, float maxClutter, float collisionScale,
            float coverScale, float approachScale, float rescueDecay, float salvageScale, float epsilon)
        {
            this.accumulationPerShip = Mathf.Max(0f, accumulationPerShip);
            this.maxClutter = Mathf.Clamp01(maxClutter);
            this.collisionScale = Mathf.Max(0f, collisionScale);
            this.coverScale = Mathf.Max(0f, coverScale);
            this.approachScale = Mathf.Max(0f, approachScale);
            this.rescueDecay = Mathf.Max(0f, rescueDecay);
            this.salvageScale = Mathf.Max(0f, salvageScale);
            this.epsilon = Mathf.Max(1e-6f, epsilon);
        }

        /// <summary>既定＝蓄積0.01/隻・擾乱上限1.0・衝突1.0・遮蔽0.8・接近1.0・救助難化1.0・回収1.0・ε0.001。</summary>
        public static DebrisFieldParams Default
            => new DebrisFieldParams(0.01f, 1f, 1f, 0.8f, 1f, 1f, 1f, 0.001f);
    }

    /// <summary>
    /// 残骸宙域の純ロジック。会戦後に生じる艦船残骸・漂流物が作る戦場環境を扱う：残骸の蓄積、
    /// 残骸によるセンサー擾乱（敵が紛れる）、衝突リスク、残骸を盾にした遮蔽、生存者の救助、
    /// 残骸からの資源回収（サルベージ）。激戦ほど残骸が積もり、それが索敵を曇らせ航行を阻むが、
    /// 同時に隠密接近の隠れ蓑と回収資源にもなる＝戦場の負の遺産が次の戦術環境になる。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DebrisFieldRules
    {
        /// <summary>残骸の蓄積量（0..1）＝撃沈数×激戦度×蓄積係数。激戦ほど残骸が積もる。</summary>
        public static float DebrisAccumulation(int shipsDestroyed, float battleIntensity, DebrisFieldParams p)
        {
            int ships = Mathf.Max(0, shipsDestroyed);
            float intensity = Mathf.Clamp01(battleIntensity);
            return Mathf.Clamp01(ships * intensity * p.accumulationPerShip);
        }

        public static float DebrisAccumulation(int shipsDestroyed, float battleIntensity)
            => DebrisAccumulation(shipsDestroyed, battleIntensity, DebrisFieldParams.Default);

        /// <summary>
        /// センサー擾乱（0..maxClutter）＝残骸/(残骸+フィルタ)。残骸に紛れて敵を見失う度合い。
        /// フィルタ（センサー性能）が高いほど残骸を弾いて擾乱が下がる。
        /// </summary>
        public static float SensorClutter(float debrisAccumulation, float sensorFiltering, DebrisFieldParams p)
        {
            float debris = Mathf.Clamp01(debrisAccumulation);
            float filter = Mathf.Max(0f, sensorFiltering);
            float ratio = debris / (debris + filter + p.epsilon);
            return Mathf.Clamp01(ratio) * p.maxClutter;
        }

        public static float SensorClutter(float debrisAccumulation, float sensorFiltering)
            => SensorClutter(debrisAccumulation, sensorFiltering, DebrisFieldParams.Default);

        /// <summary>衝突リスク（0..1）＝残骸×速度×係数。速く突っ込むほど残骸に激突しやすい。</summary>
        public static float CollisionHazard(float debrisAccumulation, float speed, DebrisFieldParams p)
        {
            float debris = Mathf.Clamp01(debrisAccumulation);
            float spd = Mathf.Clamp01(speed);
            return Mathf.Clamp01(debris * spd * p.collisionScale);
        }

        public static float CollisionHazard(float debrisAccumulation, float speed)
            => CollisionHazard(debrisAccumulation, speed, DebrisFieldParams.Default);

        /// <summary>残骸を盾にした遮蔽（0..1）＝残骸量×係数。残骸が多いほど身を隠せる。</summary>
        public static float DebrisCover(float debrisAccumulation, DebrisFieldParams p)
        {
            float debris = Mathf.Clamp01(debrisAccumulation);
            return Mathf.Clamp01(debris * p.coverScale);
        }

        public static float DebrisCover(float debrisAccumulation)
            => DebrisCover(debrisAccumulation, DebrisFieldParams.Default);

        /// <summary>残骸に紛れた隠密接近（0..1）＝擾乱×遮蔽×係数。曇った索敵と遮蔽が重なると忍び寄れる。</summary>
        public static float HiddenApproach(float sensorClutter, float debrisCover, DebrisFieldParams p)
        {
            float clutter = Mathf.Clamp01(sensorClutter);
            float cover = Mathf.Clamp01(debrisCover);
            return Mathf.Clamp01(clutter * cover * p.approachScale);
        }

        public static float HiddenApproach(float sensorClutter, float debrisCover)
            => HiddenApproach(sensorClutter, debrisCover, DebrisFieldParams.Default);

        /// <summary>
        /// 生存者救助率（0..1）＝救助努力/(救助努力+残骸×経過時間×難化)。残骸が濃く時間が経つほど
        /// 救助が難しくなる（漂流者が散り散り・酸欠）。努力が圧倒的なら救えるが、ゼロなら救助率0。
        /// </summary>
        public static float SurvivorRescue(float rescueEffort, float debrisAccumulation, float timeElapsed, DebrisFieldParams p)
        {
            float effort = Mathf.Max(0f, rescueEffort);
            float debris = Mathf.Clamp01(debrisAccumulation);
            float time = Mathf.Max(0f, timeElapsed);
            float difficulty = debris * time * p.rescueDecay;
            float ratio = effort / (effort + difficulty + p.epsilon);
            return Mathf.Clamp01(ratio);
        }

        public static float SurvivorRescue(float rescueEffort, float debrisAccumulation, float timeElapsed)
            => SurvivorRescue(rescueEffort, debrisAccumulation, timeElapsed, DebrisFieldParams.Default);

        /// <summary>サルベージ資源（0..1）＝残骸×回収能力×係数。残骸が多く回収船が優秀なほど資源が拾える。</summary>
        public static float SalvageYield(float debrisAccumulation, float salvageCapacity, DebrisFieldParams p)
        {
            float debris = Mathf.Clamp01(debrisAccumulation);
            float capacity = Mathf.Clamp01(salvageCapacity);
            return Mathf.Clamp01(debris * capacity * p.salvageScale);
        }

        public static float SalvageYield(float debrisAccumulation, float salvageCapacity)
            => SalvageYield(debrisAccumulation, salvageCapacity, DebrisFieldParams.Default);

        /// <summary>残骸宙域を航行できるか＝衝突リスクが閾値以下。閾値超えは危険で進入を控える。</summary>
        public static bool IsFieldNavigable(float collisionHazard, float threshold)
        {
            return Mathf.Clamp01(collisionHazard) <= Mathf.Clamp01(threshold);
        }

        public static bool IsFieldNavigable(float collisionHazard)
            => IsFieldNavigable(collisionHazard, 0.5f);
    }
}
