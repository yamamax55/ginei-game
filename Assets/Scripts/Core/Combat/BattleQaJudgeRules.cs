using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 固定会戦QAの<b>許容誤差つき判定</b>（純ロジック・test-first）。
    ///
    /// 盤面の実測値（位置・艦艇数・状態）を受け取り、結論を返すだけ。
    /// 前提が崩れていれば<b>未判定</b>を返し、合格にも不合格にもしない（空振り合格を作らない）。
    /// </summary>
    public static class BattleQaJudgeRules
    {
        /// <summary>退却方向への変位として認める最小距離（ワールド単位）。</summary>
        public const float MinRetreatDisplacement = 1.0f;

        /// <summary>「被弾停止」とみなす艦艇数の減少の許容（0＝1隻も減らない）。</summary>
        public const int StoppedDamageTolerance = 0;

        /// <summary>
        /// 退却方向（敵から離れる向き）の単位ベクトル。敵と同位置なら <paramref name="fallback"/> を正規化して返す。
        /// </summary>
        public static Vector2 RetreatDirection(Vector2 start, Vector2 enemy, Vector2 fallback)
        {
            Vector2 d = start - enemy;
            if (d.sqrMagnitude > 1e-6f) return d.normalized;
            return fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector2.down;
        }

        /// <summary>方向 <paramref name="dir"/> へ何だけ進んだか（負＝逆へ進んだ）。</summary>
        public static float DisplacementAlong(Vector2 start, Vector2 now, Vector2 dir)
        {
            Vector2 n = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.zero;
            return Vector2.Dot(now - start, n);
        }

        /// <summary>
        /// 軍団総退却の1隊ぶんの判定。
        /// 発令されていない→未判定（前提なし）／撤退状態でない→不合格／退却方向へ最小距離未満→不合格。
        /// </summary>
        public static BattleQaVerdict JudgeRetreatingMember(bool corpsRetreatOrdered, bool inRetreatState,
            float displacementAlongRetreat, float minDisplacement)
        {
            if (!corpsRetreatOrdered) return BattleQaVerdict.未判定;
            if (!inRetreatState) return BattleQaVerdict.不合格;
            return displacementAlongRetreat >= minDisplacement ? BattleQaVerdict.合格 : BattleQaVerdict.不合格;
        }

        /// <summary>
        /// 所属外（独立・別軍団）が巻き込まれていないか。
        /// 自軍団（無ければ空）の総退却が発令されておらず、撤退状態でもなければ合格。
        /// ただし自分の艦艇数比が個艦撤退比を下回っていれば、撤退の原因を分けられないので未判定。
        /// </summary>
        public static BattleQaVerdict JudgeBystander(bool ownCorpsRetreatOrdered, bool inRetreatState,
            float shipRatio, float individualRetreatRatio)
        {
            if (shipRatio < individualRetreatRatio) return BattleQaVerdict.未判定;
            return !ownCorpsRetreatOrdered && !inRetreatState ? BattleQaVerdict.合格 : BattleQaVerdict.不合格;
        }

        /// <summary>
        /// 撤退の原因を軍団総退却に切り分けられるか（艦艇数比が個艦撤退比以上）。
        /// 下回っていれば FleetAI 自身の撤退判断でも同じ状態になるため区別できない。
        /// </summary>
        public static bool RetreatCauseIsolated(float shipRatio, float individualRetreatRatio)
            => shipRatio >= individualRetreatRatio;

        /// <summary>
        /// 不退転の効果中の判定。効果が乗らなかった／観測中に失われた→未判定（前提崩れ）。
        /// 効果中に一度でも敗走が観測された→不合格。それ以外→合格。
        /// </summary>
        public static BattleQaVerdict JudgeLockDuration(bool lockApplied, bool aliveUntilEnd, int routedFramesDuringLock)
        {
            if (!lockApplied || !aliveUntilEnd) return BattleQaVerdict.未判定;
            return routedFramesDuringLock == 0 ? BattleQaVerdict.合格 : BattleQaVerdict.不合格;
        }

        /// <summary>
        /// 被弾の継続/停止を艦艇数の減少で判定する。
        /// 継続を期待→減っていれば合格／停止を期待→許容以内なら合格。観測区間が0秒以下なら未判定。
        /// </summary>
        public static BattleQaVerdict JudgeDamageFlow(bool expectContinuing, int shipsBefore, int shipsAfter,
            float observedSeconds, int stoppedTolerance)
        {
            if (observedSeconds <= 0f) return BattleQaVerdict.未判定;
            int lost = shipsBefore - shipsAfter;
            if (expectContinuing) return lost > 0 ? BattleQaVerdict.合格 : BattleQaVerdict.不合格;
            return lost <= Mathf.Max(0, stoppedTolerance) ? BattleQaVerdict.合格 : BattleQaVerdict.不合格;
        }

        /// <summary>
        /// 保持が軍団AIの周期を跨いで残ったか。
        /// 対照（保持していない隷下）が軍団AIの陣形に変わっていなければ未判定（軍団AIが上書きを試みていない＝空振り）。
        /// </summary>
        public static BattleQaVerdict JudgeHoldAgainstCorpsAi(bool controlChangedByCorpsAi, Formation controlFormation,
            bool held, Formation heldFormation, Formation expectedHeld)
        {
            if (!controlChangedByCorpsAi || controlFormation == expectedHeld) return BattleQaVerdict.未判定;
            return held && heldFormation == expectedHeld ? BattleQaVerdict.合格 : BattleQaVerdict.不合格;
        }

        /// <summary>
        /// 保持解除後に軍団AIの陣形へ戻ったか。保持が残っている／軍団AIが決めていない／陣形が違う→不合格。
        /// （解除の直後に軍団AIの周期が回る時間を観測側が確保する前提。）
        /// </summary>
        public static BattleQaVerdict JudgeReturnToCorpsAi(bool held, FormationOrderSource lastSource,
            Formation current, Formation corpsAiFormation)
        {
            if (held) return BattleQaVerdict.不合格;
            return lastSource == FormationOrderSource.軍団AI && current == corpsAiFormation
                ? BattleQaVerdict.合格 : BattleQaVerdict.不合格;
        }
    }
}
