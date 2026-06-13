using UnityEngine;

namespace Ginei
{
    /// <summary>反転（tipping-point）曲線の調整係数（老子「反者道之動」型・#1550 LAOZ-2）。</summary>
    public readonly struct ReversalParams
    {
        /// <summary>逆U字のピークの鋭さ（大きいほど頂点が鋭く両端が早く0へ落ちる）。</summary>
        public readonly float peakSharpness;
        /// <summary>極端の反動の強さ（強さが弱さに転じる係数＝過剰の代償の大きさ）。</summary>
        public readonly float backlashScale;
        /// <summary>禍福が反転しはじめる幸運の閾値（これを超えた幸運に不運が伏す）。</summary>
        public readonly float fortuneTippingThreshold;
        /// <summary>ピーク後の反転の急峻さ（極まって反る速さ）。</summary>
        public readonly float reversalSteepness;

        public ReversalParams(float peakSharpness, float backlashScale,
            float fortuneTippingThreshold, float reversalSteepness)
        {
            this.peakSharpness = Mathf.Max(0f, peakSharpness);
            this.backlashScale = Mathf.Max(0f, backlashScale);
            this.fortuneTippingThreshold = Mathf.Clamp01(fortuneTippingThreshold);
            this.reversalSteepness = Mathf.Max(0f, reversalSteepness);
        }

        /// <summary>
        /// 既定＝ピーク鋭さ1.0（純粋な放物線）・反動0.5・禍福閾値0.8・反転急峻1.0。
        /// 禍福閾値0.8＝幸運が極まる手前から不運が伏しはじめる（高すぎる福は禍を招く）。
        /// </summary>
        public static ReversalParams Default => new ReversalParams(1f, 0.5f, 0.8f, 1f);
    }

