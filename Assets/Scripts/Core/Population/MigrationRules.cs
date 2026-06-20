using UnityEngine;

namespace Ginei
{
    /// <summary>平時移民の調整係数。</summary>
    public readonly struct MigrationParams
    {
        /// <summary>経済格差（prosperityGap）が引力に効く重み。</summary>
        public readonly float prosperityWeight;
        /// <summary>思想的自由の格差（freedomGap）が引力に効く重み。</summary>
        public readonly float freedomWeight;
        /// <summary>引力最大・国境全開のとき1tickで動く人口の割合の上限。</summary>
        public readonly float maxFlowRatio;
        /// <summary>流出者に占める優秀層の基礎比率（引力ゼロでもこれだけは混ざる）。</summary>
        public readonly float baseBrainRatio;
        /// <summary>引力が強いほど優秀層から先に出ていく偏りの強さ（base に加算）。</summary>
        public readonly float brainDrainBias;
        /// <summary>閉じた国境が中に溜める不満の強さ（出たい圧×閉鎖度に掛ける）。</summary>
        public readonly float resentmentScale;

        public MigrationParams(float prosperityWeight, float freedomWeight, float maxFlowRatio,
                               float baseBrainRatio, float brainDrainBias, float resentmentScale)
        {
            this.prosperityWeight = Mathf.Max(0f, prosperityWeight);
            this.freedomWeight = Mathf.Max(0f, freedomWeight);
            this.maxFlowRatio = Mathf.Clamp01(maxFlowRatio);
            this.baseBrainRatio = Mathf.Clamp01(baseBrainRatio);
            this.brainDrainBias = Mathf.Clamp01(brainDrainBias);
            this.resentmentScale = Mathf.Max(0f, resentmentScale);
        }

        /// <summary>既定＝経済重み0.6・自由重み0.4・最大流出率0.05・基礎優秀比0.2・頭脳偏り0.5・閉鎖不満0.5。</summary>
        public static MigrationParams Default => new MigrationParams(0.6f, 0.4f, 0.05f, 0.2f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 平時移民の純ロジック。経済格差と思想的自由を求めて人口が自発的に国境を越える＝「人は足で投票する」。
    /// 引力（pull）が正なら流出・負なら流入（逆流）で、国境の開き具合（openness）が実際の流量を絞る。
    /// 出ていくのは優秀な層から（頭脳流出）＝受け入れ側は人数以上の人材の質を得る。
    /// 国境を閉じても出たい圧は消えず、中に不満として溜まる＝「壁は外への流出を止めて中に不満を圧縮する」。
    /// 戦火による強制移動（<see cref="RefugeeRules"/>＝追い立てられる難民）とは別系統で、
    /// こちらは平時の自発的移動だけを扱う。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MigrationRules
    {
        /// <summary>
        /// 移民の引力（-1..1）。prosperityGap/freedomGap（各 -1..1＝相手国−自国の格差）を重みで合成。
        /// 正＝出ていきたい（流出圧）・負＝入ってきたい（逆流）・0＝動機なし。
        /// </summary>
        public static float MigrationPull(float prosperityGap, float freedomGap, MigrationParams p)
        {
            float pull = p.prosperityWeight * Mathf.Clamp(prosperityGap, -1f, 1f)
                       + p.freedomWeight * Mathf.Clamp(freedomGap, -1f, 1f);
            return Mathf.Clamp(pull, -1f, 1f);
        }

        public static float MigrationPull(float prosperityGap, float freedomGap)
            => MigrationPull(prosperityGap, freedomGap, MigrationParams.Default);

        /// <summary>
        /// 1tick で実際に動く人口（符号付き＝正は流出・負は流入）。
        /// 人口×maxFlowRatio×引力×国境の開き(0..1)×dt。国境ゼロ＝誰も動けない。
        /// 大きさは人口を超えない（全員出てもそれ以上は出ない）。
        /// </summary>
        public static float FlowTick(float population, float pull, float borderOpenness, float dt, MigrationParams p)
        {
            float pop = Mathf.Max(0f, population);
            float flow = pop * p.maxFlowRatio * Mathf.Clamp(pull, -1f, 1f)
                       * Mathf.Clamp01(borderOpenness) * Mathf.Max(0f, dt);
            return Mathf.Clamp(flow, -pop, pop);
        }

        public static float FlowTick(float population, float pull, float borderOpenness, float dt)
            => FlowTick(population, pull, borderOpenness, dt, MigrationParams.Default);

        /// <summary>
        /// 流出者に占める優秀層の比率（0..1）。引力が強いほど「先に見切るのは有能な者」＝
        /// 基礎比率＋偏り×引力(正のみ)。流入（pull≦0）は基礎比率のまま＝偏りは流出側だけの現象。
        /// </summary>
        public static float BrainDrainRatio(float pull, MigrationParams p)
        {
            return Mathf.Clamp01(p.baseBrainRatio + p.brainDrainBias * Mathf.Clamp01(pull));
        }

        public static float BrainDrainRatio(float pull) => BrainDrainRatio(pull, MigrationParams.Default);

        /// <summary>
        /// 受け入れ側が得る人材の質ボーナス＝流入人数×優秀比。流出側の損失と受け入れ側の利得は同じ量
        /// （頭脳はゼロサムで移る）。flow が負（流入していない）なら0。
        /// </summary>
        public static float TalentTransfer(float flow, float brainRatio)
        {
            return Mathf.Max(0f, flow) * Mathf.Clamp01(brainRatio);
        }

        /// <summary>
        /// 閉じた国境が中に溜める不満（0..resentmentScale）。出たい圧（pull 正）×国境の閉鎖度×係数＝
        /// 壁は流出を止めるが動機は消さず、出られない者の不満として内側に圧縮される。
        /// 出たい者がいない（pull≦0）か国境全開なら0。
        /// </summary>
        public static float ClosedBorderResentment(float pull, float openness, MigrationParams p)
        {
            return Mathf.Clamp01(pull) * (1f - Mathf.Clamp01(openness)) * p.resentmentScale;
        }

        public static float ClosedBorderResentment(float pull, float openness)
            => ClosedBorderResentment(pull, openness, MigrationParams.Default);
    }
}
