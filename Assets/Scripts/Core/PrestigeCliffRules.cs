using UnityEngine;

namespace Ginei
{
    /// <summary>指導者の威光と非連続崩壊（ル・ボン型）の調整係数。</summary>
    public readonly struct PrestigeCliffParams
    {
        /// <summary>成功で積み上がる威光の係数（積み上げの遅さ＝小さいほどゆっくり）。</summary>
        public readonly float buildRate;
        /// <summary>威光の磁力スケール（威光1のときに群衆を従わせる最大の磁力）。</summary>
        public readonly float magnetismScale;
        /// <summary>崖リスクのスケール（大失敗が威光を崖から突き落とす確率の上限）。</summary>
        public readonly float cliffRiskScale;
        /// <summary>崩壊時の威光の残存率（瓦解後に残る割合・小さいほど一気に落ちる）。</summary>
        public readonly float collapseResidual;
        /// <summary>回復上限の係数（崩壊前の高さに対して取り戻せる上限の割合＝元の高さには戻らない）。</summary>
        public readonly float recoveryCeilingRatio;
        /// <summary>近接露出による神秘性侵食の係数（距離ゼロで見られ続けると威光が薄れる速さ）。</summary>
        public readonly float mystiqueErosionRate;

        public PrestigeCliffParams(float buildRate, float magnetismScale, float cliffRiskScale,
                                   float collapseResidual, float recoveryCeilingRatio, float mystiqueErosionRate)
        {
            this.buildRate = Mathf.Clamp01(buildRate);
            this.magnetismScale = Mathf.Clamp01(magnetismScale);
            this.cliffRiskScale = Mathf.Clamp01(cliffRiskScale);
            this.collapseResidual = Mathf.Clamp01(collapseResidual);
            this.recoveryCeilingRatio = Mathf.Clamp01(recoveryCeilingRatio);
            this.mystiqueErosionRate = Mathf.Clamp01(mystiqueErosionRate);
        }

        /// <summary>既定＝積上0.3・磁力1.0・崖リスク0.9・残存率0.2・回復上限0.7・神秘侵食0.5。</summary>
        public static PrestigeCliffParams Default
            => new PrestigeCliffParams(0.3f, 1.0f, 0.9f, 0.2f, 0.7f, 0.5f);
    }

    /// <summary>
    /// 指導者の威光（prestige）と非連続崩壊の純ロジック（CRWD-3 #1822・ル・ボン『群衆心理』参考）。
    /// 威光は群衆を従わせる磁力だが、積み上げるには時間がかかり（逓減）、一度の決定的な失敗・幻滅で
    /// **崖から突き落とされるように一気に瓦解する**＝崩壊は非連続。そして一度地に落ちた威光は元の高さへ戻らない
    /// （不可逆＝回復上限が崩壊前を下回る）。威光は近くで見られ続けると神秘性が薄れる＝距離が威光を保つ。
    /// <see cref="Organization"/>（#812・カリスマの日常化＝制度化が個人カリスマを超えて続く）とは別レイヤー＝
    /// 制度化ではなく威光そのものの非連続な崖。<see cref="DynastyRules"/>（#867・天命と腐敗の緩やかな進行）とも別＝
    /// じわじわではなく一度の失敗での不可逆瓦解。<see cref="ReputationRules"/>（武名の漸進的増減）とも別＝
    /// 崖の非連続性に特化。盤面非依存のplain引数・乱数は roll 引数で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PrestigeCliffRules
    {
        /// <summary>
        /// 成功による威光の積み上げ（0..1）＝現在威光 currentPrestige(0..1) に、成功の大きさ successMagnitude(0..1)
        /// ×積上係数×残余余地(1−現在威光) を加える。逓減で上限へ漸近＝積み上げには時間がかかる（崩壊の速さと非対称）。
        /// </summary>
        public static float PrestigeAccumulation(float currentPrestige, float successMagnitude, PrestigeCliffParams p)
        {
            float pr = Mathf.Clamp01(currentPrestige);
            float s = Mathf.Clamp01(successMagnitude);
            return Mathf.Clamp01(pr + s * p.buildRate * (1f - pr));
        }

        public static float PrestigeAccumulation(float currentPrestige, float successMagnitude)
            => PrestigeAccumulation(currentPrestige, successMagnitude, PrestigeCliffParams.Default);

        /// <summary>
        /// 威光の磁力（0..1）＝威光×磁力スケール。威光が高いほど群衆を強く従わせる（命令への服従・士気の源）。
        /// </summary>
        public static float PrestigeMagnetism(float prestige, PrestigeCliffParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(prestige) * p.magnetismScale);
        }

        public static float PrestigeMagnetism(float prestige)
            => PrestigeMagnetism(prestige, PrestigeCliffParams.Default);

