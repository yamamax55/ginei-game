using UnityEngine;

namespace Ginei
{
    /// <summary>混合政体の三成分（#1445・ポリュビオス『歴史』第6巻）。各々が単独で堕落へ向かう。</summary>
    public enum ConstitutionComponent
    {
        王政,   // 王政的要素＝執政官（一者の支配）。堕落すると僭主政へ
        貴族政, // 貴族政的要素＝元老院（少数の支配）。堕落すると寡頭政へ
        民主政  // 民主政的要素＝民会（多数の支配）。堕落すると衆愚政へ
    }

    /// <summary>混合政体の調整係数（ctor で全項クランプ・既定は <see cref="Default"/>）。</summary>
    public readonly struct MixedConstitutionParams
    {
        /// <summary>腐落抵抗の最大値(0..1)＝完璧な混合でもこの割合までしか腐落に抵抗できない（人為の限界）。</summary>
        public readonly float maxCorruptionResistance;
        /// <summary>混合が崩れる速さの係数(0より大)＝一成分の支配が大きいほど単一形態へ退化する速度。</summary>
        public readonly float degenerationRate;
        /// <summary>バランスの取れた混合政体（ローマ型）とみなす混合バランスの既定しきい値(0..1)。</summary>
        public readonly float balancedThreshold;

        public MixedConstitutionParams(float maxCorruptionResistance, float degenerationRate, float balancedThreshold)
        {
            this.maxCorruptionResistance = Mathf.Clamp01(maxCorruptionResistance);
            this.degenerationRate = Mathf.Max(0.0001f, degenerationRate);
            this.balancedThreshold = Mathf.Clamp01(balancedThreshold);
        }

        /// <summary>既定＝腐落抵抗の上限0.9・退化係数0.5・均衡しきい値0.6。</summary>
        public static MixedConstitutionParams Default => new MixedConstitutionParams(0.9f, 0.5f, 0.6f);
    }

    /// <summary>
    /// 混合政体の安定指数の純ロジック（POLY-2 #1445・ポリュビオス『歴史』第6巻）。
    /// 「混合政体（mixed constitution）＝ローマの強さの秘密は、王政的要素（執政官）・貴族政的要素（元老院）・
    /// 民主政的要素（民会）を混ぜ合わせ、各要素が互いを牽制したことにある＝単一形態が堕落へ向かう
    /// 政体循環（アナキュクローシス）を止める。三成分のバランスが取れているほど腐落に抵抗する」を式に落とす。
    /// 三成分の混合比（<see cref="MixedBalance"/>／シャノン的混合度 <see cref="ShannonMixedness"/>）が
    /// 腐落抵抗（<see cref="CorruptionResistance"/>）を決め、三要素の相互牽制（<see cref="MutualCheck"/>）と
    /// 合わさって政体循環を止める度合い（<see cref="CycleArrest"/>）となる。一成分が突出すると混合が崩れ
    /// 単一形態の堕落リスクが増す（<see cref="Degeneration"/>）。
    /// 役割分担：<see cref="SeparationOfPowersRules"/> は同一レベル内の三権（立法/行政/司法）の水平な抑制均衡を扱い、
    /// <see cref="CompoundRepublicRules"/>（生成済み）は連邦/州の二層主権の垂直抑制を扱う別軸。
    /// こちらは王政/貴族政/民主政という三つの「政体形態」の混合が政体循環を止めること
    /// （<see cref="AnacyclosisRules"/>＝同 EPIC POLY が扱う政体循環そのものを、混合がどれだけ抑えるか）を測る。
    /// 全入力クランプ・乱数なし決定論・基準値非破壊・Mathf.Log を使わず多項式で混合度を作る。test-first。
    /// </summary>
    public static class MixedConstitutionRules
    {
        /// <summary>
        /// 三成分のバランス度 0..1＝執政官・元老院・民会の三つが均等に近いほど高い（偏ると低い）。
        /// 各成分のシェアの均等配分(1/3)からの最大偏差を、完全集中時の偏差(2/3)で正規化して 1 から引く。
        /// 三つが 1/3 ずつなら 1.0、一成分に完全集中なら 0.0。総和0（全成分0）なら測れず0。
        /// </summary>
        public static float MixedBalance(float monarchic, float aristocratic, float democratic)
        {
            float m = Mathf.Max(0f, monarchic);
            float a = Mathf.Max(0f, aristocratic);
            float d = Mathf.Max(0f, democratic);
            float sum = m + a + d;
            if (sum <= 0f) return 0f;

            float sm = m / sum;
            float sa = a / sum;
            float sd = d / sum;

            const float even = 1f / 3f;
            float maxDev = Mathf.Max(Mathf.Abs(sm - even), Mathf.Max(Mathf.Abs(sa - even), Mathf.Abs(sd - even)));
            const float maxPossibleDev = 2f / 3f; // 一成分に完全集中したときの偏差
            return Mathf.Clamp01(1f - maxDev / maxPossibleDev);
        }

