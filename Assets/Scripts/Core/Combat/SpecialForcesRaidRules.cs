using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 特殊部隊襲撃の調整値（#特殊部隊襲撃）。浸透の効き・打撃の効き・脱出の効き・非対称効果の係数。
    /// すべて 0 以上にクランプして保持（ctor で正規化）。
    /// </summary>
    public readonly struct SpecialForcesRaidParams
    {
        /// <summary>浸透の鋭さ（隠密/(隠密+警戒) に掛ける指数。1.0で線形、>1で強者ほど通る）。</summary>
        public readonly float infiltrationSharpness;
        /// <summary>目標打撃の効き（部隊能力/(部隊+堅固) の打撃倍率。大きいほど一撃が重い）。</summary>
        public readonly float strikeStrength;
        /// <summary>脱出計画の効き（脱出計画と敵対応の綱引きの強さ）。</summary>
        public readonly float exfilStrength;
        /// <summary>非対称効果の係数（小部隊で挙げる大戦果のレバレッジ）。</summary>
        public readonly float asymmetryStrength;

        public SpecialForcesRaidParams(float infiltrationSharpness, float strikeStrength,
            float exfilStrength, float asymmetryStrength)
        {
            this.infiltrationSharpness = Mathf.Max(0.0001f, infiltrationSharpness);
            this.strikeStrength = Mathf.Max(0f, strikeStrength);
            this.exfilStrength = Mathf.Max(0f, exfilStrength);
            this.asymmetryStrength = Mathf.Max(0f, asymmetryStrength);
        }

        /// <summary>既定：浸透鋭さ1.0（線形）・打撃1.0・脱出1.0・非対称1.0。</summary>
        public static SpecialForcesRaidParams Default => new SpecialForcesRaidParams(
            DefaultInfiltrationSharpness, DefaultStrikeStrength, DefaultExfilStrength, DefaultAsymmetryStrength);

        public const float DefaultInfiltrationSharpness = 1.0f;
        public const float DefaultStrikeStrength = 1.0f;
        public const float DefaultExfilStrength = 1.0f;
        public const float DefaultAsymmetryStrength = 1.0f;

        /// <summary>襲撃が割に合うかの既定閾値（成否-損失の正味がこれ以上なら実行）。</summary>
        public const float DefaultWorthwhileThreshold = 0.0f;
    }

    /// <summary>
    /// 特殊部隊襲撃（精鋭小部隊の奇襲・破壊工作・斬首・要人救出）の純ロジック
    /// （#特殊部隊襲撃・test-first・盤面非依存）。隠密浸透→奇襲達成→高価値目標への打撃→脱出、
    /// その積で作戦の成否を決める。失敗時は精鋭を失い（非対称な損失）、成功時は小兵力で大戦果（非対称な効果）。
    /// <b>分担</b>：<see cref="DecapitationStrikeRules"/>（艦隊規模で敵旗艦＝指揮中枢を正面から討つ戦法）とは別＝
    /// こちらは少数精鋭の隠密作戦（戦闘の表に出ない奇襲）。<see cref="AmbushRules"/>（待ち伏せ）・
    /// <see cref="BoardingRules"/>（白兵での乗り込み）とも別＝浸透→打撃→脱出の一連の特殊作戦サイクルを表す。
    /// 実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版・符号付き出力は -1..1。
    /// </summary>
    public static class SpecialForcesRaidRules
    {
        // ── 隠密浸透 ──────────────────────────────────────────────

        /// <summary>既定パラメータで浸透の成功度を返す。</summary>
        public static float InfiltrationSuccess(float stealthSkill, float enemyAlertness)
            => InfiltrationSuccess(stealthSkill, enemyAlertness, SpecialForcesRaidParams.Default);

        /// <summary>
        /// 敵地への隠密浸透の成功度（0..1）。隠密技術と敵警戒の綱引き：
        /// `success = pow(隠密/(隠密+警戒), sharpness)`。
        /// 警戒0＝必ず浸透(1)、隠密0＝浸透できない(0)、互角で 0.5（sharpness=1）。
        /// </summary>
        public static float InfiltrationSuccess(float stealthSkill, float enemyAlertness, SpecialForcesRaidParams p)
        {
            float stealth = Mathf.Max(0f, stealthSkill);
            float alert = Mathf.Max(0f, enemyAlertness);

            if (stealth <= 0f) return 0f;       // 隠密皆無＝浸透不能
            if (alert <= 0f) return 1f;         // 警戒皆無＝完全浸透

            float ratio = stealth / (stealth + alert);
            return Mathf.Clamp01(Mathf.Pow(ratio, p.infiltrationSharpness));
        }

        // ── 奇襲の達成 ────────────────────────────────────────────

        /// <summary>既定パラメータで奇襲の達成度を返す。</summary>
        public static float SurpriseAchieved(float infiltrationSuccess, float timingPrecision)
            => SurpriseAchieved(infiltrationSuccess, timingPrecision, SpecialForcesRaidParams.Default);

        /// <summary>
        /// 奇襲の達成度（0..1）。浸透できて初めて奇襲が成り、タイミングの精度で完成する：
        /// `surprise = 浸透 * タイミング精度`。どちらか欠ければ奇襲は崩れる。
        /// </summary>
        public static float SurpriseAchieved(float infiltrationSuccess, float timingPrecision, SpecialForcesRaidParams p)
        {
            float infil = Mathf.Clamp01(infiltrationSuccess);
            float timing = Mathf.Clamp01(timingPrecision);
            return Mathf.Clamp01(infil * timing);
        }

        // ── 目標への打撃 ──────────────────────────────────────────

        /// <summary>既定パラメータで目標への打撃を返す。</summary>
        public static float TargetStrike(float surpriseAchieved, float teamCapability, float targetHardness)
            => TargetStrike(surpriseAchieved, teamCapability, targetHardness, SpecialForcesRaidParams.Default);

        /// <summary>
        /// 高価値目標への打撃（0..1）。奇襲を成立させた上で、部隊能力と標的の堅固さの綱引き：
        /// `strike = surprise * strikeStrength * 部隊/(部隊+堅固)`。
        /// 奇襲ゼロ＝打撃ゼロ、部隊0＝打撃0、堅固0＝部隊ぶん満額（surprise×strikeStrength で頭打ち）。
        /// </summary>
        public static float TargetStrike(float surpriseAchieved, float teamCapability, float targetHardness, SpecialForcesRaidParams p)
        {
            float surprise = Mathf.Clamp01(surpriseAchieved);
            float team = Mathf.Max(0f, teamCapability);
            if (surprise <= 0f || team <= 0f) return 0f;

            float hard = Mathf.Max(0f, targetHardness);
            float penetration = team / (team + hard);    // 堅固0＝1.0
            return Mathf.Clamp01(surprise * p.strikeStrength * penetration);
        }

        // ── 脱出 ──────────────────────────────────────────────────

        /// <summary>既定パラメータで脱出の成功度を返す。</summary>
        public static float Exfiltration(float infiltrationSuccess, float extractionPlan, float enemyResponse)
            => Exfiltration(infiltrationSuccess, extractionPlan, enemyResponse, SpecialForcesRaidParams.Default);

        /// <summary>
        /// 作戦後の脱出の成功度（0..1）。入った経路（浸透）と脱出計画を、敵の事後対応が削る：
        /// `exfil = 浸透 * extractionPlan^exfilStrength * (1 - 敵対応)`。
        /// 敵対応1.0＝退路封鎖で脱出0、敵対応0＝計画ぶん満額、浸透0＝そもそも戻れない。
        /// exfilStrength=0 なら計画項は 1（計画は効かず浸透×敵対応のみ）。
        /// </summary>
        public static float Exfiltration(float infiltrationSuccess, float extractionPlan, float enemyResponse, SpecialForcesRaidParams p)
        {
            float infil = Mathf.Clamp01(infiltrationSuccess);
            float plan = Mathf.Clamp01(extractionPlan);
            float response = Mathf.Clamp01(enemyResponse);
            if (infil <= 0f) return 0f;

            float planFactor = Mathf.Pow(plan, p.exfilStrength); // exfilStrength=0 → 1
            return Mathf.Clamp01(infil * planFactor * (1f - response));
        }

        // ── 作戦の成否 ────────────────────────────────────────────

        /// <summary>既定パラメータで作戦の成否を返す。</summary>
        public static float RaidSuccess(float targetStrike, float exfiltration)
            => RaidSuccess(targetStrike, exfiltration, SpecialForcesRaidParams.Default);

        /// <summary>
        /// 襲撃作戦全体の成否（0..1）。目標を打撃でき、かつ精鋭が脱出できて初めて完遂：
        /// `success = 打撃 * 脱出`。打てても戻れなければ（または戻れても打てなければ）作戦は成立しない。
        /// </summary>
        public static float RaidSuccess(float targetStrike, float exfiltration, SpecialForcesRaidParams p)
        {
            float strike = Mathf.Clamp01(targetStrike);
            float exfil = Mathf.Clamp01(exfiltration);
            return Mathf.Clamp01(strike * exfil);
        }

        // ── 失敗時の損失 ──────────────────────────────────────────

        /// <summary>既定パラメータで失敗時の損失を返す。</summary>
        public static float LossOnFailure(float surpriseAchieved, float enemyResponse)
            => LossOnFailure(surpriseAchieved, enemyResponse, SpecialForcesRaidParams.Default);

        /// <summary>
        /// 失敗時に精鋭を失う損失度（0..1）。奇襲を取れなかったぶんだけ、敵の対応が精鋭を捕捉する：
        /// `loss = (1 - 奇襲) * 敵対応`。奇襲を完全に取れば（surprise=1）損失0、
        /// 奇襲を取れず敵対応も激しいほど精鋭を喪う（小部隊ゆえ損失は致命的）。
        /// </summary>
        public static float LossOnFailure(float surpriseAchieved, float enemyResponse, SpecialForcesRaidParams p)
        {
            float surprise = Mathf.Clamp01(surpriseAchieved);
            float response = Mathf.Clamp01(enemyResponse);
            return Mathf.Clamp01((1f - surprise) * response);
        }

        // ── 非対称な効果 ──────────────────────────────────────────

        /// <summary>既定パラメータで非対称な効果を返す。</summary>
        public static float AsymmetricImpact(float targetStrike, float targetValue)
            => AsymmetricImpact(targetStrike, targetValue, SpecialForcesRaidParams.Default);

        /// <summary>
        /// 小部隊が挙げる非対称な戦果（0..1）。打撃に標的の価値を掛けてレバレッジする：
        /// `impact = clamp01(打撃 * targetValue * asymmetryStrength)`。
        /// 高価値目標（targetValue 高＝要人/枢要施設）を討てば、小兵力で大戦果＝戦略的衝撃。
        /// </summary>
        public static float AsymmetricImpact(float targetStrike, float targetValue, SpecialForcesRaidParams p)
        {
            float strike = Mathf.Clamp01(targetStrike);
            float value = Mathf.Clamp01(targetValue);
            return Mathf.Clamp01(strike * value * p.asymmetryStrength);
        }

        // ── 割に合うか ────────────────────────────────────────────

        /// <summary>既定閾値で襲撃が割に合うかを返す。</summary>
        public static bool IsRaidWorthwhile(float raidSuccess, float lossOnFailure)
            => IsRaidWorthwhile(raidSuccess, lossOnFailure, SpecialForcesRaidParams.DefaultWorthwhileThreshold);

        /// <summary>
        /// 襲撃が割に合うか（bool）。成否の期待利益から失敗時の損失を引いた正味が閾値以上なら実行：
        /// `(成否 - 損失) >= 閾値`。閾値は -1..1 にクランプ。
        /// </summary>
        public static bool IsRaidWorthwhile(float raidSuccess, float lossOnFailure, float threshold)
        {
            float success = Mathf.Clamp01(raidSuccess);
            float loss = Mathf.Clamp01(lossOnFailure);
            float th = Mathf.Clamp(threshold, -1f, 1f);
            return (success - loss) >= th;
        }
    }
}
