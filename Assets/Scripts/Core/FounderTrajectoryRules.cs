using UnityEngine;

namespace Ginei
{
    /// <summary>建国者の軌道（共和制軌道／専制固定／過渡）の弁別結果（#1493）。</summary>
    public enum FounderOutcome { 共和制軌道, 専制固定, 過渡 }

    /// <summary>建国者の自己廃絶テストの調整係数（#1493・マキャヴェッリ『ディスコルシ』ロムルス型）。</summary>
    public readonly struct FounderTrajectoryParams
    {
        /// <summary>制度投資の傾き（建国者の努力×dt が制度を育てる強さ）。</summary>
        public readonly float institutionGain;
        /// <summary>権力集中の傾き（建国者の野心×dt が個人権力を膨らます強さ）。</summary>
        public readonly float concentrationGain;
        /// <summary>軌道バランスの判定閾値（制度／個人権力の比がこれ以上で共和制寄り）。</summary>
        public readonly float balanceThreshold;
        /// <summary>自己廃絶テストの合格に要る自発的移譲の閾値（権力を手放す意志の最低線）。</summary>
        public readonly float handoverThreshold;
        /// <summary>建国者の罠の係数（個人権力が制度を上回る差に掛かる＝独裁固定リスク）。</summary>
        public readonly float trapScale;

        public FounderTrajectoryParams(float institutionGain, float concentrationGain,
            float balanceThreshold, float handoverThreshold, float trapScale)
        {
            this.institutionGain = Mathf.Max(0f, institutionGain);
            this.concentrationGain = Mathf.Max(0f, concentrationGain);
            this.balanceThreshold = Mathf.Max(0f, balanceThreshold);
            this.handoverThreshold = Mathf.Clamp01(handoverThreshold);
            this.trapScale = Mathf.Max(0f, trapScale);
        }

        /// <summary>既定＝制度投資0.5・権力集中0.5・軌道閾値1.0・移譲閾値0.5・罠係数1.0。</summary>
        public static FounderTrajectoryParams Default =>
            new FounderTrajectoryParams(0.5f, 0.5f, 1f, 0.5f, 1f);
    }

