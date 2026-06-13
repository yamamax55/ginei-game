using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 中間権力の調整係数（マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// 中間団体（貴族・聖職者・高等法院・都市）の重みと、専制への緩衝・滑落の感度をまとめる。
    /// </summary>
    public readonly struct IntermediatePowerParams
    {
        /// <summary>貴族の重み（中間権力の総合強度への寄与・0..1）。</summary>
        public readonly float nobilityWeight;
        /// <summary>聖職者の重み（同上・0..1）。</summary>
        public readonly float clergyWeight;
        /// <summary>高等法院など法的中間権力の重み（同上・0..1）。</summary>
        public readonly float courtsWeight;
        /// <summary>都市・自治団体の重み（同上・0..1）。</summary>
        public readonly float townsWeight;
        /// <summary>中間権力が専制への滑落を緩衝する強さ（緩衝が厚いほど専制になりにくい・0..1）。</summary>
        public readonly float bufferScale;
        /// <summary>中央集権化の圧力が中間権力を侵食する速さ（per dt・君主が貴族/法院を潰す）。</summary>
        public readonly float erosionRate;
        /// <summary>中間権力に縛られた穏健な君主政と判定する緩衝の閾値（0..1）。</summary>
        public readonly float constraintThreshold;

        public IntermediatePowerParams(
            float nobilityWeight, float clergyWeight, float courtsWeight, float townsWeight,
            float bufferScale, float erosionRate, float constraintThreshold)
        {
            this.nobilityWeight = Mathf.Clamp01(nobilityWeight);
            this.clergyWeight = Mathf.Clamp01(clergyWeight);
            this.courtsWeight = Mathf.Clamp01(courtsWeight);
            this.townsWeight = Mathf.Clamp01(townsWeight);
            this.bufferScale = Mathf.Clamp01(bufferScale);
            this.erosionRate = Mathf.Max(0f, erosionRate);
            this.constraintThreshold = Mathf.Clamp01(constraintThreshold);
        }

        /// <summary>
        /// 既定（貴族0.35／聖職者0.2／法院0.3／都市0.15・緩衝係数0.9・侵食速度0.06・穏健閾値0.4）。
        /// 重みの合計＝1（加重平均）。貴族と高等法院が中間権力の主柱（モンテスキューの重視）。
        /// </summary>
        public static IntermediatePowerParams Default => new IntermediatePowerParams(
            nobilityWeight: 0.35f, clergyWeight: 0.2f, courtsWeight: 0.3f, townsWeight: 0.15f,
            bufferScale: 0.9f, erosionRate: 0.06f, constraintThreshold: 0.4f);
    }

    /// <summary>
    /// 中間権力（pouvoirs intermédiaires）の純ロジック（MONT-4 #1446・モンテスキュー『法の精神』参考）。
    /// 君主政において、君主と人民の間にある中間団体＝貴族・聖職者・高等法院・都市などが、君主の権力を緩衝し
    /// 専制への滑落を防ぐ＝これらが破壊されると君主政は専制に堕ちる（「中間権力なくして君主なし」）。
    /// 貴族・聖職者・法院・都市の中間団体が総合的な緩衝層を成し（<see cref="IntermediateStrength"/>）、
    /// その緩衝が君主の専制への滑落を防ぎ（<see cref="BufferAgainstDespotism"/>＝緩衝が厚いほど専制になりにくい）、
    /// 中間権力が弱く君主の野心が強いほど専制へ滑落するリスクが高まり（<see cref="DespotismSlideRisk"/>）、
    /// 中央集権化の圧力が中間権力を破壊していき（<see cref="IntermediateErosionTick"/>＝君主が貴族・法院を潰す）、
    /// 中間権力を失った君主政は専制と区別がつかなくなる（<see cref="MonarchyWithoutIntermediaries"/>＝モンテスキューの警告）。
    /// 高等法院など法的中間権力は恣意的支配を阻み（<see cref="LegalChannelStrength"/>＝法による緩衝）、
    /// 貴族の特権は逆説的に専制への防壁になり（<see cref="PrivilegeAsBulwark"/>＝特権層が王に抵抗する＝MagnaCarta的）、
    /// 緩衝に縛られた穏健な君主政を判定する（<see cref="IsConstrainedMonarchy"/>）。
    /// 分担：<see cref="AssociationRules"/>（トクヴィルの自発的市民結社＝民主社会の中間団体）／
    /// <see cref="CompoundRepublicRules"/>（連邦の二層主権＝マディソンの権力分立）／
    /// <see cref="MagnaCartaRules"/>（王権制約＝契約による個別の制約）／
    /// <see cref="PolityCorruptionRules"/>（君主政→専制への腐敗そのもの）とは別＝
    /// **こちらはモンテスキューの中間団体が君主政を専制から守る（中間権力の緩衝強度）**。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（実効値パターン）。
    /// 調整値は <see cref="IntermediatePowerParams"/>（既定 <see cref="IntermediatePowerParams.Default"/>）。
    /// </summary>
    public static class IntermediatePowerRules
    {
        /// <summary>中間権力の総合強度（既定 Params）。</summary>
        public static float IntermediateStrength(float nobility, float clergy, float courts, float towns)
            => IntermediateStrength(nobility, clergy, courts, towns, IntermediatePowerParams.Default);

        /// <summary>
        /// 中間権力の総合強度（0..1）＝貴族 nobility・聖職者 clergy・高等法院 courts・都市 towns(各0..1) の
        /// 重み付き平均（君主と人民の間の緩衝層）。各中間団体が分厚いほど総合強度は高い。
        /// 重みは <see cref="IntermediatePowerParams"/>（既定は貴族と法院が主柱）。
        /// </summary>
        public static float IntermediateStrength(float nobility, float clergy, float courts, float towns,
                                                 IntermediatePowerParams p)
        {
            float n = Mathf.Clamp01(nobility);
            float c = Mathf.Clamp01(clergy);
            float j = Mathf.Clamp01(courts);
            float t = Mathf.Clamp01(towns);
            float wSum = p.nobilityWeight + p.clergyWeight + p.courtsWeight + p.townsWeight;
            if (wSum <= 0f) return 0f;
            float weighted = n * p.nobilityWeight + c * p.clergyWeight + j * p.courtsWeight + t * p.townsWeight;
            return Mathf.Clamp01(weighted / wSum);
        }

        /// <summary>専制への緩衝（既定 Params）。</summary>
        public static float BufferAgainstDespotism(float intermediateStrength)
            => BufferAgainstDespotism(intermediateStrength, IntermediatePowerParams.Default);

        /// <summary>
        /// 専制への緩衝の強さ（0..1）＝中間権力の総合強度 × bufferScale。
        /// 中間権力が厚いほど君主の権力が緩衝され、専制への滑落が抑えられる
        /// ＝中間団体が君主と人民の間に立ち、君主の意思が直接人民を押し潰すのを防ぐ。
        /// 呼び出し側は専制化の進行・正統性の侵食からこの分を差し引く想定（基準非破壊）。
        /// </summary>
        public static float BufferAgainstDespotism(float intermediateStrength, IntermediatePowerParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(intermediateStrength) * p.bufferScale);
        }

        /// <summary>
        /// 専制への滑落リスク（0..1）＝中間権力が弱く（緩衝が薄く）君主の野心が強いほど高い
        /// ＝(1 − 緩衝) × 君主の野心 monarchAmbition(0..1)。
        /// 緩衝が厚ければ野心ある君主も中間団体に阻まれて専制に届かず（リスク低）、
        /// 緩衝が薄ければ野心がそのまま専制への滑落になる（中間権力なくして君主の野心を止める者なし）。
        /// </summary>
        public static float DespotismSlideRisk(float intermediateStrength, float monarchAmbition)
            => DespotismSlideRisk(intermediateStrength, monarchAmbition, IntermediatePowerParams.Default);

        /// <summary>専制への滑落リスク（Params 指定）。</summary>
        public static float DespotismSlideRisk(float intermediateStrength, float monarchAmbition,
                                               IntermediatePowerParams p)
        {
            float buffer = BufferAgainstDespotism(intermediateStrength, p);
            float ambition = Mathf.Clamp01(monarchAmbition);
            return Mathf.Clamp01((1f - buffer) * ambition);
        }

        /// <summary>中間権力の侵食（既定 Params）。</summary>
        public static float IntermediateErosionTick(float intermediateStrength, float centralizingPressure, float dt)
            => IntermediateErosionTick(intermediateStrength, centralizingPressure, dt, IntermediatePowerParams.Default);

        /// <summary>
        /// 中央集権化による中間権力の侵食の1tick後の総合強度（0..1）。
        /// 中央集権化の圧力 centralizingPressure(0..1) が中間団体を破壊していく
        /// ＝erosionRate × centralizingPressure × dt ずつ総合強度が低下（君主が貴族・高等法院を潰す）。
        /// 中間権力を削り続けると君主政はやがて専制へ滑落する（<see cref="MonarchyWithoutIntermediaries"/>）。
        /// 基準値は変えず、新しい総合強度を返す（実効値パターン）。
        /// </summary>
        public static float IntermediateErosionTick(float intermediateStrength, float centralizingPressure, float dt,
                                                    IntermediatePowerParams p)
        {
            float s = Mathf.Clamp01(intermediateStrength);
            if (dt <= 0f) return s;
            float delta = p.erosionRate * Mathf.Clamp01(centralizingPressure) * dt;
            return Mathf.Clamp01(s - delta);
        }

        /// <summary>
        /// 中間権力を失った君主政が専制と区別がつかなくなったか＝中間権力の総合強度が threshold 未満で true
        /// （モンテスキューの警告「中間権力なくして君主なし」＝中間団体を失った君主政はもはや専制）。
        /// 緩衝層が消えれば君主の意思を阻む者がいなくなり、君主政の形式は残っても実質は専制になる。
        /// </summary>
        public static bool MonarchyWithoutIntermediaries(float intermediateStrength, float threshold)
        {
            return Mathf.Clamp01(intermediateStrength) < Mathf.Clamp01(threshold);
        }

        /// <summary>
        /// 法的中間権力の強さ（0..1）＝高等法院など courts(0..1) × 法の伝統 lawTradition(0..1)。
        /// 高等法院（パルルマン）が王令を登記・建言する慣行や法の伝統が根づくほど、法的中間権力が
        /// 君主の恣意的支配を阻む（法による緩衝＝モンテスキューが重視した「法の寄託者」）。
        /// 法院があっても法の伝統が無ければ機能せず、伝統があっても法院が無ければ受け皿が無い（積）。
        /// </summary>
        public static float LegalChannelStrength(float courts, float lawTradition)
        {
            return Mathf.Clamp01(courts) * Mathf.Clamp01(lawTradition);
        }

        /// <summary>
        /// 特権の防壁（0..1）＝貴族の特権 nobilityPrivilege(0..1) が逆説的に専制への防壁になる強さ。
        /// 貴族の特権は不平等そのものだが、特権を守ろうとする貴族層が王権の無制限な拡大に抵抗する
        /// ＝特権が中間権力の自律の源になり、王の専制への滑落を阻む（MagnaCarta 的＝特権層が王に抵抗する）。
        /// 特権が強いほど防壁は厚いが、特権ゼロなら抵抗の足場もない（線形）。
        /// </summary>
        public static float PrivilegeAsBulwark(float nobilityPrivilege)
        {
            return Mathf.Clamp01(nobilityPrivilege);
        }

        /// <summary>
        /// 穏健な君主政の判定＝中間権力の緩衝に縛られた穏健な君主政か。
        /// 専制への緩衝 bufferAgainstDespotism が constraintThreshold 以上で true
        /// （中間団体が君主の権力を十分に縛り、専制に堕ちていない穏健な君主政）。
        /// </summary>
        public static bool IsConstrainedMonarchy(float bufferAgainstDespotism, float threshold)
        {
            return Mathf.Clamp01(bufferAgainstDespotism) >= Mathf.Clamp01(threshold);
        }
    }
}