        /// <summary>
        /// 三成分の混合度をシャノン的エントロピーで測る 0..1（均等＝最大1・偏り＝低下・単一＝0）。
        /// Mathf.Log がスタブに無いため、エントロピー H=−Σ p·ln p を p(1−p) の多項式で近似する
        /// （−ln p ≈ (1−p)、係数 4 で三成分均等(各1/3)の値を 1.0 へ正規化）。
        /// 三つが 1/3 ずつなら 1.0、一成分に完全集中なら 0.0。総和0なら0。
        /// </summary>
        public static float ShannonMixedness(float monarchic, float aristocratic, float democratic)
        {
            float m = Mathf.Max(0f, monarchic);
            float a = Mathf.Max(0f, aristocratic);
            float d = Mathf.Max(0f, democratic);
            float sum = m + a + d;
            if (sum <= 0f) return 0f;

            float pm = m / sum;
            float pa = a / sum;
            float pd = d / sum;

            // Σ p(1−p) は均等(各1/3)で 2/3、単一(1,0,0)で 0 ＝多項式エントロピー近似
            float h = pm * (1f - pm) + pa * (1f - pa) + pd * (1f - pd);
            const float maxH = 2f / 3f; // 三成分均等時の最大値で正規化
            return Mathf.Clamp01(h / maxH);
        }

        /// <summary>
        /// 混合のバランスが政体循環の腐落に抵抗する度合い 0..1＝混ざっているほど単一形態の堕落を防ぐ。
        /// バランス度に比例し、<see cref="MixedConstitutionParams.maxCorruptionResistance"/> を上限に抵抗する
        /// （人為の混合でも腐落を完全には止められない）。偏った政体(balance低)は単一形態の堕落に弱い。
        /// </summary>
        public static float CorruptionResistance(float mixedBalance, MixedConstitutionParams p)
            => Mathf.Clamp01(Mathf.Clamp01(mixedBalance) * p.maxCorruptionResistance);

        /// <summary>既定パラメータ版。</summary>
        public static float CorruptionResistance(float mixedBalance)
            => CorruptionResistance(mixedBalance, MixedConstitutionParams.Default);

        /// <summary>
        /// 三要素が互いを牽制する力 0..1＝一要素が突出すると他の二つが抑える。
        /// 突出成分（最大シェア）に対する他二成分の合計シェアの比でせめぎ合いを測り、
        /// 三成分のバランス度を掛けて「均等に近いほど牽制が成り立つ」を反映する＝
        /// 突出が完全（最大シェア1）なら抑える側が無く牽制0、均等なら最大。総和0なら0。
        /// </summary>
        public static float MutualCheck(float monarchic, float aristocratic, float democratic)
        {
            float m = Mathf.Max(0f, monarchic);
            float a = Mathf.Max(0f, aristocratic);
            float d = Mathf.Max(0f, democratic);
            float sum = m + a + d;
            if (sum <= 0f) return 0f;

            float maxShare = Mathf.Max(m, Mathf.Max(a, d)) / sum;
            float othersShare = 1f - maxShare; // 突出成分を抑える側のシェア合計
            float balance = MixedBalance(monarchic, aristocratic, democratic);
            return Mathf.Clamp01(othersShare * balance);
        }

