using UnityEngine;

namespace Ginei
{
    /// <summary>縦深防御線の調整係数（多重陣地＋予備隊）。</summary>
    public readonly struct DefenseLineParams
    {
        /// <summary>縦深の逓減率（0..1）。2層目以降の陣地は前縁の depthFactor^k 倍で寄与する。</summary>
        public readonly float depthFactor;
        /// <summary>突破に要する攻撃/守備の戦力比。互角以下は突破不能、この比で確率1。</summary>
        public readonly float breakthroughRatio;
        /// <summary>浸透の基礎速度（突破口幅1・per dt）。</summary>
        public readonly float penetrationRate;
        /// <summary>予備隊が突破口を塞ぐ速度（応答1・突破口幅0・per dt）。</summary>
        public readonly float reserveSealRate;
        /// <summary>予備隊反撃の応答時間スケール（この時間で効果が半減）。</summary>
        public readonly float responseTimeScale;
        /// <summary>突出部が逆包囲の好機（機会1.0）になる浸透深度。</summary>
        public readonly float salientDepthForPocket;
        /// <summary>崩壊の伝播強度（士気ゼロの陣地は破られた割合×(1+これ) で浮き足立つ）。</summary>
        public readonly float contagionFactor;

        public DefenseLineParams(float depthFactor, float breakthroughRatio, float penetrationRate,
                                 float reserveSealRate, float responseTimeScale,
                                 float salientDepthForPocket, float contagionFactor)
        {
            this.depthFactor = Mathf.Clamp01(depthFactor);
            this.breakthroughRatio = Mathf.Max(1.01f, breakthroughRatio);
            this.penetrationRate = Mathf.Max(0f, penetrationRate);
            this.reserveSealRate = Mathf.Max(0f, reserveSealRate);
            this.responseTimeScale = Mathf.Max(0.0001f, responseTimeScale);
            this.salientDepthForPocket = Mathf.Max(0.0001f, salientDepthForPocket);
            this.contagionFactor = Mathf.Max(0f, contagionFactor);
        }

        /// <summary>既定＝縦深逓減0.5・突破比2.0・浸透速度1.0・閉塞速度1.0・応答スケール1.0・包囲化深度2.0・伝播1.0。</summary>
        public static DefenseLineParams Default => new DefenseLineParams(0.5f, 2f, 1f, 1f, 1f, 2f, 1f);
    }

