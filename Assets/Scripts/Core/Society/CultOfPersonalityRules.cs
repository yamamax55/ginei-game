using UnityEngine;

namespace Ginei
{
    /// <summary>個人崇拝（指導者神格化）の調整係数（純構造体・既定 .Default）。マジックナンバーを1か所へ集約する。</summary>
    public readonly struct CultOfPersonalityParams
    {
        /// <summary>崇拝醸成で実績(achievements)が占める重み（残りはカリスマ×宣伝が支配）。低いほど実績が要らない＝虚像崇拝。</summary>
        public readonly float achievementWeight;
        /// <summary>偶像化の増幅指数（1未満＝小さな崇拝も神格化で大きく膨らむ）。</summary>
        public readonly float idolizationExponent;
        /// <summary>偶像と実像の乖離の倍率（差をどれだけ危険として効かせるか）。</summary>
        public readonly float gapScale;
        /// <summary>偶像化が忠誠動員へ変換される倍率（神格化された指導者は動員力が高い）。</summary>
        public readonly float mobilizationScale;
        /// <summary>批判封殺比の分母を底上げするオフセット（崇拝も異論も0のときの0除算回避＝既定で異論ゼロ防止）。</summary>
        public readonly float suppressionFloor;
        /// <summary>神話の脆さで「実像との乖離」が占める重み。</summary>
        public readonly float fragilityGapWeight;
        /// <summary>神話の脆さで「批判封殺への依存」が占める重み（力で黙らせるほど一撃で崩れやすい）。</summary>
        public readonly float fragilitySuppressionWeight;
        /// <summary>崇拝が安定とみなされる動員−脆さの既定閾値。</summary>
        public readonly float stabilityThreshold;

        public CultOfPersonalityParams(float achievementWeight, float idolizationExponent, float gapScale,
            float mobilizationScale, float suppressionFloor, float fragilityGapWeight,
            float fragilitySuppressionWeight, float stabilityThreshold)
        {
            this.achievementWeight = Mathf.Clamp01(achievementWeight);
            this.idolizationExponent = Mathf.Max(0.01f, idolizationExponent);
            this.gapScale = Mathf.Max(0f, gapScale);
            this.mobilizationScale = Mathf.Max(0f, mobilizationScale);
            this.suppressionFloor = Mathf.Max(0.0001f, suppressionFloor);
            this.fragilityGapWeight = Mathf.Max(0f, fragilityGapWeight);
            this.fragilitySuppressionWeight = Mathf.Max(0f, fragilitySuppressionWeight);
            this.stabilityThreshold = Mathf.Clamp(stabilityThreshold, -1f, 1f);
        }

        /// <summary>
        /// 既定＝実績重み0.5・偶像化指数0.7・乖離倍率1.0・動員倍率1.0・封殺床0.1・脆さ(乖離0.6/封殺0.4)・安定閾値0.2。
        /// </summary>
        public static CultOfPersonalityParams Default => new CultOfPersonalityParams(
            achievementWeight: 0.5f,
            idolizationExponent: 0.7f,
            gapScale: 1f,
            mobilizationScale: 1f,
            suppressionFloor: 0.1f,
            fragilityGapWeight: 0.6f,
            fragilitySuppressionWeight: 0.4f,
            stabilityThreshold: 0.2f);
    }

    /// <summary>
    /// 個人崇拝＝指導者神格化の純ロジック。独裁者・カリスマ指導者の崇拝が、宣伝機構と（時に過大評価された）
    /// 実績の上に醸成され、偶像化（神格化）が実像の能力を超えて乖離する。偶像は忠誠を動員する一方、批判を
    /// 封殺してしか保てず、乖離が大きく封殺に頼るほど神話は脆い——衝撃ひとつで一気に崩れる。崇拝は指導者
    /// 個人に紐づくため制度が弱いと継承できず後継危機を生む（カリスマは継げない）。
    /// 「上から作った偶像は、信じられなくなると一気に剥がれる」を式に出す。
    /// 乱数なし・決定論・実効値パターン（入力は非破壊で読むだけ）。Game層非依存＝Core 純ロジック（test-first）。
    /// </summary>
    public static class CultOfPersonalityRules
    {
        /// <summary>
        /// 崇拝の醸成(0..1)＝カリスマ×宣伝機構で土台を作り、実績(achievements)で底上げする。
        /// 実績重み(achievementWeight)が低いほど実績が無くても崇拝が立つ＝虚像のカリスマ崇拝。
        /// </summary>
        public static float CultBuilding(float charisma, float propagandaApparatus, float achievements, CultOfPersonalityParams p)
        {
            float cha = Mathf.Clamp01(charisma);
            float prop = Mathf.Clamp01(propagandaApparatus);
            float ach = Mathf.Clamp01(achievements);
            float baseCult = cha * prop;                                   // カリスマと宣伝機構の積（どちらも要る）
            float achFactor = (1f - p.achievementWeight) + p.achievementWeight * ach; // 実績で底上げ
            return Mathf.Clamp01(baseCult * achFactor);
        }

        public static float CultBuilding(float charisma, float propagandaApparatus, float achievements)
            => CultBuilding(charisma, propagandaApparatus, achievements, CultOfPersonalityParams.Default);

