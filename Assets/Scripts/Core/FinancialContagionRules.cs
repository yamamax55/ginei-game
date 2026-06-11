using UnityEngine;

namespace Ginei
{
    /// <summary>金融伝染の調整係数（#1615）。</summary>
    public readonly struct FinancialContagionParams
    {
        /// <summary>エクスポージャー（市場間のつながりの太さ）が伝染力に効く重み＝太いほど速く伝わる。</summary>
        public readonly float exposureWeight;
        /// <summary>防火壁1.0ぶんがストレス流入を減衰させる最大率（1.0＝完全遮断＝連鎖を断つ）。</summary>
        public readonly float firewallDamping;
        /// <summary>流動性供給が防火壁に寄与する重み（最後の貸し手の即効性）。</summary>
        public readonly float liquidityWeight;
        /// <summary>資本規制が防火壁に寄与する重み（事前の制度的歯止め）。</summary>
        public readonly float capitalControlWeight;
        /// <summary>この防火壁強度を超えると封じ込め成功（連鎖を断ち切る閾値）。</summary>
        public readonly float containmentThreshold;

        public FinancialContagionParams(float exposureWeight, float firewallDamping,
            float liquidityWeight, float capitalControlWeight, float containmentThreshold)
        {
            this.exposureWeight = Mathf.Clamp01(exposureWeight);
            this.firewallDamping = Mathf.Clamp01(firewallDamping);
            this.liquidityWeight = Mathf.Clamp01(liquidityWeight);
            this.capitalControlWeight = Mathf.Clamp01(capitalControlWeight);
            this.containmentThreshold = Mathf.Clamp01(containmentThreshold);
        }

        /// <summary>既定＝エクスポージャー重み1.0／防火壁減衰1.0／流動性0.6・資本規制0.4／封じ込め閾値0.7。</summary>
        public static FinancialContagionParams Default
            => new FinancialContagionParams(1f, 1f, 0.6f, 0.4f, 0.7f);
    }