    /// <summary>
    /// 反者道之動（はんしゃどうのどう）＝老子「反は道の動なり」の純ロジック・汎用ユーティリティ（#1550 LAOZ-2）。
    /// <b>物事は極まると反対へ転じる＝盛者必衰・強さは弱さに・得は失に・福は禍に転じる</b>を、汎用の逆U字
    /// tipping-point 曲線として提供する（「物壮なれば則ち老ゆ」「禍は福の倚る所、福は禍の伏す所」）。
    /// 何事も極端は反転する＝強さ・繁栄・拡大が頂点で反転して衰退に向かう汎用カーブで、特定ドメインに
    /// 依存せず<b>他モジュールが利用する数学的ユーティリティ</b>として作る（<see cref="InvertedU"/> は純粋な数学関数）。
    /// <see cref="EscalationRules"/>（紛争の梯子＝一方向の昇り）とは別＝こちらは「物極まれば反る」の汎用逆U字・
    /// 反転 tipping-point 曲線。<see cref="WuWeiRules"/>（無為＝同 EPIC LAOZ）の対をなし、<see cref="HegemonyRules"/>
    /// （覇権移行の山形＝比1.0で危険が最大）と同型の「頂点で反転する」構造を抽象化する。
    /// すべて plain な float で受け渡し・全入力クランプ・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ReversalRules
    {
        /// <summary>
        /// 逆U字（0..1）＝<paramref name="peak"/> でピーク1.0、両端（x=0,1）で0へ落ちる汎用カーブ。
        /// 多項式で実装（Mathf.Sin 不使用）：ピーク位置を中心に正規化した距離の二乗で減衰させ、
        /// <see cref="ReversalParams.peakSharpness"/> 乗で鋭さを調整する。<b>極端（両端）は0・中庸（ピーク）は最大</b>＝
        /// 「強さ・繁栄・拡大は頂点で反転する」を式に出した、他モジュールが使える純粋な数学関数。
        /// </summary>
        public static float InvertedU(float x, float peak, ReversalParams p)
        {
            float v = Mathf.Clamp01(x);
            float pk = Mathf.Clamp01(peak);
            // ピークを境に左右で正規化した距離（0=ピーク, 1=端）を取り、左右非対称を許容する。
            float dist;
            if (v <= pk)
                dist = pk <= 0f ? 1f : (pk - v) / pk;
            else
                dist = pk >= 1f ? 1f : (v - pk) / (1f - pk);
            dist = Mathf.Clamp01(dist);
            // 距離0でピーク値1、距離1で0へ落ちる放物線（1-d^2）を鋭さ乗で調整。
            float baseCurve = Mathf.Clamp01(1f - dist * dist);
            float sharp = Mathf.Max(0.0001f, p.peakSharpness);
            return Mathf.Clamp01(Mathf.Pow(baseCurve, sharp));
        }

        public static float InvertedU(float x, float peak)
            => InvertedU(x, peak, ReversalParams.Default);

        /// <summary>
        /// 反転点（true＝値が閾値という極を超えた）。<b>物極まれば反る</b>の極（tipping point）に達したかの判定。
        /// </summary>
        public static bool TippingPoint(float value, float threshold)
            => Mathf.Clamp01(value) >= Mathf.Clamp01(threshold);

        /// <summary>
        /// ピークを過ぎた分の反転の強さ（0..1）＝極まって反る量。<paramref name="peak"/> 以下は0（まだ反らない）、
        /// 過ぎた分だけ <see cref="ReversalParams.reversalSteepness"/> 倍で反転が強まる。<b>頂点を越えた超過が
        /// 反転を生む</b>＝盛者必衰の「過ぎた」量を返す。
        /// </summary>
        public static float ReversalAfterPeak(float value, float peak, ReversalParams p)
        {
            float v = Mathf.Clamp01(value);
            float pk = Mathf.Clamp01(peak);
            if (v <= pk) return 0f;
            float room = 1f - pk;
            float over = room <= 0f ? 0f : (v - pk) / room; // ピーク後を0..1に正規化
            return Mathf.Clamp01(over * p.reversalSteepness);
        }

        public static float ReversalAfterPeak(float value, float peak)
            => ReversalAfterPeak(value, peak, ReversalParams.Default);

        /// <summary>
        /// 極端なほど大きい反動（0..1）＝<b>強さが弱さに転じる＝過剰の代償</b>。<paramref name="extremeThreshold"/>
        /// 以下は反動なし（0）、超えた極端さに比例し <see cref="ReversalParams.backlashScale"/> 倍で反動が増す。
        /// 強さ・拡大が極端なほど、その反動（揺り戻し）が大きくなる。
        /// </summary>
        public static float ExtremeBacklash(float intensity, float extremeThreshold, ReversalParams p)
        {
            float v = Mathf.Clamp01(intensity);
            float thr = Mathf.Clamp01(extremeThreshold);
            if (v <= thr) return 0f;
            float room = 1f - thr;
            float excess = room <= 0f ? 0f : (v - thr) / room;
            return Mathf.Clamp01(excess * p.backlashScale);
        }

        public static float ExtremeBacklash(float intensity, float extremeThreshold)
            => ExtremeBacklash(intensity, extremeThreshold, ReversalParams.Default);

        /// <summary>
        /// 逓減を経て反転する効用（-1..1）＝<b>増やすほど効くが、極で逆効果＝得が失に転じる</b>。
        /// <paramref name="optimalPoint"/> までは逆U字で上昇（逓減しつつ増益）、最適点を過ぎると減益へ転じて
        /// 負値（=失）に向かう。最適点で最大の正値、両端で負へ。投資・拡大・統制など「やり過ぎると逆効果」の汎用形。
        /// </summary>
        public static float DiminishingThenReversing(float input, float optimalPoint, ReversalParams p)
        {
            float v = Mathf.Clamp01(input);
            float opt = Mathf.Clamp01(optimalPoint);
            // 逆U字（最適点で1、両端で0）を中心0で-1..1へ写像＝最適点で最大の得、極で失。
            float u = InvertedU(v, opt, p);
            return Mathf.Clamp(u * 2f - 1f, -1f, 1f);
        }

        public static float DiminishingThenReversing(float input, float optimalPoint)
            => DiminishingThenReversing(input, optimalPoint, ReversalParams.Default);

        /// <summary>
        /// 循環＝極まれば始めに還る（0..1）。<paramref name="phase"/> 0..1 を一巡とし、0と1が繋がる盛衰の円環を返す
        /// （位相0で0、位相0.5で最大1、位相1で再び0＝逆U字を一周期に見立てた循環）。<b>満つれば欠ける＝極まれば
        /// 還る</b>の周期形。多項式（Mathf.Sin 不使用）。
        /// </summary>
        public static float CyclicalReturn(float phase)
        {
            float t = Mathf.Clamp01(phase);
            // 0→0.5で0→1、0.5→1で1→0。4t(1-t) は t=0.5 で1、両端0の対称放物線（0と1が繋がる）。
            return Mathf.Clamp01(4f * t * (1f - t));
        }

        /// <summary>
        /// 禍福は糾える縄（0..1＝幸運に伏す反転リスク）＝<b>高すぎる幸運の極に不運が伏す</b>。
        /// <see cref="ReversalParams.fortuneTippingThreshold"/> 以下は反転リスク0（福はまだ福）、閾値を超えた幸運ほど
        /// 反転リスクが線形に増す＝福の極まりが禍を招く。「福は禍の伏す所」を式に出す。
        /// </summary>
        public static float FortuneMisfortune(float fortune, ReversalParams p)
        {
            float f = Mathf.Clamp01(fortune);
            float thr = p.fortuneTippingThreshold;
            if (f <= thr) return 0f;
            float room = 1f - thr;
            return Mathf.Clamp01(room <= 0f ? 0f : (f - thr) / room);
        }

        public static float FortuneMisfortune(float fortune)
            => FortuneMisfortune(fortune, ReversalParams.Default);

        /// <summary>
        /// ピークを過ぎて反転局面に入ったか（true＝<paramref name="value"/> が <paramref name="peak"/> を超えた）。
        /// <b>盛りを過ぎ衰退へ向かう＝物壮なれば則ち老ゆ</b>の局面判定（境界ちょうどは未通過）。
        /// </summary>
        public static bool IsPastPeak(float value, float peak)
            => Mathf.Clamp01(value) > Mathf.Clamp01(peak);
    }
}
