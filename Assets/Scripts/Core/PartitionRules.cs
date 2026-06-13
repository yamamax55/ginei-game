using UnityEngine;

namespace Ginei
{
    /// <summary>戦後分割の調整係数。</summary>
    public readonly struct PartitionParams
    {
        /// <summary>取り分の不満が増幅する係数（公正ラインからの不足に掛かる）。</summary>
        public readonly float grievanceScale;
        /// <summary>分割線の恣意性で「机上で引いた度合い」が効く重み（0..1・残りは民族非整合）。</summary>
        public readonly float mapDrawnWeight;
        /// <summary>将来紛争リスクの上限（恣意的な線×抑圧された民族意識が最大のとき）。</summary>
        public readonly float maxConflictRisk;
        /// <summary>占領統治コストの係数（取り分の規模×敵対住民に掛かる）。</summary>
        public readonly float occupationCostScale;
        /// <summary>勝者連合の分裂の種が育つ係数（不満の集計に掛かる）。</summary>
        public readonly float rivalrySeedScale;

        public PartitionParams(float grievanceScale, float mapDrawnWeight, float maxConflictRisk,
            float occupationCostScale, float rivalrySeedScale)
        {
            this.grievanceScale = Mathf.Max(0f, grievanceScale);
            this.mapDrawnWeight = Mathf.Clamp01(mapDrawnWeight);
            this.maxConflictRisk = Mathf.Clamp01(maxConflictRisk);
            this.occupationCostScale = Mathf.Max(0f, occupationCostScale);
            this.rivalrySeedScale = Mathf.Max(0f, rivalrySeedScale);
        }

        /// <summary>既定＝不満係数1.5・机上重み0.6・紛争リスク上限1.0・占領コスト係数1.2・分裂種係数0.5。</summary>
        public static PartitionParams Default => new PartitionParams(1.5f, 0.6f, 1f, 1.2f, 0.5f);
    }

    /// <summary>
    /// 戦後分割の純ロジック（分配の力学）。敗戦国の領土を勝者間でどう分けるか＝貢献に見合わない
    /// 取り分は勝者連合内の次の対立軸を生み（ヴェルサイユの戦勝国間対立）、住民を無視して机上で引いた
    /// 分割線は将来の紛争の火種になる（アフリカ/中東型）。取りすぎた取り分は敵対住民の消化不良＝重い
    /// 占領負担になる。「分割は戦争を終わらせず、次の戦争の地図を引く」を式に出す。
    /// 条約一般（<see cref="TreatyRules"/>＝条約の効果/履行/破棄）・賠償（<see cref="ReparationsRules"/>
    /// ＝敗者から金を取り立てる長期帰結）とは別系統＝こちらは勝者間の領土分配そのものの力学を扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PartitionRules
    {
        /// <summary>
        /// 戦争貢献度に応じた取り分の正当ライン（0..1）。多く血を流した勝者ほど大きく取るのが
        /// 「公正」とされる基準＝そのまま貢献度を返す（取り分の正当性の物差し）。
        /// </summary>
        public static float FairShare(float victorContribution)
        {
            return Mathf.Clamp01(victorContribution);
        }

        /// <summary>
        /// 取り分の不満（0..1）＝公正ラインに足りない分×係数。貢献に見合う配分なら不満ゼロ、
        /// 貢献より少ない取り分しかもらえなかった勝者の恨みが勝者間の次の火種になる
        /// （過剰な取り分＝不満ゼロ＝もらいすぎは本人は不満を言わない）。
        /// </summary>
        public static float ShareGrievance(float actualShare, float fairShare, PartitionParams p)
        {
            float shortfall = Mathf.Max(0f, Mathf.Clamp01(fairShare) - Mathf.Clamp01(actualShare));
            return Mathf.Clamp01(shortfall * p.grievanceScale);
        }

        public static float ShareGrievance(float actualShare, float fairShare)
            => ShareGrievance(actualShare, fairShare, PartitionParams.Default);

        /// <summary>
        /// 分割線の恣意性（0..1）。住民の民族的まとまり(ethnicCoherence)を無視して机上で引いた度合い
        /// (drawnByMapNotPeople)が高いほど恣意的＝（机上度×重み ＋ 民族非整合×残り重み）。
        /// 住民を無視して引いた直線国境は将来の紛争の火種（アフリカ/中東型）。
        /// </summary>
        public static float PartitionLineArbitrariness(float ethnicCoherence, float drawnByMapNotPeople, PartitionParams p)
        {
            float mapDrawn = Mathf.Clamp01(drawnByMapNotPeople);
            float incoherence = 1f - Mathf.Clamp01(ethnicCoherence);
            return Mathf.Clamp01(mapDrawn * p.mapDrawnWeight + incoherence * (1f - p.mapDrawnWeight));
        }

        public static float PartitionLineArbitrariness(float ethnicCoherence, float drawnByMapNotPeople)
            => PartitionLineArbitrariness(ethnicCoherence, drawnByMapNotPeople, PartitionParams.Default);

        /// <summary>
        /// 将来の紛争リスク（0..1）＝分割線の恣意性×抑圧された民族意識×上限。両者の積＝どちらかが
        /// ゼロなら火種にならない（恣意的でも民族意識が眠っていれば、また民族意識が高くても線が
        /// 住民に沿っていれば、すぐには燃えない）。両方そろって初めて次の戦争の地図になる。
        /// </summary>
        public static float FutureConflictRisk(float lineArbitrariness, float suppressedNationalism, PartitionParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(lineArbitrariness) * Mathf.Clamp01(suppressedNationalism) * p.maxConflictRisk);
        }

        public static float FutureConflictRisk(float lineArbitrariness, float suppressedNationalism)
            => FutureConflictRisk(lineArbitrariness, suppressedNationalism, PartitionParams.Default);

        /// <summary>
        /// 占領統治のコスト（≥0）＝取り分の規模×敵対住民比×係数。大きな取り分は重い負担＝
        /// 取りすぎは消化不良（敵対的な住民を多く抱え込むほど統治コストが膨らむ）。
        /// </summary>
        public static float OccupationCost(float shareSize, float hostilePopulation, PartitionParams p)
        {
            return Mathf.Max(0f, Mathf.Clamp01(shareSize) * Mathf.Clamp01(hostilePopulation) * p.occupationCostScale);
        }

        public static float OccupationCost(float shareSize, float hostilePopulation)
            => OccupationCost(shareSize, hostilePopulation, PartitionParams.Default);

        /// <summary>
        /// 勝者連合の分裂の種（0..1）。各勝者の取り分の不満を集計し（最大の不満を主軸に総量で底上げ）、
        /// 分配の不満が勝者同士の次の対立軸になる。誰も不満がなければ種ゼロ＝連合は割れない。
        /// </summary>
        public static float VictorRivalrySeed(float[] grievances, PartitionParams p)
        {
            if (grievances == null || grievances.Length == 0) return 0f;
            float max = 0f;
            float sum = 0f;
            for (int i = 0; i < grievances.Length; i++)
            {
                float g = Mathf.Clamp01(grievances[i]);
                if (g > max) max = g;
                sum += g;
            }
            // 最大の不満が主軸＝最も恨む勝者が分裂を主導、残りの不満の総量×係数で底上げ（多数が不満なほど割れやすい）。
            return Mathf.Clamp01(max + (sum - max) * p.rivalrySeedScale);
        }

        public static float VictorRivalrySeed(float[] grievances)
            => VictorRivalrySeed(grievances, PartitionParams.Default);
    }
}
