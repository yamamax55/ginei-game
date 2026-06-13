using UnityEngine;

namespace Ginei
{
    /// <summary>元首の主観状態（死の自覚・統治への倦怠・遺産志向）の調整係数。</summary>
    public readonly struct RulerMindsetParams
    {
        /// <summary>死の自覚が立ち上がり始める年齢（ここを超えると加齢ぶんが効く）。</summary>
        public readonly float mortalityOnsetAge;
        /// <summary>加齢1歳あたりの死の自覚の増分（mortalityOnsetAge 超過分に掛ける）。</summary>
        public readonly float agingMortalityRate;
        /// <summary>健康悪化（1−健康）が死の自覚に与える重み。</summary>
        public readonly float healthMortalityWeight;
        /// <summary>治世1年あたりの倦怠の増分。</summary>
        public readonly float reignFatigueRate;
        /// <summary>危機負荷が倦怠に与える重み。</summary>
        public readonly float crisisFatigueWeight;
        /// <summary>倦怠が判断の質を削る最大割合（実効値の下げ幅の上限）。</summary>
        public readonly float fatigueDecisionPenalty;
        /// <summary>遺産志向が「博打的大事業」へ振れる強さ（リスク選好シフトの上振れ重み）。</summary>
        public readonly float legacyRiskWeight;
        /// <summary>死の自覚が「保身的」へ振れる強さ（リスク選好シフトの下振れ重み）。</summary>
        public readonly float mortalityCautionWeight;

        public RulerMindsetParams(float mortalityOnsetAge, float agingMortalityRate,
                                  float healthMortalityWeight, float reignFatigueRate,
                                  float crisisFatigueWeight, float fatigueDecisionPenalty,
                                  float legacyRiskWeight, float mortalityCautionWeight)
        {
            this.mortalityOnsetAge = Mathf.Max(0f, mortalityOnsetAge);
            this.agingMortalityRate = Mathf.Max(0f, agingMortalityRate);
            this.healthMortalityWeight = Mathf.Clamp01(healthMortalityWeight);
            this.reignFatigueRate = Mathf.Max(0f, reignFatigueRate);
            this.crisisFatigueWeight = Mathf.Clamp01(crisisFatigueWeight);
            this.fatigueDecisionPenalty = Mathf.Clamp01(fatigueDecisionPenalty);
            this.legacyRiskWeight = Mathf.Clamp01(legacyRiskWeight);
            this.mortalityCautionWeight = Mathf.Clamp01(mortalityCautionWeight);
        }

        /// <summary>
        /// 既定＝死の自覚は50歳から/加齢0.02/年・健康重み0.6・治世倦怠0.03/年・危機重み0.5・
        /// 倦怠の判断ペナルティ0.4・遺産リスク0.6・死の自覚の保身0.5。
        /// </summary>
        public static RulerMindsetParams Default =>
            new RulerMindsetParams(50f, 0.02f, 0.6f, 0.03f, 0.5f, 0.4f, 0.6f, 0.5f);
    }

    /// <summary>元首の主観状態のスナップショット（純データ・0..1正規化）。</summary>
    public readonly struct RulerMindset
    {
        /// <summary>死の自覚（自分の終わりをどれだけ意識しているか）。</summary>
        public readonly float mortalityAwareness;
        /// <summary>統治への倦怠（玉座への飽き・疲れ）。</summary>
        public readonly float governanceFatigue;
        /// <summary>遺産志向（名を遺したいという駆動）。</summary>
        public readonly float legacyOrientation;
        /// <summary>後継への関心。</summary>
        public readonly float successionFocus;

        public RulerMindset(float mortalityAwareness, float governanceFatigue,
                            float legacyOrientation, float successionFocus)
        {
            this.mortalityAwareness = Mathf.Clamp01(mortalityAwareness);
            this.governanceFatigue = Mathf.Clamp01(governanceFatigue);
            this.legacyOrientation = Mathf.Clamp01(legacyOrientation);
            this.successionFocus = Mathf.Clamp01(successionFocus);
        }
    }

