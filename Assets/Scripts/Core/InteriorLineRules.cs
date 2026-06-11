using UnityEngine;

namespace Ginei
{
    /// <summary>内線作戦の調整係数（ジョミニ『戦争概論』JOM-1 #1345）。</summary>
    public readonly struct InteriorLineParams
    {
        /// <summary>前線が1つ増えるごとに集中戦力を薄める割合（0..1、過多は破綻）。</summary>
        public readonly float dispersionPerFront;
        /// <summary>過伸張が始まる前線数の目安（これを超えると内線でも手が回らない）。</summary>
        public readonly float overstretchFrontThreshold;
        /// <summary>過伸張ペナルティの効き（大きいほど前線過多で急速に破綻）。</summary>
        public readonly float overstretchSteepness;
        /// <summary>各個撃破の成立しやすさを決める転用速度の基準スケール。</summary>
        public readonly float redeployScale;

        public InteriorLineParams(float dispersionPerFront, float overstretchFrontThreshold,
            float overstretchSteepness, float redeployScale)
        {
            this.dispersionPerFront = Mathf.Clamp01(dispersionPerFront);
            this.overstretchFrontThreshold = Mathf.Max(1f, overstretchFrontThreshold);
            this.overstretchSteepness = Mathf.Clamp01(overstretchSteepness);
            this.redeployScale = Mathf.Max(0.01f, redeployScale);
        }

        /// <summary>既定＝前線ごと分散0.15・過伸張は3前線から・急峻さ0.5・転用スケール10。</summary>
        public static InteriorLineParams Default => new InteriorLineParams(0.15f, 3f, 0.5f, 10f);
    }

    /// <summary>
    /// 内線作戦の優位の純ロジック（ジョミニ『戦争概論』JOM-1 #1345）。内線（interior lines）優位＝中央に
    /// 位置する側は、外縁に分散した複数の敵前線へ各個に<b>短い経路</b>で兵力を転用できる。各個撃破＝外線の敵が
    /// 連携する前に、内側から一つずつ叩く。中央配置は敵が多方向ほど活きるが、前線が多すぎると内線側も手が
    /// 回らず過伸張で破綻する。
    /// <para>
    /// 分担：<see cref="LogisticsRules"/>（版図の一体化度＝所有星系の連結成分）とは別＝こちらは<b>中央配置から
    /// の各個撃破</b>の数値モデル。<see cref="ChokepointValueRules"/>（要衝の希少性価値）とは別。同EPIC JOM の
    /// 決勝点（DecisivePointRules）とは別＝こちらは<b>内線の機動優位</b>に特化。<see cref="GalaxyMap"/> 等の盤面型は
    /// 参照せず、距離配列など plain 引数で評価。乱数なし・決定論。実効値パターン（基準非破壊）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </para>
    /// </summary>
    public static class InteriorLineRules
    {
        /// <summary>配列の総和（null/空は0・負距離は0にクランプ）。</summary>
        private static float SumNonNegative(float[] values)
        {
            if (values == null || values.Length == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                sum += Mathf.Max(0f, values[i]);
            }
            return sum;
        }

        /// <summary>
        /// 内線優位（0..1）＝外側経路の総コストに対して内側経路の総コストがどれだけ短いか。
        /// 内側（中央→各前線）の総距離が外側（外縁を回り込む）より短いほど1へ近づく。
        /// 配列は null/空安全。内側0なら最大優位、外側0なら優位なし。
        /// </summary>
        public static float InteriorAdvantage(float[] centralToFrontDistances,
            float[] exteriorToFrontDistances, InteriorLineParams p)
        {
            float inner = SumNonNegative(centralToFrontDistances);
            float outer = SumNonNegative(exteriorToFrontDistances);
            if (outer <= 0f) return 0f;        // 外線がそもそも動かない＝内線の利点なし
            if (inner <= 0f) return 1f;        // 内側が即時転用＝最大優位
            // 内側が外側より短いほど優位（inner==outer で 0、inner→0 で 1、inner>outer で 0）
            float ratio = 1f - inner / outer;
            return Mathf.Clamp01(ratio);
        }

        public static float InteriorAdvantage(float[] centralToFrontDistances, float[] exteriorToFrontDistances)
            => InteriorAdvantage(centralToFrontDistances, exteriorToFrontDistances, InteriorLineParams.Default);

        /// <summary>
        /// 中央から前線への兵力転用の速さ（0..1）。内側距離が短く機動が高いほど速い。
        /// 速さ＝機動(0..1) × redeployScale /（redeployScale + 内側距離）。距離負はクランプ。
        /// </summary>
        public static float RedeploymentSpeed(float interiorDistance, float mobility, InteriorLineParams p)
        {
            float d = Mathf.Max(0f, interiorDistance);
            float m = Mathf.Clamp01(mobility);
            float distanceFactor = p.redeployScale / (p.redeployScale + d); // 0..1、距離0で1
            return Mathf.Clamp01(m * distanceFactor);
        }

        public static float RedeploymentSpeed(float interiorDistance, float mobility)
            => RedeploymentSpeed(interiorDistance, mobility, InteriorLineParams.Default);

