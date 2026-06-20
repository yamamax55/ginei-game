using UnityEngine;

namespace Ginei
{
    /// <summary>官僚化とイノベーション死の調整係数（シュンペーター型・SCHU-3）。</summary>
    public readonly struct BureaucratizationParams
    {
        /// <summary>成功(0..1)が官僚化を進める/秒の基礎（成功が手続きを増やす）。</summary>
        public readonly float successBureaucratizationRate;
        /// <summary>規模(0..1)が官僚化を進める/秒の基礎（大きいほど手続きが要る）。</summary>
        public readonly float scaleBureaucratizationRate;
        /// <summary>革新力の逆相関の鋭さ（官僚化↑で革新力が落ちる曲がり。≥1）。</summary>
        public readonly float innovationSuppressExp;
        /// <summary>官僚化が革新を計画化・ルーティン化する最大割合（0..1）。</summary>
        public readonly float maxRoutinization;
        /// <summary>官僚化が人材流出を招く最大割合（0..1。窒息した才能が去る）。</summary>
        public readonly float maxExodus;
        /// <summary>革新力低下が成功を蝕む/秒の感度（革新力欠如が次の衰退を呼ぶ）。</summary>
        public readonly float selfUndermineRate;
        /// <summary>改革コストの非線形度（官僚化が進むほど巻き戻しが高くつく。≥1）。</summary>
        public readonly float revitalizeCostExp;

        public BureaucratizationParams(
            float successBureaucratizationRate, float scaleBureaucratizationRate, float innovationSuppressExp,
            float maxRoutinization, float maxExodus, float selfUndermineRate, float revitalizeCostExp)
        {
            this.successBureaucratizationRate = Mathf.Max(0f, successBureaucratizationRate);
            this.scaleBureaucratizationRate = Mathf.Max(0f, scaleBureaucratizationRate);
            this.innovationSuppressExp = Mathf.Max(1f, innovationSuppressExp);
            this.maxRoutinization = Mathf.Clamp01(maxRoutinization);
            this.maxExodus = Mathf.Clamp01(maxExodus);
            this.selfUndermineRate = Mathf.Max(0f, selfUndermineRate);
            this.revitalizeCostExp = Mathf.Max(1f, revitalizeCostExp);
        }

        /// <summary>
        /// 既定＝成功による官僚化0.08/秒・規模による官僚化0.05/秒・逆相関鋭さ1.5・
        /// 最大ルーティン化0.9・最大流出0.7・自壊感度0.06/秒・改革コスト非線形2。
        /// </summary>
        public static BureaucratizationParams Default
            => new BureaucratizationParams(0.08f, 0.05f, 1.5f, 0.9f, 0.7f, 0.06f, 2f);
    }

