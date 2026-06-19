using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 抗議運動（大衆運動）の調整係数。動員・規律・持続・弾圧／譲歩・分裂・世論・成果の重み。
    /// すべて非破壊の実効値パターンで使う（基準値は書き換えない）。
    /// </summary>
    public readonly struct ProtestMovementParams
    {
        /// <summary>大義の共鳴で「正当性」を重んじる比重（0..1・残りが大衆受け）。</summary>
        public readonly float legitimacyWeight;
        /// <summary>動員規模の組織力への依存度（0..1・低いほど共鳴だけで広がる自然発生型）。</summary>
        public readonly float organizingWeight;
        /// <summary>非暴力規律の挑発耐性への依存度（0..1・残りが指導力）。</summary>
        public readonly float disciplineResistanceWeight;
        /// <summary>持続性の非線形度（動員×資源×勢いの幾何平均の冪・1以上）。揃わないと続かない。</summary>
        public readonly float sustainExponent;
        /// <summary>分裂で思想の多様さが効く比重（0..1・残りが指導の非統一）。</summary>
        public readonly float diversityWeight;
        /// <summary>世論の共感で「非暴力」を重んじる比重（0..1・残りが体制の過剰反応）。</summary>
        public readonly float sympathyDisciplineWeight;
        /// <summary>政治的成果の非線形度（持続×共感×譲歩寄りの幾何平均の冪・1以上）。</summary>
        public readonly float outcomeExponent;

        public ProtestMovementParams(float legitimacyWeight, float organizingWeight,
            float disciplineResistanceWeight, float sustainExponent, float diversityWeight,
            float sympathyDisciplineWeight, float outcomeExponent)
        {
            this.legitimacyWeight = Mathf.Clamp01(legitimacyWeight);
            this.organizingWeight = Mathf.Clamp01(organizingWeight);
            this.disciplineResistanceWeight = Mathf.Clamp01(disciplineResistanceWeight);
            this.sustainExponent = Mathf.Max(1f, sustainExponent);
            this.diversityWeight = Mathf.Clamp01(diversityWeight);
            this.sympathyDisciplineWeight = Mathf.Clamp01(sympathyDisciplineWeight);
            this.outcomeExponent = Mathf.Max(1f, outcomeExponent);
        }

        /// <summary>既定＝正当性比重0.6・組織力依存0.5・挑発耐性比重0.5・持続冪1.5・多様性比重0.6・非暴力比重0.6・成果冪1.5。</summary>
        public static ProtestMovementParams Default =>
            new ProtestMovementParams(0.6f, 0.5f, 0.5f, 1.5f, 0.6f, 0.6f, 1.5f);
    }

    /// <summary>
    /// 抗議運動（大衆運動）の純ロジック。不満の正当性と大衆受けで大義が共鳴し、共鳴×組織力で人々が
    /// 動員され、指導力と挑発耐性で非暴力の規律が保たれ、動員・資源・勢いで運動が持続する。
    /// 体制は運動の力に応じて弾圧か譲歩かを選び、思想の多様さと指導の不統一が運動を分裂させ、
    /// 非暴力と体制の過剰反応が世論の共感を呼ぶ。最終的に持続・共感・譲歩寄りが政治的成果を決める。
    /// 倍率は各係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ProtestMovementRules
    {
        /// <summary>
        /// 大義の共鳴（0..1）。不満の正当性（grievanceLegitimacy）と大衆受け（popularAppeal）を
        /// legitimacyWeight で加重平均する＝正当な訴えでも広く響かなければ運動にならず、逆もまた然り。
        /// 両方が高いほど1へ近づく＝多くの人が「自分ごと」と感じる大義。
        /// </summary>
        public static float CauseResonance(float grievanceLegitimacy, float popularAppeal,
            ProtestMovementParams p)
        {
            float legitimacy = Mathf.Clamp01(grievanceLegitimacy);
            float appeal = Mathf.Clamp01(popularAppeal);
            float resonance = legitimacy * p.legitimacyWeight + appeal * (1f - p.legitimacyWeight);
            return Mathf.Clamp01(resonance);
        }

        public static float CauseResonance(float grievanceLegitimacy, float popularAppeal)
            => CauseResonance(grievanceLegitimacy, popularAppeal, ProtestMovementParams.Default);

        /// <summary>
        /// 動員規模（0..1）。大義の共鳴（causeResonance）に組織力（organizingCapacity）を掛けて人を集める。
        /// 組織力は organizingWeight で効き、(1−organizingWeight)＋organizingWeight×組織力 を共鳴に乗じる＝
        /// 組織がなくても共鳴だけである程度は集まる（自然発生）が、組織力があるほど大規模化する。
        /// </summary>
        public static float Mobilization(float causeResonance, float organizingCapacity,
            ProtestMovementParams p)
        {
            float resonance = Mathf.Clamp01(causeResonance);
            float organizing = Mathf.Clamp01(organizingCapacity);
            float factor = (1f - p.organizingWeight) + p.organizingWeight * organizing;
            return Mathf.Clamp01(resonance * factor);
        }

        public static float Mobilization(float causeResonance, float organizingCapacity)
            => Mobilization(causeResonance, organizingCapacity, ProtestMovementParams.Default);

        /// <summary>
        /// 非暴力規律（0..1）。指導力（movementLeadership）と挑発への耐性（provocationResistance）を
        /// disciplineResistanceWeight で加重平均する＝強い指導があり、当局の挑発（弾圧・煽動）に乗らない
        /// ほど暴徒化せず非暴力を保てる。どちらかが欠けると規律が崩れて暴力に転化しやすい。
        /// </summary>
        public static float NonviolentDiscipline(float movementLeadership, float provocationResistance,
            ProtestMovementParams p)
        {
            float leadership = Mathf.Clamp01(movementLeadership);
            float resistance = Mathf.Clamp01(provocationResistance);
            float discipline = leadership * (1f - p.disciplineResistanceWeight)
                + resistance * p.disciplineResistanceWeight;
            return Mathf.Clamp01(discipline);
        }

        public static float NonviolentDiscipline(float movementLeadership, float provocationResistance)
            => NonviolentDiscipline(movementLeadership, provocationResistance, ProtestMovementParams.Default);

        /// <summary>
        /// 運動の持続性（0..1）。動員（mobilization）・資源（resources）・勢い（momentum）の三者の
        /// 幾何平均を sustainExponent で非線形に効かせる＝どれか一つでも欠けると運動は続かない（積に近い）。
        /// 人が集まり、資金・物資が続き、士気の勢いが保たれて初めて長期戦に耐えられる。
        /// </summary>
        public static float MovementSustainability(float mobilization, float resources, float momentum,
            ProtestMovementParams p)
        {
            float mob = Mathf.Clamp01(mobilization);
            float res = Mathf.Clamp01(resources);
            float mom = Mathf.Clamp01(momentum);
            float geo = Mathf.Pow(mob * res * mom, 1f / 3f); // 三者の幾何平均
            float shaped = Mathf.Pow(geo, p.sustainExponent);
            return Mathf.Clamp01(shaped);
        }

        public static float MovementSustainability(float mobilization, float resources, float momentum)
            => MovementSustainability(mobilization, resources, momentum, ProtestMovementParams.Default);

        /// <summary>
        /// 弾圧寄りか譲歩寄りか（0..1・1=完全な弾圧／0=完全な譲歩）。体制の強硬さ（regimeHardline）と
        /// 運動の力（movementStrength）の比＝強硬/(強硬+運動)。強硬な体制ほど弾圧へ、運動が強いほど
        /// 体制は折れて譲歩へ傾く。双方0なら中立0.5。(1−これ)を成果側へ掛けて「譲歩寄り」を使う。
        /// </summary>
        public static float RepressionOrConcession(float regimeHardline, float movementStrength,
            ProtestMovementParams p)
        {
            float hardline = Mathf.Clamp01(regimeHardline);
            float strength = Mathf.Clamp01(movementStrength);
            float denom = hardline + strength;
            if (denom <= 0.0001f) return 0.5f; // 双方ゼロ＝対立なし＝中立
            return Mathf.Clamp01(hardline / denom);
        }

        public static float RepressionOrConcession(float regimeHardline, float movementStrength)
            => RepressionOrConcession(regimeHardline, movementStrength, ProtestMovementParams.Default);

        /// <summary>
        /// 運動の分裂（0..1）。思想の多様さ（ideologicalDiversity）×(1−指導の統一 leadershipUnity)を
        /// diversityWeight で加重平均する＝寄り合い所帯ほど割れやすく、指導が統一されていれば多様でも
        /// まとまる。1へ近いほど派閥に割れて運動が空中分解する。
        /// </summary>
        public static float MovementFragmentation(float ideologicalDiversity, float leadershipUnity,
            ProtestMovementParams p)
        {
            float diversity = Mathf.Clamp01(ideologicalDiversity);
            float disunity = 1f - Mathf.Clamp01(leadershipUnity);
            float frag = diversity * p.diversityWeight + disunity * (1f - p.diversityWeight);
            return Mathf.Clamp01(frag);
        }

        public static float MovementFragmentation(float ideologicalDiversity, float leadershipUnity)
            => MovementFragmentation(ideologicalDiversity, leadershipUnity, ProtestMovementParams.Default);

        /// <summary>
        /// 世論の共感（0..1）。非暴力の規律（nonviolentDiscipline）と体制の過剰反応（regimeOverreach）を
        /// sympathyDisciplineWeight で加重平均する＝平和的なデモへ当局が過剰に弾圧するほど世論は運動側に
        /// 同情する（殉教効果）。暴徒化すれば共感を失い、穏健な体制なら過剰反応の燃料も乏しい。
        /// </summary>
        public static float PublicSympathy(float nonviolentDiscipline, float regimeOverreach,
            ProtestMovementParams p)
        {
            float discipline = Mathf.Clamp01(nonviolentDiscipline);
            float overreach = Mathf.Clamp01(regimeOverreach);
            float sympathy = discipline * p.sympathyDisciplineWeight
                + overreach * (1f - p.sympathyDisciplineWeight);
            return Mathf.Clamp01(sympathy);
        }

        public static float PublicSympathy(float nonviolentDiscipline, float regimeOverreach)
            => PublicSympathy(nonviolentDiscipline, regimeOverreach, ProtestMovementParams.Default);

        /// <summary>
        /// 政治的成果（-1..1・正=勝利／負=弾圧されて敗北）。運動の持続性（movementSustainability）・
        /// 世論の共感（publicSympathy）・譲歩寄り(1−repressionOrConcession) の三者の幾何平均を
        /// outcomeExponent で非線形に効かせ、[0..1] の達成度を 2×−1 で [-1..1] へ写す＝続き、共感を集め、
        /// 体制が折れて初めて勝つ。弾圧寄りで体制が折れなければ達成度0付近→負へ＝弾圧されての敗北。
        /// </summary>
        public static float PoliticalOutcome(float movementSustainability, float publicSympathy,
            float repressionOrConcession, ProtestMovementParams p)
        {
            float sustain = Mathf.Clamp01(movementSustainability);
            float sympathy = Mathf.Clamp01(publicSympathy);
            float concession = 1f - Mathf.Clamp01(repressionOrConcession); // 譲歩寄り
            float geo = Mathf.Pow(sustain * sympathy * concession, 1f / 3f); // 三者の幾何平均
            float achievement = Mathf.Pow(geo, p.outcomeExponent); // 0..1
            float signed = achievement * 2f - 1f; // 0..1 → -1..1
            return Mathf.Clamp(signed, -1f, 1f);
        }

        public static float PoliticalOutcome(float movementSustainability, float publicSympathy,
            float repressionOrConcession)
            => PoliticalOutcome(movementSustainability, publicSympathy, repressionOrConcession,
                ProtestMovementParams.Default);
    }
}