        /// <summary>
        /// 各個撃破できる確率（0..1）＝転用速度(0..1) が敵の連携に要する時間にどれだけ間に合うか。
        /// 敵の連携が遅い（enemyCoordinationTime 大）ほど、また転用が速いほど高い。
        /// coordinationTime≤0（即連携）なら各個撃破は不可能＝0。
        /// </summary>
        public static float DefeatInDetailChance(float redeploymentSpeed, float enemyCoordinationTime)
        {
            float t = Mathf.Max(0f, enemyCoordinationTime);
            if (t <= 0f) return 0f; // 敵が即連携＝隙がない
            float speed = Mathf.Clamp01(redeploymentSpeed);
            // 連携時間が長いほど猶予が大きい：t/(t+1) で 0..1 へ写し、転用速度で重み付け
            float window = t / (t + 1f);
            return Mathf.Clamp01(speed * window);
        }

        /// <summary>
        /// 内線側が一前線へ集中できる戦力比（≥0）＝自軍戦力を前線数で割り、前線ごとの分散ペナルティを掛ける。
        /// 前線が多すぎると一前線への集中戦力が薄まる。frontCount≤1 は分散なし。ownForce 負は0。
        /// </summary>
        public static float ConcentrationRatio(float ownForce, int frontCount, InteriorLineParams p)
        {
            float force = Mathf.Max(0f, ownForce);
            int fronts = Mathf.Max(1, frontCount);
            float perFront = force / fronts;
            // 追加の前線ぶん（fronts-1）だけ分散ペナルティが効く（0..1で下限）
            float dispersion = Mathf.Clamp01(1f - p.dispersionPerFront * (fronts - 1));
            return perFront * dispersion;
        }

        public static float ConcentrationRatio(float ownForce, int frontCount)
            => ConcentrationRatio(ownForce, frontCount, InteriorLineParams.Default);

        /// <summary>
        /// 中央配置の価値（0..1）＝内線優位が、敵が多方向（frontCount）であるほど活きる。
        /// ただし過伸張（Overstretch）に応じて割り引く＝前線過多は中央配置でも破綻する。
        /// frontCount≤1 は多方向の利点が無く価値0。
        /// </summary>
        public static float CentralPositionValue(float interiorAdvantage, int frontCount,
            float ownForce, InteriorLineParams p)
        {
            int fronts = Mathf.Max(1, frontCount);
            if (fronts <= 1) return 0f; // 敵が1方向なら中央配置の妙味なし
            float adv = Mathf.Clamp01(interiorAdvantage);
            // 多方向ボーナス：前線が増えるほど内線の利点が出る（飽和）
            float multiFront = (fronts - 1f) / (float)fronts; // 2前線=0.5, 3=0.667...
            float stretch = Overstretch(fronts, ownForce, p);
            return Mathf.Clamp01(adv * multiFront * (1f - stretch));
        }

        public static float CentralPositionValue(float interiorAdvantage, int frontCount, float ownForce)
            => CentralPositionValue(interiorAdvantage, frontCount, ownForce, InteriorLineParams.Default);

        /// <summary>
        /// 過伸張（0..1）＝前線が多すぎて内線側も手が回らなくなる度合い。前線数が
        /// overstretchFrontThreshold を超えた超過分が、自軍戦力で割った負担として効く。
        /// 戦力が大きいほど過伸張しにくい。frontCount が閾値以下なら0。
        /// </summary>
        public static float Overstretch(int frontCount, float ownForce, InteriorLineParams p)
        {
            int fronts = Mathf.Max(1, frontCount);
            float excess = fronts - p.overstretchFrontThreshold;
            if (excess <= 0f) return 0f;
            float force = Mathf.Max(1f, ownForce); // 戦力1未満は1扱い（ゼロ割回避）
            // 超過前線 × 急峻さ を、戦力規模で正規化（戦力10で1前線ぶんの負担＝0.1×steepness）
            float burden = excess * p.overstretchSteepness * (10f / force);
            return Mathf.Clamp01(burden);
        }

        public static float Overstretch(int frontCount, float ownForce)
            => Overstretch(frontCount, ownForce, InteriorLineParams.Default);

        /// <summary>
        /// 外線に包囲されるリスク（0..1）＝内線優位が崩れるほど高い。内線で各個撃破できないと、
        /// 外縁の敵が連携して中央を包囲する。advantage=1 でリスク0、advantage=0 でリスク最大。
        /// </summary>
        public static float ExteriorEncirclementRisk(float interiorAdvantage)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(interiorAdvantage));
        }

        /// <summary>内線優位が閾値超なら内線作戦有利＝true（threshold は 0..1 にクランプ）。</summary>
        public static bool IsInteriorLineFavorable(float interiorAdvantage, float threshold)
        {
            return Mathf.Clamp01(interiorAdvantage) > Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値0.5で内線作戦有利か判定。</summary>
        public static bool IsInteriorLineFavorable(float interiorAdvantage)
            => IsInteriorLineFavorable(interiorAdvantage, 0.5f);
    }
}
