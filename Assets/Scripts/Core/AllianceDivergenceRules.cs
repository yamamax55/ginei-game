using UnityEngine;

namespace Ginei
{
    /// <summary>連合内の隠れた目標乖離（スペイン内戦型）の純データ。</summary>
    public struct AllianceDivergence
    {
        /// <summary>共通の敵への優先度（0..1・高いほど対外戦に集中）。</summary>
        public float sharedEnemyPriority;
        /// <summary>戦後の目標の対立（0..1・高いほど主導権・取り分で食い違う）。</summary>
        public float postwarConflict;
        /// <summary>内部抗争（0..1・連合内のパートナー同士の足の引っ張り合い）。</summary>
        public float internalRivalry;

        public AllianceDivergence(float sharedEnemyPriority, float postwarConflict, float internalRivalry)
        {
            this.sharedEnemyPriority = Mathf.Clamp01(sharedEnemyPriority);
            this.postwarConflict = Mathf.Clamp01(postwarConflict);
            this.internalRivalry = Mathf.Clamp01(internalRivalry);
        }
    }

    /// <summary>連合内の隠れた目標乖離（スペイン内戦型）の調整係数。</summary>
    public readonly struct AllianceDivergenceParams
    {
        /// <summary>共通の敵が結束を強いる重み（外敵が強いほど内部対立を抑える）。</summary>
        public readonly float threatUnityWeight;
        /// <summary>勝利接近で内部抗争が激化する速度（戦後が見えると主導権争いが燃える）。</summary>
        public readonly float rivalryIgniteRate;
        /// <summary>内部抗争が対外戦遂行を蝕む重み（味方同士の足の引っ張りが戦力を空費）。</summary>
        public readonly float warDrainWeight;
        /// <summary>先制粛清の強さ（戦後を見据えて一派が他派を先に潰す）。</summary>
        public readonly float purgeWeight;
        /// <summary>敵の弱化で連合が分裂するリスク係数（外圧が消えるとバラける）。</summary>
        public readonly float fractureWeight;

        public AllianceDivergenceParams(float threatUnityWeight, float rivalryIgniteRate,
            float warDrainWeight, float purgeWeight, float fractureWeight)
        {
            this.threatUnityWeight = Mathf.Clamp01(threatUnityWeight);
            this.rivalryIgniteRate = Mathf.Max(0f, rivalryIgniteRate);
            this.warDrainWeight = Mathf.Clamp01(warDrainWeight);
            this.purgeWeight = Mathf.Clamp01(purgeWeight);
            this.fractureWeight = Mathf.Clamp01(fractureWeight);
        }

        /// <summary>既定＝脅威結束重み0.7・抗争点火速度0.5・戦争侵食重み0.6・先制粛清0.5・分裂係数0.8
        /// （共通の敵が強いほど団結し、勝利が近づくと抗争が燃え、敵が弱まると戦後を巡って割れる）。</summary>
        public static AllianceDivergenceParams Default => new AllianceDivergenceParams(0.7f, 0.5f, 0.6f, 0.5f, 0.8f);
    }

