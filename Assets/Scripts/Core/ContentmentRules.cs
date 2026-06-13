using UnityEngine;

namespace Ginei
{
    /// <summary>知足（ちそく）安定の調整係数。</summary>
    public readonly struct ContentmentParams
    {
        /// <summary>適正規模ボーナスの最大上乗せ（満足が最大のときの安定ボーナス・知足者富む）。</summary>
        public readonly float maxAdequacyBonus;
        /// <summary>貪欲ペナルティの非線形度（理想超過比の冪指数・1以上）。足るを知らないほど加速して禍となる。</summary>
        public readonly float greedExponent;
        /// <summary>貪欲ペナルティの最大値（0..1・足るを知らざる禍の上限）。</summary>
        public readonly float maxGreedPenalty;
        /// <summary>小国寡民の安定重み（規模が小さいほど統治が行き届く度合い）。</summary>
        public readonly float smallStateWeight;
        /// <summary>拡大願望ドリフトの速度（per dt・外圧で知足を忘れて願望が膨らむ率）。</summary>
        public readonly float aspirationDriftRate;
        /// <summary>知足ドリフトの自然減衰（per dt・外圧が無ければ願望が静まり身の丈へ戻る率）。</summary>
        public readonly float aspirationDecayRate;
        /// <summary>身の丈の許容幅（OptimalScale から見た余裕＝統治能力にこの分だけ上乗せして適正規模とする）。</summary>
        public readonly float scaleSlack;

        public ContentmentParams(float maxAdequacyBonus, float greedExponent, float maxGreedPenalty,
            float smallStateWeight, float aspirationDriftRate, float aspirationDecayRate, float scaleSlack)
        {
            this.maxAdequacyBonus = Mathf.Max(0f, maxAdequacyBonus);
            this.greedExponent = Mathf.Max(1f, greedExponent);
            this.maxGreedPenalty = Mathf.Clamp01(maxGreedPenalty);
            this.smallStateWeight = Mathf.Clamp01(smallStateWeight);
            this.aspirationDriftRate = Mathf.Max(0f, aspirationDriftRate);
            this.aspirationDecayRate = Mathf.Max(0f, aspirationDecayRate);
            this.scaleSlack = Mathf.Clamp01(scaleSlack);
        }

        /// <summary>既定＝適正ボーナス上限0.2・貪欲冪指数2・貪欲ペナルティ上限0.5・小国寡民重み0.4・願望ドリフト0.3・知足減衰0.15・身の丈余裕0.1。</summary>
        public static ContentmentParams Default =>
            new ContentmentParams(0.2f, 2f, 0.5f, 0.4f, 0.3f, 0.15f, 0.1f);
    }

