using UnityEngine;

namespace Ginei
{
    /// <summary>救護・衛生の調整係数。</summary>
    public readonly struct MedicalParams
    {
        /// <summary>損耗のうち即死の割合（戦闘で決まる＝救護では救えない）。</summary>
        public readonly float instantDeathRatio;
        /// <summary>損耗のうち軽傷の割合（救護に関係なく復帰する）。残りが重傷＝救護の主戦場。</summary>
        public readonly float lightWoundRatio;
        /// <summary>救護能力0のときの重傷者生存率。</summary>
        public readonly float minSevereSurvival;
        /// <summary>救護能力1のときの重傷者生存率。</summary>
        public readonly float maxSevereSurvival;
        /// <summary>救命した重傷者のうち戦列復帰できない（傷痍除隊）割合。</summary>
        public readonly float invalidShare;
        /// <summary>後送速度0のときの重傷者生存倍率の下限（黄金の1時間を逃した場合）。</summary>
        public readonly float goldenHourFloor;
        /// <summary>医療崩壊時（処理能力超過）の復帰率倍率の下限＝トリアージの底。</summary>
        public readonly float overloadFloor;
        /// <summary>復帰兵が保持する経験値の割合（負傷で多少鈍る＝それでも新兵0よりはるかに上）。</summary>
        public readonly float veteranXpRetention;
        /// <summary>救護能力1のときの士気加算（「撃たれても拾ってもらえる」安心）。</summary>
        public readonly float moraleAssuranceBonus;

        public MedicalParams(
            float instantDeathRatio, float lightWoundRatio,
            float minSevereSurvival, float maxSevereSurvival,
            float invalidShare, float goldenHourFloor, float overloadFloor,
            float veteranXpRetention, float moraleAssuranceBonus)
        {
            this.instantDeathRatio = Mathf.Clamp01(instantDeathRatio);
            // 即死＋軽傷が1を超えないようクランプ（残りが重傷枠）
            this.lightWoundRatio = Mathf.Clamp(lightWoundRatio, 0f, 1f - this.instantDeathRatio);
            this.minSevereSurvival = Mathf.Clamp01(minSevereSurvival);
            this.maxSevereSurvival = Mathf.Clamp(maxSevereSurvival, this.minSevereSurvival, 1f);
            this.invalidShare = Mathf.Clamp01(invalidShare);
            this.goldenHourFloor = Mathf.Clamp01(goldenHourFloor);
            this.overloadFloor = Mathf.Clamp01(overloadFloor);
            this.veteranXpRetention = Mathf.Clamp01(veteranXpRetention);
            this.moraleAssuranceBonus = Mathf.Max(0f, moraleAssuranceBonus);
        }

        /// <summary>既定＝即死30%・軽傷30%（重傷40%）・重傷生存20〜90%・傷痍除隊25%・黄金時間下限0.5・崩壊下限0.3・経験保持80%・士気加算0.1。</summary>
        public static MedicalParams Default =>
            new MedicalParams(0.3f, 0.3f, 0.2f, 0.9f, 0.25f, 0.5f, 0.3f, 0.8f, 0.1f);
    }

    /// <summary>損耗の内訳（保存則：dead + returning + invalided = 損耗合計）。</summary>
    public readonly struct CasualtyOutcome
    {
        /// <summary>戦死（即死＋救命できなかった重傷者）。</summary>
        public readonly float dead;
        /// <summary>治療後に戦列復帰する数（軽傷＋救命した重傷者の復帰分）。</summary>
        public readonly float returning;
        /// <summary>生存したが復帰できない傷痍除隊。</summary>
        public readonly float invalided;

        public CasualtyOutcome(float dead, float returning, float invalided)
        {
            this.dead = dead;
            this.returning = returning;
            this.invalided = invalided;
        }

        /// <summary>内訳の合計（＝入力の損耗。保存則の検算用）。</summary>
        public float Total => dead + returning + invalided;
    }

