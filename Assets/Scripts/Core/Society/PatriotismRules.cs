using UnityEngine;

namespace Ginei
{
    /// <summary>愛国心・国民感情（戦時の国民の結束と動員）の調整係数。</summary>
    public readonly struct PatriotismParams
    {
        /// <summary>外敵脅威が国民の誇りを愛国の高揚へ増幅する重み（0..1・脅威ほど誇りが燃える）。</summary>
        public readonly float threatFervorWeight;
        /// <summary>旗の下集結効果における指導部結束の重み（0..1・結束した指導部ほど国民を束ねる）。</summary>
        public readonly float unityRallyWeight;
        /// <summary>戦意の動員における愛国の高揚の重み（0..1・残りは結束が担う）。</summary>
        public readonly float fervorMobilizationWeight;
        /// <summary>排外主義における扇動の重み（0..1・デマゴーグの煽りほど愛国が排外へ歪む）。</summary>
        public readonly float demagogueryWeight;
        /// <summary>戦争疲れ減衰における長期化の重み（0..1・泥沼ほど愛国が冷める）。</summary>
        public readonly float prolongedWeight;
        /// <summary>戦争疲れ減衰における犠牲の重み（0..1・戦死者ほど厭戦が募る）。</summary>
        public readonly float casualtyWeight;
        /// <summary>好戦的熱狂（暴走）リスクの非線形度（冪指数・1以上）。排外×扇動が高いほど一気に暴走。</summary>
        public readonly float jingoismExponent;

        public PatriotismParams(float threatFervorWeight, float unityRallyWeight,
            float fervorMobilizationWeight, float demagogueryWeight, float prolongedWeight,
            float casualtyWeight, float jingoismExponent)
        {
            this.threatFervorWeight = Mathf.Clamp01(threatFervorWeight);
            this.unityRallyWeight = Mathf.Clamp01(unityRallyWeight);
            this.fervorMobilizationWeight = Mathf.Clamp01(fervorMobilizationWeight);
            this.demagogueryWeight = Mathf.Clamp01(demagogueryWeight);
            this.prolongedWeight = Mathf.Clamp01(prolongedWeight);
            this.casualtyWeight = Mathf.Clamp01(casualtyWeight);
            this.jingoismExponent = Mathf.Max(1f, jingoismExponent);
        }

        /// <summary>既定＝脅威増幅0.5・結束重み0.5・動員での愛国重み0.6・扇動重み0.6・長期化重み0.5・犠牲重み0.6・暴走冪1.5。</summary>
        public static PatriotismParams Default =>
            new PatriotismParams(0.5f, 0.5f, 0.6f, 0.6f, 0.5f, 0.6f, 1.5f);
    }

