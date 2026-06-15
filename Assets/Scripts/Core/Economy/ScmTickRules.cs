using System.Collections.Generic;

namespace Ginei
{
    /// <summary>SCM計画の結果（SCM-6・#2105）。総所要・正味所要・サービスレベル・ボトルネック品目。</summary>
    public class ScmPlanResult
    {
        public Dictionary<int, float> grossReq = new Dictionary<int, float>(); // 総所要（展開後）
        public Dictionary<int, float> netReq = new Dictionary<int, float>();   // 正味所要（手持ち控除後）
        public float serviceLevel = 1f;       // 最小律＝最も逼迫した原材料の充足（0..1）
        public int criticalCommodity = -1;    // ボトルネックの原材料品目（無ければ -1）
    }

    /// <summary>
    /// SCM計画の暦境界オーケストレータ（SCM-6・#2105 配線・純ロジック・read-only 計画）。
    /// 最終需要を展開→正味化→<b>原材料に対する最小律のサービスレベル</b>とボトルネック品目を返す。
    /// 状態は変えない（計画レイヤー）。`RequirementsExplosionRules`/`NetRequirementsRules`/`MrpCoverageRules` へ委譲。test-first。
    /// </summary>
    public static class ScmTickRules
    {
        /// <summary>
        /// 最終需要（品目→数量）を計画＝展開・正味化・サービスレベル算定。サービスレベルは原材料（レシピを持たない葉）の最小充足。
        /// </summary>
        public static ScmPlanResult Plan(IDictionary<int, float> finalDemands, CommodityStock onHand,
            int maxDepth = RequirementsExplosionRules.DefaultMaxDepth)
        {
            var result = new ScmPlanResult();
            if (finalDemands != null)
                foreach (var kv in finalDemands)
                    RequirementsExplosionRules.Accumulate(kv.Key, kv.Value, result.grossReq, maxDepth);

            result.netReq = NetRequirementsRules.NetRequirements(result.grossReq, onHand);

            // サービスレベル＝原材料（レシピを持たない＝採取/調達するしかない品目）の最小充足。
            float service = 1f;
            int critical = -1;
            foreach (var kv in result.grossReq)
            {
                if (RecipeBook.ForOutput(kv.Key) != null) continue; // 中間財/完成品は生産されるので供給制約でない
                float oh = onHand != null ? onHand.Get(kv.Key) : 0f;
                float cov = MrpCoverageRules.Coverage(oh, kv.Value);
                if (cov < service) { service = cov; critical = kv.Key; }
            }
            result.serviceLevel = service;
            result.criticalCommodity = critical;
            return result;
        }
    }
}
