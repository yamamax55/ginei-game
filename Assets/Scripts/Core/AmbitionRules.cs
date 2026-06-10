using UnityEngine;

namespace Ginei
{
    /// <summary>野心と猜疑のスパイラルの調整係数（ロイエンタール型）。</summary>
    public readonly struct AmbitionParams
    {
        /// <summary>野心の成長率（功績1×天井0のとき1回で増える量）。</summary>
        public readonly float ambitionGrowthRate;
        /// <summary>猜疑の基礎蓄積率（野心1のとき per dt で増える量）。</summary>
        public readonly float suspicionRate;
        /// <summary>讒言の増幅幅（讒言1で猜疑蓄積が 1+この値 倍）。</summary>
        public readonly float whisperAmplify;
        /// <summary>共鳴利得（野心1×猜疑1のとき1回の相互増幅で双方に足される量）。</summary>
        public readonly float spiralGain;
        /// <summary>自発的反逆の重み（野心×猜疑に掛かる。0..1）。</summary>
        public readonly float voluntaryWeight;
        /// <summary>「追い込まれ」の重み（追い詰めが引き金になる加算分。0..1）。</summary>
        public readonly float corneredWeight;
        /// <summary>信頼の可視化（重用の継続・讒言の排除）による猜疑の削減率（0..1）。</summary>
        public readonly float trustVisibilityFactor;
        /// <summary>人質（家族の手元留め置き）による猜疑の削減率（0..1）。</summary>
        public readonly float hostageFactor;
        /// <summary>名誉職への転出（実権を外す）による猜疑の削減率（0..1）。</summary>
        public readonly float honoraryTransferFactor;

        public AmbitionParams(float ambitionGrowthRate, float suspicionRate, float whisperAmplify,
                              float spiralGain, float voluntaryWeight, float corneredWeight,
                              float trustVisibilityFactor, float hostageFactor, float honoraryTransferFactor)
        {
            this.ambitionGrowthRate = Mathf.Max(0f, ambitionGrowthRate);
            this.suspicionRate = Mathf.Max(0f, suspicionRate);
            this.whisperAmplify = Mathf.Max(0f, whisperAmplify);
            this.spiralGain = Mathf.Max(0f, spiralGain);
            this.voluntaryWeight = Mathf.Clamp01(voluntaryWeight);
            this.corneredWeight = Mathf.Clamp01(corneredWeight);
            this.trustVisibilityFactor = Mathf.Clamp01(trustVisibilityFactor);
            this.hostageFactor = Mathf.Clamp01(hostageFactor);
            this.honoraryTransferFactor = Mathf.Clamp01(honoraryTransferFactor);
        }

        /// <summary>既定＝野心成長0.5/猜疑率0.1/讒言増幅1/共鳴利得0.2/自発重み0.4/追込重み0.6/信頼可視化0.6/人質0.3/名誉職0.45。</summary>
        public static AmbitionParams Default
            => new AmbitionParams(0.5f, 0.1f, 1f, 0.2f, 0.4f, 0.6f, 0.6f, 0.3f, 0.45f);
    }

    /// <summary>
    /// スパイラルを解く手それぞれの「適用後に残る猜疑」（小さいほど有効）。
    /// 名誉職への転出は猜疑を下げるが実権の天井も下げる＝<see cref="AmbitionRules.AmbitionGrowth"/> で
    /// 野心が再燃しうる副作用を持つ（消費側が ceiling を下げて反映する想定）。
    /// </summary>
    public readonly struct AmbitionDefusion
    {
        /// <summary>信頼の可視化（重用の継続・讒言の排除）後の猜疑。</summary>
        public readonly float trustVisibility;
        /// <summary>人質（家族の手元留め置き）後の猜疑。</summary>
        public readonly float hostage;
        /// <summary>名誉職への転出（実権を外す）後の猜疑。</summary>
        public readonly float honoraryTransfer;

        public AmbitionDefusion(float trustVisibility, float hostage, float honoraryTransfer)
        {
            this.trustVisibility = Mathf.Clamp01(trustVisibility);
            this.hostage = Mathf.Clamp01(hostage);
            this.honoraryTransfer = Mathf.Clamp01(honoraryTransfer);
        }

        /// <summary>最も猜疑が残らない手の値（最善手の残存猜疑）。</summary>
        public float Best => Mathf.Min(trustVisibility, Mathf.Min(hostage, honoraryTransfer));
    }