    /// <summary>
    /// 愛国心・国民感情の純ロジック（戦時の国民の結束と動員）。国民の誇り（ナショナリズム）が
    /// 外敵の脅威で高揚し（旗の下集結効果＝rally round the flag）、戦意の動員へ転じる一方、
    /// 過剰な愛国は扇動と結びつき排外主義・好戦的熱狂（jingoism）へ暴走し、戦争の長期化と犠牲は
    /// 厭戦＝戦争疲れで愛国心を冷ます。動員意欲と戦争疲れの綱引きで国民士気の正味を出し、結束の可否を判定する。
    /// すべて0..1入力・符号付き出力は-1..1へclamp。乱数なし・決定論。実効値パターン（基準非破壊）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PatriotismRules
    {
        /// <summary>
        /// 愛国の高揚（0..1）。国民の誇り（ナショナリズム）が外敵の脅威で増幅する＝
        /// 誇り×(1＋脅威×脅威増幅重み)。脅威が高いほど同じ誇りでも愛国が燃え上がる。
        /// 誇り0なら高揚なし、誇りも脅威も高いと1へ近づく。
        /// </summary>
        public static float PatrioticFervor(float nationalPride, float externalThreat, PatriotismParams p)
        {
            float pride = Mathf.Clamp01(nationalPride);
            float threat = Mathf.Clamp01(externalThreat);
            float fervor = pride * (1f + threat * p.threatFervorWeight);
            return Mathf.Clamp01(fervor);
        }

        public static float PatrioticFervor(float nationalPride, float externalThreat)
            => PatrioticFervor(nationalPride, externalThreat, PatriotismParams.Default);

        /// <summary>
        /// 旗の下集結効果（0..1）。外敵の脅威で国民が指導部の下に結集する＝
        /// 脅威×(結束重み＋(1−結束重み)×指導部結束)。脅威がなければ集結はなく（脅威で乗算）、
        /// 指導部が結束しているほど効果が増す（バラバラな指導部では国民を束ねきれない）。
        /// </summary>
        public static float RallyEffect(float externalThreat, float leadershipUnity, PatriotismParams p)
        {
            float threat = Mathf.Clamp01(externalThreat);
            float unity = Mathf.Clamp01(leadershipUnity);
            float rally = threat * ((1f - p.unityRallyWeight) + p.unityRallyWeight * unity);
            return Mathf.Clamp01(rally);
        }

        public static float RallyEffect(float externalThreat, float leadershipUnity)
            => RallyEffect(externalThreat, leadershipUnity, PatriotismParams.Default);

        /// <summary>
        /// 戦意の動員（0..1）。愛国の高揚と旗の下集結の重み付き和で国民を戦争へ動員する＝
        /// 高揚×動員重み＋集結×(1−動員重み)。高揚（個々の誇り）と集結（指導部への結集）の両輪。
        /// どちらかだけでは弱く、両方が揃うと総力戦の動員へ近づく。
        /// </summary>
        public static float MobilizationWill(float patrioticFervor, float rallyEffect, PatriotismParams p)
        {
            float fervor = Mathf.Clamp01(patrioticFervor);
            float rally = Mathf.Clamp01(rallyEffect);
            float will = fervor * p.fervorMobilizationWeight + rally * (1f - p.fervorMobilizationWeight);
            return Mathf.Clamp01(will);
        }

        public static float MobilizationWill(float patrioticFervor, float rallyEffect)
            => MobilizationWill(patrioticFervor, rallyEffect, PatriotismParams.Default);

        /// <summary>
        /// 排外主義への暴走（0..1）。過剰な愛国がデマゴーグの扇動と結びつき排外へ歪む＝
        /// 高揚×(扇動重み×扇動＋(1−扇動重み))。扇動が高いほど愛国が外敵憎悪へ振れる。
        /// 扇動0でも素の愛国の一部が排外を孕む（(1−扇動重み)の下駄）、扇動が強いと急増する。
        /// </summary>
        public static float Xenophobia(float patrioticFervor, float demagoguery, PatriotismParams p)
        {
            float fervor = Mathf.Clamp01(patrioticFervor);
            float dema = Mathf.Clamp01(demagoguery);
            float x = fervor * (p.demagogueryWeight * dema + (1f - p.demagogueryWeight));
            return Mathf.Clamp01(x);
        }

        public static float Xenophobia(float patrioticFervor, float demagoguery)
            => Xenophobia(patrioticFervor, demagoguery, PatriotismParams.Default);

        /// <summary>
        /// 愛国心の減衰（0..1・戦争疲れぶん）。戦争の長期化と犠牲が厭戦＝戦争疲れを生み愛国を冷ます＝
        /// 高揚×(長期化重み×長期化＋犠牲重み×犠牲)。長く泥沼に嵌り戦死者が増えるほど冷める。
        /// 高揚が高いほど失う余地も大きい（冷める量は高揚に比例）。NetNationalMorale で動員から差し引く。
        /// </summary>
        public static float WarWearinessDecay(float patrioticFervor, float prolongedConflict,
            float casualties, PatriotismParams p)
        {
            float fervor = Mathf.Clamp01(patrioticFervor);
            float prolonged = Mathf.Clamp01(prolongedConflict);
            float losses = Mathf.Clamp01(casualties);
            float decay = fervor * (p.prolongedWeight * prolonged + p.casualtyWeight * losses);
            return Mathf.Clamp01(decay);
        }

        public static float WarWearinessDecay(float patrioticFervor, float prolongedConflict, float casualties)
            => WarWearinessDecay(patrioticFervor, prolongedConflict, casualties, PatriotismParams.Default);

        /// <summary>
        /// 国民士気の正味（-1..1）。動員意欲から戦争疲れを差し引いた国民全体の士気の向き＝
        /// 動員意欲−戦争疲れ。正なら国民は戦争を支え（結束）、負なら厭戦が勝り士気が崩れる。
        /// 旗の下集結と厭戦の綱引きの結果＝戦争の継続意志の指標。
        /// </summary>
        public static float NetNationalMorale(float mobilizationWill, float warWearinessDecay, PatriotismParams p)
        {
            float will = Mathf.Clamp01(mobilizationWill);
            float weariness = Mathf.Clamp01(warWearinessDecay);
            return Mathf.Clamp(will - weariness, -1f, 1f);
        }

        public static float NetNationalMorale(float mobilizationWill, float warWearinessDecay)
            => NetNationalMorale(mobilizationWill, warWearinessDecay, PatriotismParams.Default);

        /// <summary>
        /// 好戦的熱狂（暴走）のリスク（0..1）。排外主義と扇動が結びつき国民が好戦的熱狂へ暴走する＝
        /// (排外×扇動)を暴走冪で非線形に効かせる。どちらか低ければ暴走しないが、両方高いと一気に燃える＝
        /// 過剰なナショナリズムの危険（無謀な開戦・少数派迫害）。積＋冪で「揃うと暴走」を表す。
        /// </summary>
        public static float JingoismRisk(float xenophobia, float demagoguery, PatriotismParams p)
        {
            float x = Mathf.Clamp01(xenophobia);
            float dema = Mathf.Clamp01(demagoguery);
            float raw = Mathf.Pow(x * dema, p.jingoismExponent);
            return Mathf.Clamp01(raw);
        }

        public static float JingoismRisk(float xenophobia, float demagoguery)
            => JingoismRisk(xenophobia, demagoguery, PatriotismParams.Default);

        /// <summary>
        /// 国民が結束しているか＝国民士気の正味が閾値を超えたら true。正味が高いほど国民は戦争を支え、
        /// 動員に応じ前線を支える。厭戦が勝って閾値を割れば結束は崩れる。既定閾値0.1。
        /// </summary>
        public static bool IsNationUnified(float netNationalMorale, float threshold)
        {
            float net = Mathf.Clamp(netNationalMorale, -1f, 1f);
            float thr = Mathf.Clamp(threshold, -1f, 1f);
            return net >= thr;
        }

        public static bool IsNationUnified(float netNationalMorale)
            => IsNationUnified(netNationalMorale, 0.1f);
    }
}
