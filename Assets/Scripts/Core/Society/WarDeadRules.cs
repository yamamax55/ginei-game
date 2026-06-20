using UnityEngine;

namespace Ginei
{
    /// <summary>戦没者の弔いと社会への影響の調整係数。</summary>
    public readonly struct WarDeadParams
    {
        /// <summary>戦死者比（死者/人口）を重みへ変換するスケール（比1%で重み≈これ。比は小さいので増幅する）。</summary>
        public readonly float casualtyScale;
        /// <summary>補償水準1あたりの遺族補償負担スケール（負担＝補償水準×死者比×これ）。</summary>
        public readonly float compensationScale;
        /// <summary>慰霊の努力の重み（弔い＝努力×これ＋顕彰×顕彰重み）。</summary>
        public readonly float memorialWeight;
        /// <summary>顕彰（公的な讃え）の重み。</summary>
        public readonly float honorWeight;
        /// <summary>政治利用の効きやすさ（英霊の物語×政権の思惑×これ）。</summary>
        public readonly float politicizeScale;
        /// <summary>累積厭戦の戦争長さへの効き（戦死重み×長さ×これ）。</summary>
        public readonly float wearinessScale;
        /// <summary>社会結束における英霊の物語の効き（団結側）。</summary>
        public readonly float cohesionNarrativeWeight;
        /// <summary>社会結束における累積厭戦の効き（分断側＝物語より重い＝死は最後は厭戦へ傾く）。</summary>
        public readonly float cohesionWearinessWeight;

        public WarDeadParams(float casualtyScale, float compensationScale, float memorialWeight, float honorWeight,
            float politicizeScale, float wearinessScale, float cohesionNarrativeWeight, float cohesionWearinessWeight)
        {
            this.casualtyScale = Mathf.Max(0f, casualtyScale);
            this.compensationScale = Mathf.Max(0f, compensationScale);
            this.memorialWeight = Mathf.Clamp01(memorialWeight);
            this.honorWeight = Mathf.Clamp01(honorWeight);
            this.politicizeScale = Mathf.Max(0f, politicizeScale);
            this.wearinessScale = Mathf.Max(0f, wearinessScale);
            this.cohesionNarrativeWeight = Mathf.Clamp01(cohesionNarrativeWeight);
            this.cohesionWearinessWeight = Mathf.Clamp01(cohesionWearinessWeight);
        }

        /// <summary>既定＝戦死スケール50・補償スケール1.0・慰霊重み0.6・顕彰重み0.4・政治化1.0・厭戦0.5・結束物語重み0.8・結束厭戦重み1.0。</summary>
        public static WarDeadParams Default => new WarDeadParams(50f, 1f, 0.6f, 0.4f, 1f, 0.5f, 0.8f, 1f);
    }

