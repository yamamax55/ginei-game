using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略マップ（銀河グラフ）のノード＝星系（C-1 #34）。
    /// 回廊(Corridor)で結ばれた星系どうしのみ移動可能（回廊以外＝航行不能宙域）。
    /// 純データ（Unityシーン非依存）。座標は銀河マップ上の位置。
    /// </summary>
    [System.Serializable]
    public class StarSystem
    {
        public int id;
        public string systemName;
        public Vector2 position;
        public Faction owner;          // 所有勢力（enum・後方互換）
        public FactionData ownerData;  // 任意（多勢力対応・あれば優先）

        /// <summary>
        /// 惑星攻城の状態（#131 惑星戦）。null＝無防備な星系＝停泊だけで占領（従来動作・後方互換）。
        /// 非null＝制空権(ピラー・ドメイン/超兵器)で守られた惑星＝攻城(PlanetSiegeRules/TickSieges)で占領する。
        /// </summary>
        public Planet planet;

        /// <summary>居住可能か（#129 入植可能条件。false＝不毛で入植不可）。既定 true＝従来の星系は居住可能（後方互換）。</summary>
        public bool habitable = true;

        /// <summary>入植済み/領有済みか（#129）。false＝未入植＝入植艦で植民できる対象。既定 true＝既存の星系は領有済み（後方互換）。</summary>
        public bool isColonized = true;

        public StarSystem() { }

        public StarSystem(int id, string systemName, Vector2 position, Faction owner = Faction.帝国)
        {
            this.id = id;
            this.systemName = systemName;
            this.position = position;
            this.owner = owner;
        }
    }
}
