using UnityEngine;

namespace Ginei
{
    /// <summary>平等化の潮流の調整係数。</summary>
    public readonly struct EqualityDriftParams
    {
        /// <summary>民主的心性と経済的平準化を合成する平等化圧力の最大規模（両者1.0のときの値）。</summary>
        public readonly float pressureScale;
        /// <summary>身分・序列が平等化圧力1.0で1年に溶ける速さ（侵食レート。遅いが止まらない）。</summary>
        public readonly float erosionRate;
        /// <summary>一度溶けた身分の復元抵抗（0..1＝復元試行を割り引くラチェットの固さ）。</summary>
        public readonly float restorationResistance;
        /// <summary>身分の差が縮まる平準化の速さ（1年あたりの貴賤格差の低下レート）。</summary>
        public readonly float levelingRate;
        /// <summary>伝統が最強（1.0）のとき侵食を緩める係数（0..1＝名残が残る分。速度を落とすが止めない）。</summary>
        public readonly float traditionDrag;
        /// <summary>平準化が社会的流動性へ転化する係数（身分の差が縮むほど生まれより能力）。</summary>
        public readonly float mobilityScale;

        public EqualityDriftParams(float pressureScale, float erosionRate, float restorationResistance,
            float levelingRate, float traditionDrag, float mobilityScale)
        {
            this.pressureScale = Mathf.Max(0f, pressureScale);
            this.erosionRate = Mathf.Max(0f, erosionRate);
            this.restorationResistance = Mathf.Clamp01(restorationResistance);
            this.levelingRate = Mathf.Max(0f, levelingRate);
            this.traditionDrag = Mathf.Clamp01(traditionDrag);
            this.mobilityScale = Mathf.Max(0f, mobilityScale);
        }

        /// <summary>既定＝圧力規模1.0・侵食0.03/年・復元抵抗0.85・平準化0.04/年・伝統抑制0.5・流動性0.8。</summary>
        public static EqualityDriftParams Default => new EqualityDriftParams(1f, 0.03f, 0.85f, 0.04f, 0.5f, 0.8f);
    }

