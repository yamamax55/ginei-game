using UnityEngine;

namespace Ginei
{
    /// <summary>心理的包囲（四面楚歌）の調整係数。</summary>
    public readonly struct PsychologicalSiegeMoraleParams
    {
        /// <summary>物理包囲×心理孤立の相乗係数（両方揃うと崩壊が一気に加速＝四面楚歌の核）。</summary>
        public readonly float synergyWeight;
        /// <summary>絶望が兵の間に伝播する速度（per dt・敗北主義の蔓延）。</summary>
        public readonly float contagionRate;
        /// <summary>戦意が崩壊で侵食される速度（per dt）。</summary>
        public readonly float willErosionRate;
        /// <summary>敵の心理戦（楚歌）が孤立感を煽る増幅率。</summary>
        public readonly float psyOpAmplification;
        /// <summary>四面楚歌（物理＋心理の包囲崩壊）と判定する加速度の既定閾値。</summary>
        public readonly float fourSidedThreshold;

        public PsychologicalSiegeMoraleParams(float synergyWeight, float contagionRate,
                                              float willErosionRate, float psyOpAmplification,
                                              float fourSidedThreshold)
        {
            this.synergyWeight = Mathf.Clamp01(synergyWeight);
            this.contagionRate = Mathf.Max(0f, contagionRate);
            this.willErosionRate = Mathf.Max(0f, willErosionRate);
            this.psyOpAmplification = Mathf.Max(0f, psyOpAmplification);
            this.fourSidedThreshold = Mathf.Clamp01(fourSidedThreshold);
        }

        /// <summary>既定＝相乗0.6・絶望伝播0.15・戦意侵食0.2・心理戦増幅1.5・四面楚歌閾値0.5。</summary>
        public static PsychologicalSiegeMoraleParams Default
            => new PsychologicalSiegeMoraleParams(0.6f, 0.15f, 0.2f, 1.5f, 0.5f);
    }

    /// <summary>
    /// 心理的包囲＝四面楚歌（『項羽と劉邦』型・KORY-5 #1419）の純ロジック。物理的な包囲に加えて、
    /// 心理的な孤立（四方から楚の歌が聞こえ味方が皆漢に降ったと悟る）が士気崩壊を加速する＝項羽の軍は
    /// 包囲され、夜に四面から楚歌を聞いて「漢はすでに楚を得たか」と絶望し、戦わずして崩れた。
    /// 物理包囲（退路の遮断）と心理孤立（味方の離反×絶望）が掛け合わさると崩壊が一気に加速し、絶望が
    /// 兵の間に伝播して自壊する。指導者のカリスマ次第で玉砕の決死（項羽の最後の奮戦）か潰走（兵の逃散）に分かれる。
    /// <see cref="EncirclementRules"/>（物理包囲の降伏確率＝入力源）、<c>PropagandaRules</c>/<c>PsyOpRules</c>
    /// （世論操作・心理作戦＝別EPIC ULW）、<c>FleetMorale</c>（士気＝Game層・ここが返す係数の消費側）とは別系統＝
    /// ここは「心理的孤立が士気崩壊を加速する四面楚歌」の係数算出のみ。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PsychologicalSiegeMoraleRules
    {
        /// <summary>
        /// 物理的包囲度（0..1）＝包囲された度合い surroundedFraction（高いほど包囲）×退路の無さ。
        /// escapeRoutes は脱出路の開き具合（0..1・1=開けている）＝(1−escapeRoutes) で退路の遮断を掛ける。
        /// <see cref="EncirclementRules.Coverage"/> 等の結果を入力に取れる。
        /// </summary>
        public static float PhysicalEncirclement(float surroundedFraction, float escapeRoutes)
        {
            return Mathf.Clamp01(surroundedFraction) * (1f - Mathf.Clamp01(escapeRoutes));
        }

        /// <summary>
        /// 心理的孤立（0..1）＝味方の離反 alliesDefected と絶望感 perceivedHopelessness の相乗
        /// （四面楚歌＝「味方が皆降った」という認識）。両方が高いほど孤立が深い。
        /// </summary>
        public static float PsychologicalIsolation(float alliesDefected, float perceivedHopelessness)
        {
            float defected = Mathf.Clamp01(alliesDefected);
            float hopeless = Mathf.Clamp01(perceivedHopelessness);
            // 相加平均に相乗ボーナスを足す＝どちらか一方では孤立しきらず、両方で跳ねる。
            return Mathf.Clamp01((defected + hopeless) * 0.5f + defected * hopeless * 0.5f);
        }

        /// <summary>
        /// 士気崩壊の加速度（0..1）＝物理包囲と心理孤立の掛け合わせ（核）。物理だけ・心理だけでは緩やかだが、
        /// 両方が揃うと相乗で一気に崩れる＝四面楚歌。base＝両者の平均、それに積（synergyWeight）を上乗せ。
        /// </summary>
        public static float MoraleCollapseAcceleration(float physicalEncirclement, float psychologicalIsolation,
                                                       PsychologicalSiegeMoraleParams p)
        {
            float phys = Mathf.Clamp01(physicalEncirclement);
            float psych = Mathf.Clamp01(psychologicalIsolation);
            float baseAccel = (phys + psych) * 0.5f * (1f - p.synergyWeight);
            float synergy = phys * psych * p.synergyWeight;
            return Mathf.Clamp01(baseAccel + synergy);
        }

        public static float MoraleCollapseAcceleration(float physicalEncirclement, float psychologicalIsolation)
            => MoraleCollapseAcceleration(physicalEncirclement, psychologicalIsolation, PsychologicalSiegeMoraleParams.Default);

        /// <summary>
        /// 絶望の伝播（per dt の増分・0..1）＝孤立度 isolationLevel に比例して絶望が兵の間に広がる
        /// （敗北主義の蔓延＝戦わずして崩れる）。呼び出し側が既存の絶望に加算する想定。
        /// </summary>
        public static float DespairContagion(float isolationLevel, float dt, PsychologicalSiegeMoraleParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(isolationLevel) * p.contagionRate * Mathf.Max(0f, dt));
        }

