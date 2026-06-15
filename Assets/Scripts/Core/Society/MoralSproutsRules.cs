using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 孟子の四端（したん）＝住民（被治者）が生まれつき持つ四つの善の芽（MENC-1 #1564）。
    /// <b>惻隠の心(compassion)</b>＝仁の端（他者の苦しみを見過ごせない）／<b>羞悪の心(shame)</b>＝義の端
    /// （不正を恥じ憎む）／<b>辞譲の心(courtesy)</b>＝礼の端（へりくだり譲る）／<b>是非の心(judgment)</b>＝智の端
    /// （善悪を見分ける）。四端は善政で育ち暴政で萎む＝住民の道徳的素地（純データ）。解決は
    /// <see cref="MoralSproutsRules"/>（static）。<b>MoralStyleRules</b>（スミス三徳＝為政者の統治スタイル）/
    /// <b>GovernanceRules</b>（安定度の収束計算）とは別＝こちらは住民（被治者）の四端。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public struct MoralSprouts
    {
        /// <summary>惻隠の心 0..1＝仁の端（他者の苦しみへの共感）。</summary>
        public float compassion;
        /// <summary>羞悪の心 0..1＝義の端（不正を恥じ憎む心）。</summary>
        public float shame;
        /// <summary>辞譲の心 0..1＝礼の端（へりくだり譲る心）。</summary>
        public float courtesy;
        /// <summary>是非の心 0..1＝智の端（善悪を見分ける心）。</summary>
        public float judgment;

        public MoralSprouts(float compassion, float shame, float courtesy, float judgment)
        {
            this.compassion = Mathf.Clamp01(compassion);
            this.shame = Mathf.Clamp01(shame);
            this.courtesy = Mathf.Clamp01(courtesy);
            this.judgment = Mathf.Clamp01(judgment);
        }

        /// <summary>四端の平均（0..1）＝民の道徳的素地の総合水準。</summary>
        public float Average => (Mathf.Clamp01(compassion) + Mathf.Clamp01(shame)
            + Mathf.Clamp01(courtesy) + Mathf.Clamp01(judgment)) / 4f;

        /// <summary>四端を一律に育てた新インスタンスを返す（基準値非破壊）。amount は加算量。</summary>
        public MoralSprouts Cultivate(float amount)
        {
            float a = amount;
            return new MoralSprouts(compassion + a, shame + a, courtesy + a, judgment + a);
        }
    }

    /// <summary>四端モデルの調整係数（孟子の四端・MENC-1 #1564）。</summary>
    public readonly struct MoralSproutsParams
    {
        /// <summary>善政が四端を育てる速さ（仁政で芽が伸びる）。</summary>
        public readonly float cultivationRate;
        /// <summary>暴政が四端を萎ませる速さ。</summary>
        public readonly float witheringRate;
        /// <summary>性善説の下限＝芽は誰にもある最低水準（完全な悪にはならない）。</summary>
        public readonly float innateFloor;
        /// <summary>端→徳の写像のゲイン（芽が育って徳になる度合い）。</summary>
        public readonly float virtueGain;
        /// <summary>道徳的覚醒とみなす公徳心の既定閾値。</summary>
        public readonly float awakeningThreshold;

        public MoralSproutsParams(float cultivationRate, float witheringRate, float innateFloor,
            float virtueGain, float awakeningThreshold)
        {
            this.cultivationRate = Mathf.Max(0f, cultivationRate);
            this.witheringRate = Mathf.Max(0f, witheringRate);
            this.innateFloor = Mathf.Clamp01(innateFloor);
            this.virtueGain = Mathf.Max(0f, virtueGain);
            this.awakeningThreshold = Mathf.Clamp01(awakeningThreshold);
        }

        /// <summary>既定＝涵養0.15・萎縮0.2・性善下限0.1・徳ゲイン1.0・覚醒閾値0.6（暴政の萎みは善政の涵養より速い）。</summary>
        public static MoralSproutsParams Default => new MoralSproutsParams(0.15f, 0.2f, 0.1f, 1f, 0.6f);
    }

    /// <summary>
    /// 四端モデル＝孟子の「四端」を住民の道徳的感受性として解く（MENC-1 #1564）。人は生まれつき四つの善の芽
    /// （惻隠＝仁の端／羞悪＝義の端／辞譲＝礼の端／是非＝智の端）を持ち、<b>善政が育て暴政が萎ませる</b>＝
    /// 民の道徳的素地（社会の道徳資本）。芽は性善説の下限（<see cref="InnateGoodness"/>）を割らない＝完全な悪には
    /// ならない。育った芽は徳へ写像され（<see cref="VirtueFromSprouts"/>：惻隠→仁・羞悪→義・辞譲→礼・是非→智）、
    /// 四端の総合が民の公徳心（<see cref="CivicVirtue"/>）となる。羞悪の心は不正への義憤を生む
    /// （<see cref="RighteousIndignation"/>＝暴政への抵抗の芽）。<b>MoralStyleRules</b>（スミス三徳＝為政者側の統治
    /// スタイル）/<b>GovernanceRules</b>（安定度の収束計算）/<b>MoralForceRules</b>（浩然之気＝同 EPIC MENC・気の充溢）
    /// とは別＝こちらは住民（被治者）の道徳的素地（四端＝仁義礼智の芽）。全入力クランプ・乱数なし・決定論・
    /// 基準値非破壊（新インスタンスを返す）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MoralSproutsRules
    {
        /// <summary>
        /// 善政が四端を育てる1tick（新 <see cref="MoralSprouts"/> を返す）。仁政で芽が目標（善政の度合い）へ
        /// 近づく＝各端を goodGovernance(0..1) との差ぶん cultivationRate×dt で引き上げる。性善説の下限を割らない。
        /// </summary>
        public static MoralSprouts CultivationTick(MoralSprouts s, float goodGovernance, float dt, MoralSproutsParams p)
        {
            float g = Mathf.Clamp01(goodGovernance);
            float step = p.cultivationRate * Mathf.Max(0f, dt);
            float floor = p.innateFloor;
            return new MoralSprouts(
                GrowToward(s.compassion, g, step, floor),
                GrowToward(s.shame, g, step, floor),
                GrowToward(s.courtesy, g, step, floor),
                GrowToward(s.judgment, g, step, floor));
        }

        public static MoralSprouts CultivationTick(MoralSprouts s, float goodGovernance, float dt)
            => CultivationTick(s, goodGovernance, dt, MoralSproutsParams.Default);

        /// <summary>
        /// 暴政が四端を萎ませる1tick（新 <see cref="MoralSprouts"/> を返す）。暴政(tyranny 0..1)が強いほど各端が
        /// witheringRate×tyranny×dt ぶん萎む。ただし性善説の下限（<see cref="MoralSproutsParams.innateFloor"/>）は
        /// 割らない＝完全な悪にはならない。
        /// </summary>
        public static MoralSprouts WitheringTick(MoralSprouts s, float tyranny, float dt, MoralSproutsParams p)
        {
            float ty = Mathf.Clamp01(tyranny);
            float drop = p.witheringRate * ty * Mathf.Max(0f, dt);
            float floor = p.innateFloor;
            return new MoralSprouts(
                Wither(s.compassion, drop, floor),
                Wither(s.shame, drop, floor),
                Wither(s.courtesy, drop, floor),
                Wither(s.judgment, drop, floor));
        }

        public static MoralSprouts WitheringTick(MoralSprouts s, float tyranny, float dt)
            => WitheringTick(s, tyranny, dt, MoralSproutsParams.Default);

        /// <summary>
        /// 端（芽）が育って徳になる写像（0..1）。芽は育てれば徳になる＝端(0..1)×virtueGain をクランプ。
        /// 惻隠→仁・羞悪→義・辞譲→礼・是非→智 を各端ごとに同じ写像で解く（呼び側が対応する端を渡す）。
        /// </summary>
        public static float VirtueFromSprouts(float sprout, MoralSproutsParams p)
            => Mathf.Clamp01(Mathf.Clamp01(sprout) * p.virtueGain);

        public static float VirtueFromSprouts(float sprout)
            => VirtueFromSprouts(sprout, MoralSproutsParams.Default);

        /// <summary>
        /// 民の公徳心（0..1）＝四端の総合＝社会の道徳資本。各端を徳へ写像してから平均を取る
        /// （仁義礼智の芽がそろって育つほど高い）。
        /// </summary>
        public static float CivicVirtue(MoralSprouts s, MoralSproutsParams p)
        {
            float ren = VirtueFromSprouts(s.compassion, p); // 仁
            float gi = VirtueFromSprouts(s.shame, p);        // 義
            float rei = VirtueFromSprouts(s.courtesy, p);    // 礼
            float chi = VirtueFromSprouts(s.judgment, p);    // 智
            return Mathf.Clamp01((ren + gi + rei + chi) / 4f);
        }

        public static float CivicVirtue(MoralSprouts s) => CivicVirtue(s, MoralSproutsParams.Default);

        /// <summary>
        /// 社会調和（0..1）＝民の徳(civicVirtue 0..1)と統治の徳(governanceAlignment 0..1＝統治の道徳的方向との
        /// 一致)が合うほど高い。両者の積＝どちらが欠けても調和は生まれない（民が善くとも暴政なら、暴政でも民が
        /// 萎えていれば調和しない）。
        /// </summary>
        public static float SocialHarmony(float civicVirtue, float governanceAlignment)
            => Mathf.Clamp01(Mathf.Clamp01(civicVirtue) * Mathf.Clamp01(governanceAlignment));

        /// <summary>
        /// 孟子の性善説＝芽は誰にもある最低水準（0..1）。四端の平均が下限（innateFloor）を割っていればその下限を、
        /// 上回っていれば現状の平均を返す＝完全な悪にはならない（善の芽は消えない）。
        /// </summary>
        public static float InnateGoodness(MoralSprouts s, MoralSproutsParams p)
            => Mathf.Max(p.innateFloor, s.Average);

        public static float InnateGoodness(MoralSprouts s) => InnateGoodness(s, MoralSproutsParams.Default);

        /// <summary>
        /// 義憤（0..1）＝羞悪の心(shame 0..1)が不正(injustice 0..1)に出会って生む義憤＝暴政への抵抗の芽。
        /// 不正が無ければ義憤も無い・羞悪の心が萎えていれば不正を恥じない＝両者の積。
        /// </summary>
        public static float RighteousIndignation(float shame, float injustice)
            => Mathf.Clamp01(Mathf.Clamp01(shame) * Mathf.Clamp01(injustice));

        /// <summary>
        /// 民の道徳的覚醒（仁政が四端を開花させた）判定。公徳心(civicVirtue)が閾値(threshold)以上なら true。
        /// </summary>
        public static bool IsMoralAwakening(float civicVirtue, float threshold)
            => Mathf.Clamp01(civicVirtue) >= Mathf.Clamp01(threshold);

        /// <summary>既定閾値（<see cref="MoralSproutsParams.awakeningThreshold"/>）版。</summary>
        public static bool IsMoralAwakening(float civicVirtue)
            => IsMoralAwakening(civicVirtue, MoralSproutsParams.Default.awakeningThreshold);

        // ---- 内部ヘルパ（保存的更新・下限保持） ----

        /// <summary>現在値を目標(target)へ step ぶん近づける。ただし下限(floor)は割らない（性善説）。</summary>
        private static float GrowToward(float current, float target, float step, float floor)
        {
            float c = Mathf.Clamp01(current);
            // 目標へ向けて引き上げる（目標が下なら下げない＝善政は萎ませない）。
            float grown = c + (Mathf.Clamp01(target) - c) * Mathf.Clamp01(step);
            float result = Mathf.Max(c, grown); // 善政の涵養は単調に育てる（目標未満なら現状維持）
            return Mathf.Clamp01(Mathf.Max(floor, result));
        }

        /// <summary>現在値を drop ぶん萎ませる。ただし下限(floor)は割らない（性善説）。</summary>
        private static float Wither(float current, float drop, float floor)
        {
            float c = Mathf.Clamp01(current);
            return Mathf.Clamp01(Mathf.Max(floor, c - Mathf.Max(0f, drop)));
        }
    }
}
