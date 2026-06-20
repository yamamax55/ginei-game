namespace Ginei
{
    /// <summary>
    /// メーカー（製造業・#2016・純データ）。原材料を加工して製品を作り売る企業。汎用 <see cref="Enterprise"/>(#1022 操業)を土台に、
    /// 製造固有の状態＝歩留まり・研究開発水準・ブランド力・累積生産量（経験曲線）・1単位の原材料費を持つ。作る物の良し悪し
    /// （品質/ブランド）と作り方の巧拙（歩留まり/経験曲線）が利潤を左右する。解決は <see cref="ManufacturerRules"/>。少数集約。
    /// </summary>
    [System.Serializable]
    public class Manufacturer
    {
        public string name = "メーカー";
        public Faction faction;

        /// <summary>歩留まり（0..1＝産出のうち良品の割合。不良品は損失）。</summary>
        public float yieldRate = ManufacturerRules.DefaultYieldRate;

        /// <summary>研究開発水準（蓄積。生産性・歩留まり・製品力を上げる）。</summary>
        public float rdLevel = 0f;

        /// <summary>ブランド力（0..1。価格プレミアムの源）。</summary>
        public float brandStrength = 0f;

        /// <summary>累積生産量（経験曲線＝作るほど単価が下がる）。</summary>
        public float cumulativeOutput = 0f;

        /// <summary>1単位あたり原材料費。</summary>
        public float unitMaterialCost = 1f;

        /// <summary>1単位あたり原材料投入量（投入産出のボトルネック）。</summary>
        public float unitMaterialInput = 1f;

        public Manufacturer() { }

        public Manufacturer(string name, float yieldRate = ManufacturerRules.DefaultYieldRate, float rdLevel = 0f,
            float brandStrength = 0f, float cumulativeOutput = 0f, float unitMaterialCost = 1f,
            float unitMaterialInput = 1f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "メーカー" : name;
            this.yieldRate = yieldRate;
            this.rdLevel = rdLevel;
            this.brandStrength = brandStrength;
            this.cumulativeOutput = cumulativeOutput;
            this.unitMaterialCost = unitMaterialCost;
            this.unitMaterialInput = unitMaterialInput;
            this.faction = faction;
        }
    }
}
