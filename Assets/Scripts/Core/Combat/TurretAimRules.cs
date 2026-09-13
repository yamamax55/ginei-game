using UnityEngine;

namespace Ginei
{
    /// <summary>砲塔の見た目の旋回・反動の調整値。</summary>
    public readonly struct TurretAimParams
    {
        /// <summary>旋回速度（度/秒）。速すぎると機械らしさが消え、遅すぎると撃てない。</summary>
        public readonly float turnSpeed;

        /// <summary>この角度以内を向いていれば撃ってよい（度）。</summary>
        public readonly float fireTolerance;

        /// <summary>反動の後退量（ローカル単位）。</summary>
        public readonly float recoilKick;

        /// <summary>反動から戻りきるまでの秒数。</summary>
        public readonly float recoilRecover;

        public TurretAimParams(float turnSpeed, float fireTolerance, float recoilKick, float recoilRecover)
        {
            this.turnSpeed = Mathf.Max(1f, turnSpeed);
            this.fireTolerance = Mathf.Clamp(fireTolerance, 0.5f, 180f);
            this.recoilKick = Mathf.Max(0f, recoilKick);
            this.recoilRecover = Mathf.Max(0.01f, recoilRecover);
        }

        /// <summary>既定＝毎秒180度・許容6度・後退0.16・戻り0.35秒（Blender の反動量に合わせる）。</summary>
        public static TurretAimParams Default => new TurretAimParams(180f, 6f, 0.16f, 0.35f);
    }

    /// <summary>
    /// 砲塔の<b>見た目の旋回</b>と<b>反動</b>（純ロジック・test-first）。
    ///
    /// <b>この分離が本モジュールの主旨</b>：射界の判定は砲台の<b>固定基準</b>
    /// （<c>FortressTurret.transform.up</c>＝要塞が配置時に決めた外向き）で行い、
    /// 3Dモデルの砲塔はその<b>子</b>として実対象へ向くだけにする。
    /// モデルを回した角度を射界の基準にすると、追尾するたびに扇が付いて回り、
    /// <b>射界が際限なく広がる</b>（＝どの方角も撃てる＝砲台を潰しても死角ができない）。
    /// 死角ができることが要塞戦の攻め口なので、そこは絶対に崩さない。
    ///
    /// <see cref="CanFire"/> は「固定基準の射界の中」かつ「モデルが実対象を向き終えている」の
    /// <b>両方</b>を要求する＝見た目より広くも狭くもならない。
    ///
    /// 乱数なし・決定論。
    /// </summary>
    public static class TurretAimRules
    {
        /// <summary>-180..180 へ正規化した角度差（<paramref name="to"/> − <paramref name="from"/>）。</summary>
        public static float SignedDelta(float from, float to)
        {
            float d = Mathf.Repeat(to - from + 180f, 360f) - 180f;
            return d;
        }

        /// <summary>
        /// 現在角から目標角へ、1フレームぶんだけ近づけた角度（度）。
        /// <paramref name="dt"/> は game-time（ポーズで 0＝止まる・倍速で速く回る）。
        /// </summary>
        public static float StepAngle(float current, float desired, float dt, in TurretAimParams p)
        {
            float delta = SignedDelta(current, desired);
            float step = p.turnSpeed * Mathf.Max(0f, dt);
            if (Mathf.Abs(delta) <= step) return Mathf.Repeat(desired, 360f);
            return Mathf.Repeat(current + Mathf.Sign(delta) * step, 360f);
        }

        /// <summary>モデルが目標方向を向き終えているか（許容角の内側）。</summary>
        public static bool Aligned(float current, float desired, in TurretAimParams p)
            => Mathf.Abs(SignedDelta(current, desired)) <= p.fireTolerance;

        /// <summary>
        /// 撃ってよいか。<paramref name="inFixedArc"/>＝<b>固定基準</b>の射界に入っているか
        /// （<c>ShipCombat.FindNearestEnemyInArc</c> の判定結果をそのまま渡す）。
        /// <paramref name="hasTarget"/>＝標的が生きていて射程内か。
        /// <paramref name="silenced"/>＝沈黙（破壊）していないか。
        ///
        /// 見た目の旋回は<b>条件を足すだけ</b>で、決して緩めない
        /// ＝旋回しても固定射界の外は撃てない（射界が広がらない）。
        /// </summary>
        public static bool CanFire(bool hasTarget, bool inFixedArc, bool silenced, bool paused,
                                   float currentAngle, float desiredAngle, in TurretAimParams p)
        {
            if (!hasTarget || !inFixedArc || silenced || paused) return false;
            return Aligned(currentAngle, desiredAngle, p);
        }

        /// <summary>
        /// 発砲からの経過秒に対する砲身の後退量（0＝定位置）。
        /// 撃った瞬間に最大まで下がり、<see cref="TurretAimParams.recoilRecover"/> 秒かけて戻る。
        /// 経過が負／戻り終えたあとは 0（残留しない＝撃ち終わったのに下がったままにならない）。
        /// </summary>
        public static float RecoilOffset(float elapsedSeconds, in TurretAimParams p)
        {
            if (elapsedSeconds < 0f) return 0f;
            if (elapsedSeconds >= p.recoilRecover) return 0f;
            float t = 1f - elapsedSeconds / p.recoilRecover;   // 1 → 0
            return p.recoilKick * t * t;                        // 戻り際をなめらかに
        }

        /// <summary>方向ベクトル → 度（+x が 0 度・反時計回り）。ゼロベクトルは 0 度。</summary>
        public static float AngleOf(Vector2 dir)
        {
            if (dir.sqrMagnitude < 1e-8f) return 0f;
            return Mathf.Repeat(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, 360f);
        }
    }
}