    /// <summary>
    /// 野心（ロイエンタール型）の純ロジック。実力者の野心は功績とともに育ち、主君の猜疑と共鳴して
    /// 反逆の自己成就予言になる。「行き場のない実力が野心になる」＝功績が大きく現職の天井が低いほど育ち、
    /// 「野心の匂い×讒言」が猜疑を蓄積し、<see cref="SpiralFeedback"/> が相互増幅＝疑われた実力者は身を守る
    /// ため力を蓄え、それがさらに疑いを呼ぶ。式の核は「反逆は野心の結果ではなく、猜疑との共鳴の結果」＝
    /// 野心が最大でも猜疑0なら反逆圧力は0、引き金は自発的野心より「追い込まれ」が重い（ロイエンタールは
    /// 追い込まれて立った）。<see cref="LoyaltyRules"/>（忠誠×調略×趨勢から旗幟を解決＝戦う前に決まる戦い）
    /// とは別系統＝こちらは反逆が生まれるまでの長期スパイラルで、出力（<see cref="RebellionPressure"/>）を
    /// 忠誠低下や調略感受性の入力として消費側が橋渡しする想定（基準値非破壊）。
    /// 乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AmbitionRules
    {
        /// <summary>
        /// 野心の成長（戻り値＝新しい野心0..1）。功績（achievements 0..1）が大きく、現職での天井
        /// （ceiling 0..1＝この先どれだけ昇れるか）が低いほど育つ＝行き場のない実力が野心になる。
        /// 天井が開いていれば（ceiling=1）どれだけ功績を積んでも野心にならず、功績0なら凡庸は野心も持てない。
        /// </summary>
        public static float AmbitionGrowth(float ambition, float achievements, float ceiling, AmbitionParams p)
        {
            float blocked = Mathf.Clamp01(achievements) * (1f - Mathf.Clamp01(ceiling));
            return Mathf.Clamp01(Mathf.Clamp01(ambition) + p.ambitionGrowthRate * blocked);
        }

        public static float AmbitionGrowth(float ambition, float achievements, float ceiling)
            => AmbitionGrowth(ambition, achievements, ceiling, AmbitionParams.Default);

        /// <summary>
        /// 主君の猜疑の蓄積（戻り値＝新しい猜疑0..1）。野心の匂い×讒言（courtWhispers 0..1）で育つ＝
        /// 讒言は野心の匂いを増幅するが、匂いがなければ（ambition=0）讒言だけでは刺さらない。
        /// dt はフレームレート非依存の経過時間（負は0扱い）。
        /// </summary>
        public static float SuspicionTick(float suspicion, float ambition, float courtWhispers, float dt, AmbitionParams p)
        {
            float scent = Mathf.Clamp01(ambition);
            float amplify = 1f + p.whisperAmplify * Mathf.Clamp01(courtWhispers);
            return Mathf.Clamp01(Mathf.Clamp01(suspicion) + p.suspicionRate * scent * amplify * Mathf.Max(0f, dt));
        }

        public static float SuspicionTick(float suspicion, float ambition, float courtWhispers, float dt)
            => SuspicionTick(suspicion, ambition, courtWhispers, dt, AmbitionParams.Default);

        /// <summary>
        /// 野心と猜疑の相互増幅＝自己成就予言の核（戻り値＝双方に加算する共鳴量）。疑われた実力者は身を守る
        /// ため力を蓄え（野心↑）、それがさらに疑いを呼ぶ（猜疑↑）。積で共鳴するため、どちらか一方が0なら
        /// スパイラルは回らない＝野心だけでは、猜疑だけでは、破局に至らない。双方が高いほど加速する。
        /// </summary>
        public static float SpiralFeedback(float ambition, float suspicion, AmbitionParams p)
            => p.spiralGain * Mathf.Clamp01(ambition) * Mathf.Clamp01(suspicion);

        public static float SpiralFeedback(float ambition, float suspicion)
            => SpiralFeedback(ambition, suspicion, AmbitionParams.Default);

        /// <summary>
        /// 反逆圧力（0..1）。自発成分＝野心×猜疑×voluntaryWeight（野心が最大でも猜疑0なら0＝
        /// 反逆は野心の結果ではなく、猜疑との共鳴の結果）＋「追い込まれ」成分（cornered＝粛清・召喚・
        /// 解任の危機）＝corneredWeight の加算。引き金は追い込まれが重い＝ロイエンタールは追い込まれて立った。
        /// </summary>
        public static float RebellionPressure(float ambition, float suspicion, bool cornered, AmbitionParams p)
        {
            float voluntary = p.voluntaryWeight * Mathf.Clamp01(ambition) * Mathf.Clamp01(suspicion);
            float trigger = cornered ? p.corneredWeight : 0f;
            return Mathf.Clamp01(voluntary + trigger);
        }

        public static float RebellionPressure(float ambition, float suspicion, bool cornered)
            => RebellionPressure(ambition, suspicion, cornered, AmbitionParams.Default);

        /// <summary>
        /// スパイラルを解く手の効果。信頼の可視化（重用の継続・讒言の排除）・人質・名誉職への転出それぞれを
        /// 単独適用した「残る猜疑」を返す（小さいほど有効。既定では信頼の可視化が最も深く解く）。
        /// 名誉職への転出は実権の天井を下げる副作用を持つ＝<see cref="AmbitionGrowth"/> へ低い ceiling を
        /// 渡して野心の再燃を消費側が表現する想定。
        /// </summary>
        public static AmbitionDefusion DefusionOptions(float suspicion, AmbitionParams p)
        {
            float s = Mathf.Clamp01(suspicion);
            return new AmbitionDefusion(
                s * (1f - p.trustVisibilityFactor),
                s * (1f - p.hostageFactor),
                s * (1f - p.honoraryTransferFactor));
        }

        public static AmbitionDefusion DefusionOptions(float suspicion)
            => DefusionOptions(suspicion, AmbitionParams.Default);
    }
}