    /// <summary>
    /// 官僚化とイノベーション死の純ロジック（シュンペーター型・SCHU-3 #1587）。成功した革新的組織が成長すると
    /// <b>制度化・官僚化</b>し（<see cref="BureaucratizationTick"/>＝成功と規模が手続きを増やす）、かえって
    /// <b>革新力を失う</b>（<see cref="InnovationCapacity"/>＝官僚化と革新力は逆相関。手続きが起業家精神を窒息）。
    /// 大企業の計画化された進歩は革新をルーティン化し（<see cref="RoutinizationOfInnovation"/>＝企業家機能の陳腐化）、
    /// 窒息を嫌った革新的人材が去り（<see cref="EntrepreneurExodus"/>）、革新力の喪失が成功を蝕んで次の衰退を呼ぶ
    /// （<see cref="SelfUnderminingTick"/>）＝<b>成功→制度化→革新力喪失→衰退の自壊ループ＝成功そのものが革新の墓を掘る</b>。
    /// 古く大きい組織ほど硬直化し（<see cref="Ossification"/>）、巻き戻す改革は官僚化が深いほど高くつく
    /// （<see cref="RevitalizationCost"/>）。革新が死んだかは <see cref="IsInnovationDead"/>。
    /// 分担：`BureaucracyBloatRules`＝パーキンソン的な人数の自己増殖（定員肥大・管理コスト）／
    /// `CeremonialismRules`＝儀礼的威信での存続（中身が空でも形式で続く）／同EPIC SCHU の `CreativeDestructionRules`
    /// ＝創造的破壊（新陳代謝そのもの）。**本クラス＝成功→制度化→革新力喪失の自壊ループ（イノベーションの死）**。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（新しい値を返す）。調整値は <see cref="BureaucratizationParams"/>
    /// （既定 <see cref="BureaucratizationParams.Default"/>）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BureaucratizationRules
    {
        /// <summary>官僚化の1tick進行（既定 Params）。</summary>
        public static float BureaucratizationTick(float bureaucracy, float organizationSuccess, float scale, float dt)
            => BureaucratizationTick(bureaucracy, organizationSuccess, scale, dt, BureaucratizationParams.Default);

        /// <summary>
        /// 官僚化の1tick進行（0..1）＝成功と規模が時間で手続きを増やす。
        /// 増分＝(成功×successRate＋規模×scaleRate)×dt を現在の官僚化に加える。
        /// **成功した組織ほど・大きい組織ほど速く官僚化する**＝成功が制度化を呼ぶ自壊ループの起点。
        /// 成功0かつ規模0なら進まない（無風）。新しい官僚化を返す（引数非破壊）。
        /// </summary>
        public static float BureaucratizationTick(
            float bureaucracy, float organizationSuccess, float scale, float dt, BureaucratizationParams p)
        {
            float b = Mathf.Clamp01(bureaucracy);
            float success = Mathf.Clamp01(organizationSuccess);
            float sc = Mathf.Clamp01(scale);
            float step = Mathf.Max(0f, dt);

            float rate = success * p.successBureaucratizationRate + sc * p.scaleBureaucratizationRate;
            return Mathf.Clamp01(b + rate * step);
        }

        /// <summary>革新力（既定 Params）。</summary>
        public static float InnovationCapacity(float bureaucracy)
            => InnovationCapacity(bureaucracy, BureaucratizationParams.Default);

        /// <summary>
        /// 革新力（0..1）＝官僚化の逆相関。(1−官僚化)^innovationSuppressExp。
        /// 官僚化が進むほど急激に革新力が落ちる＝**手続きが起業家精神を窒息させる**。
        /// 官僚化0で満額1・官僚化1で0。呼び出し側が新規事業・研究効率の係数として消費する想定（基準非破壊）。
        /// </summary>
        public static float InnovationCapacity(float bureaucracy, BureaucratizationParams p)
        {
            float b = Mathf.Clamp01(bureaucracy);
            return Mathf.Clamp01(Mathf.Pow(1f - b, p.innovationSuppressExp));
        }

        /// <summary>革新のルーティン化（既定 Params）。</summary>
        public static float RoutinizationOfInnovation(float bureaucracy, float plannedProgress)
            => RoutinizationOfInnovation(bureaucracy, plannedProgress, BureaucratizationParams.Default);

        /// <summary>
        /// 革新が計画化・ルーティン化される度合い（0..1）＝官僚化×計画化された進歩×maxRoutinization。
        /// 大企業の計画的な研究開発が革新を手続きへ落とし込むほど、ひらめきは routine に置き換わる
        /// ＝**企業家機能の陳腐化**（シュンペーター）。官僚化が低ければ計画化が進んでも低い（まだ個人の才に依る）。
        /// </summary>
        public static float RoutinizationOfInnovation(float bureaucracy, float plannedProgress, BureaucratizationParams p)
        {
            float b = Mathf.Clamp01(bureaucracy);
            float planned = Mathf.Clamp01(plannedProgress);
            return Mathf.Clamp01(b * planned * p.maxRoutinization);
        }

        /// <summary>起業家人材の流出（既定 Params）。</summary>
        public static float EntrepreneurExodus(float bureaucracy, float talentMobility)
            => EntrepreneurExodus(bureaucracy, talentMobility, BureaucratizationParams.Default);

        /// <summary>
        /// 革新的人材の流出割合（0..1）＝官僚化×人材流動性×maxExodus。
        /// 官僚化を嫌う起業家気質ほど・転職の自由があるほど去る＝**窒息した才能が組織を離れる**。
        /// 流動性0（縛られて動けない）なら流出0だが、それは才能が残るのでなく死蔵されるだけ。
        /// </summary>
        public static float EntrepreneurExodus(float bureaucracy, float talentMobility, BureaucratizationParams p)
        {
            float b = Mathf.Clamp01(bureaucracy);
            float mobility = Mathf.Clamp01(talentMobility);
            return Mathf.Clamp01(b * mobility * p.maxExodus);
        }

        /// <summary>自壊の1tick（既定 Params）。</summary>
        public static float SelfUnderminingTick(float organizationSuccess, float innovationCapacity, float dt)
            => SelfUnderminingTick(organizationSuccess, innovationCapacity, dt, BureaucratizationParams.Default);

        /// <summary>
        /// 自壊の1tick＝革新力低下が成功を蝕んで次の衰退を呼ぶ（dt後の成功 0..1）。
        /// 減分＝(1−革新力)×selfUndermineRate×dt を成功から引く＝**革新力を失った組織は成功を維持できない**。
        /// 革新力が満額1なら蝕まない（革新し続ければ成功は保てる）。
        /// この成功低下は次tickの <see cref="BureaucratizationTick"/> の入力にも効く＝
        /// 成功→官僚化→革新力喪失→成功喪失→…の自壊ループ＝**成功が革新の墓を掘る**。新しい成功を返す（引数非破壊）。
        /// </summary>
        public static float SelfUnderminingTick(
            float organizationSuccess, float innovationCapacity, float dt, BureaucratizationParams p)
        {
            float success = Mathf.Clamp01(organizationSuccess);
            float innov = Mathf.Clamp01(innovationCapacity);
            float step = Mathf.Max(0f, dt);

            float erosion = (1f - innov) * p.selfUndermineRate * step;
            return Mathf.Clamp01(success - erosion);
        }

        /// <summary>
        /// 組織の硬直化（0..1）＝官僚化×組織の古さ。古く大きい（官僚化した）ほど固まり、新しい手続きを
        /// 受け付けなくなる＝過去の成功体験への固着。age（0..1＝組織年齢の規格化）と官僚化の積。
        /// 呼び出し側が変化への抵抗・適応速度の逆数として消費する想定。
        /// </summary>
        public static float Ossification(float bureaucracy, float age)
        {
            float b = Mathf.Clamp01(bureaucracy);
            float a = Mathf.Clamp01(age);
            return Mathf.Clamp01(b * a);
        }

        /// <summary>改革コスト（既定 Params）。</summary>
        public static float RevitalizationCost(float bureaucracy)
            => RevitalizationCost(bureaucracy, BureaucratizationParams.Default);

        /// <summary>
        /// 官僚化を巻き戻す改革コスト（0..1＝相対コスト）＝官僚化^revitalizeCostExp。
        /// 官僚化が深いほど非線形に高くつく＝**根を張った手続きと既得を解くのは桁違いに難しい**。
        /// 官僚化0なら0（改革不要）。呼び出し側が財政・政治資本のコストへ換算する想定。
        /// </summary>
        public static float RevitalizationCost(float bureaucracy, BureaucratizationParams p)
        {
            float b = Mathf.Clamp01(bureaucracy);
            return Mathf.Clamp01(Mathf.Pow(b, p.revitalizeCostExp));
        }

        /// <summary>
        /// 革新が死んだ判定（true＝イノベーションの死）。革新力が閾値を下回ったとき成立。
        /// 自壊ループの終端＝もはや手続きの再生産しか残らない（企業家機能の完全な陳腐化）。
        /// 閾値はクランプ。
        /// </summary>
        public static bool IsInnovationDead(float innovationCapacity, float threshold)
        {
            float innov = Mathf.Clamp01(innovationCapacity);
            float thr = Mathf.Clamp01(threshold);
            return innov < thr;
        }
    }
}