    /// <summary>
    /// 建国者の自己廃絶テスト＝共和国の創設者の試金石の純ロジック（#1493・マキャヴェッリ『ディスコルシ』
    /// ロムルス型・DISC-4）。偉大な建国者は、自らに集中した権力を制度（法・元老院・継承の仕組み）へ
    /// 移譲して身を引けるかが試金石＝<b>制度投資速度と権力集中速度の競争</b>が軌道を分ける。
    /// 権力集中が制度投資を上回って固定すれば<b>専制が固定</b>し、制度へ投資して<b>自己を廃絶</b>（権力を
    /// 自発的に手放す＝ワシントンの王位辞退）できれば<b>共和制が根づく</b>。
    /// <see cref="SuccessionRules"/>（カリスマの継承＝英雄死後に制度化分が残るか）とは別＝こちらは建国者が
    /// <b>存命中に権力を握り続けるか制度へ移譲するか</b>の軌道を扱う。
    /// <see cref="PublicPrivateSeparationRules"/>（公私分離＝国庫と私財の帰属）とは別＝こちらは権力そのものの
    /// 帰属（個人か制度か）。<see cref="Organization"/>（制度化 institutionalization・#812）と整合＝制度に投資した
    /// 建国は建国者を超えて続く（<see cref="LegacyDurability"/>）。同EPIC DISC の <c>RinnovazioneRules</c>
    /// （制度の更新・刷新）とは別＝こちらは創設の一回性（建国者が制度を残せるか）。
    /// 全入力クランプ・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FounderTrajectoryRules
    {
        /// <summary>
        /// 制度投資の1tick＝建国者が法・制度の構築に努力を割くと制度が育つ。
        /// institutionStrength += founderEffort × institutionGain × dt（0..1にクランプ）。
        /// </summary>
        public static float InstitutionInvestmentTick(float institutionStrength, float founderEffort, float dt,
            FounderTrajectoryParams p)
        {
            float inst = Mathf.Clamp01(institutionStrength);
            float effort = Mathf.Clamp01(founderEffort);
            float d = Mathf.Max(0f, dt);
            return Mathf.Clamp01(inst + effort * p.institutionGain * d);
        }

        public static float InstitutionInvestmentTick(float institutionStrength, float founderEffort, float dt)
            => InstitutionInvestmentTick(institutionStrength, founderEffort, dt, FounderTrajectoryParams.Default);

        /// <summary>
        /// 権力集中の1tick＝建国者が権力を自らに集めると個人権力が膨らむ。
        /// personalPower += ambition × concentrationGain × dt（0..1にクランプ）。
        /// </summary>
        public static float PowerConcentrationTick(float personalPower, float ambition, float dt,
            FounderTrajectoryParams p)
        {
            float power = Mathf.Clamp01(personalPower);
            float amb = Mathf.Clamp01(ambition);
            float d = Mathf.Max(0f, dt);
            return Mathf.Clamp01(power + amb * p.concentrationGain * d);
        }

        public static float PowerConcentrationTick(float personalPower, float ambition, float dt)
            => PowerConcentrationTick(personalPower, ambition, dt, FounderTrajectoryParams.Default);

        /// <summary>
        /// 軌道バランス＝制度の強さ／個人権力の比。制度が勝てば（&gt;1）共和制寄り、個人権力が勝てば（&lt;1）
        /// 専制寄り。個人権力ゼロは無限大の代わりに制度強度を尺度に返す（純粋に制度のみ＝共和制）。
        /// </summary>
        public static float TrajectoryBalance(float institutionStrength, float personalPower)
        {
            float inst = Mathf.Clamp01(institutionStrength);
            float power = Mathf.Clamp01(personalPower);
            if (power <= 0f) return inst > 0f ? float.MaxValue : 1f; // 権力なし＝制度のみ（共和制側）
            return inst / power;
        }

        /// <summary>
        /// 自己廃絶テスト（0..1）＝権力を自発的に手放せるか＝個人権力×自発的移譲（ワシントンの王位辞退）。
        /// 握った権力が大きいほど手放す行為の重み（試金石としての価値）が増す＝集めた権力を進んで返す度合い。
        /// </summary>
        public static float SelfAbnegationTest(float personalPower, float voluntaryHandover)
        {
            float power = Mathf.Clamp01(personalPower);
            float handover = Mathf.Clamp01(voluntaryHandover);
            return Mathf.Clamp01(power * handover);
        }

        /// <summary>
        /// 結末の弁別。制度が権力を上回り（trajectoryBalance≥閾値）かつ自発的移譲が手放しの閾値以上なら
        /// 共和制軌道。制度が権力に明確に劣り（trajectoryBalance&lt;閾値）かつ移譲が乏しいなら専制固定。
        /// それ以外は過渡（どちらにも確定していない）。
        /// </summary>
        public static FounderOutcome OutcomeOf(float trajectoryBalance, float handover, float threshold,
            FounderTrajectoryParams p)
        {
            float bal = Mathf.Max(0f, trajectoryBalance);
            float h = Mathf.Clamp01(handover);
            float t = Mathf.Max(0f, threshold);
            if (bal >= t && h >= p.handoverThreshold) return FounderOutcome.共和制軌道;
            if (bal < t && h < p.handoverThreshold) return FounderOutcome.専制固定;
            return FounderOutcome.過渡;
        }

        public static FounderOutcome OutcomeOf(float trajectoryBalance, float handover, float threshold)
            => OutcomeOf(trajectoryBalance, handover, threshold, FounderTrajectoryParams.Default);

        /// <summary>
        /// 建国者の罠リスク（0..1）＝個人権力が制度を上回ったまま固定すると建国者が独裁者になる。
        /// 個人権力が制度を超える差（power−inst、正のみ）×権力規模×係数。差が大きく権力が強いほど高い。
        /// </summary>
        public static float FounderTrapRisk(float personalPower, float institutionStrength,
            FounderTrajectoryParams p)
        {
            float power = Mathf.Clamp01(personalPower);
            float inst = Mathf.Clamp01(institutionStrength);
            float gap = Mathf.Max(0f, power - inst); // 権力が制度を上回る分のみ
            return Mathf.Clamp01(gap * power * p.trapScale);
        }

        public static float FounderTrapRisk(float personalPower, float institutionStrength)
            => FounderTrapRisk(personalPower, institutionStrength, FounderTrajectoryParams.Default);

        /// <summary>
        /// 遺産の持続（0..1）＝建国者の死後に制度が残るか。建国者存命中は個人カリスマも支えるため満額に近い
        /// （制度＋個人の補い）が、死後は制度のみが残る＝制度に投資した建国は建国者を超えて続く
        /// （<see cref="Organization"/> の制度化と整合）。founderDeath=false なら institutionStrength を底に
        /// 1へ寄せ、true なら institutionStrength そのもの。
        /// </summary>
        public static float LegacyDurability(float institutionStrength, bool founderDeath)
        {
            float inst = Mathf.Clamp01(institutionStrength);
            if (founderDeath) return inst; // 死後は制度のみが残る
            return Mathf.Clamp01(inst + (1f - inst) * 0.5f); // 存命中は個人が半ば補う
        }

        /// <summary>
        /// 共和制建国の判定＝制度が権力を超え（trajectoryBalance≥閾値）、自己廃絶（自発的移譲が手放しの
        /// 閾値以上）を果たした建国か。<see cref="OutcomeOf"/> が共和制軌道を返すのと同値の真偽版。
        /// </summary>
        public static bool IsRepublicanFounding(float trajectoryBalance, float handover, float threshold,
            FounderTrajectoryParams p)
            => OutcomeOf(trajectoryBalance, handover, threshold, p) == FounderOutcome.共和制軌道;

        public static bool IsRepublicanFounding(float trajectoryBalance, float handover, float threshold)
            => IsRepublicanFounding(trajectoryBalance, handover, threshold, FounderTrajectoryParams.Default);
    }
}