        /// <summary>
        /// 偶像化(0..1)＝崇拝に応じた神格化。増幅指数(idolizationExponent&lt;1)で小さな崇拝も誇張され大きく膨らむ。
        /// </summary>
        public static float Idolization(float cultBuilding, CultOfPersonalityParams p)
        {
            float cult = Mathf.Clamp01(cultBuilding);
            return Mathf.Clamp01(Mathf.Pow(cult, p.idolizationExponent));
        }

        public static float Idolization(float cultBuilding)
            => Idolization(cultBuilding, CultOfPersonalityParams.Default);

        /// <summary>
        /// 偶像と実像の乖離(-1..1)＝(偶像化 − 実際の能力)×倍率。正＝過大評価（偶像が実力を超え危うい）、
        /// 負＝過小評価（実力が偶像を上回る＝健全寄り）。
        /// </summary>
        public static float RealityGap(float idolization, float actualCompetence, CultOfPersonalityParams p)
        {
            float idol = Mathf.Clamp01(idolization);
            float comp = Mathf.Clamp01(actualCompetence);
            return Mathf.Clamp((idol - comp) * p.gapScale, -1f, 1f);
        }

        public static float RealityGap(float idolization, float actualCompetence)
            => RealityGap(idolization, actualCompetence, CultOfPersonalityParams.Default);

        /// <summary>
        /// 忠誠の動員力(0..1)＝偶像化×動員倍率。神格化された指導者ほど大衆を一斉に動かせる。
        /// </summary>
        public static float LoyaltyMobilization(float idolization, CultOfPersonalityParams p)
        {
            float idol = Mathf.Clamp01(idolization);
            return Mathf.Clamp01(idol * p.mobilizationScale);
        }

        public static float LoyaltyMobilization(float idolization)
            => LoyaltyMobilization(idolization, CultOfPersonalityParams.Default);

        /// <summary>
        /// 批判封殺(0..1)＝崇拝/(崇拝+異論許容+床)。崇拝が強く異論許容(dissentTolerance)が薄いほど批判は黙らされる。
        /// 床(suppressionFloor)は0除算回避＝完全無風（崇拝も異論も0）の事故防止。
        /// </summary>
        public static float CriticismSuppression(float cultBuilding, float dissentTolerance, CultOfPersonalityParams p)
        {
            float cult = Mathf.Clamp01(cultBuilding);
            float tol = Mathf.Clamp01(dissentTolerance);
            return Mathf.Clamp01(cult / (cult + tol + p.suppressionFloor));
        }

        public static float CriticismSuppression(float cultBuilding, float dissentTolerance)
            => CriticismSuppression(cultBuilding, dissentTolerance, CultOfPersonalityParams.Default);

        /// <summary>
        /// 神話の脆さ(0..1)＝実像との乖離(正の部分)と批判封殺への依存の加重和。偶像が実力を超え（過大評価）、
        /// 力で黙らせてしか保てないほど、神話は衝撃ひとつで一気に崩れやすい。負の乖離（過小評価）は脆さに効かない。
        /// </summary>
        public static float MythFragility(float realityGap, float criticismSuppression, CultOfPersonalityParams p)
        {
            float gap = Mathf.Max(0f, Mathf.Clamp(realityGap, -1f, 1f));   // 過大評価のぶんだけ脆い
            float supp = Mathf.Clamp01(criticismSuppression);
            return Mathf.Clamp01(gap * p.fragilityGapWeight + supp * p.fragilitySuppressionWeight);
        }

        public static float MythFragility(float realityGap, float criticismSuppression)
            => MythFragility(realityGap, criticismSuppression, CultOfPersonalityParams.Default);

        /// <summary>
        /// 後継危機(0..1)＝崇拝×(1−制度の強さ)。崇拝が個人に紐づき（cultBuilding 高）制度(institutionalStrength)が
        /// 弱いほど、カリスマを継げず後継危機が深い。制度が固ければ崇拝が強くても継承は安定する。
        /// </summary>
        public static float SuccessionCrisis(float cultBuilding, float institutionalStrength, CultOfPersonalityParams p)
        {
            float cult = Mathf.Clamp01(cultBuilding);
            float inst = Mathf.Clamp01(institutionalStrength);
            return Mathf.Clamp01(cult * (1f - inst));
        }

        public static float SuccessionCrisis(float cultBuilding, float institutionalStrength)
            => SuccessionCrisis(cultBuilding, institutionalStrength, CultOfPersonalityParams.Default);

        /// <summary>
        /// 崇拝が安定しているか：忠誠動員が神話の脆さを閾値ぶん上回るか。動員力が脆さに食われると崇拝は維持できない。
        /// </summary>
        public static bool IsCultStable(float loyaltyMobilization, float mythFragility, float threshold)
        {
            float mob = Mathf.Clamp01(loyaltyMobilization);
            float frag = Mathf.Clamp01(mythFragility);
            float th = Mathf.Clamp(threshold, -1f, 1f);
            return (mob - frag) >= th;
        }

        public static bool IsCultStable(float loyaltyMobilization, float mythFragility)
            => IsCultStable(loyaltyMobilization, mythFragility, CultOfPersonalityParams.Default.stabilityThreshold);
    }
}
