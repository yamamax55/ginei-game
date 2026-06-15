using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略重心の種別（クラウゼヴィッツ＝重心は状況による）。主力軍・首都・同盟・世論・補給拠点など、
    /// 状況に応じて「敵の力の源泉が集中する一点」がどれになるかが変わる。
    /// </summary>
    public enum CoGType
    {
        主力艦隊,
        首都星系,
        同盟関係,
        民衆支持,
        補給拠点,
    }

    /// <summary>重心分析の調整係数。ctor で全値をクランプする。</summary>
    public readonly struct CenterOfGravityParams
    {
        /// <summary>重心の重みにおける「強さ（力の大きさ）」の寄与。</summary>
        public readonly float strengthWeight;
        /// <summary>重心の重みにおける「結節（他要素との繋がり＝崩れの波及元）」の寄与。</summary>
        public readonly float connectivityWeight;
        /// <summary>重心とみなす重みの閾値（0..1）。これ以上に力が集中していれば重心と呼ぶ。</summary>
        public readonly float gravityThreshold;
        /// <summary>崩壊波及の非線形度（臨界依存度の冪指数・1以上）。依存が深いほど崩れが加速する。</summary>
        public readonly float collapseExponent;
        /// <summary>間接アプローチへ切り替える正面防御の閾値（0..1）。正面がこれ以上固ければ迂回が有利。</summary>
        public readonly float indirectThreshold;

        public CenterOfGravityParams(float strengthWeight, float connectivityWeight,
            float gravityThreshold, float collapseExponent, float indirectThreshold)
        {
            this.strengthWeight = Mathf.Max(0f, strengthWeight);
            this.connectivityWeight = Mathf.Max(0f, connectivityWeight);
            this.gravityThreshold = Mathf.Clamp01(gravityThreshold);
            this.collapseExponent = Mathf.Max(1f, collapseExponent);
            this.indirectThreshold = Mathf.Clamp01(indirectThreshold);
        }

        /// <summary>既定＝強さ重み0.6・結節重み0.4／重心閾値0.6／崩壊冪1.5／間接アプローチ閾値0.6。</summary>
        public static CenterOfGravityParams Default =>
            new CenterOfGravityParams(0.6f, 0.4f, 0.6f, 1.5f, 0.6f);
    }

    /// <summary>
    /// 重心分析（Schwerpunkt）の純ロジック＝クラウゼヴィッツ『戦争論』（CLZ-4・#1136）。重心とは
    /// <b>敵の力の源泉が集中する一点（主力軍・首都・同盟・世論・補給拠点など状況による）で、そこを叩けば
    /// 全体が崩れる</b>＝あらゆる打撃をこの一点に集中せよ。諸要素の重要度（強さ×他要素との結節）から
    /// 戦略重心を同定し、AIの攻撃優先度＝<b>重要×脆弱×到達可能</b>（叩ける重心を優先）を導く。
    /// 正面が固ければ間接アプローチ（リデルハート＝堅い正面を避け弱点へ）へ切り替え、戦力を一点に集中させる。
    /// <see cref="ChokepointValueRules"/>（回廊の要衝価値＝地理的な唯一の道・迂回路の有無）とは別＝
    /// こちらは力の源泉が集中する叩くべき一点の同定（地理に限らない）。<see cref="LogisticsRules"/>
    /// （所有星系の連結＝版図の一体化）とも別＝こちらは「どこを叩けば崩れるか」の重心。
    /// <see cref="BalanceOfPowerRules"/>（多極の同盟均衡＝システム圧力）とも別だが、同盟関係そのものを
    /// 一個の重心（CoGType.同盟関係）として扱える＝同盟を断てば敵が崩れるなら同盟が重心。
    /// 倍率・優先度は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CenterOfGravityRules
    {
        /// <summary>
        /// 重心の重み（0..1）＝その要素が力の源泉としてどれだけ集中しているか。strength（力の大きさ0..1）と
        /// connectivity（他要素との結節0..1＝そこが崩れると周りも崩れる繋がりの密度）を重み付き平均する
        /// （重み合計で正規化、合計0なら0）。強く・かつ多くの力が集まる結節ほど重心の重みが大きい。
        /// </summary>
        public static float GravityWeight(CoGType type, float strength, float connectivity, CenterOfGravityParams p)
        {
            // type は呼び出し側の重心候補ラベル。重みは強さと結節の合成で評価する（種別非依存の式）。
            float s = Mathf.Clamp01(strength);
            float c = Mathf.Clamp01(connectivity);
            float weightSum = p.strengthWeight + p.connectivityWeight;
            if (weightSum <= 0f) return 0f;
            return Mathf.Clamp01((p.strengthWeight * s + p.connectivityWeight * c) / weightSum);
        }

        /// <summary>既定係数での重心の重み（0..1）。</summary>
        public static float GravityWeight(CoGType type, float strength, float connectivity)
            => GravityWeight(type, strength, connectivity, CenterOfGravityParams.Default);

        /// <summary>重心と呼べるか＝重みが閾値以上（力がそれだけ一点に集中している）。</summary>
        public static bool IsCenterOfGravity(float gravityWeight, float threshold)
        {
            return Mathf.Clamp01(gravityWeight) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="CenterOfGravityParams.gravityThreshold"/>）での重心判定。</summary>
        public static bool IsCenterOfGravity(float gravityWeight)
            => IsCenterOfGravity(gravityWeight, CenterOfGravityParams.Default.gravityThreshold);

        /// <summary>
        /// 重心喪失の波及（0..1）＝そこを失うと全体がどれだけ崩れるか。重心の重み × 臨界依存度を冪で
        /// 非線形に効かせる＝全体が深く依存している重心ほど、失えば崩れが加速する（criticalDependency が
        /// 1に近いほど雪崩的）。重みが小さい／依存が浅ければ波及は小さい。
        /// </summary>
        public static float CollapseImpact(float gravityWeight, float criticalDependency, CenterOfGravityParams p)
        {
            float w = Mathf.Clamp01(gravityWeight);
            float dep = Mathf.Clamp01(criticalDependency);
            float depCurve = Mathf.Pow(dep, p.collapseExponent); // 依存が深いほど加速して崩れる
            return Mathf.Clamp01(w * depCurve);
        }

        /// <summary>既定係数での重心喪失の波及（0..1）。</summary>
        public static float CollapseImpact(float gravityWeight, float criticalDependency)
            => CollapseImpact(gravityWeight, criticalDependency, CenterOfGravityParams.Default);

        /// <summary>
        /// AIの攻撃優先度（0..1）＝<b>重要×脆弱×到達可能</b>の積。gravityWeight（重要＝叩く価値）×
        /// vulnerability（脆弱＝崩しやすさ0..1）× reachability（到達可能＝そこへ兵を届けられるか0..1）。
        /// どれか一つでも0なら優先度0＝価値があっても脆くなく届かない重心は後回し＝叩ける重心を優先する。
        /// </summary>
        public static float AttackPriority(float gravityWeight, float vulnerability, float reachability)
        {
            float w = Mathf.Clamp01(gravityWeight);
            float vuln = Mathf.Clamp01(vulnerability);
            float reach = Mathf.Clamp01(reachability);
            return Mathf.Clamp01(w * vuln * reach);
        }

        /// <summary>
        /// 重心の急所＝決定的弱点（critical vulnerability・0..1）＝守りの薄い力の源泉。重心の重み×（1−防御）＝
        /// 重要でありながら手薄な点ほど大きい。重要でも守りが固ければ急所にならず、手薄でも重要でなければ
        /// 急所にならない＝重い重心の守りの穴こそ叩くべき決定的弱点。
        /// </summary>
        public static float CriticalVulnerability(float gravityWeight, float defense)
        {
            float w = Mathf.Clamp01(gravityWeight);
            float def = Mathf.Clamp01(defense);
            return Mathf.Clamp01(w * (1f - def));
        }

        /// <summary>
        /// 間接アプローチの推奨度（0..1・リデルハート）＝重心の正面が固ければ迂回せよ。directDefense（正面防御
        /// 0..1）が高く、かつ flankingOption（側背・迂回の選択肢0..1）があるほど高い＝堅い正面を避け弱点へ。
        /// 正面が薄ければ正面突破で足り（低い）、迂回路が無ければ間接アプローチ自体が取れない（低い）。
        /// </summary>
        public static float IndirectApproach(float directDefense, float flankingOption, CenterOfGravityParams p)
        {
            float def = Mathf.Clamp01(directDefense);
            float flank = Mathf.Clamp01(flankingOption);
            // 正面防御が閾値を越えた分だけ迂回の旨味が出る（閾値以下なら正面で足りる）
            float thr = p.indirectThreshold;
            float excess = def <= thr ? 0f : (def - thr) / Mathf.Max(0.0001f, 1f - thr);
            return Mathf.Clamp01(excess * flank);
        }

        /// <summary>既定係数での間接アプローチ推奨度（0..1）。</summary>
        public static float IndirectApproach(float directDefense, float flankingOption)
            => IndirectApproach(directDefense, flankingOption, CenterOfGravityParams.Default);

        /// <summary>
        /// 戦力を重心一点に集中する効果（0..1・兵力分散の戒め）＝総戦力 ÷ 分散した目標数の実効値。
        /// totalForce（投入できる総戦力0..1）を dispersedTargets（同時に相手取る目標の分散度0..1＝あちこちへ
        /// 兵を割くほど高い）で割り引く＝目標を絞り一点へ集中するほど打撃が効く。分散ゼロ（一点集中）なら
        /// 総戦力がそのまま効き、分散が大きいほど各個の打撃が薄まる。
        /// </summary>
        public static float ConcentrationOfForce(float totalForce, float dispersedTargets)
        {
            float force = Mathf.Clamp01(totalForce);
            float dispersion = Mathf.Clamp01(dispersedTargets);
            // 分散度が高いほど実効打撃が薄まる（1+分散 で割る＝一点集中=分散0で満額）
            return Mathf.Clamp01(force / (1f + dispersion));
        }

        /// <summary>
        /// 自軍の重心を守る優先度（0..1）＝自軍の重心の重み×（1−自軍防御）。敵が我が重心を叩くのと同じ理屈で、
        /// 自軍の力の源泉が手薄なほど守りの優先度が上がる＝決定的弱点を晒すな。重い重心ほど・守りが薄いほど
        /// 最優先で防護する（敵 <see cref="CriticalVulnerability"/> の自軍版＝守る側の鏡像）。
        /// </summary>
        public static float ProtectOwnCoG(float ownGravityWeight, float ownDefense)
        {
            float w = Mathf.Clamp01(ownGravityWeight);
            float def = Mathf.Clamp01(ownDefense);
            return Mathf.Clamp01(w * (1f - def));
        }
    }
}
