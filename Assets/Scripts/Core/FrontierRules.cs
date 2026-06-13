using UnityEngine;

namespace Ginei
{
    /// <summary>辺境気質の調整係数。距離・通信・統制が文化へどう効くかの重みを束ねる。</summary>
    public readonly struct FrontierParams
    {
        /// <summary>辺境度が飽和する基準距離（これに達すると距離成分が最大）。</summary>
        public readonly float referenceDistance;
        /// <summary>距離が辺境度へ効く比重（0..1。残りが通信の遅さの比重）。</summary>
        public readonly float distanceWeight;
        /// <summary>駐留が中央統制をどれだけ補えるか（駐留1で薄まりをどこまで埋め戻すか・0..1）。</summary>
        public readonly float garrisonOffset;
        /// <summary>辺境の自衛力が人口から生まれる係数（自立×人口にこれを掛ける）。</summary>
        public readonly float militiaPerCapita;

        public FrontierParams(float referenceDistance, float distanceWeight, float garrisonOffset, float militiaPerCapita)
        {
            this.referenceDistance = Mathf.Max(0.0001f, referenceDistance);
            this.distanceWeight = Mathf.Clamp01(distanceWeight);
            this.garrisonOffset = Mathf.Clamp01(garrisonOffset);
            this.militiaPerCapita = Mathf.Max(0f, militiaPerCapita);
        }

        /// <summary>既定＝基準距離100・距離比重0.6・駐留補正0.7・自衛係数0.05。</summary>
        public static FrontierParams Default => new FrontierParams(100f, 0.6f, 0.7f, 0.05f);
    }

