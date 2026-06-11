using UnityEngine;

namespace Ginei
{
    /// <summary>多極経済秩序の四本柱カスケードの調整係数（#1599）。</summary>
    public readonly struct InternationalOrderParams
    {
        /// <summary>相互支持の重み（自柱の健全度のうち、周囲の柱の健全度が支える割合＝大きいほど他柱依存）。</summary>
        public readonly float supportWeight;
        /// <summary>連鎖の速さ（相互支持の不足が1ステップでどれだけ自柱を蝕むか・dt当たり）。</summary>
        public readonly float cascadeRate;
        /// <summary>秩序崩壊と見なす最弱柱の閾値（最弱柱がこれを割れば秩序全体が崩れたと判定）。</summary>
        public readonly float collapseThreshold;

        public InternationalOrderParams(float supportWeight, float cascadeRate, float collapseThreshold)
        {
            this.supportWeight = Mathf.Clamp01(supportWeight);
            this.cascadeRate = Mathf.Clamp01(cascadeRate);
            this.collapseThreshold = Mathf.Clamp01(collapseThreshold);
        }

        /// <summary>既定＝相互支持重み0.5／連鎖率0.2／崩壊閾値0.25。</summary>
        public static InternationalOrderParams Default => new InternationalOrderParams(0.5f, 0.2f, 0.25f);
    }

    /// <summary>
    /// 多極経済秩序の相互支持と連鎖崩壊（四本柱カスケード）の純ロジック（#1599・唯一の窓口・
    /// ポランニー『大転換』参考）。19世紀文明は<b>勢力均衡・国際金本位制・自己調整市場・自由主義国家</b>の
    /// 四つの制度が相互に支え合って立っていた＝一本が倒れると相互支持を失った隣が次々倒れる連鎖（ドミノ）。
    /// 秩序は複数の柱の相互依存で立ち、<b>最弱の一本が倒れると連鎖して全体が崩れる</b>＝鎖は最弱の輪で切れる
    /// （<see cref="OrderStability"/>＝最弱柱が律速・<see cref="WeakestPillar"/>＝崩壊の起点）。
    /// 柱は健全度0..1の配列で受ける汎用設計（4本に限定しないが既定は四本柱想定）。
    /// <see cref="BalanceOfPowerRules"/>（多極均衡＝勢力の力関係・一強の包囲）とは別＝こちらは
    /// <b>経済秩序を支える複数の柱の相互依存と連鎖崩壊</b>（力でなく制度の相互支持）。
    /// <see cref="CollectiveSecurityRules"/>（集団安全保障＝多国間の制裁参加と足並み）とも別＝
    /// こちらは制度の柱が互いを支える依存構造のドミノに特化。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class InternationalOrderRules
    {
        /// <summary>
        /// 秩序全体の安定度（0..1）＝最弱柱の健全度がそのまま効く＝<b>鎖は最弱の輪で切れる</b>。
        /// 他の柱がどれほど健全でも、一本が脆ければ秩序はその脆さに引きずられる。
        /// null/空配列は中立で0扱い（柱なき秩序＝立っていない）。
        /// </summary>
        public static float OrderStability(float[] pillars)
        {
            if (pillars == null || pillars.Length == 0) return 0f;
            float weakest = 1f;
            for (int i = 0; i < pillars.Length; i++)
            {
                float h = Mathf.Clamp01(pillars[i]);
                if (h < weakest) weakest = h;
            }
            return weakest;
        }

        /// <summary>
        /// 最も脆い柱の添字（崩壊の起点・同値は先勝ち・null/空は-1）。
        /// 連鎖は最弱柱から始まる＝どこが先に倒れるかの窓口。
        /// </summary>
        public static int WeakestPillar(float[] pillars)
        {
            if (pillars == null || pillars.Length == 0) return -1;
            int worst = 0;
            float worstHealth = Mathf.Clamp01(pillars[0]);
            for (int i = 1; i < pillars.Length; i++)
            {
                float h = Mathf.Clamp01(pillars[i]);
                if (h < worstHealth)
                {
                    worstHealth = h;
                    worst = i;
                }
            }
            return worst;
        }

        /// <summary>
        /// 相互支持で底上げされた自柱の実効健全度（0..1）＝周囲の柱が健全なほど自柱が支えられる。
        /// 自柱の素の健全度に、周囲の平均健全度を相互支持重み <see cref="InternationalOrderParams.supportWeight"/>
        /// ぶん混ぜる＝隣が健全なら自柱は持ちこたえ、隣が倒れれば支えを失って下がる。
        /// 周囲が無い（null/空）なら相互支持なし＝自柱の素の健全度そのまま。
        /// </summary>
        public static float MutualSupport(float pillarHealth, float[] neighborsHealth, InternationalOrderParams p)
        {
            float self = Mathf.Clamp01(pillarHealth);
            if (neighborsHealth == null || neighborsHealth.Length == 0) return self;

            float sum = 0f;
            int count = 0;
            for (int i = 0; i < neighborsHealth.Length; i++)
            {
                sum += Mathf.Clamp01(neighborsHealth[i]);
                count++;
            }
            if (count == 0) return self;
            float neighborAvg = sum / count;
            // 素の健全度に周囲の支えを重みぶん混ぜる（周囲が健全なら底上げ・脆ければ引き下げ）。
            return Mathf.Clamp01(self * (1f - p.supportWeight) + neighborAvg * p.supportWeight);
        }

        public static float MutualSupport(float pillarHealth, float[] neighborsHealth)
            => MutualSupport(pillarHealth, neighborsHealth, InternationalOrderParams.Default);

        /// <summary>
        /// 連鎖の1ステップ＝一本が弱ると相互支持が減り隣も弱る連鎖（新配列を返す・元配列は非破壊）。
        /// 各柱について、自柱を除く他柱の平均健全度（＝受けられる相互支持）が自柱を下回るぶん、
        /// 連鎖率 <see cref="InternationalOrderParams.cascadeRate"/>×dt の率で自柱を引き下げる＝
        /// 脆い柱が周囲の支えを奪い、健全な柱も巻き込まれて沈んでいくドミノ。支えが十分なら下がらない。
        /// </summary>
        public static float[] CascadeTick(float[] pillars, float dt, InternationalOrderParams p)
        {
            if (pillars == null) return new float[0];
            int n = pillars.Length;
            var result = new float[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = Mathf.Clamp01(pillars[i]);
            }
            float step = Mathf.Max(0f, dt);
            if (step <= 0f || n <= 1) return result;

            // 全柱の合計（各柱の「自分以外の平均」を出す母数）。
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                total += result[i];
            }

            var next = new float[n];
            for (int i = 0; i < n; i++)
            {
                float self = result[i];
                float othersAvg = (total - self) / (n - 1); // 自柱を除く他柱の平均＝受けられる相互支持
                // 支えが自柱を下回るぶんだけ蝕まれる（隣が倒れた柱ほど深く沈む）。
                float deficit = Mathf.Max(0f, self - othersAvg);
                float drop = deficit * Mathf.Clamp01(p.cascadeRate * step);
                next[i] = Mathf.Clamp01(self - drop);
            }
            return next;
        }

