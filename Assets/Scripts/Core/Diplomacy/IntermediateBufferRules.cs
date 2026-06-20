using UnityEngine;

namespace Ginei
{
    /// <summary>中間体バッファの調整係数（#1116）。</summary>
    public readonly struct IntermediateBufferParams
    {
        /// <summary>強制同期の基準秒数＝貯蔵不能（storability=0）のとき上流と下流をこの粒度で同期させる（既定2＝秒単位）。</summary>
        public readonly float syncSeconds;
        /// <summary>結合の固さの下限（バッファ満杯でも残る最小結合＝既定0.05＝完全には切り離せない）。</summary>
        public readonly float minCoupling;
        /// <summary>緩衝在庫投資の効きの最大値（変動最大×バッファ薄のときの投資価値上限＝既定1.0）。</summary>
        public readonly float maxInvestmentValue;

        public IntermediateBufferParams(float syncSeconds, float minCoupling, float maxInvestmentValue)
        {
            this.syncSeconds = Mathf.Max(0f, syncSeconds);
            this.minCoupling = Mathf.Clamp01(minCoupling);
            this.maxInvestmentValue = Mathf.Max(0f, maxInvestmentValue);
        }

        /// <summary>既定＝同期2秒・最小結合0.05・投資価値上限1.0。</summary>
        public static IntermediateBufferParams Default => new IntermediateBufferParams(2f, 0.05f, 1f);
    }

    /// <summary>
    /// 中間体バッファ（緩衝在庫）の純ロジック（#1116・唯一の窓口）。貯蔵できない中間財（高温溶融物・気体・生鮮）は
    /// 緩衝在庫を持てない＝<b>バッファ無しが上流の変動をそのまま下流へ伝える＝ショックを増幅</b>する型。
    /// 貯蔵のしやすさ <see cref="Storability"/>→緩衝在庫の容量 <see cref="BufferCapacity"/>（貯蔵不能なら容量ゼロ）、
    /// バッファの厚みで上流変動を吸収する増幅率 <see cref="ShockAmplification"/>（バッファ無し=1.0＝そのまま伝播）、
    /// 上下流の結合の固さ <see cref="CouplingTightness"/>（バッファ無し=密結合）、貯蔵不能な中間体の強制同期
    /// <see cref="ForcedSynchronization"/>（秒単位で上流と下流の稼働を縛る）、緩衝在庫投資の価値 <see cref="BufferInvestmentValue"/>。
    /// <see cref="ResourceStockpile"/>（物資/弾薬/燃料＝<b>貯められる備蓄財</b>）とは別＝こちらは<b>貯められない中間財</b>を扱う。
    /// <see cref="ChainFragilityRules"/>（同Wave並行・連鎖全体の脆さ）が本クラスの結合の固さを入力に取りうる＝こちらは中間体1段の緩衝を式にする。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class IntermediateBufferRules
    {
        /// <summary>
        /// 貯蔵のしやすさ（0..1）＝この中間体を緩衝在庫として貯められる度合い。
        /// 1=常温固体など貯蔵容易、0=高温溶融物・気体・生鮮など即使うか捨てるかしかない貯蔵不能。
        /// 入力をクランプして返すだけの正規化窓口。
        /// </summary>
        public static float Storability(float materialType) => Mathf.Clamp01(materialType);

        /// <summary>
        /// 持てる緩衝在庫の容量（0..1）＝貯蔵のしやすさ × タンク規模。
        /// 貯蔵不能（storability=0）なら設備をいくら持っても容量ゼロ＝バッファを持てない。
        /// </summary>
        public static float BufferCapacity(float storability, float tankSize)
            => Mathf.Clamp01(storability) * Mathf.Clamp01(tankSize);

        /// <summary>
        /// ショック増幅率（下限0）＝上流の変動が下流に伝わる強さ。
        /// バッファが厚いほど変動を吸収し、薄いほどそのまま伝える＝<b>バッファ無し(0)は増幅1.0＝そのまま下流へ</b>、
        /// 満杯(1)なら変動を吸収して0（下流は揺れない）。式＝upstreamVariance ×(1 − bufferCapacity)。
        /// </summary>
        public static float ShockAmplification(float upstreamVariance, float bufferCapacity)
        {
            float v = Mathf.Clamp01(upstreamVariance);
            float buf = Mathf.Clamp01(bufferCapacity);
            return v * (1f - buf);
        }

        /// <summary>
        /// 上下流の結合の固さ（minCoupling..1）＝1工程の揺れがどれだけ全体へ波及するか。
        /// バッファ無し(0)＝密結合(1.0)＝1工程止まれば全体停止、バッファ厚いほど疎結合（緩衝で吸収）。
        /// 満杯でも minCoupling は残る（中間体は完全には切り離せない）。式＝Lerp(1, minCoupling, bufferCapacity)。
        /// </summary>
        public static float CouplingTightness(float bufferCapacity, IntermediateBufferParams p)
        {
            float buf = Mathf.Clamp01(bufferCapacity);
            return Mathf.Lerp(1f, p.minCoupling, buf);
        }

        public static float CouplingTightness(float bufferCapacity)
            => CouplingTightness(bufferCapacity, IntermediateBufferParams.Default);

        /// <summary>
        /// 強制同期の許容秒数（0..syncSeconds）＝貯蔵不能な中間体は上流と下流の稼働を秒単位で縛る（余裕がない）。
        /// 貯蔵不能(storability=0)なら許容0秒＝完全同期（上流が止まれば下流も即停止）、貯蔵可能なほど許容秒数が伸びる。
        /// 式＝syncSeconds × storability（0なら即同期・1ならフルの余裕）。
        /// </summary>
        public static float ForcedSynchronization(float storability, IntermediateBufferParams p)
            => p.syncSeconds * Mathf.Clamp01(storability);

        public static float ForcedSynchronization(float storability)
            => ForcedSynchronization(storability, IntermediateBufferParams.Default);

        /// <summary>
        /// 緩衝在庫投資の価値（0..maxInvestmentValue）＝この中間体にバッファを足す価値。
        /// 上流の変動が大きく、いまのバッファが薄いほど投資が効く（変動を初めて吸収できる）。
        /// 既に厚いバッファ・変動の小さい工程は投資価値が低い。式＝maxInvestmentValue × variance ×(1 − currentBuffer)。
        /// </summary>
        public static float BufferInvestmentValue(float upstreamVariance, float currentBuffer, IntermediateBufferParams p)
        {
            float v = Mathf.Clamp01(upstreamVariance);
            float buf = Mathf.Clamp01(currentBuffer);
            return p.maxInvestmentValue * v * (1f - buf);
        }

        public static float BufferInvestmentValue(float upstreamVariance, float currentBuffer)
            => BufferInvestmentValue(upstreamVariance, currentBuffer, IntermediateBufferParams.Default);
    }
}
