using UnityEngine;

namespace Ginei
{
    /// <summary>市民の自衛武装組織（民兵・自警団）の調整係数。</summary>
    public readonly struct MilitiaParams
    {
        /// <summary>地元人口が動員速度を押し上げる重み（0..1・人口が多いほど頭数が早く揃う）。</summary>
        public readonly float populationWeight;
        /// <summary>地元防衛士気の基礎値（0..1・脅威も絆もゼロでも郷土を守る最低限の戦意）。</summary>
        public readonly float baseMorale;
        /// <summary>郷土愛が士気を押し上げる重み（0..1・脅威×絆＝故郷を侵される怒りで燃える）。</summary>
        public readonly float patriotismWeight;
        /// <summary>訓練不足の倍率（0..1・正規訓練の欠如がそのまま質の低さに化ける度合い）。</summary>
        public readonly float deficiencyScale;
        /// <summary>農繁期が生産両立コストを増幅する重み（0..1・畑が忙しい時期に民を兵にする痛み）。</summary>
        public readonly float harvestWeight;
        /// <summary>長期戦負担の非線形度（戦役の長さの冪指数・1以上）。長引くほど加速して耐えられない。</summary>
        public readonly float protractedExponent;
        /// <summary>民間の義務が長期戦弱点を増幅する重み（0..1・家業・農地への責務が継戦を蝕む）。</summary>
        public readonly float obligationWeight;
        /// <summary>地の利が戦闘力を押し上げる重み（0..1・庭先の地形熟知が劣勢を覆す度合い）。</summary>
        public readonly float homeGroundWeight;
        /// <summary>本土防衛価値から長期戦弱点を差し引く重み（0..1・短期決戦向きゆえ持久で価値が減る）。</summary>
        public readonly float weaknessWeight;

        public MilitiaParams(float populationWeight, float baseMorale, float patriotismWeight,
            float deficiencyScale, float harvestWeight, float protractedExponent,
            float obligationWeight, float homeGroundWeight, float weaknessWeight)
        {
            this.populationWeight = Mathf.Clamp01(populationWeight);
            this.baseMorale = Mathf.Clamp01(baseMorale);
            this.patriotismWeight = Mathf.Clamp01(patriotismWeight);
            this.deficiencyScale = Mathf.Clamp01(deficiencyScale);
            this.harvestWeight = Mathf.Clamp01(harvestWeight);
            this.protractedExponent = Mathf.Max(1f, protractedExponent);
            this.obligationWeight = Mathf.Clamp01(obligationWeight);
            this.homeGroundWeight = Mathf.Clamp01(homeGroundWeight);
            this.weaknessWeight = Mathf.Clamp01(weaknessWeight);
        }

        /// <summary>既定＝人口重み0.5・基礎士気0.3・郷土愛重み1・訓練不足倍率0.8・農繁重み0.6・長期戦冪1.5・義務重み0.5・地の利重み0.5・弱点重み0.5。</summary>
        public static MilitiaParams Default =>
            new MilitiaParams(0.5f, 0.3f, 1f, 0.8f, 0.6f, 1.5f, 0.5f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 民兵・自警団の純ロジック（市民の自衛武装組織）。市民が武器を取って郷土を守る組織の
    /// 強みと弱みを式に出す＝動員の早さ（頭数はすぐ揃う）、地元防衛の士気（郷土愛で燃える）、
    /// 訓練不足による質の低さ（正規軍に劣る）、平時の生産との両立コスト（民を兵にすると畑が荒れる）、
    /// 長期戦への不向き（持久戦で崩れる）、地の利（庭先の地形熟知）、本土防衛への価値（攻めには使えぬが守りには厚い）。
    /// 正規軍・傭兵・徴兵とは別系統＝市民が自発的に郷土を守る組織の特性を扱う。
    /// 倍率は各係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MilitiaRules
    {
        /// <summary>
        /// 動員の早さ（0..1）。地域の即応性×地元人口で民兵がどれだけ早く頭数を揃えるか＝
        /// 平時から備えがあり人口が多いほど一気に集まる。即応性が低ければ人口があっても動かない（積）。
        /// </summary>
        public static float MobilizationSpeed(float communityReadiness, float localPopulation,
            MilitiaParams p)
        {
            float readiness = Mathf.Clamp01(communityReadiness);
            float population = Mathf.Clamp01(localPopulation);
            return Mathf.Clamp01(readiness * (1f + population * p.populationWeight));
        }

        public static float MobilizationSpeed(float communityReadiness, float localPopulation)
            => MobilizationSpeed(communityReadiness, localPopulation, MilitiaParams.Default);

        /// <summary>
        /// 地元防衛の士気（0..1・郷土愛）。郷土への脅威×地域の絆で故郷を守る戦意が燃える＝
        /// 基礎士気に、侵される怒り（脅威）と隣人との結束（絆）の積を上乗せする。
        /// 故郷を侵されるほど・地域の絆が厚いほど死力を尽くす（守りの士気だけは正規軍を凌ぐ）。
        /// </summary>
        public static float HomeDefenseMorale(float homelandThreat, float communityBonds,
            MilitiaParams p)
        {
            float threat = Mathf.Clamp01(homelandThreat);
            float bonds = Mathf.Clamp01(communityBonds);
            return Mathf.Clamp01(p.baseMorale + threat * bonds * p.patriotismWeight);
        }

        public static float HomeDefenseMorale(float homelandThreat, float communityBonds)
            => HomeDefenseMorale(homelandThreat, communityBonds, MilitiaParams.Default);

        /// <summary>
        /// 訓練不足（0..1・質の低さ）。正規訓練の欠如がそのまま戦力の質の低さに化ける＝
        /// (1−正規訓練)×訓練不足倍率。訓練が行き届くほど0へ、寄せ集めの素人ほど1へ近づく。
        /// CombatEffectiveness で (1−これ) として戦闘力を削る。
        /// </summary>
        public static float TrainingDeficiency(float professionalTraining, MilitiaParams p)
        {
            float training = Mathf.Clamp01(professionalTraining);
            return Mathf.Clamp01((1f - training) * p.deficiencyScale);
        }

        public static float TrainingDeficiency(float professionalTraining)
            => TrainingDeficiency(professionalTraining, MilitiaParams.Default);

        /// <summary>
        /// 生産との両立コスト（0..1）。動員率×農繁期で平時の生産を犠牲にする痛み＝民を兵にすると畑が荒れる。
        /// 動員率を基礎に、農繁期（0..1・収穫の忙しさ）が（1＋農繁×農繁重み）で増幅する＝
        /// 同じ動員でも繁忙期に抜くほど経済が傷む。動員率0なら畑は無傷、総動員＋農繁期で最大の痛み。
        /// </summary>
        public static float EconomicTradeoff(float mobilizedRatio, float harvestSeason, MilitiaParams p)
        {
            float mobilized = Mathf.Clamp01(mobilizedRatio);
            float harvest = Mathf.Clamp01(harvestSeason);
            return Mathf.Clamp01(mobilized * (1f + harvest * p.harvestWeight));
        }

        public static float EconomicTradeoff(float mobilizedRatio, float harvestSeason)
            => EconomicTradeoff(mobilizedRatio, harvestSeason, MilitiaParams.Default);

        /// <summary>
        /// 長期戦への不向き（0..1）。戦役の長さ×民間の義務で持久戦に耐えられない度合い＝
        /// 戦役の長さを冪で非線形に効かせ、民間の義務（0..1・家業・農地への責務）が（1＋義務×義務重み）で
        /// 増幅する。短期決戦なら問題ないが、長引くほど・民間の責務が重いほど一気に崩れる（民兵は持久に弱い）。
        /// </summary>
        public static float ProtractedWarWeakness(float campaignDuration, float civilianObligations,
            MilitiaParams p)
        {
            float duration = Mathf.Clamp01(campaignDuration);
            float obligations = Mathf.Clamp01(civilianObligations);
            float baseWeak = Mathf.Pow(duration, p.protractedExponent);
            return Mathf.Clamp01(baseWeak * (1f + obligations * p.obligationWeight));
        }

        public static float ProtractedWarWeakness(float campaignDuration, float civilianObligations)
            => ProtractedWarWeakness(campaignDuration, civilianObligations, MilitiaParams.Default);

        /// <summary>
        /// 地の利（0..1）。地形熟知×地元士気で庭先の地形を知り尽くした戦いの優位＝
        /// 両者の幾何平均（積の平方根）に重みを掛ける＝どちらか一方が欠けても地の利は活きない。
        /// 地形を熟知し（隘路・伏兵）なお郷土愛で踏み止まるほど、劣勢の民兵が正規軍を翻弄する。
        /// </summary>
        public static float HomeGroundAdvantage(float terrainKnowledge, float homeDefenseMorale,
            MilitiaParams p)
        {
            float terrain = Mathf.Clamp01(terrainKnowledge);
            float morale = Mathf.Clamp01(homeDefenseMorale);
            float geo = Mathf.Sqrt(terrain * morale);
            return Mathf.Clamp01(geo);
        }

        public static float HomeGroundAdvantage(float terrainKnowledge, float homeDefenseMorale)
            => HomeGroundAdvantage(terrainKnowledge, homeDefenseMorale, MilitiaParams.Default);

        /// <summary>
        /// 民兵の戦闘力（0..1）。地元士気×(1−訓練不足)×地の利で実際の戦力を出す＝
        /// 郷土愛で燃える士気を、訓練不足（質の低さ）が削り、地の利（1＋地の利×地の利重み）が押し戻す。
        /// 士気が高くても素人なら弱く、地形を知り尽くせば劣勢を覆す＝民兵が守りで侮れない理由。
        /// </summary>
        public static float CombatEffectiveness(float homeDefenseMorale, float trainingDeficiency,
            float homeGroundAdvantage, MilitiaParams p)
        {
            float morale = Mathf.Clamp01(homeDefenseMorale);
            float deficiency = Mathf.Clamp01(trainingDeficiency);
            float hga = Mathf.Clamp01(homeGroundAdvantage);
            float raw = morale * (1f - deficiency) * (1f + hga * p.homeGroundWeight);
            return Mathf.Clamp01(raw);
        }

        public static float CombatEffectiveness(float homeDefenseMorale, float trainingDeficiency,
            float homeGroundAdvantage)
            => CombatEffectiveness(homeDefenseMorale, trainingDeficiency, homeGroundAdvantage,
                MilitiaParams.Default);

        /// <summary>
        /// 本土防衛価値（0..1）。動員速度×戦闘力−長期戦弱点で本土を守る兵力としての価値を出す＝
        /// すぐ集まり（速度）よく戦う（戦闘力）ほど守りに厚いが、持久戦の弱さ（長期戦弱点×弱点重み）が
        /// 価値を削る。攻めには使えぬが守りには厚い民兵の本質＝短期の本土防衛でこそ輝く。
        /// </summary>
        public static float HomelandDefenseValue(float mobilizationSpeed, float combatEffectiveness,
            float protractedWarWeakness, MilitiaParams p)
        {
            float speed = Mathf.Clamp01(mobilizationSpeed);
            float combat = Mathf.Clamp01(combatEffectiveness);
            float weakness = Mathf.Clamp01(protractedWarWeakness);
            float raw = speed * combat - weakness * p.weaknessWeight;
            return Mathf.Clamp01(raw);
        }

        public static float HomelandDefenseValue(float mobilizationSpeed, float combatEffectiveness,
            float protractedWarWeakness)
            => HomelandDefenseValue(mobilizationSpeed, combatEffectiveness, protractedWarWeakness,
                MilitiaParams.Default);
    }
}
