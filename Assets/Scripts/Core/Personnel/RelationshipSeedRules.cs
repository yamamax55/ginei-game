using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// lore で記した <see cref="RelationshipSeed"/> を runtime の <see cref="PersonRelationGraph"/> へ解決して張る橋渡し
    /// （PER-PROPS A1）。関係の力学・グラフ本体は既存の <see cref="PersonRelationRules"/> が窓口＝ここは名前→id 解決と
    /// intensity(0..100)→strength(0..1) 変換だけを行い、関係システムを再実装しない。決定論・純ロジック。
    /// </summary>
    public static class RelationshipSeedRules
    {
        /// <summary>intensity(0..100) を graph の strength(0..1) へ。</summary>
        public static float ToStrength(int intensity) => Mathf.Clamp(intensity, 0, 100) / 100f;

        /// <summary>
        /// fromId（種の持ち主）の seeds を、名前→id 辞書で相手を解決して graph に張る。張れた件数を返す。
        /// 相手が辞書に無い／自己参照／空名／null 入力は黙ってスキップ（戻り値の差で件数を可視化）。
        /// </summary>
        public static int Resolve(PersonRelationGraph graph, int fromId,
                                  IReadOnlyList<RelationshipSeed> seeds,
                                  IReadOnlyDictionary<string, int> nameToId)
        {
            if (graph == null || seeds == null || nameToId == null) return 0;
            int linked = 0;
            for (int i = 0; i < seeds.Count; i++)
            {
                RelationshipSeed s = seeds[i];
                if (string.IsNullOrEmpty(s.targetName)) continue;
                if (!nameToId.TryGetValue(s.targetName, out int toId)) continue;
                if (toId == fromId) continue;
                graph.Set(fromId, toId, s.kind, ToStrength(s.intensity));
                linked++;
            }
            return linked;
        }
    }
}