    /// <summary>
    /// 救護・衛生の純ロジック（人員の損耗処理）。損耗の内訳（即死/重傷/軽傷の比）は戦闘で決まるが、
    /// **重傷者が死ぬか戦列に戻るかは救護能力が決める**＝衛生への投資は帳簿に出ない「見えない兵力」。
    /// 戻る兵は歴戦の経験者であり、新兵補充による練度の希釈（<see cref="VeterancyRules.DiluteOnReinforce"/>）を防ぐ
    /// ＝衛生兵は一発も撃たずに戦力を生む「戦わない最強の兵科」。
    /// 艦体の修理は <see cref="RepairRules"/>（艦の修理＝別系統）が担い、ここは人員のみを扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MedicalRules
    {
        /// <summary>重傷者の生存率＝救護能力(0..1)で minSevereSurvival〜maxSevereSurvival を線形補間。</summary>
        public static float SevereSurvival(float medicalCapacity, MedicalParams p)
        {
            return Mathf.Lerp(p.minSevereSurvival, p.maxSevereSurvival, Mathf.Clamp01(medicalCapacity));
        }

        public static float SevereSurvival(float medicalCapacity)
            => SevereSurvival(medicalCapacity, MedicalParams.Default);

        /// <summary>
        /// 損耗の内訳。即死と軽傷は戦闘で決まり（params 比率）、重傷者の生死だけが救護能力で変わる。
        /// dead + returning + invalided = casualties（保存則）。
        /// </summary>
        public static CasualtyOutcome CasualtySplit(float casualties, float medicalCapacity, MedicalParams p)
        {
            return SplitWithSurvival(casualties, SevereSurvival(medicalCapacity, p), p);
        }

        public static CasualtyOutcome CasualtySplit(float casualties, float medicalCapacity)
            => CasualtySplit(casualties, medicalCapacity, MedicalParams.Default);

        /// <summary>
        /// 後送の速さ込みの内訳＝重傷生存率に <see cref="GoldenHourEffect(float,MedicalParams)"/> を乗算。
        /// 病院があっても運べなければ救えない。
        /// </summary>
        public static CasualtyOutcome CasualtySplit(float casualties, float medicalCapacity, float evacuationSpeed, MedicalParams p)
        {
            float survival = SevereSurvival(medicalCapacity, p) * GoldenHourEffect(evacuationSpeed, p);
            return SplitWithSurvival(casualties, survival, p);
        }

        public static CasualtyOutcome CasualtySplit(float casualties, float medicalCapacity, float evacuationSpeed)
            => CasualtySplit(casualties, medicalCapacity, evacuationSpeed, MedicalParams.Default);

        /// <summary>内訳の共通計算（重傷生存率を与えて分配）。</summary>
        private static CasualtyOutcome SplitWithSurvival(float casualties, float severeSurvival, MedicalParams p)
        {
            float total = Mathf.Max(0f, casualties);
            float severe = total * (1f - p.instantDeathRatio - p.lightWoundRatio);
            float saved = severe * Mathf.Clamp01(severeSurvival);
            float dead = total * p.instantDeathRatio + (severe - saved);
            float invalided = saved * p.invalidShare;
            float returning = total * p.lightWoundRatio + (saved - invalided);
            return new CasualtyOutcome(dead, returning, invalided);
        }

        /// <summary>
        /// 治療後復帰率＝損耗のうち戦列に戻る割合（軽傷＋重傷救命×復帰分）。
        /// 救護能力だけで決まる純関数＝「見えない兵力」の係数。
        /// </summary>
        public static float ReturnRate(float medicalCapacity, MedicalParams p)
        {
            float severeRatio = 1f - p.instantDeathRatio - p.lightWoundRatio;
            return p.lightWoundRatio + severeRatio * SevereSurvival(medicalCapacity, p) * (1f - p.invalidShare);
        }

        public static float ReturnRate(float medicalCapacity)
            => ReturnRate(medicalCapacity, MedicalParams.Default);

        /// <summary>
        /// 衛生投資の配当＝同じ損耗で「救護あり−救護なし」の復帰数の差。
        /// 衛生への投資が生む「見えない兵力」を数として見える化する（戦わずに戦力を生む式）。
        /// </summary>
        public static float MedicalDividend(float casualties, float medicalCapacity, MedicalParams p)
        {
            return Mathf.Max(0f, casualties) * (ReturnRate(medicalCapacity, p) - ReturnRate(0f, p));
        }

        public static float MedicalDividend(float casualties, float medicalCapacity)
            => MedicalDividend(casualties, medicalCapacity, MedicalParams.Default);

        /// <summary>
        /// 黄金の1時間＝後送の速さ(0..1)が重傷者の生存倍率を決める。
        /// 速度1で1.0（フル救命）、速度0で goldenHourFloor（病院に届く前に手遅れ）。重傷生存率に乗算して使う。
        /// </summary>
        public static float GoldenHourEffect(float evacuationSpeed, MedicalParams p)
        {
            return Mathf.Lerp(p.goldenHourFloor, 1f, Mathf.Clamp01(evacuationSpeed));
        }

        public static float GoldenHourEffect(float evacuationSpeed)
            => GoldenHourEffect(evacuationSpeed, MedicalParams.Default);

        /// <summary>
        /// 医療崩壊＝大量出血時のトリアージ。負傷者発生率(0..)が処理能力(0..1)以内なら倍率1.0、
        /// 超過すると capacity/casualtyRate で復帰率が崩落（下限 overloadFloor）。復帰率に乗算して使う。
        /// </summary>
        public static float CapacityOverloadPenalty(float casualtyRate, float capacity)
        {
            float rate = Mathf.Max(0f, casualtyRate);
            float cap = Mathf.Clamp01(capacity);
            if (rate <= cap) return 1f;
            return Mathf.Max(MedicalParams.Default.overloadFloor, cap / rate);
        }

        public static float CapacityOverloadPenalty(float casualtyRate, float capacity, MedicalParams p)
        {
            float rate = Mathf.Max(0f, casualtyRate);
            float cap = Mathf.Clamp01(capacity);
            if (rate <= cap) return 1f;
            return Mathf.Max(p.overloadFloor, cap / rate);
        }

        /// <summary>
        /// 経験者の温存効果＝補充に占める復帰兵の経験値保持率（復帰率×経験保持率）。
        /// 戻る兵は新兵より価値がある：<see cref="VeterancyRules.DiluteOnReinforce"/> の rookieXp 相当として渡せば、
        /// 救護が高い軍は大損耗後も練度が薄まらない。
        /// </summary>
        public static float VeteranPreservation(float returnRate, MedicalParams p)
        {
            return Mathf.Clamp01(returnRate) * p.veteranXpRetention;
        }

        public static float VeteranPreservation(float returnRate)
            => VeteranPreservation(returnRate, MedicalParams.Default);

        /// <summary>
        /// 「撃たれても拾ってもらえる」安心の士気倍率＝1＋救護能力×moraleAssuranceBonus。
        /// 士気係数（<see cref="FleetMorale"/> 側の実効値）に乗算して使う想定（基準値非破壊）。
        /// </summary>
        public static float MoraleAssurance(float medicalCapacity, MedicalParams p)
        {
            return 1f + Mathf.Clamp01(medicalCapacity) * p.moraleAssuranceBonus;
        }

        public static float MoraleAssurance(float medicalCapacity)
            => MoraleAssurance(medicalCapacity, MedicalParams.Default);
    }
}
