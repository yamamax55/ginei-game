using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略艦隊が抱える<b>艦艇数（隻）</b>の純ロジック。
    ///
    /// <b>兵力（<see cref="StrategicFleet.strength"/>）とは別物</b>：兵力は抽象的な戦闘力の指標で、
    /// 艦艇数は「その艦隊に何隻いるか」という実数。表示でも混同しない（兵力は「兵力 300」、
    /// 艦艇は「艦艇 6,000隻」）。単位は勢力の保有総艦艇（<see cref="FleetPool"/>）と同じ「隻」。
    ///
    /// <b>艦隊ごとに独立して持つ</b>のが要点：会戦で失った船は<b>その艦隊からだけ</b>減らし、
    /// ほかの艦隊へ均等割りしない。本隊と援軍もそれぞれ自分の隻数を持ち歩く。
    ///
    /// 純ロジック（非 MonoBehaviour・決定論・test-first）。
    /// </summary>
    public static class FleetShipCountRules
    {
        /// <summary>
        /// 兵力1あたりの艦艇数（後方互換の初期化に使う換算）。
        /// 既存のセーブと既存の盤面は艦艇数を持っていないので、<b>兵力から導出</b>して埋める。
        /// 値は会戦の戦術スケール（<see cref="BattleHandoff.StrengthScale"/>＝兵力1が戦術 baseStrength 40）に
        /// 揃えてある＝「兵力300の艦隊＝12,000隻」で、勢力の総艦艇プールと桁が合う。
        /// </summary>
        public const int ShipsPerStrength = BattleHandoff.StrengthScale;

        /// <summary>兵力から艦艇数を導出する（後方互換の初期化）。負は0。</summary>
        public static int FromStrength(int strength) => Mathf.Max(0, strength) * ShipsPerStrength;

        /// <summary>
        /// セーブや盤面に艦艇数が入っていなければ兵力から埋める（既に入っていればそのまま）。
        /// 旧セーブ（フィールドが無い＝0）を読んでも艦隊が空にならないようにするための窓口。
        /// </summary>
        public static int EnsureInitialized(int shipCount, int strength)
            => shipCount > 0 ? shipCount : FromStrength(strength);

        /// <summary>
        /// 会戦の損害を<b>その艦隊の艦艇数</b>へ反映する。兵力が減った割合と同じだけ隻数も減らす。
        /// 兵力が0になったら艦艇も0（全滅）。兵力が減っていなければ隻数も変えない。
        /// <paramref name="beforeStrength"/> が0以下のときは変更しない（割合を出せないため）。
        /// </summary>
        public static int AfterLosses(int beforeShips, int beforeStrength, int afterStrength)
        {
            int ships = Mathf.Max(0, beforeShips);
            if (ships == 0) return 0;
            if (beforeStrength <= 0) return ships;

            int after = Mathf.Max(0, afterStrength);
            if (after <= 0) return 0;                  // 全滅
            if (after >= beforeStrength) return ships; // 無傷（増やさない）

            // 切り捨てるが、生き残っているなら最低1隻は残す（兵力が残っているのに0隻はおかしい）。
            long kept = (long)ships * after / beforeStrength;
            return Mathf.Max(1, (int)kept);
        }

        /// <summary>
        /// 艦隊を分割・合流させるときに移す隻数（兵力の移動量に比例）。
        /// 移動元に残る隻数は呼び手が <c>before - Transferred</c> で求める。
        /// </summary>
        public static int Transferred(int beforeShips, int beforeStrength, int movedStrength)
        {
            int ships = Mathf.Max(0, beforeShips);
            if (ships == 0 || beforeStrength <= 0) return 0;
            int moved = Mathf.Clamp(movedStrength, 0, beforeStrength);
            return (int)((long)ships * moved / beforeStrength);
        }

        /// <summary>
        /// 艦隊の兵力を <paramref name="newStrength"/> へ変える<b>唯一の窓口</b>。
        /// <b>実際に艦を失った</b>ときはこれを通すこと＝艦艇数がその艦隊のぶんだけ追随する。
        ///
        /// 戦闘力の一時的な低下（士気・補給ペナルティの倍率など）と、
        /// <b>物理的な艦艇の損失</b>は別物なので、前者にこれを使ってはいけない
        /// （倍率で見かけの強さが落ちただけなら艦は減っていない）。
        /// </summary>
        public static void ApplyPhysicalLoss(StrategicFleet fleet, int newStrength)
        {
            if (fleet == null) return;
            int before = Mathf.Max(0, fleet.strength);
            int after = Mathf.Max(0, newStrength);
            fleet.SetShips(AfterLosses(fleet.Ships, before, after));
            fleet.strength = after;
        }

        /// <summary>
        /// 新しく編成した艦隊の艦艇数を確定する（難易度補正など兵力を決め終わった<b>あと</b>に呼ぶ）。
        /// これを通しておけば、以後は毎回の兵力からの導出に頼らない（導出は旧データ専用の後方互換）。
        /// </summary>
        public static void InitializeShips(StrategicFleet fleet)
        {
            if (fleet == null || fleet.shipCountSet) return;   // 確定済みは触らない
            // すでに正の値が入っていればそれを尊重し、空（0）のときだけ兵力から導出する。
            fleet.SetShips(EnsureInitialized(fleet.shipCount, fleet.strength));
        }

        /// <summary>表示用の一行（「艦艇 12,000隻」）。0 隻でも表記は出す。</summary>
        public static string Label(int shipCount) => $"艦艇 {Mathf.Max(0, shipCount):N0}隻";

        /// <summary>複数艦隊の艦艇数の合計（勢力の在席戦力を数えるとき）。</summary>
        public static int Sum(IList<int> shipCounts)
        {
            if (shipCounts == null) return 0;
            int t = 0;
            for (int i = 0; i < shipCounts.Count; i++) t += Mathf.Max(0, shipCounts[i]);
            return t;
        }
    }
}