    /// <summary>
    /// 平等化の潮流と身分侵食の純ロジック（TOCQ-5 #1498・トクヴィル『アメリカのデモクラシー』）。
    /// 境遇の平等化（égalité des conditions）は数世紀にわたり進行する摂理的・**不可逆**な大潮流＝
    /// 民主的心性×経済的平準化の圧力が貴族制の身分・序列を徐々に溶かし、民主的な平等へ向かう。
    /// **一度溶けた身分は復元しにくい（ラチェット）＝平等化は戻らない**のが核。伝統が強いと名残は残るが
    /// 速度が緩むだけで止まらない。そして平等への情熱が自由を犠牲にしうる緊張（平等な隷従の誘惑）を孕む。
    /// 農奴解放のJ字（<see cref="SerfdomRules"/>＝労働の質と忠誠の時間動態）とも、市民権の段階
    /// （<see cref="CitizenshipRules"/>＝法的地位の付与）とも、軍の席次主義
    /// （<see cref="SeniorityRules"/>＝個別の序列）とも別系統＝こちらは身分制そのものを溶かす平等化の
    /// 不可逆な長期係数（民主化圧力の長期トレンド）。平等な隷従への帰結は同EPIC TOCQ の
    /// <see cref="SoftDespotismRules"/> が扱い、ここは身分の侵食と不可逆性の力学のみを扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EqualityDriftRules
    {
        /// <summary>
        /// 平等化の圧力（0..pressureScale）＝民主的心性 democraticSentiment(0..1)×経済的平準化
        /// economicLeveling(0..1)×規模。心性だけでも経済だけでも圧力は弱く、両者が揃うほど潮流が強まる
        /// （境遇の平等化を駆動する二つの源＝意識と財）。
        /// </summary>
        public static float EqualizationPressure(float democraticSentiment, float economicLeveling, EqualityDriftParams p)
        {
            float sentiment = Mathf.Clamp01(democraticSentiment);
            float leveling = Mathf.Clamp01(economicLeveling);
            return sentiment * leveling * p.pressureScale;
        }

        public static float EqualizationPressure(float democraticSentiment, float economicLeveling)
            => EqualizationPressure(democraticSentiment, economicLeveling, EqualityDriftParams.Default);

        /// <summary>
        /// 平等化が身分・序列を時間で溶かす（hierarchy 0..1＝身分制の強さ）。1tick後の値＝
        /// 現在の身分−圧力×侵食レート×dt（年）。遅いが累積し、貴族制を徐々に侵食する。下限0で飽和。
        /// </summary>
        public static float HierarchyErosionTick(float hierarchy, float equalizationPressure, float dt, EqualityDriftParams p)
        {
            float h = Mathf.Clamp01(hierarchy);
            float pressure = Mathf.Max(0f, equalizationPressure);
            float erosion = pressure * p.erosionRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(h - erosion);
        }

        public static float HierarchyErosionTick(float hierarchy, float equalizationPressure, float dt)
            => HierarchyErosionTick(hierarchy, equalizationPressure, dt, EqualityDriftParams.Default);

        /// <summary>
        /// 一度溶けた身分の復元しにくさ（ラチェット）。erodedHierarchy(0..1＝侵食された現在の身分)へ
        /// 復元を試みても（restorationAttempt 0..1）、復元抵抗で大きく割り引かれる＝回復できる分は
        /// attempt×(1−restorationResistance) どまり。**平等化は戻らない**を式で出す（抵抗1.0なら一切戻らない）。
        /// 返り値＝復元試行後の身分（元の侵食値＋わずかな回復、上限1）。
        /// </summary>
        public static float Irreversibility(float erodedHierarchy, float restorationAttempt, EqualityDriftParams p)
        {
            float eroded = Mathf.Clamp01(erodedHierarchy);
            float attempt = Mathf.Clamp01(restorationAttempt);
            float headroom = 1f - eroded; // まだ復元しうる上の余地
            float recovered = headroom * attempt * (1f - p.restorationResistance);
            return Mathf.Clamp01(eroded + recovered);
        }

        public static float Irreversibility(float erodedHierarchy, float restorationAttempt)
            => Irreversibility(erodedHierarchy, restorationAttempt, EqualityDriftParams.Default);

        /// <summary>
        /// 身分の差が縮まる平準化の進み（0..1）。1tick後の「平準化度」＝
        /// 平等化圧力×平準化レート×dt（年）を現在値に積む＝貴賤の差が見えにくくなる。
        /// statusLeveling は0（差が歴然）→1（差がほぼ消えた）へ単調に進む。上限1で飽和。
        /// </summary>
        public static float StatusLeveling(float statusLeveling, float equalizationPressure, float dt, EqualityDriftParams p)
        {
            float current = Mathf.Clamp01(statusLeveling);
            float pressure = Mathf.Max(0f, equalizationPressure);
            float step = pressure * p.levelingRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(current + step);
        }

        public static float StatusLeveling(float statusLeveling, float equalizationPressure, float dt)
            => StatusLeveling(statusLeveling, equalizationPressure, dt, EqualityDriftParams.Default);

        /// <summary>
        /// 貴族制の名残（0..hierarchy）。伝統が強い（traditionStrength 0..1）ほど身分の名残が残る＝
        /// 現在の身分のうち伝統が守りきる分。伝統は侵食速度を緩めるだけで止めない（traditionDrag で
        /// 効きを抑える）。返り値＝身分×伝統×(1−traditionDrag) ＝速度は緩むが0には届かない名残。
        /// </summary>
        public static float AristocraticResidue(float hierarchy, float traditionStrength, EqualityDriftParams p)
        {
            float h = Mathf.Clamp01(hierarchy);
            float tradition = Mathf.Clamp01(traditionStrength);
            return h * tradition * (1f - p.traditionDrag);
        }

        public static float AristocraticResidue(float hierarchy, float traditionStrength)
            => AristocraticResidue(hierarchy, traditionStrength, EqualityDriftParams.Default);

        /// <summary>
        /// 平等化が高める社会的流動性（0..mobilityScale）＝平準化度 statusLeveling(0..1)×規模。
        /// 身分の差が縮むほど、地位は生まれでなく能力で決まる（門地から実力へ）。
        /// </summary>
        public static float MobilityIncrease(float statusLeveling, EqualityDriftParams p)
        {
            return Mathf.Clamp01(statusLeveling) * p.mobilityScale;
        }

        public static float MobilityIncrease(float statusLeveling)
            => MobilityIncrease(statusLeveling, EqualityDriftParams.Default);

        /// <summary>
        /// トクヴィルの懸念＝平等への情熱が自由を犠牲にしうる緊張（0..1）。平等(equality 0..1)が高く
        /// 自由(liberty 0..1)が低いほど大きい＝equality×(1−liberty)。平等な隷従の誘惑が最も強い領域を示す
        /// （平等は高いが自由が痩せた社会）。次段の <see cref="SoftDespotismRules"/> への入力に使う。
        /// </summary>
        public static float EqualityVsLiberty(float equality, float liberty)
        {
            float eq = Mathf.Clamp01(equality);
            float lib = Mathf.Clamp01(liberty);
            return eq * (1f - lib);
        }

        /// <summary>
        /// 身分制が溶けて民主的平等社会になった判定。身分 hierarchy が threshold 以下＝
        /// 貴族制の序列が機能しなくなった水準まで侵食された（境遇の平等化の到達点）。
        /// </summary>
        public static bool IsEgalitarianSociety(float hierarchy, float threshold)
        {
            return Mathf.Clamp01(hierarchy) <= threshold;
        }
    }
}
