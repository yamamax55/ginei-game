using UnityEngine;

namespace Ginei
{
    /// <summary>観の目・見の目の調整係数（#1387 五輪書）。</summary>
    public readonly struct BattlePerceptionParams
    {
        /// <summary>知覚半径の基礎成分（情報能力ゼロでも見える最低限の戦場視界）。</summary>
        public readonly float baseRadius;
        /// <summary>知覚半径の最大成分（情報能力×センサーが満ちたときの追加視界）。</summary>
        public readonly float maxRadius;
        /// <summary>観の目への経験の寄与（情報能力と経験をどう混ぜて大局眼を作るか・0..1）。</summary>
        public readonly float experienceWeight;
        /// <summary>俯瞰の掌握とみなす既定しきい値（観の目×状況把握がこれ以上で全体掌握）。</summary>
        public readonly float commandingThreshold;

        public BattlePerceptionParams(float baseRadius, float maxRadius, float experienceWeight, float commandingThreshold)
        {
            this.baseRadius = Mathf.Max(0f, baseRadius);
            this.maxRadius = Mathf.Max(0f, maxRadius);
            this.experienceWeight = Mathf.Clamp01(experienceWeight);
            this.commandingThreshold = Mathf.Clamp01(commandingThreshold);
        }

        /// <summary>既定＝基礎半径10・最大追加半径30・経験寄与0.35・掌握しきい値0.6。</summary>
        public static BattlePerceptionParams Default => new BattlePerceptionParams(10f, 30f, 0.35f, 0.6f);
    }

