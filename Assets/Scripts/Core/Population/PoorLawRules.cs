using UnityEngine;

namespace Ginei
{
    /// <summary>救貧の人口学的逆説の調整係数（MALT-4 #1580・マルサス『人口論』の救貧法批判）。</summary>
    public readonly struct PoorLawParams
    {
        /// <summary>救貧水準→短期に貧困を和らげる効き（福祉が今の困窮をどれだけ吸収するか）。</summary>
        public readonly float reliefStrength;
        /// <summary>救貧水準→出生刺激の感度（食べられると子を持てる＝人口増の引き金の強さ）。</summary>
        public readonly float fertilitySensitivity;
        /// <summary>刺激された出生が人口を増やす基礎速度（年あたりの人口成長係数）。</summary>
        public readonly float populationGrowthRate;
        /// <summary>人口増→一人あたり食糧・賃金を薄める強さ（マルサス＝需要増で価格上昇）。</summary>
        public readonly float dilutionStrength;
        /// <summary>救貧が出生に響きやすいほど逆説が強い人口弾力性の重み。</summary>
        public readonly float paradoxScale;
        /// <summary>長期に救済水準に関わらず収束する生存水準（マルサスの鉄則の下限）。</summary>
        public readonly float subsistenceLevel;
        /// <summary>生存水準へ収束する速度（年あたり・救済の効果が長期に剥がれる速さ）。</summary>
        public readonly float subsistencePull;
        /// <summary>救済が自らを無効化した「福祉の罠」とみなす純効果の既定しきい値。</summary>
        public readonly float welfareTrapThreshold;

        public PoorLawParams(float reliefStrength, float fertilitySensitivity, float populationGrowthRate,
                             float dilutionStrength, float paradoxScale, float subsistenceLevel,
                             float subsistencePull, float welfareTrapThreshold)
        {
            this.reliefStrength = Mathf.Clamp01(reliefStrength);
            this.fertilitySensitivity = Mathf.Max(0f, fertilitySensitivity);
            this.populationGrowthRate = Mathf.Max(0f, populationGrowthRate);
            this.dilutionStrength = Mathf.Max(0f, dilutionStrength);
            this.paradoxScale = Mathf.Max(0f, paradoxScale);
            this.subsistenceLevel = Mathf.Clamp01(subsistenceLevel);
            this.subsistencePull = Mathf.Max(0f, subsistencePull);
            this.welfareTrapThreshold = welfareTrapThreshold;
        }

        /// <summary>既定＝救済効き0.8・出生感度0.6・人口成長0.05・希釈強度0.5・逆説重み1.0・
        /// 生存水準0.3・生存収束0.1・福祉の罠しきい値0.0（純効果が負＝救済が自らを無効化）。</summary>
        public static PoorLawParams Default =>
            new PoorLawParams(0.8f, 0.6f, 0.05f, 0.5f, 1.0f, 0.3f, 0.1f, 0.0f);
    }