        /// <summary>
        /// 混合政体が政体循環（<see cref="AnacyclosisRules"/>＝アナキュクローシス）を止める度合い 0..1。
        /// 腐落抵抗が、循環へ向かう圧力(anacyclosisPressure)に逆らって循環を押し止める＝
        /// 抵抗 ×（1 − 圧力）。圧力が無ければ抵抗そのまま、圧力が満ちれば(1)止められず0。
        /// 混ざっているほど（抵抗が高いほど）単一形態への堕落の循環を止められる。
        /// </summary>
        /// <param name="corruptionResistance">混合の腐落抵抗(0..1)。</param>
        /// <param name="anacyclosisPressure">政体循環へ向かう圧力(0..1)。</param>
        public static float CycleArrest(float corruptionResistance, float anacyclosisPressure)
        {
            float r = Mathf.Clamp01(corruptionResistance);
            float pressure = Mathf.Clamp01(anacyclosisPressure);
            return Mathf.Clamp01(r * (1f - pressure));
        }

        /// <summary>
        /// 最も強い成分（偏りの方向）＝三成分のうち最大のもの。
        /// 同値の場合は王政＞貴族政＞民主政の順で先勝ち（決定論）。総和0でも既定で王政を返す。
        /// </summary>
        public static ConstitutionComponent DominantComponent(float monarchic, float aristocratic, float democratic)
        {
            float m = Mathf.Max(0f, monarchic);
            float a = Mathf.Max(0f, aristocratic);
            float d = Mathf.Max(0f, democratic);
            if (m >= a && m >= d) return ConstitutionComponent.王政;
            if (a >= d) return ConstitutionComponent.貴族政;
            return ConstitutionComponent.民主政;
        }

        /// <summary>
        /// 混合の崩れ（堕落リスクの増分）＝一成分が支配的になると混合が崩れ単一形態の堕落リスクが増す。
        /// バランス度が低く(1−mixedBalance)、一成分の支配(oneComponentDominance)が大きいほど速く崩れる＝
        /// (1−バランス) × 支配度 × 退化係数 × dt。混合が均等(balance=1)なら崩れない。
        /// 呼び出し側で堕落リスクへ加算する想定（基準値非破壊・dt でフレームレート非依存）。
        /// </summary>
        /// <param name="mixedBalance">現在の三成分バランス度(0..1)。</param>
        /// <param name="oneComponentDominance">一成分の支配の強さ(0..1)。</param>
        /// <param name="dt">経過時間。</param>
        public static float Degeneration(float mixedBalance, float oneComponentDominance, float dt, MixedConstitutionParams p)
        {
            if (dt <= 0f) return 0f;
            float imbalance = 1f - Mathf.Clamp01(mixedBalance); // 偏りの度合い
            float dominance = Mathf.Clamp01(oneComponentDominance);
            return Mathf.Max(0f, imbalance * dominance * p.degenerationRate * dt);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float Degeneration(float mixedBalance, float oneComponentDominance, float dt)
            => Degeneration(mixedBalance, oneComponentDominance, dt, MixedConstitutionParams.Default);

        /// <summary>
        /// バランスの取れた混合政体（ローマ型＝腐落に強い）か。混合バランス度が
        /// <see cref="MixedConstitutionParams.balancedThreshold"/>（または指定しきい値）以上なら、
        /// 王政・貴族政・民主政が均衡して互いを牽制し政体循環に抗える＝true。
        /// </summary>
        public static bool IsBalancedMixedConstitution(float mixedBalance, float threshold)
            => Mathf.Clamp01(mixedBalance) >= Mathf.Clamp01(threshold);

        /// <summary>既定パラメータ版（<see cref="MixedConstitutionParams.balancedThreshold"/> を使う）。</summary>
        public static bool IsBalancedMixedConstitution(float mixedBalance, MixedConstitutionParams p)
            => IsBalancedMixedConstitution(mixedBalance, p.balancedThreshold);

        /// <summary>既定パラメータ版。</summary>
        public static bool IsBalancedMixedConstitution(float mixedBalance)
            => IsBalancedMixedConstitution(mixedBalance, MixedConstitutionParams.Default);
    }
}
