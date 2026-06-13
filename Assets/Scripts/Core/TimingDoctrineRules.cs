using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 後の先ドクトリン＝武道・五輪書の「先（せん）」の三つの機（#1379）。
    /// ①先の先＝敵より先に仕掛ける（先制）／②対の先＝敵の動き出しと同時に動く（相討ち的な機）／
    /// ③後の先＝敵が仕掛けた直後の隙を打つ（カウンター＝受けて勝つ）。
    /// 特に「後の先」は敵に先に動かせてその隙を捉える反攻型の構え。
    /// </summary>
    public enum TimingDoctrine
    {
        /// <summary>先の先＝敵より先に仕掛ける先制の機。</summary>
        先の先,
        /// <summary>対の先＝敵の動き出しと同時に動く相討ち的な機。</summary>
        対の先,
        /// <summary>後の先＝敵が仕掛けた直後の隙を打つカウンターの機（受けて勝つ）。</summary>
        後の先,
    }

    /// <summary>
    /// 後の先ドクトリン＝武道「先」の三つの機の純ロジック（GRN-2 #1379・五輪書/武道）。
    /// 「先には先の先・対の先・後の先の三つの機があり、特に後の先は敵に先に動かせてその隙を捉える
    /// 反攻型の構え＝受けて勝つ」を式に出す。各機の主導権の取り方（<see cref="InitiativeValue"/>）、
    /// 敵が仕掛けて隙を見せた瞬間の反撃の窓（<see cref="CounterWindow"/>）、後の先の反撃が決まると
    /// 効果が大きい（<see cref="GoNoSenBonus"/>）、先の先は読まれると逆に隙になる（<see cref="SenNoSenRisk"/>）、
    /// 対の先は同時に動く相討ちの機（<see cref="TaiNoSenSynchrony"/>）、AIの反攻型/先制型の傾き
    /// （<see cref="ReactiveAIBias"/>）、敵を誘って攻めさせる釣り（<see cref="BaitEffectiveness"/>）、
    /// 後の先の構えが機能している判定（<see cref="IsCounterPosture"/>）を返す。
    /// 分担：<see cref="FleetDoctrineRules"/> は海軍戦略思想（決戦/漸減/通商破壊/現存艦隊）の艦隊運用、
    /// <see cref="BattleRhythmRules"/>（同EPIC GRN）は会戦の拍子（テンポ・リズム）、
    /// <see cref="AmbushRules"/> は伏兵・奇襲（秘匿と警戒の綱引き）、
    /// <see cref="DeterrenceRules"/> は抑止（報復能力×信憑性）を担う。
    /// ここは「後の先（カウンター）の構え＝反攻型 AI 補正」のみ。
    /// 全入力クランプ・乱数なし決定論。倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TimingDoctrineRules
    {
        /// <summary>
        /// 各機の主導権の取り方（0..1）＝自軍が先に主導権を握りにいく度合い。
        /// 先の先＝先制（高・自軍の即応度に比例して先んじる）／対の先＝中庸（敵と同時＝半分主導）／
        /// 後の先＝待ち（低・敵に先に動かせてから打つ＝主導権はあえて譲る）。
        /// ownReadiness＝自軍の即応度(0..1)。先の先は即応度が高いほど先んじられる。
        /// </summary>
        public static float InitiativeValue(TimingDoctrine doctrine, float ownReadiness, TimingDoctrineParams p)
        {
            float readiness = Mathf.Clamp01(ownReadiness);
            switch (doctrine)
            {
                case TimingDoctrine.先の先:
                    // 先制＝即応度が高いほど主導権を握る。
                    return Mathf.Clamp01(p.SenInitiativeBase + (1f - p.SenInitiativeBase) * readiness);
                case TimingDoctrine.対の先:
                    // 同時＝中庸の主導（半分は譲る）。
                    return Mathf.Clamp01(p.TaiInitiative);
                case TimingDoctrine.後の先:
                default:
                    // 待ち＝あえて主導権を譲り、敵が動いた隙を打つ（即応度は受けの安定に薄く効く）。
                    return Mathf.Clamp01(p.GoInitiativeBase * readiness);
            }
        }

        public static float InitiativeValue(TimingDoctrine doctrine, float ownReadiness)
            => InitiativeValue(doctrine, ownReadiness, TimingDoctrineParams.Default);

        /// <summary>
        /// 反撃の窓（0..1）＝敵が仕掛けて隙を見せた瞬間に開く反撃のチャンス（後の先の核）。
        /// 敵が深く攻めに出るほど（enemyCommitment 高）、かつ自軍が受けの構えを保てているほど（ownReadiness 高）
        /// 大きく開く＝敵が攻めに出た直後の隙。両者の積で「敵が動いてこそ初めて窓が開く」を出す。
        /// enemyCommitment＝敵の攻め込み度(0..1)、ownReadiness＝自軍の即応度(0..1)。
        /// </summary>
        public static float CounterWindow(float enemyCommitment, float ownReadiness, TimingDoctrineParams p)
        {
            float commit = Mathf.Clamp01(enemyCommitment);
            float readiness = Mathf.Clamp01(ownReadiness);
            // 敵が動かなければ（commit=0）窓は開かない＝受けて勝つには相手に先に動かせる必要がある。
            return Mathf.Clamp01(commit * (p.WindowReadinessFloor + (1f - p.WindowReadinessFloor) * readiness));
        }

        public static float CounterWindow(float enemyCommitment, float ownReadiness)
            => CounterWindow(enemyCommitment, ownReadiness, TimingDoctrineParams.Default);

        /// <summary>
        /// 後の先のボーナス倍率（≥1.0）＝反撃が決まると効果が大きい（受けて勝つ＝敵の勢いを利用）。
        /// 反撃の窓 counterWindow(0..1) が大きく、受けの構え defensivePosture(0..1) が固いほど跳ねる。
        /// 敵の勢いを利用するため、防御の構えがボーナスを底上げする。基準値に掛けて使う。
        /// </summary>
        public static float GoNoSenBonus(float counterWindow, float defensivePosture, TimingDoctrineParams p)
        {
            float window = Mathf.Clamp01(counterWindow);
            float posture = Mathf.Clamp01(defensivePosture);
            // 構えの分だけ反撃倍率の伸びしろが増す＝受けて勝つ。
            float gain = window * (p.GoBonusBase + p.GoPostureWeight * posture);
            return 1f + Mathf.Max(0f, gain);
        }

        public static float GoNoSenBonus(float counterWindow, float defensivePosture)
            => GoNoSenBonus(counterWindow, defensivePosture, TimingDoctrineParams.Default);

        /// <summary>
        /// 先の先のリスク（0..1）＝先制は読まれると逆に隙になる（先走りの危険）。
        /// 先の先以外の機ではリスク0（先制していないので先走りの隙はない）。
        /// 先の先のとき、敵の即応度 enemyReadiness が高いほど読まれて隙になるリスクが上がる。
        /// </summary>
        public static float SenNoSenRisk(TimingDoctrine doctrine, float enemyReadiness, TimingDoctrineParams p)
        {
            if (doctrine != TimingDoctrine.先の先) return 0f;
            // 敵が備えているほど先制が読まれ、空振りの隙になる。
            return Mathf.Clamp01(p.SenRiskBase + (1f - p.SenRiskBase) * Mathf.Clamp01(enemyReadiness));
        }

        public static float SenNoSenRisk(TimingDoctrine doctrine, float enemyReadiness)
            => SenNoSenRisk(doctrine, enemyReadiness, TimingDoctrineParams.Default);

        /// <summary>
        /// 対の先の同期度（0..1）＝敵と同時に動く相討ち的な機。タイミングが合えば相手を制す。
        /// 自軍の起動タイミング timing(0..1) と敵の起動タイミング enemyTiming(0..1) が近いほど同期は高い
        /// （差が0で1＝完全同期、差が大きいほど落ちる）。同期が高いほど相討ちで先んじて制せる。
        /// </summary>
        public static float TaiNoSenSynchrony(float timing, float enemyTiming, TimingDoctrineParams p)
        {
            float diff = Mathf.Abs(Mathf.Clamp01(timing) - Mathf.Clamp01(enemyTiming));
            // タイミングのズレに比例して同期が落ちる（鋭さで調整）。
            return Mathf.Clamp01(1f - p.SyncSharpness * diff);
        }

        public static float TaiNoSenSynchrony(float timing, float enemyTiming)
            => TaiNoSenSynchrony(timing, enemyTiming, TimingDoctrineParams.Default);

        /// <summary>
        /// AIの反攻型/先制型の行動傾き（-1..+1）＝後の先＝守って反撃する反攻型の AI 補正。
        /// 負＝先制型（先の先＝先んじて攻める）／0付近＝中庸（対の先＝同時）／正＝反攻型（後の先＝受けて反撃）。
        /// 後の先は敵に先に動かせるよう守りに構える AI へ傾ける。
        /// </summary>
        public static float ReactiveAIBias(TimingDoctrine doctrine, TimingDoctrineParams p)
        {
            switch (doctrine)
            {
                case TimingDoctrine.先の先: return -p.AIBiasMagnitude;
                case TimingDoctrine.対の先: return 0f;
                case TimingDoctrine.後の先:
                default: return p.AIBiasMagnitude;
            }
        }

        public static float ReactiveAIBias(TimingDoctrine doctrine)
            => ReactiveAIBias(doctrine, TimingDoctrineParams.Default);

        /// <summary>
        /// 誘いの効果（0..1）＝後の先は敵を誘って攻めさせる釣りが効く（わざと隙を見せて誘う）。
        /// 自軍が誘いの構え counterPosture(0..1) を取り、敵の攻撃性 enemyAggression(0..1) が高いほど
        /// 釣られて攻めに出る＝両者の積。攻めっ気のない敵は誘いに乗らない。
        /// </summary>
        public static float BaitEffectiveness(float counterPosture, float enemyAggression, TimingDoctrineParams p)
        {
            float posture = Mathf.Clamp01(counterPosture);
            float aggression = Mathf.Clamp01(enemyAggression);
            return Mathf.Clamp01(posture * aggression * p.BaitGain);
        }

        public static float BaitEffectiveness(float counterPosture, float enemyAggression)
            => BaitEffectiveness(counterPosture, enemyAggression, TimingDoctrineParams.Default);

        /// <summary>
        /// 後の先の反攻の構えが機能している判定。後の先のドクトリンで、かつ反撃の窓 counterWindow が
        /// 閾値 threshold 以上に開いているとき＝true（敵に先に動かせて隙を捉えられる状態）。
        /// 後の先以外の機では常に false（カウンターの構えではない）。
        /// </summary>
        public static bool IsCounterPosture(TimingDoctrine doctrine, float counterWindow, float threshold)
        {
            if (doctrine != TimingDoctrine.後の先) return false;
            return Mathf.Clamp01(counterWindow) >= Mathf.Clamp01(threshold);
        }
    }

    /// <summary>
    /// TimingDoctrineRules の調整値（#1379・マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// 各機の主導権の取り方・反撃の窓・後の先のボーナス・先の先のリスク・対の先の同期・AI傾き・誘いの係数を持つ。
    /// </summary>
    public readonly struct TimingDoctrineParams
    {
        /// <summary>先の先の主導権の基礎値（即応度0でもこの程度は先んじる）。</summary>
        public readonly float SenInitiativeBase;
        /// <summary>対の先の主導権（中庸＝半分譲る）。</summary>
        public readonly float TaiInitiative;
        /// <summary>後の先の主導権の基礎係数（待ちのため低い）。</summary>
        public readonly float GoInitiativeBase;

        /// <summary>反撃の窓で即応度が低くても確保される下限の伸び（受けの安定の基礎）。</summary>
        public readonly float WindowReadinessFloor;

        /// <summary>後の先ボーナスの基礎ゲイン（窓に掛かる）。</summary>
        public readonly float GoBonusBase;
        /// <summary>後の先ボーナスで防御の構えが追加するゲイン（受けて勝つ＝敵の勢いを利用）。</summary>
        public readonly float GoPostureWeight;

        /// <summary>先の先のリスク基礎値（敵が無警戒でも先走りには下地のリスクがある）。</summary>
        public readonly float SenRiskBase;

        /// <summary>対の先の同期がタイミングのズレで落ちる鋭さ。</summary>
        public readonly float SyncSharpness;

        /// <summary>AI傾きの振れ幅（先制型=−/反攻型=＋の絶対値）。</summary>
        public readonly float AIBiasMagnitude;

        /// <summary>誘いの効果のゲイン（構え×攻撃性に掛かる）。</summary>
        public readonly float BaitGain;

        public TimingDoctrineParams(
            float senInitiativeBase, float taiInitiative, float goInitiativeBase,
            float windowReadinessFloor,
            float goBonusBase, float goPostureWeight,
            float senRiskBase,
            float syncSharpness,
            float aiBiasMagnitude,
            float baitGain)
        {
            SenInitiativeBase = senInitiativeBase;
            TaiInitiative = taiInitiative;
            GoInitiativeBase = goInitiativeBase;
            WindowReadinessFloor = windowReadinessFloor;
            GoBonusBase = goBonusBase;
            GoPostureWeight = goPostureWeight;
            SenRiskBase = senRiskBase;
            SyncSharpness = syncSharpness;
            AIBiasMagnitude = aiBiasMagnitude;
            BaitGain = baitGain;
        }

        /// <summary>
        /// 既定。先の先主導基礎0.6/対の先主導0.5/後の先主導基礎0.3、反撃の窓の即応下限0.4、
        /// 後の先ボーナス基礎0.4＋構え重み0.4（窓1.0×構え1.0で最大1.8倍）、先の先リスク基礎0.2、
        /// 対の先の同期鋭さ1.5、AI傾き0.7、誘いゲイン1.0。
        /// </summary>
        public static TimingDoctrineParams Default => new TimingDoctrineParams(
            senInitiativeBase: 0.6f, taiInitiative: 0.5f, goInitiativeBase: 0.3f,
            windowReadinessFloor: 0.4f,
            goBonusBase: 0.4f, goPostureWeight: 0.4f,
            senRiskBase: 0.2f,
            syncSharpness: 1.5f,
            aiBiasMagnitude: 0.7f,
            baitGain: 1.0f);
    }
}
