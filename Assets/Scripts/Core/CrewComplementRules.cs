using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 乗員定数（クルー・コンプリメント）の調整値。艦サイズ1単位あたりの基準乗員数・自動化の省人化幅・運用テンポの
    /// 過酷さレート・自動化が信頼性込みで人手を補う係数・疲労の蓄積レート・乗員不足とみなす既定の充足しきい値。
    /// すべて ctor で安全側へクランプ。盤面非依存・決定論。
    /// </summary>
    public readonly struct CrewComplementParams
    {
        /// <summary>艦サイズ1単位あたりの基準乗員数（自動化0のときに必要となる素の人員密度）。</summary>
        public readonly float crewPerSize;
        /// <summary>自動化が必要乗員を削れる最大比（0.6＝完全自動化で必要乗員を6割まで省ける＝下限4割は残る）。</summary>
        public readonly float automationManningReduction;
        /// <summary>運用テンポ1単位あたりの勤務シフト過酷さレート（不足×テンポに掛ける＝張り詰めるほどシフトが過酷）。</summary>
        public readonly float watchStrainRate;
        /// <summary>疲労の蓄積レート（シフト過酷×任務期間に掛ける＝長く過酷なほど疲れが積もる）。</summary>
        public readonly float fatigueRate;
        /// <summary>乗員が足りているとみなす既定の充足しきい値（これ以上で十分配置とみなす）。</summary>
        public readonly float adequateThreshold;

        public CrewComplementParams(float crewPerSize, float automationManningReduction,
            float watchStrainRate, float fatigueRate, float adequateThreshold)
        {
            this.crewPerSize = Mathf.Max(0f, crewPerSize);
            this.automationManningReduction = Mathf.Clamp01(automationManningReduction);
            this.watchStrainRate = Mathf.Clamp(watchStrainRate, 0f, 4f);
            this.fatigueRate = Mathf.Clamp(fatigueRate, 0f, 4f);
            this.adequateThreshold = Mathf.Clamp01(adequateThreshold);
        }

        /// <summary>既定：乗員密度100・自動化省人化0.6・過酷さレート1.0・疲労レート0.5・充足しきい0.8。</summary>
        public static CrewComplementParams Default => new CrewComplementParams(
            DefaultCrewPerSize, DefaultAutomationManningReduction,
            DefaultWatchStrainRate, DefaultFatigueRate, DefaultAdequateThreshold);

        public const float DefaultCrewPerSize = 100f;
        public const float DefaultAutomationManningReduction = 0.6f;
        public const float DefaultWatchStrainRate = 1.0f;
        public const float DefaultFatigueRate = 0.5f;
        public const float DefaultAdequateThreshold = 0.8f;
    }

    /// <summary>
    /// 乗員定数（艦の人員配置と充足）の純ロジック（唯一の窓口）＝<b>艦に何人乗せれば回るのか、いま足りているのか</b>。
    /// 艦サイズと自動化で<b>必要乗員数</b>を出し、実乗員との比で<b>充足率</b>を得る。不足×運用テンポで<b>勤務シフトの
    /// 過酷さ</b>が増し、充足×練度で各部署の<b>運用効率</b>が決まる。自動化は信頼性込みで人手を補い（信頼性が低い自動化は
    /// 当てにならない）、過酷なシフトを長い任務で続けると<b>疲労</b>が積もる。損耗（死傷）で充足が削られても<b>戦闘配置</b>を
    /// どこまで維持できるかを返す。盤面非依存の plain 引数・実効値パターン（基準値非破壊）・test-first。
    /// </summary>
    public static class CrewComplementRules
    {
        /// <summary>既定パラメータで必要乗員数。</summary>
        public static float RequiredComplement(float shipSize, float automationLevel)
            => RequiredComplement(shipSize, automationLevel, CrewComplementParams.Default);

        /// <summary>
        /// 艦サイズ×(1−自動化省人化)で必要乗員数（≥0）。required＝サイズ×乗員密度×(1−自動化×省人化幅)。
        /// 自動化が進むほど少ない乗員で回るが、省人化幅で下限が残る（艦は無人にはならない）。
        /// </summary>
        public static float RequiredComplement(float shipSize, float automationLevel, CrewComplementParams p)
        {
            float size = Mathf.Max(0f, shipSize);
            float auto = Mathf.Clamp01(automationLevel);
            float reduction = 1f - auto * p.automationManningReduction;
            return Mathf.Max(0f, size * p.crewPerSize * reduction);
        }

        /// <summary>既定パラメータで充足率。</summary>
        public static float ManningRatio(float actualCrew, float requiredComplement)
            => ManningRatio(actualCrew, requiredComplement, CrewComplementParams.Default);

        /// <summary>
        /// 実乗員/必要数で充足率（0..1、過剰は1で頭打ち）。必要数0なら充足1.0（人手不要の艦は常に満員扱い）。
        /// ratio＝clamp01(実乗員/必要数)＝定員に対してどれだけ人が乗っているか。
        /// </summary>
        public static float ManningRatio(float actualCrew, float requiredComplement, CrewComplementParams p)
        {
            float actual = Mathf.Max(0f, actualCrew);
            float required = Mathf.Max(0f, requiredComplement);
            if (required <= 0f) return 1f;
            return Mathf.Clamp01(actual / required);
        }

        /// <summary>既定パラメータで勤務シフトの過酷さ。</summary>
        public static float WatchSystemStrain(float manningRatio, float operationalTempo)
            => WatchSystemStrain(manningRatio, operationalTempo, CrewComplementParams.Default);

        /// <summary>
        /// (1−充足)×運用テンポで勤務シフトの過酷さ（0..1）。人が足りないほど・運用テンポが高いほど一人あたりの
        /// 直（ワッチ）が増えて過酷になる。strain＝clamp01((1−充足)×テンポ×レート)。満員（充足1）なら過酷さ0。
        /// </summary>
        public static float WatchSystemStrain(float manningRatio, float operationalTempo, CrewComplementParams p)
        {
            float manning = Mathf.Clamp01(manningRatio);
            float tempo = Mathf.Clamp01(operationalTempo);
            return Mathf.Clamp01((1f - manning) * tempo * p.watchStrainRate);
        }

        /// <summary>既定パラメータで各部署の運用効率。</summary>
        public static float StationEffectiveness(float manningRatio, float crewProficiency)
            => StationEffectiveness(manningRatio, crewProficiency, CrewComplementParams.Default);

        /// <summary>
        /// 充足×練度で各部署（砲術・機関・ダメコン等）の運用効率（0..1）。人が揃い、かつ熟練していて初めて部署が
        /// 機能する。effectiveness＝充足×練度。どちらか欠ければ部署は回らない（人だけでも腕だけでも不足）。
        /// </summary>
        public static float StationEffectiveness(float manningRatio, float crewProficiency, CrewComplementParams p)
        {
            float manning = Mathf.Clamp01(manningRatio);
            float prof = Mathf.Clamp01(crewProficiency);
            return Mathf.Clamp01(manning * prof);
        }

        /// <summary>既定パラメータで省人化の効力。</summary>
        public static float AutomationOffset(float automationLevel, float systemReliability)
            => AutomationOffset(automationLevel, systemReliability, CrewComplementParams.Default);

        /// <summary>
        /// 自動化×信頼性で省人化の効力（0..1）＝自動化がどれだけ人手を肩代わりできているか。offset＝自動化×信頼性。
        /// 信頼性の低い自動化は当てにならず人手の代わりにならない（故障する自動化は乗員を解放しない）。
        /// </summary>
        public static float AutomationOffset(float automationLevel, float systemReliability, CrewComplementParams p)
        {
            float auto = Mathf.Clamp01(automationLevel);
            float reliability = Mathf.Clamp01(systemReliability);
            return Mathf.Clamp01(auto * reliability);
        }

        /// <summary>既定パラメータで乗員疲労。</summary>
        public static float FatigueAccumulation(float watchSystemStrain, float missionDuration)
            => FatigueAccumulation(watchSystemStrain, missionDuration, CrewComplementParams.Default);

        /// <summary>
        /// シフト過酷×任務期間で乗員疲労（0..1）。過酷なシフトを長期任務で続けるほど疲れが積もる。
        /// fatigue＝clamp01(シフト過酷×任務期間×レート)。過酷さ0（満員の余裕運用）なら期間が長くても疲労0。
        /// </summary>
        public static float FatigueAccumulation(float watchSystemStrain, float missionDuration, CrewComplementParams p)
        {
            float strain = Mathf.Clamp01(watchSystemStrain);
            float duration = Mathf.Max(0f, missionDuration);
            return Mathf.Clamp01(strain * duration * p.fatigueRate);
        }

        /// <summary>既定パラメータで損耗後の戦闘配置維持。</summary>
        public static float BattleStationsCapacity(float manningRatio, float casualties)
            => BattleStationsCapacity(manningRatio, casualties, CrewComplementParams.Default);

        /// <summary>
        /// (充足−死傷)で損耗後の戦闘配置維持力（0..1）。被弾で人員を失うと配置が手薄になる。
        /// capacity＝clamp01(充足−死傷率)。満員（充足1）でも死傷4割なら維持0.6。死傷が充足を上回れば配置不能（0）。
        /// </summary>
        public static float BattleStationsCapacity(float manningRatio, float casualties, CrewComplementParams p)
        {
            float manning = Mathf.Clamp01(manningRatio);
            float cas = Mathf.Clamp01(casualties);
            return Mathf.Clamp01(manning - cas);
        }

        /// <summary>乗員が足りているか（bool）＝充足率が既定しきい値以上。</summary>
        public static bool IsAdequatelyManned(float manningRatio)
            => IsAdequatelyManned(manningRatio, CrewComplementParams.DefaultAdequateThreshold);

        /// <summary>
        /// 乗員が足りているか（bool）＝充足率がしきい値以上。定員割れの艦を無理に出すと部署が回らない＝出撃の足切り。
        /// </summary>
        public static bool IsAdequatelyManned(float manningRatio, float threshold)
        {
            float manning = Mathf.Clamp01(manningRatio);
            return manning >= Mathf.Clamp01(threshold);
        }
    }
}