        /// <summary>
        /// 崖リスク（0..1）＝威光 prestige(0..1)×失敗の大きさ failureMagnitude(0..1)×崖リスクスケール。
        /// 威光が高いほど落差が大きく、また失敗が大きいほど崖から突き落とされやすい
        /// ＝高みにある威光ほど一度の失敗で砕けやすい。
        /// </summary>
        public static float CliffRisk(float prestige, float failureMagnitude, PrestigeCliffParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(prestige) * Mathf.Clamp01(failureMagnitude) * p.cliffRiskScale);
        }

        public static float CliffRisk(float prestige, float failureMagnitude)
            => CliffRisk(prestige, failureMagnitude, PrestigeCliffParams.Default);

        /// <summary>
        /// 失敗後の威光（0..1）。失敗の大きさ failureMagnitude が閾値 threshold を超えたら**非連続に瓦解**＝
        /// 威光を残存率 collapseResidual 倍へ一気に落とす（崖）。閾値以下なら漸進的に削るだけ（失敗ぶんを差し引く）。
        /// 「崩れる時は一瞬」の核。
        /// </summary>
        public static float PrestigeCollapse(float prestige, float failureMagnitude, float threshold, PrestigeCliffParams p)
        {
            float pr = Mathf.Clamp01(prestige);
            float f = Mathf.Clamp01(failureMagnitude);
            float th = Mathf.Clamp01(threshold);
            if (f > th)
                return Mathf.Clamp01(pr * p.collapseResidual);   // 非連続な崖＝一気に落ちる
            return Mathf.Clamp01(pr - f * pr);                   // 閾値以下は漸進的に削る
        }

        public static float PrestigeCollapse(float prestige, float failureMagnitude, float threshold)
            => PrestigeCollapse(prestige, failureMagnitude, threshold, PrestigeCliffParams.Default);

        /// <summary>
        /// 不可逆性係数（0..1）＝崩落の深さ collapsedDepth(0..1) が大きいほど1へ漸近。
        /// 深く崩れた威光ほど元に戻りにくい＝回復に掛かる抵抗。
        /// </summary>
        public static float IrreversibilityFactor(float collapsedDepth)
        {
            float d = Mathf.Clamp01(collapsedDepth);
            return Mathf.Clamp01(d);
        }

        /// <summary>
        /// 積み上げの遅さと崩壊の速さの非対称度（0..1）＝崩壊が積み上げよりどれだけ速いか。
        /// fallTime が buildTime に対して短いほど1へ近づく（=瞬時崩壊）。両者とも非負へクランプ、
        /// buildTime=0 は非対称ゼロ（比較不能）。
        /// </summary>
        public static float SlowBuildFastFall(float buildTime, float fallTime)
        {
            float b = Mathf.Max(0f, buildTime);
            float f = Mathf.Max(0f, fallTime);
            if (b <= 0f) return 0f;
            return Mathf.Clamp01(1f - f / (b + f));   // f≪b で1へ、f=b で0.5、f≫b で0へ
        }

        /// <summary>
        /// 崩壊後に回復できる威光の上限（0..1）＝崩壊前の威光 preCollapsePrestige×回復上限割合。
        /// 一度地に落ちた威光は元の高さには戻らない＝上限は常に崩壊前を下回る（recoveryCeilingRatio&lt;1）。
        /// </summary>
        public static float RecoveryCeiling(float preCollapsePrestige, PrestigeCliffParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(preCollapsePrestige) * p.recoveryCeilingRatio);
        }

        public static float RecoveryCeiling(float preCollapsePrestige)
            => RecoveryCeiling(preCollapsePrestige, PrestigeCliffParams.Default);

        /// <summary>
        /// 神秘性侵食後の威光（0..1）＝近接露出 exposure(0..1) に比例して威光が薄れる。
        /// 近くで見られ続ける（exposure→1）ほど神秘が剥げる＝威光は距離で保たれる。
        /// </summary>
        public static float MystiqueErosion(float prestige, float exposure, PrestigeCliffParams p)
        {
            float pr = Mathf.Clamp01(prestige);
            float e = Mathf.Clamp01(exposure);
            return Mathf.Clamp01(pr - pr * e * p.mystiqueErosionRate);
        }

        public static float MystiqueErosion(float prestige, float exposure)
            => MystiqueErosion(prestige, exposure, PrestigeCliffParams.Default);

        /// <summary>威光が砕けたか＝威光が閾値 threshold(0..1) を下回った状態（磁力を失い群衆が離れる）。</summary>
        public static bool IsPrestigeShattered(float prestige, float threshold)
        {
            return Mathf.Clamp01(prestige) < Mathf.Clamp01(threshold);
        }
    }
}