    /// <summary>
    /// 知足（ちそく）安定の純ロジック（LAOZ-3 #1554・老子参考）。「足るを知る者は富む（知足者富）」
    /// 「小国寡民＝小さな国・少ない民が理想」＝過度な拡大を求めず適正規模に満足することが安定をもたらす。
    /// 「禍は足るを知らざるより大なるは莫し（足るを知らない以上の禍はない）」＝理想を超えて拡大を求めると禍を招く。
    /// 身の丈（理想規模）に満足する国は安定し、貪欲に広げると各所が薄くなり禍となる、を式に出す。
    /// 物流（<see cref="LogisticsRules"/>＝版図が回廊で繋がる一体化度＝分断のペナルティ側）の<b>正の側を補完</b>する
    /// ＝こちらは適正規模に満足する知足のボーナス（拡大しすぎない満足の安定）。
    /// 過剰拡張（<see cref="OverextensionRules"/>＝負担/国力の比＝過拡張の負担）とは別＝あちらは規模の重さの罰、
    /// こちらは身の丈に満足することの恵み。無為（WuWeiRules・同EPIC LAOZ＝作為しない治）とも別系統。
    /// 繁栄が紐帯を溶かす（<see cref="AsabiyyaRules"/>＝集団紐帯の世代減衰）とも別＝こちらは規模への満足の安定。
    /// 全入力クランプ・乱数なし・決定論・基準非破壊（倍率は各係数に掛けて使う・実効値パターン）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ContentmentRules
    {
        /// <summary>
        /// 知足の満足度（0..1）＝現規模が理想規模に近いほど高い（身の丈＝知足）。
        /// 理想ちょうどで1、理想から離れる（過小でも過大でも）ほど下がる。
        /// 拡大しすぎ（理想超過）も満たなさすぎも不満＝足るを知るとは身の丈を知ること。
        /// </summary>
        public static float ContentmentLevel(float currentSize, float idealSize)
        {
            float cur = Mathf.Clamp01(currentSize);
            float ideal = Mathf.Clamp01(idealSize);
            float gap = Mathf.Abs(cur - ideal); // 身の丈からの乖離
            return Mathf.Clamp01(1f - gap);
        }

        /// <summary>
        /// 適正規模ボーナス（実効値≥1.0）＝知足の満足度が高いほど安定ボーナス（知足者富む）。
        /// 満足0で1.0（恵みなし）、満足1で 1＋maxAdequacyBonus。各係数（安定・統合等）に掛けて使う。
        /// </summary>
        public static float AdequacyBonus(float contentmentLevel, ContentmentParams p)
        {
            float c = Mathf.Clamp01(contentmentLevel);
            return 1f + c * p.maxAdequacyBonus;
        }

        public static float AdequacyBonus(float contentmentLevel)
            => AdequacyBonus(contentmentLevel, ContentmentParams.Default);

        /// <summary>
        /// 貪欲のペナルティ（0..maxGreedPenalty）＝理想を超えて拡大を求めるほどの禍（足るを知らざる禍）。
        /// 理想以下（身の丈の内）なら0（無傷）、理想を超えた超過分だけを冪で非線形に効かせる
        /// ＝足るを知らないほど加速して禍が大きくなる。各係数に（1−これ）を掛けて使う。
        /// </summary>
        public static float GreedPenalty(float currentSize, float idealSize, ContentmentParams p)
        {
            float cur = Mathf.Clamp01(currentSize);
            float ideal = Mathf.Clamp01(idealSize);
            if (cur <= ideal) return 0f; // 身の丈の内なら禍なし
            float excess = cur - ideal; // 理想を超えた拡大＝貪欲
            float raw = Mathf.Pow(excess, p.greedExponent);
            return Mathf.Min(p.maxGreedPenalty, raw);
        }

        public static float GreedPenalty(float currentSize, float idealSize)
            => GreedPenalty(currentSize, idealSize, ContentmentParams.Default);

        /// <summary>
        /// 小国寡民の安定（0..1）＝小さくまとまった国ほど一体化しやすく安定する。
        /// 規模が小さいほど統治が行き届く（小国寡民重みで規模の小ささを安定へ）＋一体化度がそれを支える。
        /// 大きく散らばった国は統治が行き届かない＝小さくまとまるほど安定する、を式に出す。
        /// </summary>
        public static float SmallStateStability(float size, float cohesion, ContentmentParams p)
        {
            float s = Mathf.Clamp01(size);
            float coh = Mathf.Clamp01(cohesion);
            float smallness = 1f - s; // 規模が小さいほど統治が行き届く
            // 小ささ（小国寡民重みで配合）と一体化度の両方が高いほど安定。
            float scaleTerm = Mathf.Lerp(1f, smallness, p.smallStateWeight);
            return Mathf.Clamp01(scaleTerm * coh);
        }

        public static float SmallStateStability(float size, float cohesion)
            => SmallStateStability(size, cohesion, ContentmentParams.Default);

        /// <summary>
        /// 拡大願望のドリフト（1tick後の願望 0..1）＝外圧・煽りで拡大願望が膨らむ（知足を忘れる）。
        /// 外圧が高いほど願望が膨らみ、外圧が無ければ自然減衰して身の丈（知足）へ静まる。
        /// </summary>
        public static float AspirationDrift(float aspiration, float externalPressure, float dt, ContentmentParams p)
        {
            float a = Mathf.Clamp01(aspiration);
            float pressure = Mathf.Clamp01(externalPressure);
            float t = Mathf.Max(0f, dt);
            // 外圧ぶんは願望UP（知足を忘れる）・残りぶんは知足へ減衰。
            float rise = p.aspirationDriftRate * pressure * t;
            float decay = p.aspirationDecayRate * (1f - pressure) * t;
            return Mathf.Clamp01(a + rise - decay);
        }

        public static float AspirationDrift(float aspiration, float externalPressure, float dt)
            => AspirationDrift(aspiration, externalPressure, dt, ContentmentParams.Default);

        /// <summary>
        /// 知足の安定（0..1）＝物質的充足＋知足の心で得る安定（足るを知れば富む）。
        /// 物質が乏しくても知足の心があれば安定し、物質が足りていても知足を欠けば不満が残る。
        /// 物質的充足を土台に、知足（満足度）がそれを安定へ昇華させる＝両者の積で「足るを知る者は富む」。
        /// </summary>
        public static float SatisfactionStability(float contentment, float materialSufficiency)
        {
            float c = Mathf.Clamp01(contentment);
            float m = Mathf.Clamp01(materialSufficiency);
            // 物質的充足を土台に、知足が安定へ底上げ（充足の半分＋知足が残り半分を満たす）。
            return Mathf.Clamp01(m * (0.5f + 0.5f * c));
        }

        /// <summary>
        /// 統治能力に見合った適正規模（0..1）＝能力以上に広げない（身の丈の理想規模）。
        /// 統治能力に許容幅（scaleSlack）だけ上乗せした規模が適正＝能力相応＋わずかな余裕まで。
        /// これを ContentmentLevel／GreedPenalty の idealSize に渡せば「能力に見合う身の丈」を基準にできる。
        /// </summary>
        public static float OptimalScale(float governanceCapacity, ContentmentParams p)
        {
            float cap = Mathf.Clamp01(governanceCapacity);
            return Mathf.Clamp01(cap + p.scaleSlack);
        }

        public static float OptimalScale(float governanceCapacity)
            => OptimalScale(governanceCapacity, ContentmentParams.Default);

        /// <summary>
        /// 身の丈を超えて拡大しすぎたか＝現規模が理想規模を threshold 以上に超過したら true（過拡張＝足るを知らず）。
        /// 理想の内、または超過が許容幅以内なら false（知足の内）。
        /// </summary>
        public static bool IsOverreaching(float currentSize, float idealSize, float threshold)
        {
            float cur = Mathf.Clamp01(currentSize);
            float ideal = Mathf.Clamp01(idealSize);
            float th = Mathf.Max(0f, threshold);
            return cur - ideal > th;
        }
    }
}
