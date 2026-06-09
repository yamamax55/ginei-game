using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 戦役の世界状態＝盤面（<see cref="GalaxyMap"/>）と勢力ごとの国家状態（<see cref="FactionState"/>）を束ねる
    /// 最上層の統合（社会シミュ ↔ 地理盤面）。各勢力の腐敗・合意・希望が進み、版図の一体化度（物流 #844）が
    /// 実効安定度を割り引く＝散在する帝国は栄えにくい。解決は <see cref="CampaignRules"/>（static）。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class CampaignState
    {
        public GalaxyMap map;
        public List<FactionState> states = new List<FactionState>();

        public CampaignState() { }
        public CampaignState(GalaxyMap map) { this.map = map; }
    }
}
