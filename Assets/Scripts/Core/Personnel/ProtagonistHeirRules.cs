using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 主人公の世継ぎ生成の純ロジック（採用「主人公の死で詰まない」・#907 解答の継承側・#646 接続）。貴族/王家の主人公が
    /// 死んだとき、操作座を継ぐ<b>世継ぎ Person</b> を作る＝同勢力・出自の初任段（<see cref="OriginRules.CommissionFloor"/>）から、
    /// 能力は先代の素質を継ぎつつ<b>平均回帰</b>（100%は継がない）。継承の発火（誰が死ぬか・いつ）は Game 層、後継者の席次/家督の
    /// 細部は <c>SuccessionRules</c>#646 へ委譲＝本ルールは<b>世継ぎ Person の組成のみ</b>（並行系を作らない・additive）。決定論・test-first。
    /// </summary>
    public static class ProtagonistHeirRules
    {
        /// <summary>世継ぎの能力＝先代の素質を平均(50)へ <paramref name="regression"/> ぶん引き戻した値（1..100）。
        /// 既定0.7＝優秀な親の子は優秀寄りだが同等ではない（凡庸な親の子は平均へ寄る）。</summary>
        public static int InheritStat(int parentStat, float regression = 0.7f)
            => Mathf.Clamp(Mathf.RoundToInt(50f + (parentStat - 50f) * Mathf.Clamp01(regression)), 1, 100);

        /// <summary>先代 <paramref name="predecessor"/> から世継ぎ Person を作る（同勢力・出自の初任段・能力は平均回帰）。</summary>
        public static Person CreateHeir(Person predecessor, PersonOrigin origin, int newId, string heirName, int birthYear)
        {
            Faction f = predecessor != null ? predecessor.faction : Faction.同盟;
            var heir = new Person(newId, string.IsNullOrEmpty(heirName) ? "世継ぎ" : heirName, f, PersonRole.軍人)
            {
                birthYear = birthYear,
                rankTier = Mathf.Max(1, OriginRules.CommissionFloor(origin)), // 出自の初任段から出仕（貴族3/王家4）
            };
            if (predecessor != null)
            {
                heir.leadership = InheritStat(predecessor.leadership);
                heir.attack = InheritStat(predecessor.attack);
                heir.defense = InheritStat(predecessor.defense);
                heir.mobility = InheritStat(predecessor.mobility);
                heir.operation = InheritStat(predecessor.operation);
                heir.intelligence = InheritStat(predecessor.intelligence);
            }
            return heir;
        }
    }
}