    /// <summary>
    /// 縦深防御線の純ロジック（複数陣地の防衛線＝前縁突破後の浸透と予備隊の反撃）。
    /// 攻撃側は「一点突破（前縁は破れるが他正面は手付かず）か、広正面（全部に薄く＝どこも破れない）か」の
    /// トレードオフを負い、防衛側は前縁が破られる前提で予備隊が突破口を塞ぐ＝<b>防衛線の本体は前縁でなく予備隊</b>
    /// （予備応答ゼロなら狭い突破口でも浸透は止まらない）。深入りした突出部は逆包囲の好機（バルジの教訓）。
    /// 単一要塞の防御（シールド・主砲・難攻不落）は <see cref="FortressRules"/>（点の防御）が担い、
    /// こちらは複数陣地を束ねた線と縦深の防御を扱う（別系統）。乱数なし・決定論（判定は呼び出し側 roll）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DefenseLineRules
    {
        /// <summary>
        /// 防衛線の総合強度＝陣地守備×正面数×縦深倍率。縦深倍率は 1 + f + f^2 + …（f=depthFactor）＝
        /// 陣地を重ねるほど強いが逓減する（2層目以降は半価）。depth/sectorCount が0以下なら線は無い＝0。
        /// </summary>
        public static float LineStrength(float garrisonPerSector, int sectorCount, int depth, DefenseLineParams p)
        {
            if (sectorCount <= 0 || depth <= 0) return 0f;
            float garrison = Mathf.Max(0f, garrisonPerSector);
            float multiplier = 0f;
            float layer = 1f;
            for (int k = 0; k < depth; k++)
            {
                multiplier += layer;
                layer *= p.depthFactor;
            }
            return garrison * sectorCount * multiplier;
        }

        public static float LineStrength(float garrisonPerSector, int sectorCount, int depth)
            => LineStrength(garrisonPerSector, sectorCount, depth, DefenseLineParams.Default);

        /// <summary>
        /// 焦点正面の突破確率（0..1）。一点集中度（0=全正面へ均等、1=一点に全力）で焦点に向く実効攻撃力が
        /// 攻撃総力の 1/sectorCount〜1.0 に変わり、守備に対する比が1以下なら0・breakthroughRatio 倍で1。
        /// ＝広く攻めれば全部弱く・一点なら破れるが他正面は手付かず、のトレードオフ。
        /// </summary>
        public static float BreakthroughChance(float attackStrength, float attackConcentration,
                                               float sectorGarrison, int sectorCount, DefenseLineParams p)
        {
            if (sectorCount <= 0) return 0f;
            float attack = Mathf.Max(0f, attackStrength);
            float focusShare = Mathf.Lerp(1f / sectorCount, 1f, Mathf.Clamp01(attackConcentration));
            float ratio = attack * focusShare / Mathf.Max(0.0001f, sectorGarrison);
            return Mathf.Clamp01((ratio - 1f) / (p.breakthroughRatio - 1f));
        }

        public static float BreakthroughChance(float attackStrength, float attackConcentration,
                                               float sectorGarrison, int sectorCount)
            => BreakthroughChance(attackStrength, attackConcentration, sectorGarrison, sectorCount, DefenseLineParams.Default);

        /// <summary>
        /// 浸透の1tick進行＝新しい浸透深度（0以上）。前進は突破口幅に比例し、予備隊の閉塞は
        /// 応答度×(1−幅) に比例＝狭い突破口は予備隊が塞ぎ（後退しうる）、広い崩壊は塞ぎようがなく止まらない。
        /// 予備応答0なら狭くても進み続ける＝防衛線の本体は予備隊。
        /// </summary>
        public static float PenetrationDepthTick(float penetration, float breakthroughWidth,
                                                 float reserveResponse, float dt, DefenseLineParams p)
        {
            float width = Mathf.Clamp01(breakthroughWidth);
            float response = Mathf.Clamp01(reserveResponse);
            float time = Mathf.Max(0f, dt);
            float advance = p.penetrationRate * width * time;
            float seal = p.reserveSealRate * response * (1f - width) * time;
            return Mathf.Max(0f, Mathf.Max(0f, penetration) + advance - seal);
        }

        public static float PenetrationDepthTick(float penetration, float breakthroughWidth,
                                                 float reserveResponse, float dt)
            => PenetrationDepthTick(penetration, breakthroughWidth, reserveResponse, dt, DefenseLineParams.Default);

        /// <summary>
        /// 予備隊の反撃効果＝予備戦力 ÷ (1+応答時間/スケール) ÷ (1+浸透深度)。
        /// 早いほど効く（応答スケール経過で半減）＝浸透が浅いうちに叩くのが予備隊の時間価値。
        /// </summary>
        public static float ReserveCounterattack(float reserveStrength, float penetration,
                                                 float responseTime, DefenseLineParams p)
        {
            float reserve = Mathf.Max(0f, reserveStrength);
            float timeFactor = 1f / (1f + Mathf.Max(0f, responseTime) / p.responseTimeScale);
            float depthFactor = 1f / (1f + Mathf.Max(0f, penetration));
            return reserve * timeFactor * depthFactor;
        }

        public static float ReserveCounterattack(float reserveStrength, float penetration, float responseTime)
            => ReserveCounterattack(reserveStrength, penetration, responseTime, DefenseLineParams.Default);

        /// <summary>
        /// 突出部の逆包囲機会（0..1）。両肩（突破口の左右の陣地）が持ちこたえている時のみ、浸透が深いほど
        /// 攻撃側が袋に入る＝salientDepthForPocket で機会1.0（バルジの教訓）。肩が崩れていれば袋にならず0。
        /// </summary>
        public static float EncirclementOpportunity(float penetration, bool flankHolding, DefenseLineParams p)
        {
            if (!flankHolding) return 0f;
            return Mathf.Clamp01(Mathf.Max(0f, penetration) / p.salientDepthForPocket);
        }

        public static float EncirclementOpportunity(float penetration, bool flankHolding)
            => EncirclementOpportunity(penetration, flankHolding, DefenseLineParams.Default);

        /// <summary>
        /// 防衛線の連鎖崩壊リスク（0..1）＝破られた正面の割合×(1 + 伝播×(1−士気))。
        /// 隣が破られた陣地は浮き足立つ＝士気が低いほど割合以上に崩れる（高士気なら割合どおり持つ）。
        /// </summary>
        public static float LineCollapseRisk(int brokenSectors, int totalSectors, float morale, DefenseLineParams p)
        {
            if (totalSectors <= 0) return 0f;
            float fraction = Mathf.Clamp01(Mathf.Max(0, brokenSectors) / (float)totalSectors);
            float panic = 1f + p.contagionFactor * (1f - Mathf.Clamp01(morale));
            return Mathf.Clamp01(fraction * panic);
        }

        public static float LineCollapseRisk(int brokenSectors, int totalSectors, float morale)
            => LineCollapseRisk(brokenSectors, totalSectors, morale, DefenseLineParams.Default);
    }
}