    /// <summary>
    /// 金融伝染の純ロジック（KNDB-3 #1615・唯一の窓口）。一つの市場（星系）の取付け・暴落が
    /// <b>エクスポージャー（つながり）を伝って隣の市場へ飛び火する</b>（リーマン型）＝つながりが太いほど速く伝わり、
    /// <b>防火壁（流動性供給＋資本規制）だけが連鎖を断つ</b>（<see cref="FirewallEffectiveness"/>／<see cref="ContainmentThreshold"/>）。
    /// 平時は分散がリスクを下げるが、危機が深まると<b>相関が1へ崩壊</b>して分散が効かなくなる
    /// （分散の幻想＝みな同時に落ちる・<see cref="CorrelationBreakdown"/>／<see cref="EffectiveDiversification"/>）。
    /// <see cref="BankRules"/>（単体銀行の取付け・信用創造）とは別＝こちらは<b>市場間の面的波及</b>。
    /// <see cref="ChainFragilityRules"/>（生産網の物理的な遮断伝播）とも別＝こちらは<b>金融ショックの伝染</b>。
    /// 防火壁の供給そのもの（最後の貸し手の発動可否）は LenderOfLastResortRules（同EPIC）が扱う＝本ルールは
    /// 与えられた防火壁強度で伝染が断たれるかを解く。乱数は roll 引数で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FinancialContagionRules
    {
        /// <summary>
        /// 伝染力（0..1）。相手とのエクスポージャー exposure（0..1＝つながりの太さ）×震源の深刻度 sourceSeverity（0..1）。
        /// つながりが太く震源が深いほど強く伝わる＝どちらか0なら伝わらない（積）。エクスポージャー重みで効きを調整。
        /// </summary>
        public static float TransmissionStrength(float exposure, float sourceSeverity, FinancialContagionParams p)
        {
            float e = Mathf.Clamp01(exposure);
            float s = Mathf.Clamp01(sourceSeverity);
            // つながりの太さを重みで効かせる（exposureWeight=1で素通し＝つながりがそのまま伝染力）。
            float linked = Mathf.Lerp(s, e * s, p.exposureWeight);
            return Mathf.Clamp01(linked);
        }

        public static float TransmissionStrength(float exposure, float sourceSeverity)
            => TransmissionStrength(exposure, sourceSeverity, FinancialContagionParams.Default);

        /// <summary>
        /// 防火壁の強さ（0..1）。流動性供給 liquiditySupport（0..1＝即効の現金注入）と
        /// 資本規制 capitalControl（0..1＝事前の制度的歯止め）を重み合成＝両輪で連鎖を断つ。
        /// どちらも0なら防火壁0（伝染は減衰せず素通し）。
        /// </summary>
        public static float FirewallEffectiveness(float liquiditySupport, float capitalControl, FinancialContagionParams p)
        {
            float liq = Mathf.Clamp01(liquiditySupport);
            float cap = Mathf.Clamp01(capitalControl);
            float total = p.liquidityWeight + p.capitalControlWeight;
            if (total <= 0f) return 0f;
            return Mathf.Clamp01((liq * p.liquidityWeight + cap * p.capitalControlWeight) / total);
        }

        public static float FirewallEffectiveness(float liquiditySupport, float capitalControl)
            => FirewallEffectiveness(liquiditySupport, capitalControl, FinancialContagionParams.Default);

        /// <summary>
        /// 伝染の1ステップ＝隣のストレスがエクスポージャー経由で流入し、防火壁で減衰した自市場ストレスへ加算する。
        /// 流入量＝(neighborStress−localStress の正の差)×伝染力(エクスポージャー)×(1−防火壁減衰)×dt
        /// ＝隣がより高ストレスなときだけ太いつながりを伝って流れ込み、<b>防火壁がそのぶんを減衰</b>（連鎖を断つ）。
        /// 防火壁が <see cref="FinancialContagionParams.firewallDamping"/> ぶん効くと流入が止まる。戻り値は新しい自市場ストレス（0..1）。
        /// </summary>
        public static float ContagionTick(float localStress, float neighborStress, float exposure, float firewall, float dt, FinancialContagionParams p)
        {
            float local = Mathf.Clamp01(localStress);
            if (dt <= 0f) return local;
            float neighbor = Mathf.Clamp01(neighborStress);
            float gap = Mathf.Max(0f, neighbor - local);             // 隣がより高ストレスなぶんだけ流れ込む
            float link = Mathf.Clamp01(exposure);                    // つながりが太いほど速く伝わる
            float fw = Mathf.Clamp01(firewall);
            float damp = 1f - fw * p.firewallDamping;                // 防火壁が流入を減衰＝連鎖を断つ
            float inflow = gap * link * damp * dt;
            return Mathf.Clamp01(local + inflow);
        }

        public static float ContagionTick(float localStress, float neighborStress, float exposure, float firewall, float dt)
            => ContagionTick(localStress, neighborStress, exposure, firewall, dt, FinancialContagionParams.Default);

        /// <summary>
        /// 相関の崩壊（0..1）。系全体のストレス systemicStress（0..1）が深いほど市場間の相関が1へ崩壊する
        /// ＝平時は資産ごとにバラバラに動く（低相関）が、危機では<b>みな同時に落ちる</b>（相関→1＝分散の幻想）。
        /// systemicStress=0で相関0（完全に分散が効く）・systemicStress=1で相関1（全てが連動）。
        /// </summary>
        public static float CorrelationBreakdown(float systemicStress)
        {
            float s = Mathf.Clamp01(systemicStress);
            // 危機が深いほど非線形に相関が立ち上がる（平時はほぼ無相関・危機で急に1へ）。
            return Mathf.Clamp01(s * s);
        }

        /// <summary>
        /// 実効分散効果（0..1）。名目の分散 nominalDiversification（0..1＝銘柄/市場を散らした度合い）は、
        /// 危機で相関が崩壊する（<see cref="CorrelationBreakdown"/>）ぶんだけ効かなくなる＝
        /// <b>平時は分散がリスクを下げるが、危機では実効分散が0へ消える</b>（相関崩壊で全てが同時に落ちる）。
        /// 実効分散＝名目分散×(1−相関崩壊)。
        /// </summary>
        public static float EffectiveDiversification(float nominalDiversification, float systemicStress)
        {
            float nominal = Mathf.Clamp01(nominalDiversification);
            float corr = CorrelationBreakdown(systemicStress);
            return Mathf.Clamp01(nominal * (1f - corr));
        }

        /// <summary>
        /// 取付けの決定論判定。ストレス stress（0..1）が高く信認 confidence（0..1）が低いほど発生確率が上がり、
        /// roll（0..1）がその確率を下回れば取付け（true）。stress×(1−confidence) を確率とする。
        /// </summary>
        public static bool BankRunProbability(float stress, float confidence, float roll)
        {
            float s = Mathf.Clamp01(stress);
            float conf = Mathf.Clamp01(confidence);
            float prob = Mathf.Clamp01(s * (1f - conf));
            return Mathf.Clamp01(roll) < prob;
        }

        /// <summary>
        /// 系全体の崩壊リスク（0..1）。平均ストレス avgStress（0..1）×平均接続 avgExposure（0..1）
        /// ＝<b>つながりが太いほどショックが系全体へ広がる</b>（高ストレス×高接続で系が崩壊）。
        /// どちらか0なら崩壊しない（分散しきった系・あるいはストレス無し）。
        /// </summary>
        public static float SystemicCollapseRisk(float avgStress, float avgExposure)
        {
            float s = Mathf.Clamp01(avgStress);
            float e = Mathf.Clamp01(avgExposure);
            return Mathf.Clamp01(s * e);
        }

        /// <summary>
        /// 封じ込め成功の判定。防火壁強度 firewall（0..1）が <see cref="FinancialContagionParams.containmentThreshold"/>
        /// を超えれば連鎖を断ち切れる（true）＝<b>防火壁だけが連鎖を断つ</b>の閾値版。
        /// </summary>
        public static bool ContainmentThreshold(float firewall, FinancialContagionParams p)
            => Mathf.Clamp01(firewall) >= p.containmentThreshold;

        public static bool ContainmentThreshold(float firewall)
            => ContainmentThreshold(firewall, FinancialContagionParams.Default);
    }
}