    /// <summary>
    /// 元首の主観状態（Ruler Mindset）の純ロジック。老い・病・長い治世が「自分の終わり」を意識させ、
    /// それが統治姿勢を変える＝後継への関心、保身、あるいは遺産を遺そうと大事業に走る。心境のモデル。
    /// 死の自覚は加齢と健康悪化から、統治への倦怠は治世の長さと危機の連続から募り、両者が遺産志向・
    /// 後継への関心・リスク選好・改革／保守の綱引き・引退の傾きを動かす（実効値パターン・基準非破壊）。
    /// 分担：<see cref="IllnessRules"/>＝病臥（発症・執務低下・隠蔽＝肉体の状態）とは別＝統治者の主観的心境
    /// （死の自覚・倦怠・遺産志向）／<see cref="SenescenceRules"/>＝名将の衰え（能力の下り坂）とは別＝心理状態。
    /// 同 EPIC HDRN の AbdicationRules（退位）／HeirDesignationRules（立太子）の入力源。
    /// 盤面非依存の plain 引数。Game/SO 型は参照しない。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RulerMindsetRules
    {
        /// <summary>
        /// 死の自覚（0..1）。mortalityOnsetAge を超えた加齢ぶん×agingMortalityRate と、健康悪化（1−health）
        /// ×healthMortalityWeight の和。老いと病が重なるほど自分の終わりを強く意識する。
        /// </summary>
        public static float MortalityAwareness(float age, float health, RulerMindsetParams p)
        {
            float a = Mathf.Max(0f, age);
            float h = Mathf.Clamp01(health);
            float aging = Mathf.Max(0f, a - p.mortalityOnsetAge) * p.agingMortalityRate;
            float ill = (1f - h) * p.healthMortalityWeight;
            return Mathf.Clamp01(aging + ill);
        }

        public static float MortalityAwareness(float age, float health)
            => MortalityAwareness(age, health, RulerMindsetParams.Default);

        /// <summary>
        /// 統治への倦怠（0..1）。治世の長さ×reignFatigueRate と、危機負荷×crisisFatigueWeight の和。
        /// 長く玉座にあり危機に追われ続けるほど統治に飽き疲れる。
        /// </summary>
        public static float GovernanceFatigue(float reignLength, float crisisLoad, RulerMindsetParams p)
        {
            float r = Mathf.Max(0f, reignLength);
            float c = Mathf.Clamp01(crisisLoad);
            return Mathf.Clamp01(r * p.reignFatigueRate + c * p.crisisFatigueWeight);
        }

        public static float GovernanceFatigue(float reignLength, float crisisLoad)
            => GovernanceFatigue(reignLength, crisisLoad, RulerMindsetParams.Default);

        /// <summary>
        /// 遺産志向（0..1）。死の自覚と未達成感（achievementGap＝やり残したことの大きさ）の積。
        /// 終わりが見えるほど、そしてまだ名を成していないほど、遺産を遺したい衝動が強まる
        /// （どちらか 0 なら駆動は生まれない＝もう満ち足りた者・まだ若い者は急がない）。
        /// </summary>
        public static float LegacyOrientation(float mortalityAwareness, float achievementGap)
        {
            float m = Mathf.Clamp01(mortalityAwareness);
            float g = Mathf.Clamp01(achievementGap);
            return Mathf.Clamp01(m * g);
        }

        /// <summary>
        /// 後継への関心（0..1）。死の自覚が高いほど高まり、後継者がいる（hasHeir）と
        /// より具体的な関心（誰に・どう継がせるか）へ増幅される＝後継不在では関心はあっても宙に浮く。
        /// </summary>
        public static float SuccessionFocus(float mortalityAwareness, bool hasHeir)
        {
            float m = Mathf.Clamp01(mortalityAwareness);
            float heir = hasHeir ? 1f : 0f;
            // 後継不在でも基礎関心の半分は立つ／後継ありで満額へ。
            return Mathf.Clamp01(m * Mathf.Lerp(0.5f, 1f, heir));
        }

        /// <summary>
        /// リスク選好のシフト（−1..+1）。終わりが見えると人は二つに割れる：遺産を遺そうと博打的大事業に
        /// 走る（legacyOrientation×legacyRiskWeight＝上振れ）か、残りを波風立てず守ろうと保身的になる
        /// （mortalityAwareness×mortalityCautionWeight＝下振れ）。差し引きの傾き。
        /// 正＝博打的（攻め）／負＝保身的（守り）。
        /// </summary>
        public static float RiskAppetiteShift(float mortalityAwareness, float legacyOrientation,
                                              RulerMindsetParams p)
        {
            float m = Mathf.Clamp01(mortalityAwareness);
            float l = Mathf.Clamp01(legacyOrientation);
            float bold = l * p.legacyRiskWeight;
            float cautious = m * p.mortalityCautionWeight;
            return Mathf.Clamp(bold - cautious, -1f, 1f);
        }

        public static float RiskAppetiteShift(float mortalityAwareness, float legacyOrientation)
            => RiskAppetiteShift(mortalityAwareness, legacyOrientation, RulerMindsetParams.Default);

        /// <summary>
        /// 改革か現状維持かの傾き（−1..+1）。倦怠は現状維持へ（変えるのが億劫＝下振れ）、
        /// 遺産志向は改革へ（爪痕を残したい＝上振れ）の綱引き。
        /// 正＝改革志向／負＝現状維持志向。
        /// </summary>
        public static float ReformVsConservatism(float governanceFatigue, float legacyOrientation)
        {
            float f = Mathf.Clamp01(governanceFatigue);
            float l = Mathf.Clamp01(legacyOrientation);
            return Mathf.Clamp(l - f, -1f, 1f);
        }

        /// <summary>
        /// 倦怠下の判断の質の実効倍率（1−fatigueDecisionPenalty..1）。倦怠が高いほど判断が雑になる。
        /// 元首の能力の実効値に掛けて使う（基準値は書き換えない＝実効値パターン）。
        /// </summary>
        public static float DecisionQualityUnderFatigue(float governanceFatigue, RulerMindsetParams p)
        {
            float f = Mathf.Clamp01(governanceFatigue);
            return Mathf.Clamp01(1f - f * p.fatigueDecisionPenalty);
        }

        public static float DecisionQualityUnderFatigue(float governanceFatigue)
            => DecisionQualityUnderFatigue(governanceFatigue, RulerMindsetParams.Default);

        /// <summary>
        /// 引退（退位）を考える傾き（0..1）。死の自覚と倦怠の平均が退位へ押し、権力への執着が抗う。
        /// 「もう疲れた・終わりが近い」と「まだ手放せない」の差し引き＝AbdicationRules の入力源。
        /// </summary>
        public static float GracefulExitInclination(float mortalityAwareness, float governanceFatigue,
                                                    float attachmentToPower)
        {
            float push = 0.5f * (Mathf.Clamp01(mortalityAwareness) + Mathf.Clamp01(governanceFatigue));
            float hold = Mathf.Clamp01(attachmentToPower);
            return Mathf.Clamp01(push * (1f - hold));
        }

        /// <summary>遺産志向に駆られた心境か（legacyOrientation が閾値以上）。大事業・大改革の AI 判断のゲート。</summary>
        public static bool IsLegacyDriven(float legacyOrientation, float threshold)
        {
            return Mathf.Clamp01(legacyOrientation) >= Mathf.Clamp01(threshold);
        }

        public static bool IsLegacyDriven(float legacyOrientation) => IsLegacyDriven(legacyOrientation, 0.5f);
    }
}