    /// <summary>
    /// 救貧の人口学的逆説の純ロジック（MALT-4 #1580・マルサス『人口論』の<b>救貧法批判</b>）。
    /// 福祉（救貧）は短期に貧者を救うが、食べられるようになることで<b>出生を刺激</b>して人口を増やし、
    /// 増えた人口が一人あたりの食糧・賃金を薄める（需要増で食糧価格が上がる）ため、長期には救済の効果が帳消しになる
    /// ＝「救貧は短期に貧者を救うが出生を刺激し人口を増やし長期に賃金を帳消しにする＝慈悲が人口圧を高めて
    /// 自らを無効化する逆説」を式に出す。短期の善（救済）が長期の停滞（生存水準への収束）を生む。
    /// 税による所得の<b>再分配</b>は <see cref="RedistributionRules"/>、市場圧力への<b>保護ラチェット</b>は
    /// <see cref="SocialProtectionRules"/>、人口の<b>予防的・積極的チェック</b>（マルサスの人口抑制機構そのもの）は
    /// <see cref="MalthusianCheckRules"/>（同 EPIC MALT）が扱い、ここは救貧の<b>人口学的逆説</b>
    /// （福祉→出生刺激→人口増→賃金帳消し）のみを扱う。係数は基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 乱数なし・決定論。全入力クランプ。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PoorLawRules
    {
        /// <summary>
        /// 救貧が短期に貧困を和らげる効果（0..1＝今すぐ削れる貧困量）。救貧水準（welfareLevel 0..1）と
        /// 現在の貧困（poverty 0..1）の積に効きを掛ける＝貧困が深いほど、また救済が手厚いほど短期の救いは大きい
        /// （貧困がなければ救う相手もいない）。これは<b>短期</b>の善であり、長期は <see cref="WageDilution"/> が打ち消す。
        /// </summary>
        public static float ShortTermRelief(float welfareLevel, float poverty, PoorLawParams p)
        {
            float w = Mathf.Clamp01(welfareLevel);
            float pov = Mathf.Clamp01(poverty);
            return Mathf.Clamp01(p.reliefStrength * w * pov);
        }

        public static float ShortTermRelief(float welfareLevel, float poverty)
            => ShortTermRelief(welfareLevel, poverty, PoorLawParams.Default);

        /// <summary>
        /// 救済が出生を刺激する度合い（0..1）。救貧水準が高いほど大きい＝食べられるようになると子を持てる
        /// （マルサス＝救済が需要側で結婚・出産の障壁を下げる）。これが人口増の引き金で、逆説の発火点。
        /// </summary>
        public static float FertilityStimulus(float welfareLevel, PoorLawParams p)
        {
            float w = Mathf.Clamp01(welfareLevel);
            return Mathf.Clamp01(p.fertilitySensitivity * w);
        }

        public static float FertilityStimulus(float welfareLevel)
            => FertilityStimulus(welfareLevel, PoorLawParams.Default);

        /// <summary>
        /// 刺激された出生が人口を時間で増やす（1tick後の人口）。現在人口に、出生刺激（fertilityStimulus 0..1）に
        /// 比例した成長を掛けて増やす＝慈悲が人口圧を高める過程そのもの。dt は年単位。人口は非負。
        /// </summary>
        public static float PopulationPressureTick(float population, float fertilityStimulus, float dt, PoorLawParams p)
        {
            float pop = Mathf.Max(0f, population);
            float stim = Mathf.Clamp01(fertilityStimulus);
            float growth = p.populationGrowthRate * stim * Mathf.Max(0f, dt);
            return Mathf.Max(0f, pop * (1f + growth));
        }

        public static float PopulationPressureTick(float population, float fertilityStimulus, float dt)
            => PopulationPressureTick(population, fertilityStimulus, dt, PoorLawParams.Default);

        /// <summary>
        /// 人口増が一人あたり食糧・賃金を薄める量（0..1＝救済から差し引かれる希釈）。人口が固定の食糧供給
        /// （foodSupply 0..1）に対して過剰なほど大きい＝需要が供給を上回るとマルサス的に価格が上がり賃金が痩せる
        /// （人口/食糧の超過比に希釈強度を掛ける）。これが長期に救済を帳消しにする力。
        /// </summary>
        public static float WageDilution(float population, float foodSupply, PoorLawParams p)
        {
            float pop = Mathf.Max(0f, population);
            float food = Mathf.Max(0.0001f, Mathf.Clamp01(foodSupply)); // ゼロ除算回避＝供給ほぼゼロは飢餓
            float overshoot = Mathf.Max(0f, pop / food - 1f); // 食糧1単位あたり人口の超過分
            return Mathf.Clamp01(p.dilutionStrength * overshoot);
        }

        public static float WageDilution(float population, float foodSupply)
            => WageDilution(population, foodSupply, PoorLawParams.Default);

        /// <summary>
        /// 救貧の純効果（-1..1）＝短期救済（shortTermRelief 0..1）−長期の賃金帳消し（wageDilution 0..1）。
        /// 初期は希釈が小さく正（救済が効く）だが、人口が増えて希釈が育つと負へ反転する
        /// ＝時間で正→負へ転じる救貧の逆説の核心（短期の善が長期に帳消しになる）。
        /// </summary>
        public static float NetWelfareEffect(float shortTermRelief, float wageDilution)
        {
            float relief = Mathf.Clamp01(shortTermRelief);
            float dilution = Mathf.Clamp01(wageDilution);
            return Mathf.Clamp(relief - dilution, -1f, 1f);
        }

        /// <summary>
        /// 逆説の強さ（0..1）。救貧水準が高く、かつ人口弾力性（populationElasticity 0..1＝救済が出生に響きやすさ）が
        /// 高いほど大きい＝救済が出生に直結する社会ほど慈悲が早く人口圧へ転化し逆説が強い（弾力性ゼロなら逆説なし）。
        /// </summary>
        public static float ParadoxIndex(float welfareLevel, float populationElasticity, PoorLawParams p)
        {
            float w = Mathf.Clamp01(welfareLevel);
            float e = Mathf.Clamp01(populationElasticity);
            return Mathf.Clamp01(p.paradoxScale * w * e);
        }

        public static float ParadoxIndex(float welfareLevel, float populationElasticity)
            => ParadoxIndex(welfareLevel, populationElasticity, PoorLawParams.Default);

        /// <summary>
        /// 長期の一人あたり生活水準の収束（1tick後の生活水準 0..1）。救済で一時的に上がった生活水準は、
        /// 救済水準に関わらず時間とともに生存水準（subsistenceLevel）へ引き戻される＝マルサスの鉄則
        /// （人口が増えて余剰を食い潰し、長期には誰もが生存ぎりぎりへ収束する）。dt は年単位。
        /// 救済水準が高いほど収束先がわずかに持ち上がる（救済が完全に無意味ではないが鉄則は支配的）。
        /// </summary>
        public static float LongRunSubsistence(float welfareLevel, float currentLivingStandard, float dt, PoorLawParams p)
        {
            float w = Mathf.Clamp01(welfareLevel);
            float current = Mathf.Clamp01(currentLivingStandard);
            // 収束先＝生存水準を基準に、救済ぶんをわずかに上乗せ（鉄則が支配＝大半は生存水準へ）
            float target = Mathf.Clamp01(p.subsistenceLevel + (1f - p.subsistenceLevel) * 0.25f * w);
            float t = Mathf.Clamp01(p.subsistencePull * Mathf.Max(0f, dt));
            return Mathf.Clamp01(Mathf.Lerp(current, target, t));
        }

        public static float LongRunSubsistence(float welfareLevel, float currentLivingStandard, float dt)
            => LongRunSubsistence(welfareLevel, currentLivingStandard, dt, PoorLawParams.Default);

        /// <summary>
        /// 救済が自らを無効化した「福祉の罠」か（純効果がしきい値を下回ったか）。純効果（netWelfareEffect）が
        /// しきい値（既定0＝負へ反転）を割ると、救済は人口圧で賃金を帳消しにし自らを無効化した罠に陥った
        /// ＝慈悲が逆説に飲まれた状態（既定しきい値は <see cref="PoorLawParams"/>）。
        /// </summary>
        public static bool IsWelfareTrap(float netWelfareEffect, float threshold)
            => Mathf.Clamp(netWelfareEffect, -1f, 1f) < threshold;

        public static bool IsWelfareTrap(float netWelfareEffect)
            => IsWelfareTrap(netWelfareEffect, PoorLawParams.Default.welfareTrapThreshold);
    }
}
