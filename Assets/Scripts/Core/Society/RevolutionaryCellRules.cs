using UnityEngine;

namespace Ginei
{
    /// <summary>革命細胞（地下革命組織）の調整係数（秘匿・急進化・浸透・武装・摘発・生存・機運の重み）。</summary>
    public readonly struct RevolutionaryCellParams
    {
        /// <summary>細胞秘匿の重み（分散構造×規律に掛ける）。</summary>
        public readonly float securityWeight;
        /// <summary>急進化の重み（不満強度×弾圧体験に掛ける）。</summary>
        public readonly float radicalizationWeight;
        /// <summary>大衆浸透の重み（宣伝×大衆の不満に掛ける）。</summary>
        public readonly float penetrationWeight;
        /// <summary>武装準備の重み（武器備蓄×軍事訓練に掛ける）。</summary>
        public readonly float armamentWeight;
        /// <summary>摘発リスクの重み（当局の諜報×(1-秘匿)に掛ける）。</summary>
        public readonly float infiltrationWeight;
        /// <summary>革命機運の重み（浸透×急進×体制の弱体に掛ける）。</summary>
        public readonly float momentumWeight;

        public RevolutionaryCellParams(float securityWeight, float radicalizationWeight, float penetrationWeight,
            float armamentWeight, float infiltrationWeight, float momentumWeight)
        {
            this.securityWeight = Mathf.Clamp01(securityWeight);
            this.radicalizationWeight = Mathf.Clamp01(radicalizationWeight);
            this.penetrationWeight = Mathf.Clamp01(penetrationWeight);
            this.armamentWeight = Mathf.Clamp01(armamentWeight);
            this.infiltrationWeight = Mathf.Clamp01(infiltrationWeight);
            this.momentumWeight = Mathf.Clamp01(momentumWeight);
        }

        /// <summary>
        /// 既定＝秘匿重み1.0・急進化重み1.0・浸透重み1.0・武装重み1.0・摘発重み1.0・機運重み1.0
        /// （素の幾何/積モデルをそのまま反映する中立既定）。
        /// </summary>
        public static RevolutionaryCellParams Default =>
            new RevolutionaryCellParams(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
    }

    /// <summary>
    /// 革命を企てる地下細胞組織（革命細胞）の純ロジック。
    /// 細胞の秘匿構造、不満と弾圧による急進化（過激化）、大衆への浸透、武装蜂起の準備、
    /// 当局による摘発、組織の生存、そして革命の機運の醸成を扱う。
    /// <para>
    /// 分担：<see cref="ResistanceMovementRules"/> は占領下の抵抗運動（外敵の占領者を追い出す地下組織）、
    /// 本クラスは自国の体制転覆を狙う革命組織に特化する（秘匿された細胞が当局の摘発を逃れ、
    /// 大衆へ浸透し、武装を整え、体制の弱体に乗じて革命の機運を醸成できるか）。
    /// </para>
    /// 全入力クランプ・乱数なし決定論・実効値パターン（基準値非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RevolutionaryCellRules
    {
        /// <summary>
        /// 細胞の秘匿（0..1）＝分散構造 compartmentalization×構成員規律 memberDiscipline×秘匿重み。
        /// 細胞を分散させ（横の連絡を断ち）構成員が規律を守るほど秘匿が立つ＝どちらか欠ければ秘匿は崩れる。
        /// </summary>
        public static float CellSecurity(float compartmentalization, float memberDiscipline, RevolutionaryCellParams p)
        {
            float t = Mathf.Clamp01(compartmentalization) * Mathf.Clamp01(memberDiscipline);
            return Mathf.Clamp01(t * p.securityWeight);
        }

        public static float CellSecurity(float compartmentalization, float memberDiscipline)
            => CellSecurity(compartmentalization, memberDiscipline, RevolutionaryCellParams.Default);

        /// <summary>
        /// 急進化＝過激化（0..1）＝不満の強度 grievanceIntensity×弾圧体験 repressionExperience の幾何平均×重み。
        /// 強い不満が弾圧の体験と結びつくと過激化する（どちらか0なら急進化しない＝幾何平均）。
        /// </summary>
        public static float Radicalization(float grievanceIntensity, float repressionExperience, RevolutionaryCellParams p)
        {
            float g = Mathf.Clamp01(grievanceIntensity);
            float r = Mathf.Clamp01(repressionExperience);
            float geo = Mathf.Sqrt(g * r);
            return Mathf.Clamp01(geo * p.radicalizationWeight);
        }

        public static float Radicalization(float grievanceIntensity, float repressionExperience)
            => Radicalization(grievanceIntensity, repressionExperience, RevolutionaryCellParams.Default);

        /// <summary>
        /// 大衆への浸透（0..1）＝宣伝の到達 propagandaReach×大衆の不満 popularGrievance×浸透重み。
        /// 宣伝が届き、かつ大衆に不満があって初めて浸透する＝届かない宣伝も満ち足りた大衆も浸透しない。
        /// </summary>
        public static float MassPenetration(float propagandaReach, float popularGrievance, RevolutionaryCellParams p)
        {
            float t = Mathf.Clamp01(propagandaReach) * Mathf.Clamp01(popularGrievance);
            return Mathf.Clamp01(t * p.penetrationWeight);
        }

        public static float MassPenetration(float propagandaReach, float popularGrievance)
            => MassPenetration(propagandaReach, popularGrievance, RevolutionaryCellParams.Default);

        /// <summary>
        /// 武装蜂起の準備（0..1）＝武器備蓄 weaponsCache×軍事訓練 militaryTraining の幾何平均×重み。
        /// 武器があっても扱えねば蜂起できず、訓練があっても武器が無ければ戦えない（どちらか0なら準備不足＝幾何平均）。
        /// </summary>
        public static float ArmamentReadiness(float weaponsCache, float militaryTraining, RevolutionaryCellParams p)
        {
            float w = Mathf.Clamp01(weaponsCache);
            float m = Mathf.Clamp01(militaryTraining);
            float geo = Mathf.Sqrt(w * m);
            return Mathf.Clamp01(geo * p.armamentWeight);
        }

        public static float ArmamentReadiness(float weaponsCache, float militaryTraining)
            => ArmamentReadiness(weaponsCache, militaryTraining, RevolutionaryCellParams.Default);

        /// <summary>
        /// 当局による摘発リスク（0..1）＝当局の諜報 stateIntelligence×(1-細胞秘匿 cellSecurity)×重み。
        /// 当局の諜報が強く、かつ細胞の秘匿が甘いほど摘発される＝秘匿が完璧なら諜報が強くても露見しない。
        /// </summary>
        public static float StateInfiltration(float stateIntelligence, float cellSecurity, RevolutionaryCellParams p)
        {
            float intel = Mathf.Clamp01(stateIntelligence);
            float exposure = 1f - Mathf.Clamp01(cellSecurity);
            return Mathf.Clamp01(intel * exposure * p.infiltrationWeight);
        }

        public static float StateInfiltration(float stateIntelligence, float cellSecurity)
            => StateInfiltration(stateIntelligence, cellSecurity, RevolutionaryCellParams.Default);

        /// <summary>
        /// 組織の生存（0..1）＝細胞秘匿 cellSecurity×大衆浸透 massPenetration×(1-摘発リスク stateInfiltration) の幾何平均（三乗根）。
        /// 秘匿で身を隠し、大衆に根を張り、摘発を逃れる三本柱がそろうほど生き延びる＝どれか1本崩れると組織は潰れる。
        /// </summary>
        public static float OrganizationalSurvival(float cellSecurity, float massPenetration, float stateInfiltration, RevolutionaryCellParams p)
        {
            float survival = Mathf.Clamp01(cellSecurity)
                           * Mathf.Clamp01(massPenetration)
                           * (1f - Mathf.Clamp01(stateInfiltration));
            float geo = Mathf.Pow(Mathf.Max(0f, survival), 1f / 3f);
            return Mathf.Clamp01(geo);
        }

        public static float OrganizationalSurvival(float cellSecurity, float massPenetration, float stateInfiltration)
            => OrganizationalSurvival(cellSecurity, massPenetration, stateInfiltration, RevolutionaryCellParams.Default);

        /// <summary>
        /// 革命の機運（0..1）＝大衆浸透 massPenetration×急進化 radicalization×体制の弱体 regimeWeakness の幾何平均（三乗根）×重み。
        /// 大衆に浸透し、組織が過激化し、体制が弱っている三つがそろって革命の機運が醸成される＝どれか欠ければ機は熟さない。
        /// </summary>
        public static float RevolutionaryMomentum(float massPenetration, float radicalization, float regimeWeakness, RevolutionaryCellParams p)
        {
            float prod = Mathf.Clamp01(massPenetration) * Mathf.Clamp01(radicalization) * Mathf.Clamp01(regimeWeakness);
            float geo = Mathf.Pow(Mathf.Max(0f, prod), 1f / 3f);
            return Mathf.Clamp01(geo * p.momentumWeight);
        }

        public static float RevolutionaryMomentum(float massPenetration, float radicalization, float regimeWeakness)
            => RevolutionaryMomentum(massPenetration, radicalization, regimeWeakness, RevolutionaryCellParams.Default);

        /// <summary>既定の蜂起閾値（革命機運×武装準備がこれ以上で武装蜂起＝革命が起こせる）。</summary>
        public const float DefaultRevolutionThreshold = 0.3f;

        /// <summary>
        /// 革命が起こせるか＝革命機運 revolutionaryMomentum×武装準備 armamentReadiness が threshold 以上。
        /// 機運があっても丸腰なら蜂起できず、武装があっても機運が無ければ大衆はついてこない＝両方が要る。
        /// </summary>
        public static bool IsRevolutionViable(float revolutionaryMomentum, float armamentReadiness, float threshold)
        {
            float t = Mathf.Clamp01(revolutionaryMomentum) * Mathf.Clamp01(armamentReadiness);
            return t >= Mathf.Clamp01(threshold);
        }

        public static bool IsRevolutionViable(float revolutionaryMomentum, float armamentReadiness)
            => IsRevolutionViable(revolutionaryMomentum, armamentReadiness, DefaultRevolutionThreshold);
    }
}
