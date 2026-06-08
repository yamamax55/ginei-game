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
