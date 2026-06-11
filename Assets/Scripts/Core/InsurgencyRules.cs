using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 占領地反乱（インサージェンシー）の純データ struct（スペイン内戦/ゲリラ戦型・SPW-2 #1394 の中核状態）。
    /// 散発的な不満が、外部勢力（隣国・亡命政府）の扇動・武器・資金・指導を得て組織的な反乱へ成長する過程を表す。
    /// </summary>
    public struct InsurgencyState
    {
        /// <summary>組織化度（0..1・散発的不満→組織的反乱の成熟度。1=統制の取れた反乱組織）。</summary>
        public float organization;
        /// <summary>住民支持（0..1・民心が反乱の海。占領者の暴虐で上がり反乱側の統治で根づく）。</summary>
        public float popularSupport;
        /// <summary>武装勢力の強さ（0..1・外部支援で底上げされる戦闘能力）。</summary>
        public float armedStrength;

        public InsurgencyState(float organization, float popularSupport, float armedStrength)
        {
            this.organization = Mathf.Clamp01(organization);
            this.popularSupport = Mathf.Clamp01(popularSupport);
            this.armedStrength = Mathf.Clamp01(armedStrength);
        }
    }

    /// <summary>占領地反乱の調整係数。</summary>
    public readonly struct InsurgencyParams
    {
        /// <summary>組織化の速度（per dt・外部扇動×現地不満が時間で反乱を組織化する率）。</summary>
        public readonly float organizationRate;
        /// <summary>外部支援（武器/資金/指導）が武装勢力を底上げする最大重み（0..1）。</summary>
        public readonly float externalSupportWeight;
        /// <summary>反乱圧力の増幅最大倍率（組織化×武装が占領者への反乱圧を何倍にするか・1以上）。</summary>
        public readonly float maxAmplification;
        /// <summary>住民支持が動く速度（per dt・占領者の暴虐＋反乱統治）。</summary>
        public readonly float supportRate;
        /// <summary>暴虐が支持を反乱側へ押しやる重み（0..1）。</summary>
        public readonly float brutalityWeight;
        /// <summary>反乱側の統治が支持を根づかせる重み（0..1）。</summary>
        public readonly float insurgentGovWeight;
        /// <summary>国境聖域が反乱を持続させる最大上乗せ（0..1・隣国に逃げ込める安全地帯）。</summary>
        public readonly float safeHavenWeight;
        /// <summary>対反乱作戦が反乱を削る速度（per dt・掃討＋住民保護努力1のとき）。</summary>
        public readonly float counterInsurgencyRate;
        /// <summary>住民保護を伴わない掃討の逆効果重み（0..1・暴力一辺倒は反乱を太らせる）。</summary>
        public readonly float heavyHandedBacklash;
        /// <summary>内戦への拡大速度（per dt・組織化×武装がゲリラ戦を本格内戦へ押し上げる率）。</summary>
        public readonly float escalationRate;

        public InsurgencyParams(float organizationRate, float externalSupportWeight, float maxAmplification,
            float supportRate, float brutalityWeight, float insurgentGovWeight, float safeHavenWeight,
            float counterInsurgencyRate, float heavyHandedBacklash, float escalationRate)
        {
            this.organizationRate = Mathf.Max(0f, organizationRate);
            this.externalSupportWeight = Mathf.Clamp01(externalSupportWeight);
            this.maxAmplification = Mathf.Max(1f, maxAmplification);
            this.supportRate = Mathf.Max(0f, supportRate);
            this.brutalityWeight = Mathf.Clamp01(brutalityWeight);
            this.insurgentGovWeight = Mathf.Clamp01(insurgentGovWeight);
            this.safeHavenWeight = Mathf.Clamp01(safeHavenWeight);
            this.counterInsurgencyRate = Mathf.Max(0f, counterInsurgencyRate);
            this.heavyHandedBacklash = Mathf.Clamp01(heavyHandedBacklash);
            this.escalationRate = Mathf.Max(0f, escalationRate);
        }

        /// <summary>
        /// 既定＝組織化0.15・外部支援重み0.6・増幅上限2.0・支持速度0.1・暴虐重み0.5・反乱統治重み0.4・
        /// 聖域0.5・対反乱0.2・掃討逆効果0.4・内戦拡大0.08。
        /// </summary>
        public static InsurgencyParams Default =>
            new InsurgencyParams(0.15f, 0.6f, 2.0f, 0.1f, 0.5f, 0.4f, 0.5f, 0.2f, 0.4f, 0.08f);
    }

    /// <summary>
    /// 占領地反乱組織化の純ロジック（スペイン内戦/ゲリラ戦型・SPW-2 #1394）。
    /// 占領された地域で、外部勢力（隣国・亡命政府）の扇動・武器・資金・指導を受けて反乱が組織化される＝
    /// 散発的な不満が外部支援を得て組織的な反乱（insurgency）へ成長し、占領者への反乱圧力を増幅する。
    /// 式の核：組織化＝外部扇動×現地不満で時間成長／外部支援（武器・資金・指導）が武装を底上げ／
    /// 組織化×武装×占領者の脆弱性が <see cref="GovernanceRules.RebelPressure"/> を乗算で増幅する。
    /// <para>
    /// 分担：<see cref="GovernanceRules"/> は占領地の反乱リスク＝内生（安定度から自然に湧く圧）を出す。
    /// <see cref="ResistanceRules"/> は破壊工作・情報漏れと弾圧/懐柔ジレンマ＝レジスタンスの統合後退を扱う。
    /// <see cref="GuerrillaDoctrineRules"/> は遊撃戦の戦術モデル（同EPIC SPW）。
    /// <see cref="HomelandResistanceRules"/> は侵攻深度に応じた縦深抵抗（祖国防衛）。
    /// 本クラスは「外部支援された組織的反乱」＝<see cref="InsurgencyState"/> を中核データに、
    /// 外部扇動が反乱圧力を増幅する一点に責任を持つ（内生の反乱リスクとは別系統）。
    /// </para>
    /// 全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class InsurgencyRules
    {
        /// <summary>
        /// 組織化の1tick後の組織化度（0..1）＝外部扇動 externalAgitation×現地の不満 grievance で時間成長。
        /// 外部の扇動と現地の不満が両方そろうほど散発的不満が組織的反乱へ育つ（どちらか0なら育たない）。
        /// </summary>
        public static float OrganizationTick(float organization, float externalAgitation, float grievance, float dt, InsurgencyParams p)
        {
            float drive = Mathf.Clamp01(externalAgitation) * Mathf.Clamp01(grievance);
            float gain = p.organizationRate * drive * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(organization) + gain);
        }

        public static float OrganizationTick(float organization, float externalAgitation, float grievance, float dt)
            => OrganizationTick(organization, externalAgitation, grievance, dt, InsurgencyParams.Default);

        /// <summary>
        /// 外部支援による武装勢力の底上げ（0..1）＝組織化度を土台に、武器・資金・指導の平均で能力を引き上げる。
        /// 受け皿（組織化）が無ければ支援は活きない＝組織化された反乱ほど外部支援を武力へ転化できる。
        /// </summary>
        public static float ExternalSupportBoost(float organization, float weaponsSupply, float funding, float leadership, InsurgencyParams p)
        {
            float support = (Mathf.Clamp01(weaponsSupply) + Mathf.Clamp01(funding) + Mathf.Clamp01(leadership)) / 3f;
            return Mathf.Clamp01(Mathf.Clamp01(organization) * support * p.externalSupportWeight);
        }

        public static float ExternalSupportBoost(float organization, float weaponsSupply, float funding, float leadership)
            => ExternalSupportBoost(organization, weaponsSupply, funding, leadership, InsurgencyParams.Default);

        /// <summary>
        /// 反乱圧力の増幅倍率（1..maxAmplification）＝組織化された反乱の強さ×占領者の脆弱性。
        /// <see cref="GovernanceRules.RebelPressure"/> に乗算して、外部支援された組織的反乱が内生の反乱圧を膨らませる。
        /// 反乱が無い（強さ0）なら倍率1.0＝増幅なし＝従来の内生圧のまま（後方互換）。
        /// </summary>
        public static float RebelPressureAmplification(float insurgencyStrength, float occupierVulnerability, InsurgencyParams p)
        {
            float t = Mathf.Clamp01(insurgencyStrength) * Mathf.Clamp01(occupierVulnerability);
            return Mathf.Lerp(1f, p.maxAmplification, t);
        }

        public static float RebelPressureAmplification(float insurgencyStrength, float occupierVulnerability)
            => RebelPressureAmplification(insurgencyStrength, occupierVulnerability, InsurgencyParams.Default);

        /// <summary>
        /// 住民支持の1tick後（0..1）＝占領者の暴虐 occupierBrutality が支持を反乱側へ押しやり、
        /// 反乱側の統治 insurgentGovernance が支持を根づかせる。民心が反乱の海＝支持の純増減を時間積分。
        /// </summary>
        public static float PopularSupportTick(float popularSupport, float occupierBrutality, float insurgentGovernance, float dt, InsurgencyParams p)
        {
            float push = Mathf.Clamp01(occupierBrutality) * p.brutalityWeight + Mathf.Clamp01(insurgentGovernance) * p.insurgentGovWeight;
            float delta = p.supportRate * push * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(popularSupport) + delta);
        }

        public static float PopularSupportTick(float popularSupport, float occupierBrutality, float insurgentGovernance, float dt)
            => PopularSupportTick(popularSupport, occupierBrutality, insurgentGovernance, dt, InsurgencyParams.Default);

        /// <summary>
        /// 国境聖域の持続上乗せ（0..safeHavenWeight）＝国境への近さ borderProximity×隣国の安全地帯 externalSanctuary。
        /// 隣国に逃げ込める聖域があるほど反乱は掃討されても生き延びる（外部支援された反乱の生命線）。
        /// </summary>
        public static float SafeHaven(float borderProximity, float externalSanctuary, InsurgencyParams p)
        {
            float t = Mathf.Clamp01(borderProximity) * Mathf.Clamp01(externalSanctuary);
            return t * p.safeHavenWeight;
        }

        public static float SafeHaven(float borderProximity, float externalSanctuary)
            => SafeHaven(borderProximity, externalSanctuary, InsurgencyParams.Default);

        /// <summary>
        /// 対反乱作戦の純効果＝反乱を削る量（per dt・呼び出し側が組織化/武装から差し引く）。
        /// 掃討 occupierEffort は反乱を削るが、住民保護 populationProtection を欠く暴力的掃討は逆効果で
        /// 削りを目減りさせる（heavyHandedBacklash）。住民保護を伴う掃討ほど効く＝COINの要諦。
        /// </summary>
        public static float CounterInsurgencyEffect(float occupierEffort, float populationProtection, float dt, InsurgencyParams p)
        {
            float effort = Mathf.Clamp01(occupierEffort);
            // 住民保護が乏しいほど逆効果で削りが減る（保護1で減衰0・保護0で最大減衰）。
            float backlash = (1f - Mathf.Clamp01(populationProtection)) * p.heavyHandedBacklash;
            float effective = effort * (1f - backlash);
            return Mathf.Max(0f, p.counterInsurgencyRate * effective * Mathf.Max(0f, dt));
        }

        public static float CounterInsurgencyEffect(float occupierEffort, float populationProtection, float dt)
            => CounterInsurgencyEffect(occupierEffort, populationProtection, dt, InsurgencyParams.Default);

        /// <summary>
        /// 内戦への拡大量（per dt）＝組織化度×武装勢力の強さで、ゲリラ戦が本格的な内戦へ押し上がる。
        /// 組織化されて武装した反乱だけが内戦へ拡大する（どちらか欠ければ0）。<see cref="CivilWarRules"/> へ渡す入力。
        /// </summary>
        public static float InsurgencyEscalation(float organization, float armedStrength, float dt, InsurgencyParams p)
        {
            float t = Mathf.Clamp01(organization) * Mathf.Clamp01(armedStrength);
            return Mathf.Max(0f, p.escalationRate * t * Mathf.Max(0f, dt));
        }

        public static float InsurgencyEscalation(float organization, float armedStrength, float dt)
            => InsurgencyEscalation(organization, armedStrength, dt, InsurgencyParams.Default);

        /// <summary>
        /// 外部支援された組織的反乱に成長したか＝組織化度×外部支援 externalSupport が threshold 以上。
        /// 組織化と外部支援の両方が要る＝散発的不満（組織化低）でも支援だけ（externalSupport高）でも閾値に届かない。
        /// </summary>
        public static bool IsOrganizedInsurgency(float organization, float externalSupport, float threshold)
        {
            float strength = Mathf.Clamp01(organization) * Mathf.Clamp01(externalSupport);
            return strength >= Mathf.Clamp01(threshold);
        }
    }
}