    /// <summary>
    /// 辺境気質の純ロジック。中央（首都）から遠い星系ほど「中央の手が届かない」辺境度が育ち、統制が薄れて
    /// 自立の気風・独立志向・実験の自由が生まれる＝「距離は文化を作る＝遠い辺境は別の国になっていく」。
    /// 物理的な版図の連結度は <see cref="LogisticsRules"/>（所有回廊の最大連結成分＝国力を出し切れるか）が担い、
    /// ここはその距離が生む文化的効果（自立・独立志向）を扱う＝分担を分ける。生じた独立志向は
    /// <see cref="CultureRules.SeparatismRisk"/>（少数民族の分離独立リスク）の土壌として接続する想定。
    /// 全入力クランプ・乱数なし決定論・基準値非破壊（係数を返すのみ）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FrontierRules
    {
        // --- 調整値（マジックナンバー禁止＝const に集約） ---
        public const float MaxFrontierIndex = 1f;        // 辺境度の上限
        public const float BaseControl = 1f;             // 中央付近の統制（中枢=満額）
        public const float MinControl = 0.1f;            // どれだけ辺境でも残る最低限の統制
        public const float SelfRelianceFloor = 0.1f;     // 中枢でも残る最低限の自助
        public const float InnovationFloor = 0.2f;       // 中枢でも残る最低限の試行の自由

        /// <summary>
        /// 辺境度(0..1)＝中央の手がどれだけ届かないか。基準距離に対する距離成分と、通信の遅さ(=1−communicationSpeed)
        /// を <see cref="FrontierParams.distanceWeight"/> で混ぜる。遠く・通信が遅いほど辺境＝中央の統制が及ばない。
        /// </summary>
        /// <param name="distanceToCapital">首都からの距離（0以上）。</param>
        /// <param name="communicationSpeed">通信速度(0..1)。1=即時（中央と密）・0=途絶（孤立）。</param>
        public static float FrontierIndex(float distanceToCapital, float communicationSpeed, FrontierParams p)
        {
            float distFrac = Mathf.Clamp01(Mathf.Max(0f, distanceToCapital) / p.referenceDistance);
            float commLag = 1f - Mathf.Clamp01(communicationSpeed);
            float idx = p.distanceWeight * distFrac + (1f - p.distanceWeight) * commLag;
            return Mathf.Clamp(idx, 0f, MaxFrontierIndex);
        }

        public static float FrontierIndex(float distanceToCapital, float communicationSpeed)
            => FrontierIndex(distanceToCapital, communicationSpeed, FrontierParams.Default);

        /// <summary>
        /// 自立の気風(0..1)＝辺境ほど自前で問題を解決する開拓者精神。辺境度に比例しつつ、中枢でも
        /// <see cref="SelfRelianceFloor"/> は残る（誰しも多少は自助する）。
        /// </summary>
        public static float SelfReliance(float frontierIndex)
        {
            float fi = Mathf.Clamp(frontierIndex, 0f, MaxFrontierIndex);
            return Mathf.Clamp01(Mathf.Lerp(SelfRelianceFloor, 1f, fi));
        }

        /// <summary>
        /// 中央統制の浸透(0..1)＝距離が統制を薄め、駐留がそれを補う。辺境度ぶん統制が落ち、駐留(0..1)で
        /// <see cref="FrontierParams.garrisonOffset"/> の割合まで埋め戻す（剣で距離を縮める）。下限 <see cref="MinControl"/>。
        /// </summary>
        /// <param name="garrisonStrength">駐留戦力の充実度(0..1)。</param>
        public static float ControlPenetration(float frontierIndex, float garrisonStrength, FrontierParams p)
        {
            float fi = Mathf.Clamp(frontierIndex, 0f, MaxFrontierIndex);
            float g = Mathf.Clamp01(garrisonStrength);
            // 距離で削れる統制ぶんを、駐留が garrisonOffset の比率で回復する
            float erosion = fi * (1f - g * p.garrisonOffset);
            float control = BaseControl - erosion;
            return Mathf.Clamp(control, MinControl, BaseControl);
        }

        public static float ControlPenetration(float frontierIndex, float garrisonStrength)
            => ControlPenetration(frontierIndex, garrisonStrength, FrontierParams.Default);

        /// <summary>
        /// 独立志向(0..1)＝辺境度×中央の冷遇。遠さだけでは離れない＝冷遇されて初めて分離の土壌になる
        /// （見捨てられた辺境は別の国を志す）。この値は <see cref="CultureRules.SeparatismRisk"/> の土壌として渡す想定。
        /// </summary>
        /// <param name="centralNeglect">中央の冷遇度(0..1)。投資・配慮が薄いほど高い。</param>
        public static float IndependenceSentiment(float frontierIndex, float centralNeglect)
        {
            float fi = Mathf.Clamp(frontierIndex, 0f, MaxFrontierIndex);
            float neglect = Mathf.Clamp01(centralNeglect);
            return Mathf.Clamp01(fi * neglect);
        }

        /// <summary>
        /// 辺境の自衛力（0以上）＝自立の気風×人口×係数。中央が守らない辺境は自ら武装する＝二面性：
        /// 外敵への防壁にも、中央への反乱基盤にもなる（戦力にも独立の牙にもなる）。
        /// </summary>
        public static float FrontierMilitia(float selfReliance, float population, FrontierParams p)
        {
            float sr = Mathf.Clamp01(selfReliance);
            float pop = Mathf.Max(0f, population);
            return sr * pop * p.militiaPerCapita;
        }

        public static float FrontierMilitia(float selfReliance, float population)
            => FrontierMilitia(selfReliance, population, FrontierParams.Default);

        /// <summary>
        /// 辺境の自由(0..1)＝中央の目が届かないぶん新しい試みの実験場になる。辺境度に比例しつつ、中枢でも
        /// <see cref="InnovationFloor"/> は残る（どこでも多少の実験はある）。中央集権の窒息と対をなす。
        /// </summary>
        public static float InnovationFreedom(float frontierIndex)
        {
            float fi = Mathf.Clamp(frontierIndex, 0f, MaxFrontierIndex);
            return Mathf.Clamp01(Mathf.Lerp(InnovationFloor, 1f, fi));
        }
    }
}
