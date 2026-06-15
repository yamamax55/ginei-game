using UnityEngine;

namespace Ginei
{
    /// <summary>病臥（英雄の病）の調整係数。</summary>
    public readonly struct IllnessParams
    {
        /// <summary>基礎発症確率（若く頑健で閑職でもこれだけはある）。</summary>
        public readonly float baseOnsetChance;
        /// <summary>発症リスクが上がり始める年齢（ここまでは加齢リスクなし）。</summary>
        public readonly float ageOnsetStart;
        /// <summary>加齢リスクの上昇率（開始年齢の超過1歳あたり）。</summary>
        public readonly float ageOnsetRate;
        /// <summary>激務の重み（stress=1 で発症確率が (1+この値) 倍）。</summary>
        public readonly float stressWeight;
        /// <summary>虚弱の重み（constitution=0 で発症確率が (1+この値) 倍）。</summary>
        public readonly float frailtyWeight;
        /// <summary>発症確率の上限（どれほど条件が重なっても確実にはならない）。</summary>
        public readonly float maxOnsetChance;
        /// <summary>執務能力倍率の下限（重篤でも玉璽は握れる＝完全なゼロにはしない）。</summary>
        public readonly float minCapacity;
        /// <summary>隠蔽の崩れ速度（重症度1×単位時間あたりの基準減少量）。</summary>
        public readonly float concealDecayRate;
        /// <summary>側近の目の重み（courtProximity=1 で隠蔽の崩れが (1+この値) 倍）。</summary>
        public readonly float proximityWeight;
        /// <summary>継承レースの過熱速度（死期が見え後継が曖昧なときの上昇率）。</summary>
        public readonly float raceRiseRate;
        /// <summary>継承レースの鎮静速度（後継が明確なときの低下率）。</summary>
        public readonly float raceCalmRate;
        /// <summary>病床の権威が立ち上がる重症度の閾値（これ以下は平時の重み）。</summary>
        public readonly float deathbedThreshold;
        /// <summary>病床の権威の最大ボーナス（重症度1＝死の床で遺言の効力が (1+この値) 倍）。</summary>
        public readonly float maxDeathbedBonus;

        public IllnessParams(float baseOnsetChance, float ageOnsetStart, float ageOnsetRate,
                             float stressWeight, float frailtyWeight, float maxOnsetChance,
                             float minCapacity, float concealDecayRate, float proximityWeight,
                             float raceRiseRate, float raceCalmRate,
                             float deathbedThreshold, float maxDeathbedBonus)
        {
            this.baseOnsetChance = Mathf.Clamp01(baseOnsetChance);
            this.ageOnsetStart = Mathf.Max(0f, ageOnsetStart);
            this.ageOnsetRate = Mathf.Max(0f, ageOnsetRate);
            this.stressWeight = Mathf.Max(0f, stressWeight);
            this.frailtyWeight = Mathf.Max(0f, frailtyWeight);
            this.maxOnsetChance = Mathf.Clamp01(maxOnsetChance);
            this.minCapacity = Mathf.Clamp01(minCapacity);
            this.concealDecayRate = Mathf.Max(0f, concealDecayRate);
            this.proximityWeight = Mathf.Max(0f, proximityWeight);
            this.raceRiseRate = Mathf.Max(0f, raceRiseRate);
            this.raceCalmRate = Mathf.Max(0f, raceCalmRate);
            this.deathbedThreshold = Mathf.Clamp(deathbedThreshold, 0f, 0.99f);
            this.maxDeathbedBonus = Mathf.Max(0f, maxDeathbedBonus);
        }

        /// <summary>
        /// 既定＝基礎発症0.01・加齢開始40歳・加齢率0.005/歳・激務重み1・虚弱重み1・発症上限0.5・
        /// 執務下限0.1・隠蔽崩れ0.2・側近重み1・レース過熱0.5・鎮静0.5・病床閾値0.5・病床ボーナス0.5。
        /// </summary>
        public static IllnessParams Default => new IllnessParams(
            0.01f, 40f, 0.005f, 1f, 1f, 0.5f,
            0.1f, 0.2f, 1f, 0.5f, 0.5f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 病臥（ラインハルト型）の純ロジック。英雄の突発的な病＝発症（高齢×激務×虚弱）、執務不能
    /// （<see cref="CapacityFactor"/>＝能力の低下倍率・実効値パターン）、病状の隠蔽と漏洩
    /// （重いほど・側近の目が多いほど隠しきれない）、死期が見えた政権の継承レース
    /// （後継が曖昧なほど醜く過熱し、明確なら鎮まる）、そして病床の権威
    /// （死にゆく英雄の言葉は重い＝遺言の効力）。「英雄の病は本人の悲劇であり国家の試験」。
    /// 分担：<see cref="LifecycleRules"/>＝加齢死亡（寿命カーブ＝緩やかな必然）／
    /// <see cref="SenescenceRules"/>＝能力の加齢曲線（誰にも来る下り坂）／本クラス＝突発的な
    /// 健康イベント（病に倒れる・隠す・漏れる・後継が争う）。倍率は基準能力に掛けて使う
    /// （実効値パターン・基準フィールド非破壊）。乱数は roll を引数で受ける決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class IllnessRules
    {
        /// <summary>
        /// 発症確率（0..maxOnsetChance）。基礎＋加齢リスク（開始年齢の超過×加齢率）を土台に、
        /// 激務（stress 0..1）で (1+stressWeight×stress) 倍、虚弱（constitution 0..1＝1で頑健）で
        /// (1+frailtyWeight×(1−constitution)) 倍に膨らむ＝高齢×激務×虚弱が重なるほど倒れやすい。
        /// 上限 maxOnsetChance で頭打ち（確実な発症はない）。
        /// </summary>
        public static float OnsetChance(float age, float stress, float constitution, IllnessParams p)
        {
            float a = Mathf.Max(0f, age);
            float s = Mathf.Clamp01(stress);
            float c = Mathf.Clamp01(constitution);
            float risk = p.baseOnsetChance + p.ageOnsetRate * Mathf.Max(0f, a - p.ageOnsetStart);
            risk *= 1f + p.stressWeight * s;
            risk *= 1f + p.frailtyWeight * (1f - c);
            return Mathf.Clamp(risk, 0f, p.maxOnsetChance);
        }

        public static float OnsetChance(float age, float stress, float constitution)
            => OnsetChance(age, stress, constitution, IllnessParams.Default);

        /// <summary>発症判定（決定論）。roll（0..1）が確率未満なら倒れる。roll=1 は決して発症しない。</summary>
        public static bool Strikes(float chance, float roll)
        {
            return Mathf.Clamp01(roll) < Mathf.Clamp01(chance);
        }

        /// <summary>
        /// 執務能力の低下倍率（minCapacity..1）。健康（severity=0）で1.0、重症度に比例して落ち、
        /// 死の床（severity=1）でも minCapacity は残る。基準能力に掛けて使う（実効値パターン＝
        /// 基準フィールドは書き換えない）＝病が癒えれば満額に戻る。
        /// </summary>
        public static float CapacityFactor(float severity, IllnessParams p)
        {
            float s = Mathf.Clamp01(severity);
            return Mathf.Lerp(1f, p.minCapacity, s);
        }

        public static float CapacityFactor(float severity) => CapacityFactor(severity, IllnessParams.Default);

        /// <summary>
        /// 隠蔽の維持（0..1）。隠蔽度は 重症度×崩れ速度×(1+側近重み×courtProximity) の速さで
        /// dt ぶん侵食される＝病が重いほど・側近の目（courtProximity 0..1）が多いほど隠しきれない。
        /// severity=0 なら隠すものがない＝崩れない。0で下げ止まる（隠蔽の完全崩壊）。
        /// </summary>
        public static float ConcealmentTick(float concealment, float severity, float courtProximity,
                                            float dt, IllnessParams p)
        {
            float conceal = Mathf.Clamp01(concealment);
            float sev = Mathf.Clamp01(severity);
            float prox = Mathf.Clamp01(courtProximity);
            float t = Mathf.Max(0f, dt);
            float decay = p.concealDecayRate * sev * (1f + p.proximityWeight * prox);
            return Mathf.Max(0f, conceal - decay * t);
        }

        public static float ConcealmentTick(float concealment, float severity, float courtProximity, float dt)
            => ConcealmentTick(concealment, severity, courtProximity, dt, IllnessParams.Default);

        /// <summary>
        /// 漏洩の衝撃（0..1）＝重症度×要人度（position 0..1）。一兵卒の病は誰も揺らさず、
        /// 皇帝の不治（severity=1×position=1）は国家を最大限に揺らす。
        /// 支持・安定度・市場（#113/#109/#179）への外生ショックとして使う想定。
        /// </summary>
        public static float LeakImpact(float severity, float position)
        {
            return Mathf.Clamp01(severity) * Mathf.Clamp01(position);
        }

        /// <summary>
        /// 継承レースの過熱（0..1）。死期が見える（perceivedSeverity＝周囲が知覚する重症度。隠蔽が
        /// 効いていれば実際より低い）ほど・後継が曖昧（heirClarity 0..1＝1で明確）なほど
        /// raceRiseRate で過熱し、後継が明確なら raceCalmRate×heirClarity で鎮まる＝
        /// 遺言・立太子がレースを鎮める唯一の手。dt ぶん進めて 0..1 にクランプ。
        /// </summary>
        public static float SuccessionRaceTick(float raceIntensity, float perceivedSeverity,
                                               float heirClarity, float dt, IllnessParams p)
        {
            float race = Mathf.Clamp01(raceIntensity);
            float sev = Mathf.Clamp01(perceivedSeverity);
            float clarity = Mathf.Clamp01(heirClarity);
            float t = Mathf.Max(0f, dt);
            float delta = p.raceRiseRate * sev * (1f - clarity) - p.raceCalmRate * clarity;
            return Mathf.Clamp01(race + delta * t);
        }

        public static float SuccessionRaceTick(float raceIntensity, float perceivedSeverity,
                                               float heirClarity, float dt)
            => SuccessionRaceTick(raceIntensity, perceivedSeverity, heirClarity, dt, IllnessParams.Default);

        /// <summary>
        /// 病床の権威（1..1+maxDeathbedBonus）＝遺言の効力倍率。閾値 deathbedThreshold までは平時の
        /// 重み（1.0）、死期が見えるほど言葉は重くなり、死の床（severity=1）で最大＝死にゆく英雄の
        /// 一言が後継を立て、レースを鎮め、国を縛る。正統性・継承（<see cref="SuccessionRules"/>）への
        /// 倍率として掛けて使う（実効値パターン）。
        /// </summary>
        public static float DeathbedAuthority(float severity, IllnessParams p)
        {
            float s = Mathf.Clamp01(severity);
            if (s <= p.deathbedThreshold) return 1f;
            float depth = (s - p.deathbedThreshold) / (1f - p.deathbedThreshold);
            return 1f + p.maxDeathbedBonus * depth;
        }

        public static float DeathbedAuthority(float severity) => DeathbedAuthority(severity, IllnessParams.Default);
    }
}
