using UnityEngine;

namespace Ginei
{
    /// <summary>複数性と公的領域の純データ＝視点の多様性・原子化・共に行動する力（アーレント）。</summary>
    public struct PoliticalSpace
    {
        /// <summary>視点の多様性（perspectiveDiversity 0..1）＝人々が皆異なる視点を持つ度合い。これが公的領域を成り立たせる。</summary>
        public float perspectiveDiversity;
        /// <summary>原子化（atomization 0..1）＝人々が孤立した原子に分断された度合い。全体主義はこれを進めて複数性を破壊する。</summary>
        public float atomization;
        /// <summary>共に行動する力（actionCapacity 0..1）＝人々が共に語り行動する力。原子化はこれを奪う。</summary>
        public float actionCapacity;

        public PoliticalSpace(float perspectiveDiversity, float atomization, float actionCapacity)
        {
            this.perspectiveDiversity = Mathf.Clamp01(perspectiveDiversity);
            this.atomization = Mathf.Clamp01(atomization);
            this.actionCapacity = Mathf.Clamp01(actionCapacity);
        }
    }

    /// <summary>複数性と公的領域の調整係数。</summary>
    public readonly struct PluralityParams
    {
        /// <summary>孤立・恐怖が人々を原子化する速さ（per dt・全体主義の手口）。</summary>
        public readonly float atomizationRate;
        /// <summary>画一化圧力が視点の多様性を削る速さ（per dt・複数性の侵食）。</summary>
        public readonly float erosionRate;
        /// <summary>共に行動する力から（暴力でない）権力が生まれる強さ（アーレントの権力観）。</summary>
        public readonly float powerScale;
        /// <summary>孤独・無意味感が全体主義イデオロギーへの脆弱性を生む強さ。</summary>
        public readonly float vulnerabilityScale;

        public PluralityParams(float atomizationRate, float erosionRate,
                               float powerScale, float vulnerabilityScale)
        {
            this.atomizationRate = Mathf.Max(0f, atomizationRate);
            this.erosionRate = Mathf.Max(0f, erosionRate);
            this.powerScale = Mathf.Clamp01(powerScale);
            this.vulnerabilityScale = Mathf.Clamp01(vulnerabilityScale);
        }

        /// <summary>既定＝原子化速度0.06・侵食速度0.05・権力係数0.9・脆弱性係数0.8。</summary>
        public static PluralityParams Default => new PluralityParams(0.06f, 0.05f, 0.9f, 0.8f);
    }

    /// <summary>
    /// 複数性と公的領域の純ロジック（BNAL-2 #1532・アーレント『全体主義の起原』『人間の条件』参考）。
    /// 複数性（plurality）＝人間は皆異なる視点を持ち、その多様性が公的領域（政治の空間）を成り立たせる。
    /// 視点の多様性×自由な集会が公的領域の活力を生み、人々が共に行動するときに（暴力でない）権力が生まれる。
    /// 全体主義は人々を孤立した原子（atomization）にして複数性を破壊し、共に行動する力（action capacity）を奪う
    /// ＝孤立と恐怖が人々を原子化し、画一化圧力が視点の多様性を削り、孤独と無意味感が全体主義イデオロギーへの
    /// 脆弱性を生む。共通の関心からの自発的結社（市民社会）は原子化の逆をいく。
    /// <see cref="FreePressRules"/>（報道による腐敗の監視）・<see cref="PreferenceFalsificationRules"/>（選好偽装＝
    /// 表明と本音の乖離）とは別＝こちらは「複数性の喪失と公的領域の崩壊（共に行動する力）」を扱う。同 EPIC BNAL の
    /// <see cref="ThoughtlessnessRules"/>（悪の凡庸性＝無思考の加担）・<see cref="TotalitarianRules"/>（全体主義の
    /// 支配機構）とも分担。乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PluralityRules
    {
        /// <summary>
        /// 公的領域の活力（0..1）＝視点の多様性 perspectiveDiversity × 自由な集会 freeAssembly(0..1)。
        /// 異なる視点を持つ人々が自由に集い語り合うとき政治の空間が立ち上がる（複数性が政治空間を生む）。
        /// どちらか欠ければ公的領域は痩せる（積＝両方が要る）。
        /// </summary>
        public static float PublicRealmVitality(float perspectiveDiversity, float freeAssembly)
        {
            return Mathf.Clamp01(perspectiveDiversity) * Mathf.Clamp01(freeAssembly);
        }

        /// <summary>
        /// 共に行動する力（0..1）＝視点の多様性が高く、原子化が低いほど共に行動できる。
        /// ＝perspectiveDiversity×(1−atomization)。孤立した原子に分断されると（多様性があっても）行動できない
        /// ＝孤立は共に行動する力を奪う（全体主義の狙い）。
        /// </summary>
        public static float ActionCapacity(float perspectiveDiversity, float atomization)
        {
            return Mathf.Clamp01(perspectiveDiversity) * (1f - Mathf.Clamp01(atomization));
        }

        /// <summary>
        /// 原子化の1tick後の値（0..1）。孤立 isolation(0..1) と恐怖 terror(0..1) が人々を原子化する
        /// （atomizationRate×(孤立+恐怖)の平均×dt ずつ上昇）。全体主義は孤立と恐怖で人々を原子に砕く手口。
        /// </summary>
        public static float AtomizationTick(float atomization, float isolation, float terror, float dt, PluralityParams p)
        {
            float drivers = (Mathf.Clamp01(isolation) + Mathf.Clamp01(terror)) * 0.5f;
            float delta = p.atomizationRate * drivers * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(atomization) + delta);
        }