        public static float[] CascadeTick(float[] pillars, float dt)
            => CascadeTick(pillars, dt, InternationalOrderParams.Default);

        /// <summary>
        /// 倒れた柱が依存する柱を引き倒す伝染（0..1）＝崩壊した柱の脆さが、依存度に応じて依存先の柱を削る。
        /// 倒れた柱の不健全度（1−failedPillarHealth）と相互依存度 interdependence（0..1）の積ぶん、
        /// 依存柱 dependentPillar の健全度を引き下げる＝<b>一本の崩壊が次を倒すドミノ</b>。
        /// 依存度0なら無影響（独立した柱は連鎖しない）、倒れた柱が健全なら伝染なし。
        /// </summary>
        public static float CollapseContagion(float failedPillarHealth, float dependentPillar, float interdependence)
        {
            float failedHealth = Mathf.Clamp01(failedPillarHealth);
            float dependent = Mathf.Clamp01(dependentPillar);
            float dep = Mathf.Clamp01(interdependence);
            float failure = 1f - failedHealth;            // 倒れた柱の不健全度
            float contagion = failure * dep;              // 依存しているぶんだけ引き倒される
            return Mathf.Clamp01(dependent * (1f - contagion));
        }

        /// <summary>
        /// 秩序全体の崩壊判定＝最弱柱が閾値 threshold を割れば秩序は崩れた＝最弱の一本が全体を決める。
        /// 引数省略版は <see cref="InternationalOrderParams.collapseThreshold"/> を使う。
        /// null/空配列は崩壊扱い（柱なき秩序は立っていない＝true）。
        /// </summary>
        public static bool OrderCollapsed(float[] pillars, float threshold)
        {
            if (pillars == null || pillars.Length == 0) return true;
            return OrderStability(pillars) < Mathf.Clamp01(threshold);
        }

        public static bool OrderCollapsed(float[] pillars)
            => OrderCollapsed(pillars, InternationalOrderParams.Default.collapseThreshold);

        public static bool OrderCollapsed(float[] pillars, InternationalOrderParams p)
            => OrderCollapsed(pillars, p.collapseThreshold);

        /// <summary>
        /// 再建の難しさ（0..1）＝倒れた柱が多いほど秩序を立て直すのが難しい。
        /// collapsedCount／totalPillars の割合＝全柱が倒れれば1（壊滅・再建ほぼ不能）、
        /// 一本も倒れていなければ0。柱が全て倒れた状態からの再建は、相互支持の足場が無く最も難しい。
        /// totalPillars が0以下なら1扱い（再建すべき柱の枠すら無い）。
        /// </summary>
        public static float RestorationDifficulty(int collapsedCount, int totalPillars)
        {
            if (totalPillars <= 0) return 1f;
            int collapsed = Mathf.Clamp(collapsedCount, 0, totalPillars);
            return Mathf.Clamp01((float)collapsed / totalPillars);
        }

        /// <summary>
        /// 秩序の耐連鎖性（0..1）＝柱の冗長性 redundancy（0..1＝代替制度の厚み）が連鎖に耐える。
        /// 基礎は秩序の安定度（最弱柱）だが、冗長性が高いほど一本の崩壊を代替が肩代わりして連鎖を止める＝
        /// 安定度の不足ぶんを冗長性で底上げする。冗長0なら安定度そのまま（最弱柱で即連鎖）。
        /// </summary>
        public static float Resilience(float[] pillars, float redundancy)
        {
            float stability = OrderStability(pillars);
            float redun = Mathf.Clamp01(redundancy);
            // 安定度の不足ぶん（1−安定度）を冗長性が肩代わりして底上げする。
            return Mathf.Clamp01(stability + (1f - stability) * redun);
        }
    }
}
