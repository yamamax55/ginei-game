using UnityEngine;

namespace Ginei
{
    /// <summary>世代断絶（失われた世代）の調整係数（#1416）。</summary>
    public readonly struct GenerationalWoundParams
    {
        /// <summary>世代喪失の影響が顕在化するまでの遅延年数（20〜30年後＝遅効）。</summary>
        public readonly float impactDelayYears;
        /// <summary>質の高い人材ほど前線で死ぬ＝指導者欠乏の増幅（下士官・若手将校の喪失）。</summary>
        public readonly float talentSelectionBias;
        /// <summary>人材プールの1世代あたりの回復速度（一度失った世代は買い戻せない＝遅い）。</summary>
        public readonly float poolRecoveryRate;
        /// <summary>戦闘曝露が生存者へ残す心の傷の最大強度。</summary>
        public readonly float traumaScale;
        /// <summary>人材プール枯渇が国家を年あたりに弱体化させる割合（静かな衰退）。</summary>
        public readonly float declineRate;
        /// <summary>一世代が丸ごと失われたとみなす損耗率の既定しきい値。</summary>
        public readonly float lostThreshold;

        public GenerationalWoundParams(
            float impactDelayYears, float talentSelectionBias, float poolRecoveryRate,
            float traumaScale, float declineRate, float lostThreshold)
        {
            this.impactDelayYears = Mathf.Max(1f, impactDelayYears);
            this.talentSelectionBias = Mathf.Clamp01(talentSelectionBias);
            this.poolRecoveryRate = Mathf.Clamp01(poolRecoveryRate);
            this.traumaScale = Mathf.Clamp01(traumaScale);
            this.declineRate = Mathf.Max(0f, declineRate);
            this.lostThreshold = Mathf.Clamp01(lostThreshold);
        }

        /// <summary>既定＝顕在化25年・人材選別バイアス0.3・回復0.2/世代・傷0.8・衰退0.05/年・喪失判定0.5。</summary>
        public static GenerationalWoundParams Default
            => new GenerationalWoundParams(25f, 0.3f, 0.2f, 0.8f, 0.05f, 0.5f);
    }