        public static float AtomizationTick(float atomization, float isolation, float terror, float dt)
            => AtomizationTick(atomization, isolation, terror, dt, PluralityParams.Default);

        /// <summary>
        /// 複数性の侵食＝視点の多様性の1tick後の値（0..1）。画一化圧力 conformityPressure(0..1) が
        /// 視点の多様性を削る（erosionRate×conformityPressure×dt ずつ低下）。画一化は複数性を殺す。
        /// </summary>
        public static float PluralityErosion(float perspectiveDiversity, float conformityPressure, float dt, PluralityParams p)
        {
            float delta = p.erosionRate * Mathf.Clamp01(conformityPressure) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(perspectiveDiversity) - delta);
        }

        public static float PluralityErosion(float perspectiveDiversity, float conformityPressure, float dt)
            => PluralityErosion(perspectiveDiversity, conformityPressure, dt, PluralityParams.Default);

        /// <summary>
        /// 全体主義状態の判定。原子化が進み（threshold 以上）、かつ複数性が失われた（視点の多様性が
        /// 1−threshold 未満）とき true。全体主義は原子化で複数性を破壊し共に行動する力を奪った状態。
        /// </summary>
        public static bool IsTotalitarian(float atomization, float perspectiveDiversity, float threshold)
        {
            float t = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(atomization) >= t
                   && Mathf.Clamp01(perspectiveDiversity) < (1f - t);
        }

        /// <summary>
        /// 自発的結社（0..1）＝共に行動する力 actionCapacity × 共通の関心 sharedConcern(0..1)。
        /// 共通の関心を持つ人々が自ら集って結社を生む（市民社会＝原子化の逆）。
        /// 行動する力が無いか関心が無ければ結社は生まれない（積）。
        /// </summary>
        public static float SpontaneousAssociation(float actionCapacity, float sharedConcern)
        {
            return Mathf.Clamp01(actionCapacity) * Mathf.Clamp01(sharedConcern);
        }

        /// <summary>
        /// 共にある権力（0..1）＝共に行動する力 actionCapacity から（暴力でない）権力が生まれる
        /// ＝actionCapacity×powerScale。アーレントの権力観＝権力は人々が共に行動するときに生まれる
        /// （武力＝violence とは別物＝孤立した原子からは権力は生まれない）。
        /// </summary>
        public static float PowerFromTogetherness(float actionCapacity, PluralityParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(actionCapacity) * p.powerScale);
        }

        public static float PowerFromTogetherness(float actionCapacity)
            => PowerFromTogetherness(actionCapacity, PluralityParams.Default);

        /// <summary>
        /// 孤独の脆弱性（0..1）＝孤独 isolation(0..1) と無意味感 meaninglessness(0..1) が全体主義
        /// イデオロギーへの脆弱性を生む。＝(孤独+無意味感)の平均×脆弱性係数。
        /// 孤立し意味を失った人ほど全体主義の物語に飛びつく（複数性の喪失が招く脆さ）。
        /// </summary>
        public static float LonelinessVulnerability(float isolation, float meaninglessness, PluralityParams p)
        {
            float drivers = (Mathf.Clamp01(isolation) + Mathf.Clamp01(meaninglessness)) * 0.5f;
            return Mathf.Clamp01(drivers * p.vulnerabilityScale);
        }

        public static float LonelinessVulnerability(float isolation, float meaninglessness)
            => LonelinessVulnerability(isolation, meaninglessness, PluralityParams.Default);
    }
}
