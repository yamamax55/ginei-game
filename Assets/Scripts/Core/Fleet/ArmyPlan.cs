using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 軍自動編成（<see cref="ArmyOrganizationRules"/>）が産む<b>軍の完全な編成案</b>（純データ）。
    /// 前線の艦隊群（<see cref="fleets"/>）＋梯団（<see cref="corps"/>）＋予備兵力（<see cref="reserve"/>＝プールに留め置く分）。
    /// プールの総兵力 = <see cref="FrontlineStrength"/> ＋ <see cref="reserve"/>（端数なく保存される）。
    /// </summary>
    public sealed class ArmyPlan
    {
        /// <summary>前線へ展開する艦隊群（兵力降順とは限らない＝生成順）。</summary>
        public readonly List<FleetOrder> fleets = new();
        /// <summary>艦隊を束ねる梯団群（指揮官を充足できた塊のみ）。</summary>
        public readonly List<CorpsOrder> corps = new();
        /// <summary>予備として総プールに残す兵力（隻）。</summary>
        public int reserve;

        /// <summary>前線へ展開した総兵力＝全艦隊の兵力合計。</summary>
        public int FrontlineStrength
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < fleets.Count; i++) sum += fleets[i].strength;
                return sum;
            }
        }
    }
}