    /// <summary>
    /// 世代断絶＝失われた世代（Lost Generation）の純ロジック（#1416・レマルク『西部戦線異状なし』型）。
    /// 大量の戦死が一つの世代を丸ごと奪い、20〜30年後にその世代が担うはずだった指導層・熟練層を空洞化させて
    /// 国家を長期的に弱体化させる＝失われた世代の<b>遅効</b>（<see cref="DelayedImpactTick"/> が核）。生き残った者も心に傷を負う。
    /// <see cref="GenerationalMemoryRules"/>（戦争記憶の風化＝平時の忘却）とは別系統＝こちらは大量戦死が将来の指導者層を空洞化させる。
    /// <see cref="LifecycleRules"/>（個人の加齢死亡）・<see cref="DemographicsRules"/>（人口動態の量）・
    /// <see cref="MentorshipRules"/>（師弟の世代間伝承）とも分担＝こちらは人口でなく<b>質</b>（指導者・熟練層）の喪失を扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class GenerationalWoundRules
    {
        /// <summary>
        /// 世代の損耗率（0..1）＝若年層の戦死（youthCasualties 0..1）でその世代がどれだけ失われたか。
        /// 出征した世代の規模（cohortSize 0..1）が大きいほど一国の世代全体への打撃になる＝両者の積。
        /// </summary>
        public static float GenerationLoss(float youthCasualties, float cohortSize)
        {
            return Mathf.Clamp01(youthCasualties) * Mathf.Clamp01(cohortSize);
        }

        /// <summary>
        /// 将来の指導者・専門家の欠乏（0..1）＝失われた世代から出るはずだった指導層の喪失。
        /// 質の高い人材ほど前線で死ぬ（下士官・若手将校が真っ先に倒れる）ため、世代損耗を talentSelectionBias 分だけ増幅。
        /// leadershipFraction（その世代に占める指導者候補の比率 0..1）が高いほど痛手が大きい。
        /// </summary>
        public static float FutureLeaderShortage(float generationLoss, float leadershipFraction, GenerationalWoundParams p)
        {
            float loss = Mathf.Clamp01(generationLoss);
            float frac = Mathf.Clamp01(leadershipFraction);
            float amplified = Mathf.Clamp01(loss * (1f + p.talentSelectionBias));
            return amplified * frac;
        }

        public static float FutureLeaderShortage(float generationLoss, float leadershipFraction)
            => FutureLeaderShortage(generationLoss, leadershipFraction, GenerationalWoundParams.Default);

        /// <summary>
        /// 世代喪失の<b>遅効</b>（0..1）＝影響は impactDelayYears 後に顕在化する。yearsElapsed（戦後経過 0..1で正規化）が
        /// 0 のときはまだ表面化せず、遅延年数に達したとき世代損耗の全量が指導層欠落として祟る。dt 年ぶん顕在化を進める。
        /// 「今日の戦死が未来の指導層欠落として遅れて現れる」を式にしたもの（この遅効が失われた世代の核）。
        /// </summary>
        public static float DelayedImpactTick(float manifested, float generationLoss, float yearsElapsed, float dt, GenerationalWoundParams p)
        {
            float current = Mathf.Clamp01(manifested);
            float loss = Mathf.Clamp01(generationLoss);
            float elapsed = Mathf.Clamp01(yearsElapsed);
            float years = Mathf.Max(0f, dt);
            // 顕在化の進行速度＝遅延年数の逆数で1世代かけて滲み出す。経過が遅延に近いほど目標値（全量）へ寄る。
            float target = loss * elapsed;
            float rate = years / p.impactDelayYears;
            return Mathf.Clamp01(Mathf.MoveTowards(current, target, rate));
        }

        public static float DelayedImpactTick(float manifested, float generationLoss, float yearsElapsed, float dt)
            => DelayedImpactTick(manifested, generationLoss, yearsElapsed, dt, GenerationalWoundParams.Default);

        /// <summary>
        /// 人材プールの枯渇（0..1）＝失われた世代の分だけ人材プールが枯れる。回復には数世代かかる
        /// （recoveryGenerations 0..1＝既に回復に充てた世代数の進捗）。一度失った世代は買い戻せないため、
        /// 回復は poolRecoveryRate を上限に緩やかにしか進まない。
        /// </summary>
        public static float TalentPoolDepletion(float generationLoss, float recoveryGenerations, GenerationalWoundParams p)
        {
            float loss = Mathf.Clamp01(generationLoss);
            float recovery = Mathf.Clamp01(recoveryGenerations) * p.poolRecoveryRate;
            return Mathf.Clamp01(loss * (1f - recovery));
        }

        public static float TalentPoolDepletion(float generationLoss, float recoveryGenerations)
            => TalentPoolDepletion(generationLoss, recoveryGenerations, GenerationalWoundParams.Default);

        /// <summary>
        /// 生存者の心の傷（0..1）＝生き残った者も深い傷を負う。戦闘曝露（combatExposure 0..1）が直接の原因で、
        /// 世代が大きく失われた（generationLoss）ほど周囲の喪失も重なって傷が深まる＝社会復帰困難。
        /// </summary>
        public static float SurvivorTrauma(float combatExposure, float generationLoss, GenerationalWoundParams p)
        {
            float exposure = Mathf.Clamp01(combatExposure);
            float loss = Mathf.Clamp01(generationLoss);
            // 曝露を基礎に、喪失で最大2倍まで増幅（仲間の死が傷を深める）。
            return Mathf.Clamp01(exposure * (1f + loss) * p.traumaScale);
        }

        public static float SurvivorTrauma(float combatExposure, float generationLoss)
            => SurvivorTrauma(combatExposure, generationLoss, GenerationalWoundParams.Default);

        /// <summary>
        /// 制度的知識の断絶（0..1）＝指導層の断絶で技術・経験の世代間伝承が途切れる。将来の指導者欠乏
        /// （futureLeaderShortage）が深いほど、また師弟の連鎖が断たれている（mentorshipBroken 0..1）ほど大きい
        /// （<see cref="MentorshipRules"/> 連動＝師がいない）。両者が揃って初めて断絶が決定的になる。
        /// </summary>
        public static float InstitutionalKnowledgeGap(float futureLeaderShortage, float mentorshipBroken)
        {
            float shortage = Mathf.Clamp01(futureLeaderShortage);
            float broken = Mathf.Clamp01(mentorshipBroken);
            // 欠乏を基礎に、伝承の断絶が最大2倍まで増幅（師がいないと欠乏が知識の喪失へ直結）。
            return Mathf.Clamp01(shortage * (1f + broken));
        }

        /// <summary>
        /// 世代の傷による国家の長期弱体化を dt 年ぶん進める（0..1）＝人口でなく質の喪失による静かな衰退。
        /// 人材プールの枯渇（talentPoolDepletion）が深いほど declineRate×dt で単調に進む。
        /// </summary>
        public static float NationalDeclineFromWound(float decline, float talentPoolDepletion, float dt, GenerationalWoundParams p)
        {
            float current = Mathf.Clamp01(decline);
            float depletion = Mathf.Clamp01(talentPoolDepletion);
            float years = Mathf.Max(0f, dt);
            return Mathf.Clamp01(current + depletion * p.declineRate * years);
        }

        public static float NationalDeclineFromWound(float decline, float talentPoolDepletion, float dt)
            => NationalDeclineFromWound(decline, talentPoolDepletion, dt, GenerationalWoundParams.Default);

        /// <summary>
        /// 一世代が丸ごと失われたかの判定＝世代損耗（generationLoss）がしきい値（threshold 0..1）以上。
        /// 「大量の戦死が一世代を丸ごと奪った」＝失われた世代の成立。
        /// </summary>
        public static bool IsLostGeneration(float generationLoss, float threshold)
        {
            return Mathf.Clamp01(generationLoss) >= Mathf.Clamp01(threshold);
        }

        public static bool IsLostGeneration(float generationLoss)
            => IsLostGeneration(generationLoss, GenerationalWoundParams.Default.lostThreshold);
    }
}
