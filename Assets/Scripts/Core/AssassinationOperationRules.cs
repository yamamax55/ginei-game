using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 要人暗殺工作（ゲーム内の政治的除去工作）の調整値。襲撃成功・標的の生存余地・実行犯捕縛・
    /// 首謀者露見・政治的混乱・殉教効果・正味戦略価値の各係数。すべて 0 以上にクランプして保持（ctor で正規化）。
    /// <see cref="AssassinationParams"/>（地球教型テロの成否解決）とは別物＝こちらは余波まで含めた工作評価。
    /// </summary>
    public readonly struct AssassinationOperationParams
    {
        /// <summary>標的の頑健さが生存に効く強さ（大きいほど成功度が高くても生き延びやすい）。</summary>
        public readonly float survivalStrength;
        /// <summary>脱出計画が捕縛を軽減する強さ（大きいほど逃げ切りやすい）。</summary>
        public readonly float escapeStrength;
        /// <summary>警備対応が捕縛を増やす強さ（大きいほど捕まりやすい）。</summary>
        public readonly float responseStrength;
        /// <summary>秘匿の甘さ（=1-秘匿）が露見に効く強さ。</summary>
        public readonly float exposureStrength;
        /// <summary>後継不在が政治的混乱を増幅する強さ。</summary>
        public readonly float chaosStrength;
        /// <summary>標的の人望が殉教効果（敵対勢力結束）に効く強さ。</summary>
        public readonly float martyrStrength;

        public AssassinationOperationParams(float survivalStrength, float escapeStrength, float responseStrength,
            float exposureStrength, float chaosStrength, float martyrStrength)
        {
            this.survivalStrength = Mathf.Max(0f, survivalStrength);
            this.escapeStrength = Mathf.Max(0f, escapeStrength);
            this.responseStrength = Mathf.Max(0f, responseStrength);
            this.exposureStrength = Mathf.Max(0f, exposureStrength);
            this.chaosStrength = Mathf.Max(0f, chaosStrength);
            this.martyrStrength = Mathf.Max(0f, martyrStrength);
        }

        /// <summary>既定：生存強さ1.0・脱出0.5・警備対応1.0・露見1.0・混乱1.0・殉教1.0。</summary>
        public static AssassinationOperationParams Default => new AssassinationOperationParams(
            DefaultSurvivalStrength, DefaultEscapeStrength, DefaultResponseStrength,
            DefaultExposureStrength, DefaultChaosStrength, DefaultMartyrStrength);

        public const float DefaultSurvivalStrength = 1.0f;
        public const float DefaultEscapeStrength = 0.5f;
        public const float DefaultResponseStrength = 1.0f;
        public const float DefaultExposureStrength = 1.0f;
        public const float DefaultChaosStrength = 1.0f;
        public const float DefaultMartyrStrength = 1.0f;

        /// <summary>暗殺の実行可否の既定閾値（正味価値がこれ以上で実行に値する）。</summary>
        public const float DefaultViableThreshold = 0.5f;
        /// <summary>暗殺が「実行に値する」とみなす襲撃成功度の下限（これ未満は論外）。</summary>
        public const float MinViableSuccess = 0.3f;
    }

    /// <summary>
    /// 要人暗殺工作（ゲーム内の政治的除去工作）の純ロジック（test-first・盤面非依存・決定論）。
    /// 銀英伝で起きる宮廷の陰謀・要人襲撃をゲーム数値で抽象化する。実在の人物を対象とするものではない。
    /// 流れ＝襲撃成功度（技量×好機 vs 護衛）→標的が生き延びる余地（頑健さ）→実行犯の捕縛（脱出計画/警備対応）
    /// →首謀者の露見（実行犯捕縛×秘匿の甘さ）→成立後の政治的余波（混乱・殉教効果）→正味戦略価値→実行可否。
    /// <b>分担</b>：<see cref="AssassinationRules"/>（地球教型テロの成否・露見の roll 解決）の上位＝
    /// こちらは「割に合うか」までを評価する工作プランナー。会戦で敵旗艦を討つ
    /// <see cref="DecapitationStrikeRules"/>（戦術）とも別＝戦略レイヤーの陰謀モデル。
    /// 指揮系統の臨時継承は <see cref="BattlefieldCommandRules"/>、勢力間の関係は外交ルール群が担う。
    /// 実効値パターン（基準値非破壊・倍率/度合いを返す）・各メソッドに Params 明示版＋Default 委譲版。
    /// </summary>
    public static class AssassinationOperationRules
    {
        // ── 襲撃成功度 ────────────────────────────────────────────

        /// <summary>既定パラメータで襲撃成功度を返す。</summary>
        public static float AttemptSuccess(float assassinSkill, float guardStrength, float opportunity)
            => AttemptSuccess(assassinSkill, guardStrength, opportunity, AssassinationOperationParams.Default);

        /// <summary>
        /// 襲撃が護衛を破って標的に届く度合い（0..1）。
        /// `success = (技量*好機) / (技量*好機 + 護衛)`。
        /// 護衛0＝必ず成功(1)、技量か好機が0＝届かない(0)。攻撃力(技量×好機)と護衛が互角で 0.5。
        /// </summary>
        public static float AttemptSuccess(float assassinSkill, float guardStrength, float opportunity, AssassinationOperationParams p)
        {
            float skill = Mathf.Max(0f, assassinSkill);
            float opp = Mathf.Clamp01(opportunity);
            float guard = Mathf.Max(0f, guardStrength);

            float offense = skill * opp;
            if (offense <= 0f) return 0f;       // 技量か好機が無い＝届かない
            if (guard <= 0f) return 1f;         // 護衛皆無＝必中

            return offense / (offense + guard);
        }

        // ── 標的の生存余地 ────────────────────────────────────────

        /// <summary>既定パラメータで標的の生存度を返す。</summary>
        public static float TargetSurvival(float attemptSuccess, float targetVitality)
            => TargetSurvival(attemptSuccess, targetVitality, AssassinationOperationParams.Default);

        /// <summary>
        /// 襲撃が成功しても標的が生き延びる余地（0..1。1＝生存／0＝死）。
        /// `survival = 1 - 成功度 / (1 + survivalStrength*頑健さ)`。
        /// 成功度が高いほど生存は下がるが、頑健さが高いほど生存余地が残る。成功度0＝完全生存。
        /// </summary>
        public static float TargetSurvival(float attemptSuccess, float targetVitality, AssassinationOperationParams p)
        {
            float success = Mathf.Clamp01(attemptSuccess);
            float vitality = Mathf.Clamp01(targetVitality);

            float lethality = success / (1f + p.survivalStrength * vitality);
            return Mathf.Clamp01(1f - lethality);
        }

        // ── 実行犯の捕縛 ──────────────────────────────────────────

        /// <summary>既定パラメータで実行犯捕縛確率を返す。</summary>
        public static float AssassinCapture(float attemptSuccess, float escapePlan, float securityResponse)
            => AssassinCapture(attemptSuccess, escapePlan, securityResponse, AssassinationOperationParams.Default);

        /// <summary>
        /// 実行犯が捕縛される確率（0..1）。
        /// 露見の素地（成功し騒ぎになるほど追われる＝base = 0.5 + 0.5*成功度）に、
        /// 警備対応で `*(1+responseStrength*対応)`、脱出計画で `*(1-escapeStrength*計画)` を掛けて 0..1 に頭打ち。
        /// 脱出計画が万全なら大きく軽減、警備が固いと増える。
        /// </summary>
        public static float AssassinCapture(float attemptSuccess, float escapePlan, float securityResponse, AssassinationOperationParams p)
        {
            float success = Mathf.Clamp01(attemptSuccess);
            float escape = Mathf.Clamp01(escapePlan);
            float response = Mathf.Clamp01(securityResponse);

            float baseRisk = 0.5f + 0.5f * success;                 // 騒ぎが大きいほど追われる
            float responded = baseRisk * (1f + p.responseStrength * response);
            float evaded = responded * Mathf.Max(0f, 1f - p.escapeStrength * escape);
            return Mathf.Clamp01(evaded);
        }

        // ── 首謀者の露見 ──────────────────────────────────────────

        /// <summary>既定パラメータで首謀者露見確率を返す。</summary>
        public static float MastermindExposure(float assassinCapture, float operationalSecurity)
            => MastermindExposure(assassinCapture, operationalSecurity, AssassinationOperationParams.Default);

        /// <summary>
        /// 実行犯の捕縛から首謀者が露見する確率（0..1）。
        /// 秘匿の甘さ `slack = 1 - 秘匿` に対し `exposure = 捕縛 * exposureStrength * slack` を 0..1 に頭打ち。
        /// 実行犯が捕まっても秘匿が完璧（slack=0）なら首謀者は割れない。捕縛0＝露見0。
        /// </summary>
        public static float MastermindExposure(float assassinCapture, float operationalSecurity, AssassinationOperationParams p)
        {
            float capture = Mathf.Clamp01(assassinCapture);
            float security = Mathf.Clamp01(operationalSecurity);
            float slack = 1f - security;

            return Mathf.Clamp01(capture * p.exposureStrength * slack);
        }

        // ── 政治的混乱 ────────────────────────────────────────────

        /// <summary>既定パラメータで政治的混乱度を返す。</summary>
        public static float PoliticalChaos(float targetImportance, float successionClarity)
            => PoliticalChaos(targetImportance, successionClarity, AssassinationOperationParams.Default);

        /// <summary>
        /// 標的を除去した後の政治的混乱（0..1）。標的の重要度が高く後継が不在なほど大きい。
        /// `chaos = 重要度 * (1 + chaosStrength*(1-後継明確度)) / (1 + chaosStrength)` を 0..1 に正規化。
        /// 後継が完全に明確（successionClarity=1）なら混乱は重要度に対して抑えられ、不在なら最大化。
        /// </summary>
        public static float PoliticalChaos(float targetImportance, float successionClarity, AssassinationOperationParams p)
        {
            float importance = Mathf.Clamp01(targetImportance);
            float clarity = Mathf.Clamp01(successionClarity);
            float vacancy = 1f - clarity;

            float amplified = importance * (1f + p.chaosStrength * vacancy);
            float norm = amplified / (1f + p.chaosStrength);    // 後継不在(vacancy=1)で重要度そのもの
            return Mathf.Clamp01(norm);
        }

        // ── 殉教効果 ──────────────────────────────────────────────

        /// <summary>既定パラメータで殉教効果を返す。</summary>
        public static float MartyrEffect(float targetPopularity)
            => MartyrEffect(targetPopularity, AssassinationOperationParams.Default);

        /// <summary>
        /// 人望ある標的を殺したことで生じる殉教効果（0..1＝敵対勢力の結束＝暗殺側のコスト）。
        /// `martyr = clamp01(martyrStrength * 人望)`。無名の標的（人望0）なら殉教効果なし。
        /// </summary>
        public static float MartyrEffect(float targetPopularity, AssassinationOperationParams p)
        {
            float popularity = Mathf.Clamp01(targetPopularity);
            return Mathf.Clamp01(p.martyrStrength * popularity);
        }

        // ── 正味戦略価値 ──────────────────────────────────────────

        /// <summary>
        /// 暗殺の正味戦略価値（-1..1）。敵を混乱させる利益から、殉教効果と首謀者露見のコストを差し引く。
        /// `gain = clamp(混乱 - 殉教 - 露見, -1, 1)`。混乱がコストを上回れば正＝暗殺が割に合う。
        /// </summary>
        public static float NetStrategicGain(float politicalChaos, float martyrEffect, float mastermindExposure)
        {
            float chaos = Mathf.Clamp01(politicalChaos);
            float martyr = Mathf.Clamp01(martyrEffect);
            float exposure = Mathf.Clamp01(mastermindExposure);
            return Mathf.Clamp(chaos - martyr - exposure, -1f, 1f);
        }

        // ── 実行可否 ──────────────────────────────────────────────

        /// <summary>既定閾値で暗殺の実行可否を返す。</summary>
        public static bool IsAssassinationViable(float attemptSuccess, float netStrategicGain)
            => IsAssassinationViable(attemptSuccess, netStrategicGain, AssassinationOperationParams.DefaultViableThreshold);

        /// <summary>
        /// 暗殺が実行に値するか。襲撃成功の見込みが下限（<see cref="AssassinationOperationParams.MinViableSuccess"/>）以上で、
        /// かつ正味戦略価値が閾値以上のとき true。閾値は 0..1 にクランプ。
        /// 成功の見込みが薄い、または割に合わない（露見・殉教が混乱を食う）なら見送り。
        /// </summary>
        public static bool IsAssassinationViable(float attemptSuccess, float netStrategicGain, float threshold)
        {
            float success = Mathf.Clamp01(attemptSuccess);
            float gain = Mathf.Clamp(netStrategicGain, -1f, 1f);
            float th = Mathf.Clamp01(threshold);
            return success >= AssassinationOperationParams.MinViableSuccess && gain >= th;
        }
    }
}