    /// <summary>
    /// 戦没者（戦死者）の弔いと社会への影響の純ロジック。戦死者数の重み（人口比）、遺族への補償負担、
    /// 慰霊と顕彰による弔い、弔い×犠牲の意味づけで生まれる英霊の物語（死を無駄にしない）、その物語の政治利用、
    /// 累積戦死者による厭戦、世代を超える戦没者の記憶、社会結束への影響（団結か厭戦か）を扱う。
    /// 英霊の物語は短期には社会を団結させるが、戦死の重みが積もれば厭戦が物語を上回り結束は崩れる＝死は最後に厭戦へ傾く。
    /// 乱数なし・決定論・入力クランプ・実効値パターン。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WarDeadRules
    {
        /// <summary>
        /// 戦死者の社会への損失の重み（0..1）＝戦死者比（死者/人口）×戦死スケール。
        /// 人口比が小さくても多数の死は社会を蝕む＝スケールで増幅し 1 で飽和。人口0以下なら重み0（母数不明＝計上しない）。
        /// </summary>
        public static float CasualtyWeight(float deathCount, float populationBase, WarDeadParams p)
        {
            float deaths = Mathf.Max(0f, deathCount);
            float pop = Mathf.Max(0f, populationBase);
            if (pop <= 0f) return 0f;
            float ratio = deaths / pop;
            return Mathf.Clamp01(ratio * p.casualtyScale);
        }

        public static float CasualtyWeight(float deathCount, float populationBase)
            => CasualtyWeight(deathCount, populationBase, WarDeadParams.Default);

        /// <summary>
        /// 遺族への補償負担（0..1）＝補償水準(0..1)×戦死者比(0..1)×補償スケール。
        /// 手厚い補償を多数の遺族へ払うほど財政を圧迫＝1 で飽和。死者比は CasualtyWeight 前の生の比（死者/人口）。
        /// </summary>
        public static float BereavedCompensation(float compensationLevel, float deathCount, float populationBase, WarDeadParams p)
        {
            float level = Mathf.Clamp01(compensationLevel);
            float deaths = Mathf.Max(0f, deathCount);
            float pop = Mathf.Max(0f, populationBase);
            if (pop <= 0f) return 0f;
            float ratio = Mathf.Clamp01(deaths / pop);
            return Mathf.Clamp01(level * ratio * p.compensationScale);
        }

        public static float BereavedCompensation(float compensationLevel, float deathCount, float populationBase)
            => BereavedCompensation(compensationLevel, deathCount, populationBase, WarDeadParams.Default);

        /// <summary>
        /// 戦没者の弔い（0..1）＝慰霊の努力(0..1)×慰霊重み＋顕彰(0..1)×顕彰重み。
        /// 努力（墓・式典）と公的な讃え（叙勲・記念）の合成。既定は慰霊重み&gt;顕彰重み＝弔いの本体は努力。
        /// </summary>
        public static float Commemoration(float memorialEffort, float publicHonor, WarDeadParams p)
        {
            float effort = Mathf.Clamp01(memorialEffort);
            float honor = Mathf.Clamp01(publicHonor);
            return Mathf.Clamp01(effort * p.memorialWeight + honor * p.honorWeight);
        }

        public static float Commemoration(float memorialEffort, float publicHonor)
            => Commemoration(memorialEffort, publicHonor, WarDeadParams.Default);

        /// <summary>
        /// 英霊の物語（0..1）＝弔い(0..1)×犠牲の意味づけ(0..1)。
        /// 手厚く弔っても「何のための死か」を意味づけられなければ物語にならない＝両方が要る（積）。
        /// </summary>
        public static float HeroicNarrative(float commemoration, float sacrificeMeaning, WarDeadParams p)
        {
            float comm = Mathf.Clamp01(commemoration);
            float meaning = Mathf.Clamp01(sacrificeMeaning);
            return Mathf.Clamp01(comm * meaning);
        }

        public static float HeroicNarrative(float commemoration, float sacrificeMeaning)
            => HeroicNarrative(commemoration, sacrificeMeaning, WarDeadParams.Default);

        /// <summary>
        /// 戦死者の政治利用（0..1）＝英霊の物語(0..1)×政権の思惑(0..1)×政治化スケール。
        /// 物語が強く政権が利用を望むほど死者は政治の道具になる＝英霊を旗印に統治を正当化する。
        /// </summary>
        public static float PoliticalUseOfDead(float heroicNarrative, float regimeAgenda, WarDeadParams p)
        {
            float narrative = Mathf.Clamp01(heroicNarrative);
            float agenda = Mathf.Clamp01(regimeAgenda);
            return Mathf.Clamp01(narrative * agenda * p.politicizeScale);
        }

        public static float PoliticalUseOfDead(float heroicNarrative, float regimeAgenda)
            => PoliticalUseOfDead(heroicNarrative, regimeAgenda, WarDeadParams.Default);

        /// <summary>
        /// 累積厭戦（0..1）＝戦死者の重み(0..1)×戦争の長さ(0..1)×厭戦スケール。
        /// 戦死が重く戦争が長引くほど社会は戦いに倦む＝1 で飽和。戦争の長さは正規化済み（0=開戦直後/1=長期化）。
        /// </summary>
        public static float AccumulatedWarWeariness(float casualtyWeight, float warDuration, WarDeadParams p)
        {
            float weight = Mathf.Clamp01(casualtyWeight);
            float duration = Mathf.Clamp01(warDuration);
            return Mathf.Clamp01(weight * duration * p.wearinessScale);
        }

        public static float AccumulatedWarWeariness(float casualtyWeight, float warDuration)
            => AccumulatedWarWeariness(casualtyWeight, warDuration, WarDeadParams.Default);

        /// <summary>
        /// 戦没者の記憶（0..1）＝弔い(0..1)×世代継承(0..1)。
        /// 弔っても次世代へ語り継がれなければ記憶は薄れる＝両方が要る（積）。無名兵士も継承されれば記憶に残る。
        /// </summary>
        public static float CollectiveMemory(float commemoration, float generationalTransmission, WarDeadParams p)
        {
            float comm = Mathf.Clamp01(commemoration);
            float transmission = Mathf.Clamp01(generationalTransmission);
            return Mathf.Clamp01(comm * transmission);
        }

        public static float CollectiveMemory(float commemoration, float generationalTransmission)
            => CollectiveMemory(commemoration, generationalTransmission, WarDeadParams.Default);

        /// <summary>
        /// 社会結束への影響（-1..1）＝英霊の物語×物語重み − 累積厭戦×厭戦重み。
        /// 正＝死を意味づけて団結／負＝積もる死で厭戦・分断。既定は厭戦重み≥物語重み＝死は最後に厭戦へ傾く。
        /// </summary>
        public static float SocialCohesionImpact(float heroicNarrative, float accumulatedWarWeariness, WarDeadParams p)
        {
            float narrative = Mathf.Clamp01(heroicNarrative);
            float weariness = Mathf.Clamp01(accumulatedWarWeariness);
            float net = narrative * p.cohesionNarrativeWeight - weariness * p.cohesionWearinessWeight;
            return Mathf.Clamp(net, -1f, 1f);
        }

        public static float SocialCohesionImpact(float heroicNarrative, float accumulatedWarWeariness)
            => SocialCohesionImpact(heroicNarrative, accumulatedWarWeariness, WarDeadParams.Default);
    }
}
