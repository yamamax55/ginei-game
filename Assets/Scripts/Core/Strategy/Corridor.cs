namespace Ginei
{
    /// <summary>回廊の型。要衝＝イゼルローン型チョークポイント、通商＝フェザーン型。</summary>
    public enum CorridorType { 通商, 要衝 }

    /// <summary>
    /// 戦略マップのエッジ＝回廊（C-1 #34）。2つの星系を結ぶ。
    /// length は航行コスト（>0、ワープ時間の基準）。回り込み・迂回は不可
    /// （グラフに裏エッジを持たせない方針）。純データ。
    /// </summary>
    [System.Serializable]
    public class Corridor
    {
        public int aId;
        public int bId;
        public float length = 1f;            // 航行コスト
        public CorridorType type = CorridorType.通商;

        public Corridor() { }

        public Corridor(int aId, int bId, float length = 1f, CorridorType type = CorridorType.通商)
        {
            this.aId = aId;
            this.bId = bId;
            this.length = length;
            this.type = type;
        }

        /// <summary>この回廊が指定星系に接続しているか。</summary>
        public bool Connects(int systemId) => systemId == aId || systemId == bId;

        /// <summary>指定星系の反対側の星系IDを返す（接続していなければ -1）。</summary>
        public int Other(int systemId)
        {
            if (systemId == aId) return bId;
            if (systemId == bId) return aId;
            return -1;
        }
    }
}
