using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 企業＝操業する経済アクター（#1022 企業経済・#179 市場の供給側・純データ）。POP の工員（#110）を雇い、産業（<see cref="sector"/>）で
    /// 財を<b>生産</b>し、売上から賃金を引いた<b>利潤</b>を得て<b>再投資（資本蓄積 #269）</b>する。労働が生む価値と賃金の差＝<b>剰余価値/搾取（#271）</b>。
    /// 上場企業の<b>株価の顔</b>は <see cref="Company"/>(#185)＝こちらは中身（生産・雇用・利潤）。解決は <see cref="EnterpriseRules"/>。
    /// 少数を集約（タイクン化回避＝個別の在庫/会計は持たない）。
    /// </summary>
    [Serializable]
    public class Enterprise
    {
        public string name = "企業";
        public Faction faction;

        /// <summary>産業（何を作るか・<see cref="SystemType"/>＝工業/農業/鉱業/居住）。</summary>
        public SystemType sector = SystemType.工業;

        /// <summary>所有形態（私有＝資本家／国有＝国家）。利潤の行き先と雇用の振る舞いが変わる（<see cref="PropertyRules"/>）。既定=私有。</summary>
        public Ownership ownership = Ownership.私有;

        /// <summary>雇用（POP工員から雇った人数・#110）。</summary>
        public float employees = 100f;

        /// <summary>資本（設備＝1人あたり産出を底上げ・蓄積で増える #269）。</summary>
        public float capital = 1000f;

        /// <summary>生産性（1人あたり産出）。</summary>
        public float productivity = 1f;

        /// <summary>賃金率（1人あたり賃金＝POPへ分配・生活水準 #181）。</summary>
        public float wageRate = 1f;

        public Enterprise() { }

        public Enterprise(Faction faction, SystemType sector, float employees, float capital = 1000f,
            float productivity = 1f, float wageRate = 1f, string name = "企業", Ownership ownership = Ownership.私有)
        {
            this.faction = faction;
            this.sector = sector;
            this.employees = Mathf.Max(0f, employees);
            this.capital = Mathf.Max(0f, capital);
            this.productivity = Mathf.Max(0f, productivity);
            this.wageRate = Mathf.Max(0f, wageRate);
            this.name = string.IsNullOrEmpty(name) ? "企業" : name;
            this.ownership = ownership;
        }
    }
}
