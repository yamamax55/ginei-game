using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 無政府状態の純データ（#1459・ホッブズ『リヴァイアサン』の自然状態）。主権（共通権力）が崩れた
    /// 宙域の「無法度・暴力水準・経済崩壊」を持つ＝万人の万人に対する闘争の現況スナップショット。
    /// </summary>
    public struct AnarchyState
    {
        /// <summary>無法度(0..1)＝主権の不在。1=共通権力が完全に消えた自然状態。</summary>
        public float lawlessness;
        /// <summary>暴力水準(0..1)＝万人の闘争の烈しさ。1=険悪で残忍で短い生活。</summary>
        public float violence;
        /// <summary>経済崩壊(0..1)＝生産・交易の停止度。1=誰も投資せず近代的産業が育たない。</summary>
        public float economicCollapse;

        public AnarchyState(float lawlessness, float violence, float economicCollapse)
        {
            this.lawlessness = Mathf.Clamp01(lawlessness);
            this.violence = Mathf.Clamp01(violence);
            this.economicCollapse = Mathf.Clamp01(economicCollapse);
        }
    }

    /// <summary>無政府コストの調整係数。自然状態のコスト・波及・悪化の重みを束ねる。</summary>
    public readonly struct AnarchyCostParams
    {
        /// <summary>自然状態コストで無法度が占める比重(0..1。残りが暴力の比重)。</summary>
        public readonly float lawlessnessWeight;
        /// <summary>経済麻痺の急峻さ（指数・1以上。無法が高いほど産業が非線形に止まる）。</summary>
        public readonly float paralysisExponent;
        /// <summary>無政府宙域の波及が届く減衰の鋭さ（大きいほど近隣しか不安定化しない）。</summary>
        public readonly float destabilizationFalloff;
        /// <summary>主権の空白が暴力を深める速度（per duration×無法×脅威）。</summary>
        public readonly float vacuumRate;
        /// <summary>軍閥が割拠しうる空白の最低水準（これ未満は強者も出ない）。</summary>
        public readonly float warlordThreshold;

        public AnarchyCostParams(float lawlessnessWeight, float paralysisExponent, float destabilizationFalloff, float vacuumRate, float warlordThreshold)
        {
            this.lawlessnessWeight = Mathf.Clamp01(lawlessnessWeight);
            this.paralysisExponent = Mathf.Max(1f, paralysisExponent);
            this.destabilizationFalloff = Mathf.Max(0f, destabilizationFalloff);
            this.vacuumRate = Mathf.Max(0f, vacuumRate);
            this.warlordThreshold = Mathf.Clamp01(warlordThreshold);
        }

        /// <summary>既定＝無法比重0.5・麻痺指数1.5・波及減衰2・空白悪化速度0.05・軍閥閾値0.3。</summary>
        public static AnarchyCostParams Default => new AnarchyCostParams(0.5f, 1.5f, 2f, 0.05f, 0.3f);
    }

    /// <summary>
    /// 無政府宙域の自然状態コストの純ロジック（#1459・LEVI-1・ホッブズ『リヴァイアサン』参考）。
    /// 主権（共通権力）が無い「自然状態」は『万人の万人に対する闘争』で、生活は『孤独で貧しく、険悪で
    /// 残忍で短い』。安全がないので生産も交易も発展しない＝崩壊した無政府宙域は高コストで、その無法は
    /// 隣接星系へ波及して不安定化させる。主権の空白は暴力を時間で深め、やがて強者（軍閥）が割拠する＝
    /// 主権の自然発生。そしてこの自然状態のコストの高さこそが、人々が安全のために主権へ服し秩序を再樹立する
    /// 動機を生む（リヴァイアサンの価値＝どんな専制も無秩序よりまし）。
    /// 内戦の経済崩壊（<see cref="CivilWarRules"/>＝二陣営の消耗戦の帰結）・辺境の自立（<see cref="FrontierRules"/>＝
    /// 距離が生む文化）とは別系統＝こちらは「主権そのものの不在」が生む万人の闘争のコスト（<see cref="AnarchyState"/>が
    /// 中核データ）。同EPIC LEVI の <see cref="SecurityDilemmaRules"/>（猜疑が招く軍拡の悪循環）が主権間の不信を扱うのに対し、
    /// ここは主権が消えた内側の無秩序を扱う＝分担を分ける。
    /// 全入力クランプ・乱数なし決定論・基準値非破壊（係数を返すのみ）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AnarchyCostRules
    {
        // --- 調整値（マジックナンバー禁止＝const に集約） ---
        public const float MaxCost = 1f;              // 自然状態コストの上限
        public const float MinLeviathanFloor = 0f;   // 主権価値の下限（無法が0なら秩序の追加価値も0）
        public const float DefaultCollapseThreshold = 0.6f; // 無政府崩壊と見なす既定の閾値

        /// <summary>
        /// 自然状態のコスト(0..maxCost)＝無法度×暴力水準を比重で混ぜる＝『孤独で貧しく、険悪で残忍で短い』生活の重さ。
        /// 主権が無いほど（無法）・闘争が烈しいほど（暴力）コストが上がる。これがあらゆる派生コストの源。
        /// </summary>
        public static float StateOfNatureCost(float lawlessness, float violence, AnarchyCostParams p)
        {
            float law = Mathf.Clamp01(lawlessness);
            float vio = Mathf.Clamp01(violence);
            float cost = p.lawlessnessWeight * law + (1f - p.lawlessnessWeight) * vio;
            return Mathf.Clamp(cost, 0f, MaxCost);
        }

        public static float StateOfNatureCost(float lawlessness, float violence)
            => StateOfNatureCost(lawlessness, violence, AnarchyCostParams.Default);

        /// <summary>
        /// 経済麻痺度(0..1)＝安全がないので生産・交易が止まる＝誰も投資しない（近代的産業が育たない）。
        /// 無法度に指数(>=1)を掛けて非線形に＝少しの無法は耐えても、無法が深まると産業が一気に止まる。
        /// </summary>
        public static float EconomicParalysis(float lawlessness, AnarchyCostParams p)
        {
            float law = Mathf.Clamp01(lawlessness);
            return Mathf.Clamp01(Mathf.Pow(law, 1f / p.paralysisExponent));
        }

        public static float EconomicParalysis(float lawlessness)
            => EconomicParalysis(lawlessness, AnarchyCostParams.Default);

        /// <summary>
        /// 隣接の不安定化(0..1)＝無政府宙域が隣の星系へ無法を波及させる。自然状態コスト×距離減衰＝近いほど強い
        /// （無法は伝染する）。distance(0..1)＝0で隣接（最大波及）・1で遠隔（ほぼ届かない）。
        /// </summary>
        public static float NeighborDestabilization(float stateOfNatureCost, float distance, AnarchyCostParams p)
        {
            float cost = Mathf.Clamp01(stateOfNatureCost);
            float d = Mathf.Clamp01(distance);
            float falloff = 1f - Mathf.Clamp01(d * p.destabilizationFalloff);
            return Mathf.Clamp01(cost * falloff);
        }

        public static float NeighborDestabilization(float stateOfNatureCost, float distance)
            => NeighborDestabilization(stateOfNatureCost, distance, AnarchyCostParams.Default);

        /// <summary>
        /// 主権の空白が深める暴力(0..1)＝リヴァイアサン不在の時間経過の悪化。現在の暴力に、無法度×外的脅威×
        /// 経過時間×悪化速度を加える＝放置された無政府はひとりでに険悪化していく（共通権力が無いと止まらない）。
        /// </summary>
        public static float SecurityVacuumTick(float lawlessness, float externalThreat, float dt, AnarchyCostParams p)
        {
            float law = Mathf.Clamp01(lawlessness);
            float threat = Mathf.Clamp01(externalThreat);
            float growth = law * threat * p.vacuumRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(law + growth);
        }

        public static float SecurityVacuumTick(float lawlessness, float externalThreat, float dt)
            => SecurityVacuumTick(lawlessness, externalThreat, dt, AnarchyCostParams.Default);

        /// <summary>
        /// 再統合（主権の再樹立）への動機(0..1)＝自然状態のコストが高いほど、秩序回復への動機が強い
        /// （人々は安全のために主権に服する）。回復力(0..1)＝再樹立を担う勢力の実力で底上げする
        /// ＝コストが高くても、まとめ上げる強者がいなければ動機は実を結ばない。
        /// </summary>
        public static float ReintegrationIncentive(float stateOfNatureCost, float restorationCapacity)
        {
            float cost = Mathf.Clamp01(stateOfNatureCost);
            float cap = Mathf.Clamp01(restorationCapacity);
            return Mathf.Clamp01(cost * cap);
        }

        /// <summary>
        /// リヴァイアサン（共通権力）がもたらす秩序の価値(0..maxCost)＝自然状態のコストそのもの。
        /// 主権が無ければ被るコストの大きさが、主権を立てる価値に等しい＝どんな専制も無秩序よりまし（ホッブズ）。
        /// </summary>
        public static float LeviathanValue(float stateOfNatureCost)
        {
            return Mathf.Clamp(Mathf.Clamp01(stateOfNatureCost), MinLeviathanFloor, MaxCost);
        }

        /// <summary>
        /// 軍閥の割拠(0..1)＝無政府の空白に強者が割って入る＝主権の自然発生。空白が <see cref="AnarchyCostParams.warlordThreshold"/>
        /// を超えてはじめて、強者の実力(0..1)に応じて軍閥が立つ＝空白が浅ければ強者も動かない。
        /// </summary>
        public static float WarlordEmergence(float securityVacuum, float strongmanPower, AnarchyCostParams p)
        {
            float vac = Mathf.Clamp01(securityVacuum);
            if (vac < p.warlordThreshold) return 0f;
            float strong = Mathf.Clamp01(strongmanPower);
            float opportunity = (vac - p.warlordThreshold) / Mathf.Max(0.0001f, 1f - p.warlordThreshold);
            return Mathf.Clamp01(opportunity * strong);
        }

        public static float WarlordEmergence(float securityVacuum, float strongmanPower)
            => WarlordEmergence(securityVacuum, strongmanPower, AnarchyCostParams.Default);

        /// <summary>
        /// 万人の闘争の無政府状態に陥った判定＝自然状態コストが閾値以上。生産も交易も止まり主権の再樹立が
        /// 切実に必要な水準（true＝崩壊）。
        /// </summary>
        public static bool IsAnarchicCollapse(float stateOfNatureCost, float threshold)
        {
            return Mathf.Clamp01(stateOfNatureCost) >= Mathf.Clamp01(threshold);
        }

        public static bool IsAnarchicCollapse(float stateOfNatureCost)
            => IsAnarchicCollapse(stateOfNatureCost, DefaultCollapseThreshold);
    }
}
