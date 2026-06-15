using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 行政エネルギーと単一執政の純ロジック（FED-5 #1489・『ザ・フェデラリスト』第70篇ハミルトン）。
    /// 「行政部のエネルギー（energy in the executive）は良い統治の本質的要素＝単一の執政（unity＝合議制でなく一人）が
    /// 決断の速さ・活力・秘密保持・責任の明確さを生む。複数執政は対立で麻痺し責任が拡散する」をモデル化。
    /// 核は<b>行政エネルギー（統一の活力）と権力集中の危険のトレードオフ</b>＝単一執政は決断が速く責任が明確だが、
    /// 権力集中はクーデター/専制リスクと裏腹で、<b>任期制限と責任の明確さがその危険を抑える</b>（マディソン的歯止め）。
    /// <see cref="CivilianControlRules"/>（軍政関係＝文民が軍を統制するか）・<see cref="CoupRules"/>（クーデターの発生/帰結）・
    /// <see cref="TermLimitRules"/>（権力の時間制約＝任期制限の慣習）・<see cref="EmergencyPowersRules"/>（非常大権＝憲法停止）とは別＝
    /// こちらは<b>単一執政の決断力</b>そのもの（行政エネルギー vs 権力集中）。任期制限・クーデターは上記へ委譲し read-only に参照する想定。
    /// 乱数は持たない（決定論）。調整値は <see cref="ExecutiveEnergyParams"/> に集約。test-first。
    /// </summary>
    public static class ExecutiveEnergyRules
    {
        /// <summary>
        /// 執政の統一（0..1）＝執政の数が少ない（単一）ほど統一が高い。
        /// numberOfExecutives は「合議制の度合い」（0＝一人＝完全な単一執政／1＝多人数の合議制）。
        /// 一人なら統一は最大、合議制ほど統一は失われる（補数＝1−合議度）。
        /// </summary>
        public static float ExecutiveUnity(float numberOfExecutives)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(numberOfExecutives));
        }

        /// <summary>
        /// 決断速度（0..1）＝統一が高いほど決断が速い（一人なら即決・合議は遅い）。
        /// 決断力（decisiveness＝執政個人の果断さ）を、統一が下限 <see cref="ExecutiveEnergyParams.PluralityDecisionFloor"/> から
        /// 満額（1.0）へ写像した倍率で実効化する＝合議制では果断さがあっても会議で薄まる。
        /// </summary>
        public static float DecisionSpeed(float executiveUnity, float decisiveness, ExecutiveEnergyParams p)
        {
            float unity = Mathf.Clamp01(executiveUnity);
            float decisive = Mathf.Clamp01(decisiveness);
            // 統一度で決断の通りやすさが下限から満額へ＝合議は同じ果断さでも遅い
            float unityFactor = Mathf.Lerp(p.PluralityDecisionFloor, 1f, unity);
            return Mathf.Clamp01(decisive * unityFactor);
        }

        /// <summary>決断速度（既定パラメータ）。</summary>
        public static float DecisionSpeed(float executiveUnity, float decisiveness)
            => DecisionSpeed(executiveUnity, decisiveness, ExecutiveEnergyParams.Default);

        /// <summary>
        /// 責任の明確さ（0..1）＝単一執政は責任の所在が明確（誰の責任か分かる＝責任を隠せない）。
        /// 統一が高いほど責任は一点に集まり、合議制では責任が拡散して「誰の責任でもない」になる
        /// ＝統一度に比例（合議制でも最低限の責任は残る下限 <see cref="ExecutiveEnergyParams.PluralAccountabilityFloor"/>）。
        /// </summary>
        public static float Accountability(float executiveUnity, ExecutiveEnergyParams p)
        {
            float unity = Mathf.Clamp01(executiveUnity);
            return Mathf.Clamp01(Mathf.Lerp(p.PluralAccountabilityFloor, 1f, unity));
        }

        /// <summary>責任の明確さ（既定パラメータ）。</summary>
        public static float Accountability(float executiveUnity)
            => Accountability(executiveUnity, ExecutiveEnergyParams.Default);

        /// <summary>
        /// 複数執政の麻痺（0..1）＝複数執政は内部対立で麻痺する（責任の拡散と相互妨害）。
        /// 合議度（numberOfExecutives）と内部不和（internalDiscord）の積に比例＝一人なら麻痺は起きず、
        /// 多人数かつ対立が深いほど相互妨害で決断が止まる（カルタゴの執政官、ローマの三頭政治の轍）。
        /// </summary>
        public static float PluralityParalysis(float numberOfExecutives, float internalDiscord, ExecutiveEnergyParams p)
        {
            float plurality = Mathf.Clamp01(numberOfExecutives);
            float discord = Mathf.Clamp01(internalDiscord);
            return Mathf.Clamp01(plurality * discord * p.ParalysisScale);
        }

        /// <summary>複数執政の麻痺（既定パラメータ）。</summary>
        public static float PluralityParalysis(float numberOfExecutives, float internalDiscord)
            => PluralityParalysis(numberOfExecutives, internalDiscord, ExecutiveEnergyParams.Default);

        /// <summary>
        /// 行政エネルギーと権力集中の危険のトレードオフ（0..1）＝統一の活力（energy）と引き換えに生じる危険。
        /// 統一が高いほど危険（権力集中）は大きいが、制度的制約（institutionalConstraint）がそれを抑える
        /// ＝危険＝統一 ×（1−制度的制約）。制約が満点なら統一の活力は危険を伴わない（憲法・議会・司法の歯止め）。
        /// </summary>
        public static float EnergyVsSafety(float executiveUnity, float institutionalConstraint, ExecutiveEnergyParams p)
        {
            float unity = Mathf.Clamp01(executiveUnity);
            float constraint = Mathf.Clamp01(institutionalConstraint);
            // 統一の活力が生む危険を制度的制約が割り引く＝エネルギーと安全のトレードオフ
            return Mathf.Clamp01(unity * (1f - constraint) * p.ConcentrationDangerScale);
        }

        /// <summary>行政エネルギーと安全のトレードオフ（既定パラメータ）。</summary>
        public static float EnergyVsSafety(float executiveUnity, float institutionalConstraint)
            => EnergyVsSafety(executiveUnity, institutionalConstraint, ExecutiveEnergyParams.Default);

        /// <summary>
        /// 権力集中によるクーデター/専制リスク（0..1）＝統一（集中）はリスクを上げるが、
        /// 任期制限（termLimit）と責任の明確さ（accountability）がそれを抑える（マディソン的歯止め）。
        /// 生のリスク＝統一に比例、歯止め＝任期制限と責任の明確さの加重平均で割り引く
        /// ＝集中しても、いつか退き（任期制限）責任を負う（明確さ）なら専制には至りにくい。
        /// </summary>
        public static float CoupRiskFromConcentration(float executiveUnity, float termLimit, float accountability, ExecutiveEnergyParams p)
        {
            float unity = Mathf.Clamp01(executiveUnity);
            float term = Mathf.Clamp01(termLimit);
            float account = Mathf.Clamp01(accountability);
            // 歯止め＝任期制限と責任の明確さの加重平均（どちらも集中の危険を抑える）
            float check = term * p.TermLimitCheckWeight + account * (1f - p.TermLimitCheckWeight);
            float rawRisk = unity * p.ConcentrationDangerScale;
            return Mathf.Clamp01(rawRisk * (1f - check));
        }

        /// <summary>権力集中のクーデターリスク（既定パラメータ）。</summary>
        public static float CoupRiskFromConcentration(float executiveUnity, float termLimit, float accountability)
            => CoupRiskFromConcentration(executiveUnity, termLimit, accountability, ExecutiveEnergyParams.Default);

        /// <summary>
        /// 危機対応能力（0..1）＝危機に素早く対応する能力（単一執政の真価＝非常時の決断）。
        /// 決断速度（decisionSpeed）が主、行政エネルギー（energy＝活力・士気・秘密保持の総合）が従＝
        /// 決断が速くてもエネルギーがなければ実行が伴わない（速さ ×（下限＋活力ぶん））。
        /// </summary>
        public static float CrisisResponseCapacity(float decisionSpeed, float energy, ExecutiveEnergyParams p)
        {
            float speed = Mathf.Clamp01(decisionSpeed);
            float vigor = Mathf.Clamp01(energy);
            // 活力が下限から満額へ実行力を底上げ＝決断を行動に移す力
            float energyFactor = Mathf.Lerp(p.MinEnergyFactor, 1f, vigor);
            return Mathf.Clamp01(speed * energyFactor);
        }

        /// <summary>危機対応能力（既定パラメータ）。</summary>
        public static float CrisisResponseCapacity(float decisionSpeed, float energy)
            => CrisisResponseCapacity(decisionSpeed, energy, ExecutiveEnergyParams.Default);

        /// <summary>
        /// 活力ある責任明確な行政（良い執政）の判定＝決断速度と責任の明確さの両方が閾値以上。
        /// ハミルトンの理想＝速く決め、かつ誰の責任か明確（活力だけでも責任だけでも良い執政ではない）。
        /// </summary>
        public static bool IsEnergeticExecutive(float decisionSpeed, float accountability, float threshold)
        {
            float t = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(decisionSpeed) >= t && Mathf.Clamp01(accountability) >= t;
        }
    }

    /// <summary>
    /// ExecutiveEnergyRules の調整値（マジックナンバー集約・ctor で全値クランプ）。既定は <see cref="Default"/>。
    /// 合議制でも下限の決断・責任は残し（ゼロ割れ防止）、統一の活力は制度的制約で割り引かれる前提を既定値で担保する。
    /// </summary>
    public readonly struct ExecutiveEnergyParams
    {
        /// <summary>合議制（統一0）でも残る決断の通りやすさの下限（0..1）。</summary>
        public readonly float PluralityDecisionFloor;
        /// <summary>合議制（統一0）でも残る責任の明確さの下限（0..1）。</summary>
        public readonly float PluralAccountabilityFloor;
        /// <summary>複数執政の麻痺の規模係数（0..1）。</summary>
        public readonly float ParalysisScale;
        /// <summary>権力集中が生む危険の規模係数（0..1）。</summary>
        public readonly float ConcentrationDangerScale;
        /// <summary>クーデターリスクの歯止めにおける任期制限の重み（残りは責任の明確さ・0..1）。</summary>
        public readonly float TermLimitCheckWeight;
        /// <summary>危機対応で活力ゼロでも残る実行力の下限（0..1）。</summary>
        public readonly float MinEnergyFactor;

        public ExecutiveEnergyParams(
            float pluralityDecisionFloor, float pluralAccountabilityFloor, float paralysisScale,
            float concentrationDangerScale, float termLimitCheckWeight, float minEnergyFactor)
        {
            PluralityDecisionFloor = Mathf.Clamp01(pluralityDecisionFloor);
            PluralAccountabilityFloor = Mathf.Clamp01(pluralAccountabilityFloor);
            ParalysisScale = Mathf.Clamp01(paralysisScale);
            ConcentrationDangerScale = Mathf.Clamp01(concentrationDangerScale);
            TermLimitCheckWeight = Mathf.Clamp01(termLimitCheckWeight);
            MinEnergyFactor = Mathf.Clamp01(minEnergyFactor);
        }

        /// <summary>
        /// 既定（合議制の決断下限0.3・合議制の責任下限0.2・麻痺規模1.0・集中危険規模0.8・任期制限の歯止め重み0.5・活力下限0.4）。
        /// 合議制は決断が3割・責任は2割まで落ち、統一の活力は制度的制約・任期制限・責任の明確さで割り引かれる。
        /// </summary>
        public static ExecutiveEnergyParams Default => new ExecutiveEnergyParams(
            pluralityDecisionFloor: 0.3f, pluralAccountabilityFloor: 0.2f, paralysisScale: 1f,
            concentrationDangerScale: 0.8f, termLimitCheckWeight: 0.5f, minEnergyFactor: 0.4f);
    }
}
