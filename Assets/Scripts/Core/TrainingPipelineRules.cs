using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 訓練パイプラインの調整値（新兵を戦力化する流れ）。教官の律速比・施設係数・急造ペナルティ等。
    /// 盤面非依存の plain 引数で扱う。test-first。
    /// </summary>
    public readonly struct TrainingPipelineParams
    {
        /// <summary>教官1人が同時に面倒を見られる新兵数（訓練の律速）。</summary>
        public readonly float traineesPerInstructor;
        /// <summary>標準的な訓練期間（この期間で練度が満ちる目安・game-日）。</summary>
        public readonly float standardDuration;
        /// <summary>急造ペナルティの強さ（期間不足が損耗に効く度合い）。</summary>
        public readonly float rushPenaltyScale;
        /// <summary>実戦1回あたりの練度上昇量（生き残った会戦数に掛ける）。</summary>
        public readonly float experiencePerBattle;
        /// <summary>施設1単位あたりが収容できる新兵数（パイプライン上限の係数）。</summary>
        public readonly float capacityPerFacility;

        public TrainingPipelineParams(
            float traineesPerInstructor,
            float standardDuration,
            float rushPenaltyScale,
            float experiencePerBattle,
            float capacityPerFacility)
        {
            this.traineesPerInstructor = Mathf.Max(0.01f, traineesPerInstructor);
            this.standardDuration = Mathf.Max(0.01f, standardDuration);
            this.rushPenaltyScale = Mathf.Clamp01(rushPenaltyScale);
            this.experiencePerBattle = Mathf.Clamp01(experiencePerBattle);
            this.capacityPerFacility = Mathf.Max(0.01f, capacityPerFacility);
        }

        /// <summary>既定：教官1人につき20名・標準30日・急造ペナルティ0.6・実戦0.1/回・施設1単位100名。</summary>
        public static TrainingPipelineParams Default => new TrainingPipelineParams(
            DefaultTraineesPerInstructor,
            DefaultStandardDuration,
            DefaultRushPenaltyScale,
            DefaultExperiencePerBattle,
            DefaultCapacityPerFacility);

        public const float DefaultTraineesPerInstructor = 20f;
        public const float DefaultStandardDuration = 30f;
        public const float DefaultRushPenaltyScale = 0.6f;
        public const float DefaultExperiencePerBattle = 0.1f;
        public const float DefaultCapacityPerFacility = 100f;
    }

    /// <summary>
    /// 訓練パイプライン（兵卒の新兵を訓練して戦力化する流れ）の純ロジック。
    /// 募集した新兵を訓練の質・教官の数・期間で練度に変える。急いで前線に出すと損耗が大きく、
    /// じっくり訓練すると時間がかかる。実戦経験が最も練度を上げる（生き残れば）。
    /// 盤面非依存の plain 引数・乱数なし・入力クランプ・実効値パターン（基準値非破壊）・test-first。
    /// <para>分担（混同しない）：</para>
    /// <list type="bullet">
    /// <item><see cref="OfficerAcademyRules"/>/<see cref="MilitaryAcademyRules"/> は<b>士官（将校）教育</b>＝人物の任官選抜。本クラスは<b>兵卒（新兵）の訓練パイプライン</b>＝部隊の戦力化で別物。</item>
    /// <item><see cref="VeterancyRules"/> は会戦を経た<b>練度の段階</b>（新兵/一般/精鋭/古参）。本クラスは訓練による<b>初期練度</b>を作る側で別物。</item>
    /// </list>
    /// </summary>
    public static class TrainingPipelineRules
    {
        /// <summary>練度（quality）の基準スケール 0..1。</summary>
        public const float MaxQuality = 1f;
        /// <summary>十分に訓練されたと見なす既定しきい値。</summary>
        public const float DefaultTrainedThreshold = 0.6f;

        // ── 訓練の処理能力（教官比で律速） ─────────────────────────────

        /// <summary>既定 Params で訓練の処理能力を返す。</summary>
        public static float TrainingThroughput(float instructorCount, float traineeIntake)
            => TrainingThroughput(instructorCount, traineeIntake, TrainingPipelineParams.Default);

        /// <summary>
        /// 訓練の処理能力（実際に訓練できる新兵数）。教官が見られる総数 = 教官数×<see cref="TrainingPipelineParams.traineesPerInstructor"/> と
        /// 入校した新兵数(traineeIntake) の小さい方で律速する（教官が足りなければ入校しても捌けない）。
        /// </summary>
        public static float TrainingThroughput(float instructorCount, float traineeIntake, TrainingPipelineParams p)
        {
            float instructors = Mathf.Max(0f, instructorCount);
            float intake = Mathf.Max(0f, traineeIntake);
            float instructorCapacity = instructors * p.traineesPerInstructor;
            return Mathf.Min(instructorCapacity, intake);
        }

        // ── 修了時の練度（期間×質） ───────────────────────────────────

        /// <summary>
        /// 訓練修了時の練度 0..1。訓練の質(trainingQuality 0..1)を、期間が標準(<see cref="TrainingPipelineParams.standardDuration"/>)に
        /// 達していなければ <see cref="RushTrainingPenalty"/> ぶん割り引く。期間が標準以上なら質そのまま（過剰訓練で上振れはしない）。
        /// </summary>
        public static float TraineeQuality(float trainingDuration, float trainingQuality)
            => TraineeQuality(trainingDuration, trainingQuality, TrainingPipelineParams.Default);

        /// <summary>訓練修了時の練度 0..1（Params 明示版）。</summary>
        public static float TraineeQuality(float trainingDuration, float trainingQuality, TrainingPipelineParams p)
        {
            float quality = Mathf.Clamp01(trainingQuality);
            float penalty = RushTrainingPenalty(trainingDuration, p); // 0..1（端折るほど大きい）
            return Mathf.Clamp01(quality * (1f - penalty));
        }

        // ── 戦力化できる割合 ──────────────────────────────────────────

        /// <summary>
        /// 期間内に戦力化できた割合 0..1 ＝ 処理能力(throughput) / 入校数(intake)。
        /// 入校0なら0（誰も育っていない）。throughput が intake を超えても1で頭打ち。
        /// </summary>
        public static float GraduationRate(float trainingThroughput, float traineeIntake)
        {
            float intake = Mathf.Max(0f, traineeIntake);
            if (intake <= 0f) return 0f;
            float graduated = Mathf.Clamp(trainingThroughput, 0f, intake);
            return Mathf.Clamp01(graduated / intake);
        }

        // ── 急造ペナルティ ────────────────────────────────────────────

        /// <summary>既定 Params で急造ペナルティを返す。</summary>
        public static float RushTrainingPenalty(float trainingDuration)
            => RushTrainingPenalty(trainingDuration, TrainingPipelineParams.Default);

        /// <summary>
        /// 訓練を端折った（標準期間に満たない）ぶんの練度不足＝損耗ペナルティ 0..1。
        /// 不足率 = clamp01((標準 - 期間)/標準) に <see cref="TrainingPipelineParams.rushPenaltyScale"/> を掛ける。
        /// 期間が標準以上なら0（ペナルティなし）。
        /// </summary>
        public static float RushTrainingPenalty(float trainingDuration, TrainingPipelineParams p)
        {
            float duration = Mathf.Max(0f, trainingDuration);
            float shortfall = Mathf.Clamp01((p.standardDuration - duration) / p.standardDuration);
            return Mathf.Clamp01(shortfall * p.rushPenaltyScale);
        }

        // ── 実戦経験 ──────────────────────────────────────────────────

        /// <summary>既定 Params で実戦経験による練度上昇後の値を返す。</summary>
        public static float CombatExperienceGain(int survivedBattles, float baseQuality)
            => CombatExperienceGain(survivedBattles, baseQuality, TrainingPipelineParams.Default);

        /// <summary>
        /// 実戦（生き残った会戦数）による練度上昇後の練度 0..1。実戦が最も練度を上げる＝
        /// baseQuality に survivedBattles×<see cref="TrainingPipelineParams.experiencePerBattle"/> を加算（上限1）。
        /// 生き残った会戦だけが効く（負数は0扱い）。
        /// </summary>
        public static float CombatExperienceGain(int survivedBattles, float baseQuality, TrainingPipelineParams p)
        {
            float baseQ = Mathf.Clamp01(baseQuality);
            int battles = survivedBattles > 0 ? survivedBattles : 0;
            float gain = battles * p.experiencePerBattle;
            return Mathf.Clamp01(baseQ + gain);
        }

        // ── 教官の前線流出 ────────────────────────────────────────────

        /// <summary>
        /// 教官を前線に出したことで訓練に残る割合 0..1 ＝ (総教官 - 前線へ) / 総教官。
        /// 教官を抜くほど訓練が滞る（残存比が下がる）。総教官0なら0（そもそも回らない）。
        /// </summary>
        public static float InstructorDrain(float instructorsToFront, float totalInstructors)
        {
            float total = Mathf.Max(0f, totalInstructors);
            if (total <= 0f) return 0f;
            float toFront = Mathf.Clamp(instructorsToFront, 0f, total);
            float remaining = total - toFront;
            return Mathf.Clamp01(remaining / total);
        }

        // ── パイプライン上限 ──────────────────────────────────────────

        /// <summary>既定 Params でパイプライン上限を返す。</summary>
        public static float PipelineCapacity(float instructorCount, float facilityCapacity)
            => PipelineCapacity(instructorCount, facilityCapacity, TrainingPipelineParams.Default);

        /// <summary>
        /// 訓練パイプラインの上限（同時に育てられる新兵数）＝ 教官能力と施設収容の小さい方で律速。
        /// 教官能力 = 教官数×<see cref="TrainingPipelineParams.traineesPerInstructor"/>、施設収容 = facilityCapacity×<see cref="TrainingPipelineParams.capacityPerFacility"/>。
        /// </summary>
        public static float PipelineCapacity(float instructorCount, float facilityCapacity, TrainingPipelineParams p)
        {
            float instructors = Mathf.Max(0f, instructorCount);
            float facility = Mathf.Max(0f, facilityCapacity);
            float byInstructors = instructors * p.traineesPerInstructor;
            float byFacility = facility * p.capacityPerFacility;
            return Mathf.Min(byInstructors, byFacility);
        }

        // ── 戦力化判定 ────────────────────────────────────────────────

        /// <summary>練度が既定しきい値以上で十分に訓練されたと見なす。</summary>
        public static bool IsFullyTrained(float traineeQuality)
            => IsFullyTrained(traineeQuality, DefaultTrainedThreshold);

        /// <summary>練度(0..1)がしきい値以上なら十分に訓練されたと見なす（戦力として送り出せる）。</summary>
        public static bool IsFullyTrained(float traineeQuality, float threshold)
        {
            float quality = Mathf.Clamp01(traineeQuality);
            float th = Mathf.Clamp01(threshold);
            return quality >= th;
        }
    }
}