    /// <summary>
    /// 連合内の隠れた目標乖離（スペイン内戦型・#1398）の純ロジック＝連合内の戦後を見据えた目標の食い違い。
    /// 同じ陣営で戦う連合のパートナーは、表向きは共通の敵と戦いながら、内心では戦後を見据えた相反する
    /// 目標を抱える＝スペイン内戦の共和国側で共産党・アナキスト・社会主義者が戦後の主導権を争い、
    /// 対ファシスト戦と並行して内部抗争・粛清した。共通の敵が強いほど内部対立を抑えて団結するが、
    /// 勝利が近づく（戦後が見えてくる）ほど内部抗争が激化し、対外戦の遂行を蝕む（味方同士で足を引っ張る）。
    /// 一派は戦後を見据えて他派を先に潰し（内ゲバ・粛清）、敵が弱まると連合が分裂する。
    /// <see cref="BurdenSharingRules"/>（同盟の負担分担・ただ乗り）・<see cref="PartitionRules"/>（戦後分割
    /// ＝勝者間の領土分配）・<see cref="CoalitionRules"/>（連立＝政策希釈とキングメーカー）・
    /// <see cref="LoyaltyRules"/>（関ヶ原型＝戦う前の寝返り）とは別系統＝こちらは共通の敵と戦う連合内部の
    /// 戦後を見据えた目標の食い違いそのものを扱う（AllianceDivergence が中核データ）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AllianceDivergenceRules
    {
        /// <summary>
        /// 戦後の利益相反（0..1）＝各パートナーの戦後目標と共通目標の乖離度の平均。
        /// 戦後の取り分・主導権で目標が食い違うほど大きい＝共通の敵を倒した後の青写真が割れている。
        /// 配列 null/空は0（パートナーがいなければ相反もない）。手書きループ。
        /// </summary>
        public static float PostwarInterestConflict(float[] partnerGoals, float commonGoal)
        {
            if (partnerGoals == null || partnerGoals.Length == 0) return 0f;
            float common = Mathf.Clamp01(commonGoal);
            float sum = 0f;
            for (int i = 0; i < partnerGoals.Length; i++)
            {
                sum += Mathf.Abs(Mathf.Clamp01(partnerGoals[i]) - common);
            }
            return Mathf.Clamp01(sum / partnerGoals.Length);
        }

        /// <summary>
        /// 脅威下の団結（0..1）＝共通の敵が強いほど内部対立を抑えて団結する。
        /// 戦後の対立を、外敵の強さ×threatUnityWeight ぶんだけ割り引いて実効的な内部不和を返す
        /// （敵が強い＝結束、敵が弱る＝戦後対立が表に出てバラける）。
        /// </summary>
        public static float UnityUnderThreat(float sharedEnemyStrength, float postwarConflict, AllianceDivergenceParams p)
        {
            float threat = Mathf.Clamp01(sharedEnemyStrength);
            float conflict = Mathf.Clamp01(postwarConflict);
            // 外敵が強いほど抑制が効く＝戦後対立を (1 - 敵強さ×重み) 倍に圧縮。
            return Mathf.Clamp01(conflict * (1f - threat * p.threatUnityWeight));
        }

        public static float UnityUnderThreat(float sharedEnemyStrength, float postwarConflict)
            => UnityUnderThreat(sharedEnemyStrength, postwarConflict, AllianceDivergenceParams.Default);

        /// <summary>
        /// 内部抗争の時間発展（0..1）＝勝利が近づくほど（戦後が見えてくると）内部抗争が激化する。
        /// 戦後対立×勝利接近を目標値とし、rivalryIgniteRate×dt でそこへ近づける＝敵が倒れる前に
        /// 主導権を握ろうとする。勝利が遠い（victoryProximity→0）うちは抗争が育たない。
        /// </summary>
        public static float InternalRivalryTick(float internalRivalry, float postwarConflict,
            float victoryProximity, float dt, AllianceDivergenceParams p)
        {
            float current = Mathf.Clamp01(internalRivalry);
            float conflict = Mathf.Clamp01(postwarConflict);
            float proximity = Mathf.Clamp01(victoryProximity);
            float step = Mathf.Max(0f, dt);
            // 戦後対立と勝利接近の積が抗争の到達点＝両方そろって初めて燃える。
            float target = Mathf.Clamp01(conflict * proximity);
            if (target <= current) return current; // 抗争は接近で激化する一方向（鎮静はここでは扱わない）。
            return Mathf.Clamp01(Mathf.MoveTowards(current, target, p.rivalryIgniteRate * step));
        }

        public static float InternalRivalryTick(float internalRivalry, float postwarConflict,
            float victoryProximity, float dt)
            => InternalRivalryTick(internalRivalry, postwarConflict, victoryProximity, dt, AllianceDivergenceParams.Default);

        /// <summary>
        /// 対外戦遂行の侵食（0..1）＝内部抗争×warDrainWeight。味方同士で足を引っ張るほど対外戦に
        /// 振り向ける戦力が空費される＝連合は共通の敵に全力を出せない（スペイン共和国の自滅）。
        /// </summary>
        public static float WarEffortDrain(float internalRivalry, AllianceDivergenceParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(internalRivalry) * p.warDrainWeight);
        }

        public static float WarEffortDrain(float internalRivalry)
            => WarEffortDrain(internalRivalry, AllianceDivergenceParams.Default);

        /// <summary>
        /// 先制粛清（0..1）＝内部抗争×自派の勢力×purgeWeight。連合内の一派が戦後を見据えて他派を
        /// 先に潰す＝対ファシスト戦中の内ゲバ・粛清。抗争が高く、潰せるだけの勢力を持つ派ほど踏み切る
        /// （無力な派は抗争があっても粛清を仕掛けられない）。
        /// </summary>
        public static float PreemptivePurge(float internalRivalry, float factionPower, AllianceDivergenceParams p)
        {
            float rivalry = Mathf.Clamp01(internalRivalry);
            float power = Mathf.Clamp01(factionPower);
            return Mathf.Clamp01(rivalry * power * p.purgeWeight);
        }

        public static float PreemptivePurge(float internalRivalry, float factionPower)
            => PreemptivePurge(internalRivalry, factionPower, AllianceDivergenceParams.Default);

        /// <summary>
        /// 連合の結束度（0..1）＝共通の敵への集中×（1−内部抗争）。共通の敵に集中するほど強く、
        /// 内部抗争が高いほど脆い＝外への集中と内なる争いのせめぎ合い。
        /// </summary>
        public static float AllianceCohesion(float sharedEnemyPriority, float internalRivalry)
        {
            return Mathf.Clamp01(sharedEnemyPriority) * (1f - Mathf.Clamp01(internalRivalry));
        }

        /// <summary>
        /// 連合の分裂リスク（0..1）＝戦後対立×敵の弱化度×fractureWeight。敵が弱まるほど共通の脅威
        /// という接着剤が剥がれ、戦後を巡って連合が割れる＝両者の積（戦後対立がなければ、また敵が
        /// まだ強ければ、すぐには割れない）。
        /// </summary>
        public static float CoalitionFractureRisk(float postwarConflict, float sharedEnemyWeakening, AllianceDivergenceParams p)
        {
            float conflict = Mathf.Clamp01(postwarConflict);
            float weakening = Mathf.Clamp01(sharedEnemyWeakening);
            return Mathf.Clamp01(conflict * weakening * p.fractureWeight);
        }

        public static float CoalitionFractureRisk(float postwarConflict, float sharedEnemyWeakening)
            => CoalitionFractureRisk(postwarConflict, sharedEnemyWeakening, AllianceDivergenceParams.Default);

        /// <summary>
        /// 連合が内部から崩れつつあるか＝内部抗争が閾値を超え、かつ結束が（1−閾値）を下回る。
        /// 内ゲバが燃え、共通の敵への集中が失われたとき＝連合は外敵より先に内から崩れる。
        /// </summary>
        public static bool IsAllianceUnraveling(float internalRivalry, float allianceCohesion, float threshold)
        {
            float t = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(internalRivalry) > t && Mathf.Clamp01(allianceCohesion) < (1f - t);
        }
    }
}
