namespace Ginei
{
    /// <summary>
    /// 工作機械メーカー（マザーマシン・#2023・純データ）。「機械を作る機械」を作る全製造業の上流＝技術基盤。工作機械の精度が
    /// 下流製造（#2016/#2020/#2022）の品質上限を決める。精度水準・R&D水準・受注残・戦略物資としての輸出規制を持つ。高精度機は
    /// 兵器製造を可能にする dual-use ゆえ規制対象。解決は <see cref="MachineToolRules"/>。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class MachineToolMaker
    {
        public string name = "工作機械メーカー";
        public Faction faction;

        /// <summary>精度水準（0..1。下流製造の品質上限を決める＝マザーマシンの肝）。</summary>
        public float precision = 0.5f;

        /// <summary>R&D水準（精度・数値制御を高める蓄積）。</summary>
        public float rdLevel = 0f;

        /// <summary>受注残（受注したが未納の残高＝受注産業の指標）。</summary>
        public float orderBacklog = 0f;

        /// <summary>戦略物資として輸出規制対象か（高精度機は兵器製造を可能にする）。</summary>
        public bool exportControlled = false;

        public MachineToolMaker() { }

        public MachineToolMaker(string name, float precision = 0.5f, float rdLevel = 0f,
            float orderBacklog = 0f, bool exportControlled = false, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "工作機械メーカー" : name;
            this.precision = precision;
            this.rdLevel = rdLevel;
            this.orderBacklog = orderBacklog;
            this.exportControlled = exportControlled;
            this.faction = faction;
        }
    }
}