    /// <summary>
    /// 観見二つの目（かんけんふたつのめ）の純ロジック（#1387 五輪書）。宮本武蔵いわく
    /// 「観の目（かんのめ＝全体・大局・敵の意図を見抜く強い目）を強く、見の目（けんのめ＝目に見える
    /// 表面の太刀の動きを見る目）を弱く」＝目先の動きに惑わされず戦場全体と敵の本心を見抜く。
    /// 提督の情報能力（<see cref="AdmiralData.intelligence"/>＝read-only）が戦場知覚の広さと深さを決め、
    /// 観の目が強く見の目に頼りすぎないほど大局が明晰になり、敵の陽動・欺瞞に惑わされない。
    /// 分担：<see cref="ReconRules"/> は偵察の推定精度（敵戦力を何隻と読むか）、
    /// <see cref="CommunicationsRules"/> は指揮の遅延（命令が届く速さ）を担う。ここは
    /// 「戦場をどれだけ広く・深く知覚し大局と意図を見抜くか」という観見二つの目だけを扱い、
    /// <see cref="DeceptionRules"/> の戦略的欺瞞に対しては観の目で耐性を与える。
    /// 乱数なし決定論・全入力クランプ。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BattlePerceptionRules
    {
        /// <summary>
        /// 知覚半径＝提督の情報能力(0..1)×センサーの被覆(0..1)で戦場を知覚する広さ（観の目で広く見える）。
        /// baseRadius を底に、情報能力とセンサーの積ぶんだけ maxRadius を加える＝情報の将は遠くまで戦場を掴む。
        /// </summary>
        public static float PerceptionRadius(float intelligence, float sensorCoverage, BattlePerceptionParams p)
        {
            float intel = Mathf.Clamp01(intelligence);
            float sensor = Mathf.Clamp01(sensorCoverage);
            return p.baseRadius + p.maxRadius * intel * sensor;
        }

        public static float PerceptionRadius(float intelligence, float sensorCoverage)
            => PerceptionRadius(intelligence, sensorCoverage, BattlePerceptionParams.Default);

        /// <summary>
        /// 観の目(0..1)＝大局・本質・敵の意図を見抜く力。情報能力(0..1)を主とし経験(0..1)で底上げする
        /// （情報能力＋経験で戦場全体が見える）。experienceWeight ぶんを経験へ、残りを情報能力へ配分。
        /// </summary>
        public static float KanNoMe(float intelligence, float experience, BattlePerceptionParams p)
        {
            float intel = Mathf.Clamp01(intelligence);
            float exp = Mathf.Clamp01(experience);
            float w = p.experienceWeight;
            return Mathf.Clamp01(intel * (1f - w) + exp * w);
        }

        public static float KanNoMe(float intelligence, float experience)
            => KanNoMe(intelligence, experience, BattlePerceptionParams.Default);

        /// <summary>
        /// 見の目(0..1)＝目に見える表面の動き（敵の太刀筋）への注目。surfaceFocus そのもの。
        /// 強すぎると目先に惑わされる＝適度は要るが頼りすぎは禁物（武蔵いわく「見の目よわく」）。
        /// </summary>
        public static float KenNoMe(float surfaceFocus)
        {
            return Mathf.Clamp01(surfaceFocus);
        }

        /// <summary>
        /// 大局の明晰さ(0..1)＝観の目が強く見の目に頼りすぎないほど高い（観を強く見を弱く）。
        /// kanNoMe を主成分に、見の目の過剰（kenNoMe が観の目を超えるぶん）を引いて目先への没入を罰する。
        /// </summary>
        public static float BigPictureClarity(float kanNoMe, float kenNoMe)
        {
            float kan = Mathf.Clamp01(kanNoMe);
            float ken = Mathf.Clamp01(kenNoMe);
            // 見の目が観の目を上回った超過ぶんだけ大局が曇る（観≧見なら罰ゼロ）。
            float excess = Mathf.Max(0f, ken - kan);
            return Mathf.Clamp01(kan - excess);
        }

        /// <summary>
        /// 敵の意図・本心を見抜く度合い(0..1)＝観の目で陽動・欺瞞を見破る。
        /// kanNoMe を底に、敵の欺瞞(0..1)が観の目を上回った超過ぶんだけ読みを曇らせる
        /// （観の目が敵の欺瞞を上回れば惑わされない＝<see cref="DeceptionRules"/> への耐性）。
        /// </summary>
        public static float IntentReading(float kanNoMe, float enemyDeception)
        {
            float kan = Mathf.Clamp01(kanNoMe);
            float deception = Mathf.Clamp01(enemyDeception);
            // 欺瞞が観の目を超えたぶんだけ意図の読みが鈍る（観≧欺瞞なら満額）。
            float fooled = Mathf.Max(0f, deception - kan);
            return Mathf.Clamp01(kan - fooled);
        }

        /// <summary>
        /// 戦場全体の状況把握(0..1)＝どこで何が起きているか。知覚半径を最大半径で正規化した広さと、
        /// 情報の質(0..1)を掛けて、広く知覚しても情報が粗ければ把握は不完全という関係を出す。
        /// </summary>
        public static float SituationalAwareness(float perceptionRadius, float informationQuality, BattlePerceptionParams p)
        {
            float reach = p.baseRadius + p.maxRadius; // 知覚半径の上限。
            float coverage = reach > 0f ? Mathf.Clamp01(perceptionRadius / reach) : 0f;
            return Mathf.Clamp01(coverage * Mathf.Clamp01(informationQuality));
        }

        public static float SituationalAwareness(float perceptionRadius, float informationQuality)
            => SituationalAwareness(perceptionRadius, informationQuality, BattlePerceptionParams.Default);

        /// <summary>
        /// 視野狭窄のリスク(0..1)＝見の目が強く観の目が弱いほど高い（目先の戦闘に没入して大局を見失う）。
        /// 見の目が観の目を上回った超過ぶんがそのままリスク＝観で見を上回っていれば視野は狭まらない。
        /// </summary>
        public static float TunnelVisionRisk(float kenNoMe, float kanNoMe)
        {
            float ken = Mathf.Clamp01(kenNoMe);
            float kan = Mathf.Clamp01(kanNoMe);
            return Mathf.Clamp01(Mathf.Max(0f, ken - kan));
        }

        /// <summary>
        /// 俯瞰の掌握判定＝観の目で戦場を俯瞰し全体を掌握したか。
        /// 観の目と状況把握の積が threshold 以上なら true（観の目だけでも状況把握だけでも届かない＝両輪）。
        /// </summary>
        public static bool IsCommandingView(float kanNoMe, float situationalAwareness, float threshold)
        {
            float kan = Mathf.Clamp01(kanNoMe);
            float awareness = Mathf.Clamp01(situationalAwareness);
            return kan * awareness >= Mathf.Clamp01(threshold);
        }

        public static bool IsCommandingView(float kanNoMe, float situationalAwareness)
            => IsCommandingView(kanNoMe, situationalAwareness, BattlePerceptionParams.Default.commandingThreshold);
    }
}
