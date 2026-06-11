using UnityEngine;

namespace Ginei
{
    /// <summary>複合共和制の調整係数（ctor で全項クランプ・既定は <see cref="Default"/>）。</summary>
    public readonly struct CompoundRepublicParams
    {
        /// <summary>垂直抑制の鋭さ（0より大）。大きいほど中央/地方の片方が痩せた時に抑制が急落する。</summary>
        public readonly float verticalSharpness;
        /// <summary>専制リスク低下の最大係数(0..1)。二重の安全保障が完全に効いてもこの割合までしか下げない。</summary>
        public readonly float maxRiskReduction;
        /// <summary>二層主権が均衡したとみなす垂直抑制の既定しきい値(0..1)。</summary>
        public readonly float balancedThreshold;

        public CompoundRepublicParams(float verticalSharpness, float maxRiskReduction, float balancedThreshold)
        {
            this.verticalSharpness = Mathf.Max(0.0001f, verticalSharpness);
            this.maxRiskReduction = Mathf.Clamp01(maxRiskReduction);
            this.balancedThreshold = Mathf.Clamp01(balancedThreshold);
        }

        /// <summary>既定＝垂直抑制の鋭さ1.0・リスク低下の上限0.8・均衡しきい値0.5。</summary>
        public static CompoundRepublicParams Default => new CompoundRepublicParams(1f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 複合共和制と二層主権の純ロジック（FED-3 #1481・『ザ・フェデラリスト』第51篇／マディソン）。
    /// 「複合共和国（compound republic）＝権力をまず連邦と州の二つの政府に分割し、次に各政府内で三権に
    /// 分割する＝二重の分割が垂直（連邦/州）と水平（三権）の二重の抑制を生み、市民の権利に二重の安全保障
    /// （double security）を与えて専制を防ぐ」を式に落とす。中央へ列挙的に委譲した権限
    /// （<see cref="DelegatedPower"/>）と州が保留した残余権限（<see cref="ReservedPower"/>）の配分が
    /// 垂直抑制の強さ（<see cref="VerticalCheckStrength"/>）を決め、水平の三権抑制と合わさって二重の安全保障
    /// （<see cref="DoubleSecurity"/>）となり専制リスクを下げる（<see cref="TyrannyRiskReduction"/>）。
    /// 役割分担：<see cref="FederalismRules"/> は中央⇔地方の分権度の振り子（最適分権点）を扱い、
    /// こちらは委譲/保留の配分が生む垂直チェックそのものを測る別軸。
    /// <see cref="ConstitutionRules"/> は憲法による権力の制約範囲、
    /// <see cref="SeparationOfPowersRules"/> は同一レベル内の三権の水平抑制（こちらは入力に取る）、
    /// <see cref="AmbitionCounterRules"/>（同 EPIC FED＝「野心が野心に対抗する」）は人の動機による抑制を扱う。
    /// 全入力クランプ・乱数なし決定論・基準値非破壊。test-first。
    /// </summary>
    public static class CompoundRepublicRules
    {
        /// <summary>
        /// 中央へ委譲された権限(0..1)＝列挙された範囲に限定される。中央の権威(centralAuthority)が、
        /// 憲法で列挙された範囲(enumeratedScope)の分だけ実権として委譲される＝両者の積。
        /// 列挙範囲が狭ければ(0)中央は権威があっても権限を持たず、範囲が広く権威も強ければ(1)中央へ集中。
        /// </summary>
        /// <param name="centralAuthority">中央政府の権威(0..1)。</param>
        /// <param name="enumeratedScope">憲法に列挙された委譲範囲(0..1)。</param>
        public static float DelegatedPower(float centralAuthority, float enumeratedScope)
            => Mathf.Clamp01(Mathf.Clamp01(centralAuthority) * Mathf.Clamp01(enumeratedScope));

        /// <summary>
        /// 州が保留した権限(0..1)＝委譲されなかった残余（修正第10条の精神）。1 − 委譲ぶん。
        /// 中央へ多く委譲するほど州の保留権限は痩せ、列挙が狭ければ州に広く残る。
        /// </summary>
        public static float ReservedPower(float delegatedPower)
            => Mathf.Clamp01(1f - Mathf.Clamp01(delegatedPower));

        /// <summary>
        /// 垂直抑制の強さ(0..1)＝中央と地方が互いを牽制する力。両方が実質を持つほど強く、
        /// どちらかが空（一極集中）なら抑制は消える＝相乗平均型 (4×委譲×保留)^鋭さ。
        /// 委譲も保留も0.5（半々）のとき積0.25→正規化1.0で最大。完全集中(委譲1/保留0)や
        /// 完全分権(委譲0/保留1)では片方が0となり抑制0。鋭さ(verticalSharpness)が大きいほど
        /// 中央/地方の偏りに敏感に抑制が落ちる。
        /// </summary>
        public static float VerticalCheckStrength(float delegatedPower, float reservedPower, CompoundRepublicParams p)
        {
            float d = Mathf.Clamp01(delegatedPower);
            float r = Mathf.Clamp01(reservedPower);
            // 4×d×r は d=r=0.5 で1.0・片方0で0＝二層がともに実質を持つほど大きい
            float product = Mathf.Clamp01(4f * d * r);
            return Mathf.Clamp01(Mathf.Pow(product, p.verticalSharpness));
        }

        /// <summary>既定パラメータ版。</summary>
        public static float VerticalCheckStrength(float delegatedPower, float reservedPower)
            => VerticalCheckStrength(delegatedPower, reservedPower, CompoundRepublicParams.Default);

        /// <summary>
        /// 二重の安全保障(0..1)＝マディソンの double security。垂直（連邦/州）の抑制と
        /// 水平（三権・<see cref="SeparationOfPowersRules.CheckBalance"/> を入力に取る）の抑制が
        /// ともに効いて市民の権利を二重に守る＝相乗平均（両方が要る・片方が空なら守りは脆い）。
        /// </summary>
        /// <param name="verticalCheck">垂直抑制の強さ(0..1)。</param>
        /// <param name="horizontalCheck">水平の三権抑制（均衡度）(0..1)。</param>
        public static float DoubleSecurity(float verticalCheck, float horizontalCheck)
        {
            float v = Mathf.Clamp01(verticalCheck);
            float h = Mathf.Clamp01(horizontalCheck);
            // 相乗平均＝どちらかが0なら二重の安全保障は成立しない
            return Mathf.Clamp01(Mathf.Sqrt(v * h));
        }

        /// <summary>
        /// 二重の抑制が下げる専制リスクの係数(0..1)。二重の安全保障に比例して、
        /// <see cref="CompoundRepublicParams.maxRiskReduction"/> を上限に専制リスクを割り引く。
        /// 例：低下係数0.6なら基準の専制リスクを 0.6 ぶん減らせる（呼び出し側で乗算合成）。
        /// </summary>
        public static float TyrannyRiskReduction(float doubleSecurity, CompoundRepublicParams p)
            => Mathf.Clamp01(Mathf.Clamp01(doubleSecurity) * p.maxRiskReduction);

        /// <summary>既定パラメータ版。</summary>
        public static float TyrannyRiskReduction(float doubleSecurity)
            => TyrannyRiskReduction(doubleSecurity, CompoundRepublicParams.Default);

        /// <summary>
        /// 越権への抵抗(0..1)＝州の保留権限が中央の越権(centralOverreach)に抵抗する＝地方が砦になる。
        /// 保留権限×越権の強さ。州に権限が残るほど(保留1)、越権が大きいほど抵抗のせめぎ合いが強い。
        /// 州に何も残らなければ(保留0)中央の越権は無抵抗で通る＝0。
        /// </summary>
        /// <param name="reservedPower">州が保留した権限(0..1)。</param>
        /// <param name="centralOverreach">中央の越権の強さ(0..1)。</param>
        public static float EncroachmentResistance(float reservedPower, float centralOverreach)
            => Mathf.Clamp01(Mathf.Clamp01(reservedPower) * Mathf.Clamp01(centralOverreach));

        /// <summary>
        /// 主権の争い(0..1)＝権限の重複領域(overlap)で中央と地方が主権を争う緊張。
        /// 重複が大きく、かつ両者がともに実権を持つ（委譲も保留も実質を持つ）ほど境界紛争が激化する＝
        /// overlap × 4×委譲×保留。どちらかが空（明確な一極）なら争う相手が無く緊張0。
        /// </summary>
        /// <param name="delegatedPower">中央へ委譲された権限(0..1)。</param>
        /// <param name="reservedPower">州が保留した権限(0..1)。</param>
        /// <param name="overlap">権限の重複領域の大きさ(0..1)。</param>
        public static float SovereigntyContest(float delegatedPower, float reservedPower, float overlap)
        {
            float d = Mathf.Clamp01(delegatedPower);
            float r = Mathf.Clamp01(reservedPower);
            float o = Mathf.Clamp01(overlap);
            float bothSubstantial = Mathf.Clamp01(4f * d * r); // 両者がともに実権を持つ度合い
            return Mathf.Clamp01(o * bothSubstantial);
        }

        /// <summary>
        /// 二層主権が均衡し専制に強いか。垂直抑制の強さが
        /// <see cref="CompoundRepublicParams.balancedThreshold"/>（または指定しきい値）以上なら、
        /// 連邦と州が互いを実質的に牽制できる＝複合共和制として均衡し専制に強い＝true。
        /// </summary>
        public static bool IsBalancedFederalism(float verticalCheckStrength, float threshold)
            => Mathf.Clamp01(verticalCheckStrength) >= Mathf.Clamp01(threshold);

        /// <summary>既定パラメータ版（<see cref="CompoundRepublicParams.balancedThreshold"/> を使う）。</summary>
        public static bool IsBalancedFederalism(float verticalCheckStrength, CompoundRepublicParams p)
            => IsBalancedFederalism(verticalCheckStrength, p.balancedThreshold);

        /// <summary>既定パラメータ版。</summary>
        public static bool IsBalancedFederalism(float verticalCheckStrength)
            => IsBalancedFederalism(verticalCheckStrength, CompoundRepublicParams.Default);
    }
}
