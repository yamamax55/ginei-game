using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 指揮所（旗艦の作戦室＝艦隊司令部）の調整値。幕僚の規模・処理能力の効き、状況把握の伸び、
    /// 意思決定の速さ、被狙撃脆弱性・斬首リスク、継承の効き、中枢喪失の麻痺ペナルティ。
    /// すべて ctor でクランプ。実効値パターン（基準値は別に持ち、ここは係数だけ）。
    /// </summary>
    public readonly struct CommandPostParams
    {
        /// <summary>処理能力が飽和する幕僚の標準人数（この人数で頭数係数1.0）。</summary>
        public readonly float staffNormCount;
        /// <summary>状況把握の伸び係数（情報流入×処理能力にこれを掛ける）。</summary>
        public readonly float awarenessGain;
        /// <summary>意思決定の速さの伸び係数（状況把握×決断力にこれを掛ける）。</summary>
        public readonly float tempoGain;
        /// <summary>脆弱性の伸び係数（放射兆候×(1-防御態勢)にこれを掛ける）。</summary>
        public readonly float vulnerabilityGain;
        /// <summary>斬首リスクの伸び係数（脆弱性×敵照準にこれを掛ける）。</summary>
        public readonly float decapitationGain;
        /// <summary>継承の伸び係数（代替指揮所×権限委譲にこれを掛ける）。</summary>
        public readonly float successionGain;
        /// <summary>中枢喪失の麻痺が実効を削る重み（意思決定の速さから麻痺×この重みを引く）。</summary>
        public readonly float paralysisWeight;

        public CommandPostParams(
            float staffNormCount,
            float awarenessGain,
            float tempoGain,
            float vulnerabilityGain,
            float decapitationGain,
            float successionGain,
            float paralysisWeight)
        {
            this.staffNormCount = Mathf.Max(1f, staffNormCount);
            this.awarenessGain = Mathf.Clamp01(awarenessGain);
            this.tempoGain = Mathf.Clamp01(tempoGain);
            this.vulnerabilityGain = Mathf.Clamp01(vulnerabilityGain);
            this.decapitationGain = Mathf.Clamp01(decapitationGain);
            this.successionGain = Mathf.Clamp01(successionGain);
            this.paralysisWeight = Mathf.Clamp01(paralysisWeight);
        }

        /// <summary>既定：標準幕僚10名・状況把握1.0・速さ1.0・脆弱1.0・斬首1.0・継承1.0・麻痺重み1.0。</summary>
        public static CommandPostParams Default => new CommandPostParams(
            DefaultStaffNormCount, DefaultAwarenessGain, DefaultTempoGain, DefaultVulnerabilityGain,
            DefaultDecapitationGain, DefaultSuccessionGain, DefaultParalysisWeight);

        public const float DefaultStaffNormCount = 10f;
        public const float DefaultAwarenessGain = 1.0f;
        public const float DefaultTempoGain = 1.0f;
        public const float DefaultVulnerabilityGain = 1.0f;
        public const float DefaultDecapitationGain = 1.0f;
        public const float DefaultSuccessionGain = 1.0f;
        public const float DefaultParalysisWeight = 1.0f;
    }

    /// <summary>
    /// 指揮所＝司令部の機能と脆弱性の純ロジック（盤面非依存）。
    /// <b>幕僚と処理能力で状況を把握し、決断力で素早く意思決定する</b>一方、指揮所は放射兆候で狙われ、
    /// 斬首（指揮中枢の喪失）を受ければ代替指揮所と権限委譲が無いほど麻痺する。
    /// 実効＝意思決定の速さ−麻痺リスク。係数は線形/多項式で組む（Log/Exp 不使用＝終盤ラグ規律・決定論）。
    /// 実効値パターン（基準値非破壊）。
    /// <para>
    /// 分担：<see cref="CommandDelayRules"/>（命令が末端に届く伝達ラグ）・<see cref="BattlefieldCommandRules"/>
    /// （指揮官戦死時の臨時指揮継承）・<see cref="DecapitationStrikeRules"/>（斬首打撃の戦果）とは
    /// <b>連携するが別物</b>＝こちらは「司令部そのものの処理能力と被狙撃脆弱性・継承での麻痺」を数値化する。
    /// </para>
    /// </summary>
    public static class CommandPostRules
    {
        // ---- 指揮所の処理能力（幕僚数×練度） ----

        /// <summary>既定パラメータで指揮所の処理能力を返す。</summary>
        public static float StaffProcessing(float staffCount, float staffCompetence)
            => StaffProcessing(staffCount, staffCompetence, CommandPostParams.Default);

        /// <summary>
        /// 幕僚の頭数（標準人数で飽和）と練度（0..1）から指揮所の処理能力（0..1）を返す。
        /// `processing = clamp01(staffCount/staffNormCount) * competence`。
        /// 幕僚が薄い／練度が低いと司令部の捌ける情報が少ない。
        /// </summary>
        public static float StaffProcessing(float staffCount, float staffCompetence, CommandPostParams p)
        {
            float headcount = Mathf.Clamp01(Mathf.Max(0f, staffCount) / p.staffNormCount);
            float competence = Mathf.Clamp01(staffCompetence);
            return Mathf.Clamp01(headcount * competence);
        }

        // ---- 状況把握（情報流入×処理能力） ----

        /// <summary>既定パラメータで状況把握を返す。</summary>
        public static float SituationalAwareness(float intelInflow, float staffProcessing)
            => SituationalAwareness(intelInflow, staffProcessing, CommandPostParams.Default);

        /// <summary>
        /// 情報の流入（0..1）と指揮所の処理能力（0..1）から状況把握（0..1）を返す。
        /// `awareness = clamp01(inflow * processing * awarenessGain)`。
        /// 情報が入っても捌けねば把握できず、捌けても情報が来なければ把握できない。
        /// </summary>
        public static float SituationalAwareness(float intelInflow, float staffProcessing, CommandPostParams p)
        {
            float inflow = Mathf.Clamp01(intelInflow);
            float processing = Mathf.Clamp01(staffProcessing);
            return Mathf.Clamp01(inflow * processing * p.awarenessGain);
        }

        // ---- 意思決定の速さ（状況把握×決断力） ----

        /// <summary>既定パラメータで意思決定の速さを返す。</summary>
        public static float DecisionTempo(float situationalAwareness, float commanderDecisiveness)
            => DecisionTempo(situationalAwareness, commanderDecisiveness, CommandPostParams.Default);

        /// <summary>
        /// 状況把握（0..1）と指揮官の決断力（0..1）から意思決定の速さ（0..1）を返す。
        /// `tempo = clamp01(awareness * decisiveness * tempoGain)`。
        /// 把握していても決断できねば遅く、決断力があっても見えていなければ速く動けない。
        /// </summary>
        public static float DecisionTempo(float situationalAwareness, float commanderDecisiveness, CommandPostParams p)
        {
            float awareness = Mathf.Clamp01(situationalAwareness);
            float decisiveness = Mathf.Clamp01(commanderDecisiveness);
            return Mathf.Clamp01(awareness * decisiveness * p.tempoGain);
        }

        // ---- 指揮所の被狙撃脆弱性（放射兆候×(1-防御態勢)） ----

        /// <summary>既定パラメータで指揮所の脆弱性を返す。</summary>
        public static float CommandPostVulnerability(float signatureEmission, float defensivePosture)
            => CommandPostVulnerability(signatureEmission, defensivePosture, CommandPostParams.Default);

        /// <summary>
        /// 放射兆候（通信・索敵の電磁放射 0..1）と防御態勢（0..1）から指揮所の被狙撃脆弱性（0..1）を返す。
        /// `vuln = clamp01(emission * (1 - posture) * vulnerabilityGain)`。
        /// 司令部は活発に通信するほど位置を晒し、防御態勢が整うほど狙われにくい。
        /// </summary>
        public static float CommandPostVulnerability(float signatureEmission, float defensivePosture, CommandPostParams p)
        {
            float emission = Mathf.Clamp01(signatureEmission);
            float posture = Mathf.Clamp01(defensivePosture);
            return Mathf.Clamp01(emission * (1f - posture) * p.vulnerabilityGain);
        }

        // ---- 斬首リスク（脆弱性×敵照準） ----

        /// <summary>既定パラメータで斬首リスクを返す。</summary>
        public static float DecapitationRisk(float commandPostVulnerability, float enemyTargeting)
            => DecapitationRisk(commandPostVulnerability, enemyTargeting, CommandPostParams.Default);

        /// <summary>
        /// 指揮所の脆弱性（0..1）と敵の照準（指揮中枢を狙う集中 0..1）から斬首リスク（0..1）を返す。
        /// `risk = clamp01(vuln * targeting * decapitationGain)`。
        /// 脆弱でも敵が狙っていなければリスクは低く、敵が狙っても脆弱でなければ命中しない。
        /// </summary>
        public static float DecapitationRisk(float commandPostVulnerability, float enemyTargeting, CommandPostParams p)
        {
            float vuln = Mathf.Clamp01(commandPostVulnerability);
            float targeting = Mathf.Clamp01(enemyTargeting);
            return Mathf.Clamp01(vuln * targeting * p.decapitationGain);
        }

        // ---- 指揮継承（代替指揮所×権限委譲） ----

        /// <summary>既定パラメータで指揮継承を返す。</summary>
        public static float SuccessionContinuity(float alternateCommandPost, float delegationReadiness)
            => SuccessionContinuity(alternateCommandPost, delegationReadiness, CommandPostParams.Default);

        /// <summary>
        /// 代替指揮所の整備（0..1）と権限委譲の即応（後継者への委任 0..1）から指揮継承（0..1）を返す。
        /// `continuity = clamp01(alternate * delegation * successionGain)`。
        /// 代替司令部があっても委譲の段取りが無ければ繋がらず、委譲があっても受け皿が無ければ繋がらない。
        /// </summary>
        public static float SuccessionContinuity(float alternateCommandPost, float delegationReadiness, CommandPostParams p)
        {
            float alternate = Mathf.Clamp01(alternateCommandPost);
            float delegation = Mathf.Clamp01(delegationReadiness);
            return Mathf.Clamp01(alternate * delegation * p.successionGain);
        }

        // ---- 中枢喪失の麻痺（斬首リスク×(1-継承)） ----

        /// <summary>既定パラメータで中枢喪失の麻痺を返す。</summary>
        public static float ParalysisOnLoss(float decapitationRisk, float successionContinuity)
            => ParalysisOnLoss(decapitationRisk, successionContinuity, CommandPostParams.Default);

        /// <summary>
        /// 斬首リスク（0..1）と指揮継承（0..1）から中枢喪失時の麻痺（0..1）を返す。
        /// `paralysis = clamp01(risk * (1 - continuity))`。
        /// 斬首を受けても継承が万全なら麻痺せず、継承が無ければ斬首の度合いそのまま麻痺する。
        /// </summary>
        public static float ParalysisOnLoss(float decapitationRisk, float successionContinuity, CommandPostParams p)
        {
            float risk = Mathf.Clamp01(decapitationRisk);
            float continuity = Mathf.Clamp01(successionContinuity);
            return Mathf.Clamp01(risk * (1f - continuity));
        }

        // ---- 指揮所の実効（意思決定の速さ−麻痺リスク） ----

        /// <summary>既定パラメータで指揮所の実効を返す。</summary>
        public static float CommandPostEffectiveness(float decisionTempo, float paralysisOnLoss)
            => CommandPostEffectiveness(decisionTempo, paralysisOnLoss, CommandPostParams.Default);

        /// <summary>
        /// 意思決定の速さ（0..1）から中枢喪失の麻痺リスク（重み付き）を差し引いた指揮所の実効（0..1）を返す。
        /// `effectiveness = clamp01(tempo - paralysisWeight * paralysis)`。
        /// 速く決断できても斬首で麻痺すれば司令部は機能せず、麻痺が無ければ速さがそのまま実効になる。
        /// </summary>
        public static float CommandPostEffectiveness(float decisionTempo, float paralysisOnLoss, CommandPostParams p)
        {
            float tempo = Mathf.Clamp01(decisionTempo);
            float paralysis = Mathf.Clamp01(paralysisOnLoss);
            return Mathf.Clamp01(tempo - p.paralysisWeight * paralysis);
        }
    }
}
