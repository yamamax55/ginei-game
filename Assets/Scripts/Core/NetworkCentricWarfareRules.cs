using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ネットワーク中心戦の調整値（NCW #ネットワーク中心戦）＝情報共有が戦力を相乗的に高める運用の係数。
    /// 情報優越→共有状況認識→自己同期/決定優越→テンポ加速、ネットワークの規模効果（メトカーフ近似）、
    /// 情報過多のリスクを束ねる。すべて ctor で安全側へクランプ（決定論・実効値パターン）。
    /// </summary>
    public readonly struct NetworkCentricWarfareParams
    {
        /// <summary>共有状況認識の下駄＝ネットワーク分配0でも残る最低共有度（分配で1.0へ伸びる）。</summary>
        public readonly float distributionFloor;
        /// <summary>規模効果の最大値（接続ノードが十分多いときの上限ボーナス＝収穫逓増の天井）。</summary>
        public readonly float maxScaleEffect;
        /// <summary>規模効果が最大値の半分に達する接続ノード数（有理飽和の半飽和点・メトカーフ近似）。</summary>
        public readonly float scaleHalfNodes;
        /// <summary>情報過多が立ち始める負荷の閾値（0..1。これ以下は処理しきれて過多なし）。</summary>
        public readonly float overloadThreshold;
        /// <summary>閾値超過分→情報過多の感度（大きいほど少しの超過で判断不能へ）。</summary>
        public readonly float overloadScale;
        /// <summary>戦闘力相乗倍率のテンポ・規模効果からの上振れ幅（最大 1+この値へ寄与）。</summary>
        public readonly float synergyGain;
        /// <summary>戦闘力相乗倍率の情報過多による下振れ幅（最大この値ぶん減じる）。</summary>
        public readonly float overloadPenalty;

        public NetworkCentricWarfareParams(float distributionFloor, float maxScaleEffect, float scaleHalfNodes,
            float overloadThreshold, float overloadScale, float synergyGain, float overloadPenalty)
        {
            this.distributionFloor = Mathf.Clamp01(distributionFloor);
            this.maxScaleEffect = Mathf.Clamp(maxScaleEffect, 0f, 4f);
            this.scaleHalfNodes = Mathf.Clamp(scaleHalfNodes, 1f, 10000f);
            this.overloadThreshold = Mathf.Clamp01(overloadThreshold);
            this.overloadScale = Mathf.Clamp(overloadScale, 0f, 10f);
            this.synergyGain = Mathf.Clamp(synergyGain, 0f, 4f);
            this.overloadPenalty = Mathf.Clamp(overloadPenalty, 0f, 4f);
        }

        /// <summary>既定：共有下駄0.2・規模効果上限1.0・半飽和点10ノード・過多閾値0.6・過多感度2.0・相乗上振れ1.0・過多下振れ1.0。</summary>
        public static NetworkCentricWarfareParams Default => new NetworkCentricWarfareParams(
            DefaultDistributionFloor, DefaultMaxScaleEffect, DefaultScaleHalfNodes,
            DefaultOverloadThreshold, DefaultOverloadScale, DefaultSynergyGain, DefaultOverloadPenalty);

        public const float DefaultDistributionFloor = 0.2f;
        public const float DefaultMaxScaleEffect = 1.0f;
        public const float DefaultScaleHalfNodes = 10f;
        public const float DefaultOverloadThreshold = 0.6f;
        public const float DefaultOverloadScale = 2.0f;
        public const float DefaultSynergyGain = 1.0f;
        public const float DefaultOverloadPenalty = 1.0f;
    }

    /// <summary>
    /// ネットワーク中心戦の純ロジック（NCW・唯一の窓口）＝<b>情報共有が戦力を相乗的に高める</b>運用。
    /// センサー網と処理能力で<see cref="InformationSuperiority"/>（情報優越）を得、ネットワークで配って
    /// <see cref="SharedAwareness"/>（共有状況認識）を作る。共有認識は中央指令なしの協調＝<see cref="SelfSynchronization"/>
    /// （自己同期）と速い決断＝<see cref="DecisionSuperiority"/>（決定優越）を生み、両者が<see cref="TempoAcceleration"/>
    /// （テンポ加速＝OODA を速く回す）へ結ぶ。接続ノードが増えるほど<see cref="NetworkScaleEffect"/>（規模効果＝
    /// メトカーフの法則的に価値が増えるが上限あり）が効く一方、流量が処理を超えれば<see cref="InformationOverload"/>
    /// （情報過多＝多すぎて判断不能）に陥る。最終的に<see cref="CombatPowerMultiplier"/>が戦闘力の相乗倍率（0.5〜2.0）を出す。
    /// <b>分担</b>：局所火力の二乗則そのもの（会戦ダメージ側）は <see cref="LanchesterRules"/>、兵力を集める運用判断は
    /// <see cref="ForceConcentrationRules"/> が担う＝こちらは<b>情報の共有が戦力を相乗させる</b>側の数値。
    /// 盤面非依存の plain 引数・各メソッドに Params 明示版＋Default 委譲版・LINQ/乱数/Log/Exp 不使用・test-first。
    /// </summary>
    public static class NetworkCentricWarfareRules
    {
        // ── 情報優越 ──────────────────────────────────────────────

        /// <summary>センサー網×処理能力で情報優越（0..1）。両方が高いときだけ優越（どちらか欠ければ崩れる）。</summary>
        public static float InformationSuperiority(float sensorReach, float dataProcessing)
            => InformationSuperiority(sensorReach, dataProcessing, NetworkCentricWarfareParams.Default);

        /// <summary>情報優越＝clamp01(sensorReach)×clamp01(dataProcessing)（積＝両立が要る）。</summary>
        public static float InformationSuperiority(float sensorReach, float dataProcessing, NetworkCentricWarfareParams p)
        {
            float s = Mathf.Clamp01(sensorReach);
            float d = Mathf.Clamp01(dataProcessing);
            return Mathf.Clamp01(s * d);
        }

        // ── 共有状況認識 ──────────────────────────────────────────

        /// <summary>情報優越×ネットワーク分配で共有状況認識（0..1）。分配0でも下駄ぶんは共有される。</summary>
        public static float SharedAwareness(float informationSuperiority, float networkDistribution)
            => SharedAwareness(informationSuperiority, networkDistribution, NetworkCentricWarfareParams.Default);

        /// <summary>共有状況認識＝clamp01(infoSup) × Lerp(distributionFloor,1,clamp01(networkDistribution))。</summary>
        public static float SharedAwareness(float informationSuperiority, float networkDistribution, NetworkCentricWarfareParams p)
        {
            float info = Mathf.Clamp01(informationSuperiority);
            float dist = Mathf.Lerp(p.distributionFloor, 1f, Mathf.Clamp01(networkDistribution));
            return Mathf.Clamp01(info * dist);
        }

        // ── 自己同期 ──────────────────────────────────────────────

        /// <summary>共有認識×部隊の主体性で自己同期（0..1）＝中央指令なしの自発的協調。</summary>
        public static float SelfSynchronization(float sharedAwareness, float unitInitiative)
            => SelfSynchronization(sharedAwareness, unitInitiative, NetworkCentricWarfareParams.Default);

        /// <summary>自己同期＝clamp01(sharedAwareness)×clamp01(unitInitiative)（共有と主体性の両方が要る）。</summary>
        public static float SelfSynchronization(float sharedAwareness, float unitInitiative, NetworkCentricWarfareParams p)
        {
            float a = Mathf.Clamp01(sharedAwareness);
            float i = Mathf.Clamp01(unitInitiative);
            return Mathf.Clamp01(a * i);
        }

        // ── 決定優越 ──────────────────────────────────────────────

        /// <summary>共有認識×決定速度で決定優越（0..1）＝相手より速く正しく決める優位。</summary>
        public static float DecisionSuperiority(float sharedAwareness, float decisionSpeed)
            => DecisionSuperiority(sharedAwareness, decisionSpeed, NetworkCentricWarfareParams.Default);

        /// <summary>決定優越＝clamp01(sharedAwareness)×clamp01(decisionSpeed)。</summary>
        public static float DecisionSuperiority(float sharedAwareness, float decisionSpeed, NetworkCentricWarfareParams p)
        {
            float a = Mathf.Clamp01(sharedAwareness);
            float s = Mathf.Clamp01(decisionSpeed);
            return Mathf.Clamp01(a * s);
        }

        // ── テンポ加速 ────────────────────────────────────────────

        /// <summary>自己同期×決定優越でテンポの加速（0..1）＝OODA を速く回し相手の反応を置き去る。</summary>
        public static float TempoAcceleration(float selfSynchronization, float decisionSuperiority)
            => TempoAcceleration(selfSynchronization, decisionSuperiority, NetworkCentricWarfareParams.Default);

        /// <summary>テンポ加速＝clamp01(selfSync)×clamp01(decisionSup)。</summary>
        public static float TempoAcceleration(float selfSynchronization, float decisionSuperiority, NetworkCentricWarfareParams p)
        {
            float sync = Mathf.Clamp01(selfSynchronization);
            float dec = Mathf.Clamp01(decisionSuperiority);
            return Mathf.Clamp01(sync * dec);
        }

        // ── ネットワークの規模効果 ────────────────────────────────

        /// <summary>接続ノード数の規模効果（0..maxScaleEffect・メトカーフ近似）＝収穫逓増だが上限で飽和。</summary>
        public static float NetworkScaleEffect(int connectedNodes)
            => NetworkScaleEffect(connectedNodes, NetworkCentricWarfareParams.Default);

        /// <summary>規模効果＝maxScaleEffect × n/(n+scaleHalfNodes)（n=max(0,nodes)・有理飽和＝Log/Exp 不使用）。
        /// ノードが半飽和点 scaleHalfNodes のとき最大の半分、多いほど上限へ漸近。1ノード以下はほぼ0。</summary>
        public static float NetworkScaleEffect(int connectedNodes, NetworkCentricWarfareParams p)
        {
            float n = Mathf.Max(0f, connectedNodes);
            if (n <= 1f) return 0f;
            float effective = n - 1f; // 1ノードでは規模効果なし（つながって初めて価値）
            return p.maxScaleEffect * (effective / (effective + p.scaleHalfNodes));
        }

        // ── 情報過多 ──────────────────────────────────────────────

        /// <summary>データ量×(1-選別能力)で情報過多（0..1）＝多すぎて判断できなくなるリスク。</summary>
        public static float InformationOverload(float dataVolume, float filteringCapacity)
            => InformationOverload(dataVolume, filteringCapacity, NetworkCentricWarfareParams.Default);

        /// <summary>情報過多＝clamp01( (clamp01(dataVolume)×(1-clamp01(filtering)) − overloadThreshold) × overloadScale )。
        /// 選別後の実効負荷が閾値以下なら 0（さばける）、超えた分だけ感度を掛けて過多に。</summary>
        public static float InformationOverload(float dataVolume, float filteringCapacity, NetworkCentricWarfareParams p)
        {
            float vol = Mathf.Clamp01(dataVolume);
            float unfiltered = 1f - Mathf.Clamp01(filteringCapacity);
            float load = vol * unfiltered;
            float over = load - p.overloadThreshold;
            if (over <= 0f) return 0f;
            return Mathf.Clamp01(over * p.overloadScale);
        }

        // ── 戦闘力の相乗倍率 ──────────────────────────────────────

        /// <summary>テンポ×規模効果−過多で戦闘力の相乗倍率（0.5〜2.0）。情報共有が戦力を相乗的に高める総合。</summary>
        public static float CombatPowerMultiplier(float tempoAcceleration, float networkScaleEffect, float informationOverload)
            => CombatPowerMultiplier(tempoAcceleration, networkScaleEffect, informationOverload, NetworkCentricWarfareParams.Default);

        /// <summary>戦闘力相乗倍率＝clamp( 1 + synergyGain×tempo×(1+scale)/2 − overloadPenalty×overload, 0.5, 2.0 )。
        /// テンポと規模効果の相乗で上振れ、情報過多で下振れ。基準1.0・拮抗なし。</summary>
        public static float CombatPowerMultiplier(float tempoAcceleration, float networkScaleEffect, float informationOverload, NetworkCentricWarfareParams p)
        {
            float tempo = Mathf.Clamp01(tempoAcceleration);
            float scale = Mathf.Max(0f, networkScaleEffect);
            float overload = Mathf.Clamp01(informationOverload);
            // 規模効果(0..maxScaleEffect)を相乗の倍率(1..1+max)へ：テンポを規模で増幅する
            float synergy = p.synergyGain * tempo * (1f + scale) * 0.5f;
            float penalty = p.overloadPenalty * overload;
            return Mathf.Clamp(1f + synergy - penalty, MinMultiplier, MaxMultiplier);
        }

        /// <summary>戦闘力相乗倍率の下限（情報劣勢・過多で崩れても 0.5 は残る）。</summary>
        public const float MinMultiplier = 0.5f;
        /// <summary>戦闘力相乗倍率の上限（完全な情報優越でも 2.0 が天井）。</summary>
        public const float MaxMultiplier = 2.0f;
    }
}
