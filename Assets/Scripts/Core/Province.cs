using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 内政（#109 P-1/P-2 最小ループ）の純データ：星系/惑星の統治状態。
    /// 「支配＝即・産出」ではなく <see cref="stability"/>(安定度)×<see cref="integration"/>(統合度) で
    /// 産出・徴募が変わる土台。占領直後は未統合(integration=0)で不安定→時間で統合→安定が回復する。
    /// 数値の解決は <see cref="GovernanceRules"/>(static) が唯一の窓口。
    /// 所有勢力は StarSystem.owner が出所＝ここには持たない（住民の思想 <see cref="nativeIdeology"/> のみ保持）。
    /// 建設マイクロ・通貨経済は持たない（タイクン回避・EPIC #109 方針）。
    /// </summary>
    [System.Serializable]
    public class Province
    {
        /// <summary>紐づく星系ID（StarSystem.id）。</summary>
        public int systemId;

        /// <summary>住民の思想傾向（FactionData.ideology 文字列）。占領しても即は変わらない＝不安定の源。</summary>
        public string nativeIdeology = "";

        /// <summary>
        /// 経済類型（工業/農業/鉱業/居住）。<b>この惑星が産出する資源</b>を決める（#93 L-1 を惑星層へ・#767 ハイブリッド）。
        /// 産出は <see cref="ResourceProductionRules"/> が類型×安定度比例で解決。既定=居住（少量物資＝後方互換）。
        /// </summary>
        public SystemType systemType = SystemType.居住;

        /// <summary>人口規模（Pop。徴募・産出のスケール）。<see cref="demographics"/> があればその合計に同期される。</summary>
        public float population = 100f;

        /// <summary>
        /// 年齢コホート人口（出生・加齢・死亡の器・LIFE-3 #153）。null＝未設定（静的 <see cref="population"/> のみ＝後方互換）。
        /// 出生死亡の駆動は <see cref="PopulationDynamicsRules"/>（安定度で出生/死亡を増減）。設定すると人口が時間で増減する。
        /// </summary>
        public Population demographics = null;

        /// <summary>
        /// 労働力構成＝生産年齢人口の職業別シェア（#110 職業・null＝未設定＝類型既定で見積り＝後方互換）。
        /// 解決は <see cref="OccupationRules"/>（適所度・徴募源・失業圧）。職業は惑星の <see cref="systemType"/> でバイアスされる。
        /// </summary>
        public Workforce workforce = null;

        /// <summary>
        /// POP の労働技能ストック（職業別の集約熟練度・#2034 SKILL-1・null＝未形成＝後方互換）。
        /// 教育（#155-157）→OJT で年々形成（<see cref="PopLaborTickRules.TickYear"/>）。生産性#93・賃金#1969・徴募#96 の質に効く。
        /// </summary>
        public SkillStock skills = null;

        /// <summary>
        /// 労働賃金指数（POPLAB-4・#1969・既定1.0）。労働逼迫（就業率）×技能（#2034）で年々調整（<see cref="LaborWageTickRules.TickYear"/>）。
        /// 高い＝人手不足/高技能で高給。生活水準#181/支持#113 の係数（実効値パターン・基準非破壊）。
        /// </summary>
        public float wageIndex = 1f;

        /// <summary>
        /// 生活水準（POPDEM-4・#181・0..1・既定0.5）。POP要求物資の充足率からマズロー階層#403 で年々算出（<see cref="PopConsumptionTickRules.TickYear"/>）。
        /// 高い＝必需が満たされ快適/奢侈も充足。支持#113 の係数。
        /// </summary>
        public float livingStandard = 0.5f;

        /// <summary>飢餓の深さ（POPDEM-3・0..1・既定0）。必需（食料）の不足＝1−必需充足率。供給切れ#94/不安定で上がり、不満#113/人口減#153 の圧。</summary>
        public float foodShortage = 0f;

        /// <summary>希少資源（戦略資源 #178）の鉱床を持つか。<b>偏在</b>＝大半の惑星は false（鉱床なし＝後方互換）。</summary>
        public bool hasStrategicResource = false;

        /// <summary>この惑星が産出する希少資源（<see cref="hasStrategicResource"/>=true のとき有効）。解決は <see cref="StrategicResourceRules"/>。</summary>
        public StrategicResourceType strategicResource = StrategicResourceType.レアメタル;

        /// <summary>鉱床の豊富さ（0..1）。希少資源の産出率に乗る（0＝枯れ・1＝豊富）。</summary>
        public float strategicAbundance = 0f;

        /// <summary>安定度・治安（0..100）。低いと産出減・反乱リスク。既定＝中立。</summary>
        public float stability = GovernanceRules.BaseStability;

        /// <summary>占領統合度（0＝占領直後/未統合 .. 1＝完全統合）。未統合ぶんが安定を押し下げる。</summary>
        public float integration = 1f;

        /// <summary>住民の信仰（#172-175・null=未配線=後方互換）。`ReligionTickRules` が年次で進める。</summary>
        public Religion religion = null;

        /// <summary>住民の文化・民族（#194・null=未配線=後方互換）。`CultureTickRules` が年次で進める。</summary>
        public Culture culture = null;

        public Province() { }

        public Province(int systemId, string nativeIdeology, float population = 100f)
        {
            this.systemId = systemId;
            this.nativeIdeology = nativeIdeology ?? "";
            this.population = Mathf.Max(0f, population);
            this.stability = GovernanceRules.BaseStability;
            this.integration = 1f; // 既定は自国領＝統合済み
        }
    }
}