        public static float DespairContagion(float isolationLevel, float dt)
            => DespairContagion(isolationLevel, dt, PsychologicalSiegeMoraleParams.Default);

        /// <summary>
        /// 敵の心理戦（楚歌＝「お前の味方は皆降った」）の効果（0..1）＝敵の発信 enemyMessaging ×
        /// 受け手の脆弱性 vulnerability ×増幅率。孤立感を煽り心理孤立を底上げする。
        /// </summary>
        public static float EnemyPsyOpEffect(float enemyMessaging, float vulnerability, PsychologicalSiegeMoraleParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(enemyMessaging) * Mathf.Clamp01(vulnerability) * p.psyOpAmplification);
        }

        public static float EnemyPsyOpEffect(float enemyMessaging, float vulnerability)
            => EnemyPsyOpEffect(enemyMessaging, vulnerability, PsychologicalSiegeMoraleParams.Default);

        /// <summary>
        /// 戦意の侵食（per dt の減少分・0..1）＝崩壊加速度 moraleCollapseAcceleration に比例して戦意が時間で崩れる
        /// （包囲された軍の自壊）。呼び出し側が既存の戦意から差し引く想定。
        /// </summary>
        public static float WillToFightErosion(float moraleCollapseAcceleration, float dt, PsychologicalSiegeMoraleParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(moraleCollapseAcceleration) * p.willErosionRate * Mathf.Max(0f, dt));
        }

        public static float WillToFightErosion(float moraleCollapseAcceleration, float dt)
            => WillToFightErosion(moraleCollapseAcceleration, dt, PsychologicalSiegeMoraleParams.Default);

        /// <summary>
        /// 玉砕（決死）か潰走かの分岐（−1..+1）。+1=玉砕の決死（項羽の最後の奮戦）／−1=潰走（兵の逃散）。
        /// 包囲が固いほど追い詰められて決死へ傾くが、指導者のカリスマ leaderCharisma が低いと潰走へ崩れる。
        /// = (カリスマ×包囲) と (1−カリスマ) の綱引き＝カリスマが結束を保つ間だけ玉砕に転じる。
        /// </summary>
        public static float LastStandOrRout(float physicalEncirclement, float leaderCharisma)
        {
            float phys = Mathf.Clamp01(physicalEncirclement);
            float charisma = Mathf.Clamp01(leaderCharisma);
            float stand = charisma * phys;           // カリスマある指導者＋追い詰められ＝決死
            float rout = (1f - charisma) * phys;      // カリスマ無し＋追い詰められ＝潰走
            return Mathf.Clamp(stand - rout, -1f, 1f);
        }

        /// <summary>
        /// 四面楚歌か＝物理包囲＋心理孤立による崩壊加速度が閾値以上。物理だけ・心理だけでは満たさず、
        /// 両者が揃って初めて成立する（味方が皆降ったという絶望が物理包囲に加わって崩壊する状態）。
        /// </summary>
        public static bool IsFourSidedSiege(float physicalEncirclement, float psychologicalIsolation,
                                            float threshold, PsychologicalSiegeMoraleParams p)
        {
            float accel = MoraleCollapseAcceleration(physicalEncirclement, psychologicalIsolation, p);
            return accel >= Mathf.Clamp01(threshold);
        }

        public static bool IsFourSidedSiege(float physicalEncirclement, float psychologicalIsolation, float threshold)
            => IsFourSidedSiege(physicalEncirclement, psychologicalIsolation, threshold, PsychologicalSiegeMoraleParams.Default);

        public static bool IsFourSidedSiege(float physicalEncirclement, float psychologicalIsolation)
            => IsFourSidedSiege(physicalEncirclement, psychologicalIsolation,
                                PsychologicalSiegeMoraleParams.Default.fourSidedThreshold,
                                PsychologicalSiegeMoraleParams.Default);
    }
}
