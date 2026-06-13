using UnityEngine;

namespace Ginei
{
    /// <summary>攻城の姿勢（プレイヤーの戦術選択）。強襲＝速いが血を流す／包囲＝遅いが損害小・敵の戦意を折る。</summary>
    public enum SiegePosture { 強襲, 包囲 }

    /// <summary>
    /// 攻城姿勢のトレードオフ（#131 惑星戦 第3段・プレイヤーの駆け引き・純ロジック・test-first）。
    /// <see cref="SiegePosture"/> ごとの倍率を一表で定める（<see cref="FormationTraitRules"/> 流の trait 表＝唯一の出所）。
    /// ・強襲：軌道制圧・地上侵攻・守備隊の物理損耗が速いが、包囲艦の被害が大きい（血を流して速攻）。
    /// ・包囲：被害は小さく軌道/地上の攻めは進まないが、<b>四面楚歌</b>（物理包囲×心理孤立）で守備隊の士気を削り、
    ///   閾値を割らせて<b>降伏</b>に追い込む（戦わずして落とす）。
    /// 損害/速度の絶対式は <see cref="SiegeAssaultRules"/>、二者の地上消耗は <see cref="GroundInvasionRules"/>、
    /// 士気崩壊の係数は <see cref="PsychologicalSiegeMoraleRules"/> が担い、本ルールは「姿勢の倍率」と
    /// その委譲だけを供給する（重複実装しない）。決定論・乱数なし。純ロジック（非 MonoBehaviour）。
    /// </summary>
    public static class SiegePostureRules
    {
        // ===== 姿勢ごとの倍率（trait 表・作者調整可）=====
        /// <summary>軌道制圧（S-AV）の速度倍率。包囲はほぼ攻めない。</summary>
        public static float SuppressMultiplier(SiegePosture p) => p == SiegePosture.強襲 ? 1f : 0.1f;
        /// <summary>地上侵略の進行倍率。包囲は地上強襲しない（0）＝降伏でしか落ちない。</summary>
        public static float InvadeMultiplier(SiegePosture p) => p == SiegePosture.強襲 ? 1.4f : 0f;
        /// <summary>守備隊の物理損耗倍率。包囲は頭数を削らない（0）。</summary>
        public static float GrindMultiplier(SiegePosture p) => p == SiegePosture.強襲 ? 1.5f : 0f;
        /// <summary>包囲艦が受ける被害倍率。強襲は血を流す／包囲はわずか。</summary>
        public static float CasualtyMultiplier(SiegePosture p) => p == SiegePosture.強襲 ? 1.6f : 0.15f;
        /// <summary>守備隊士気の崩壊倍率（四面楚歌）。包囲が主役。</summary>
        public static float MoraleErosionMultiplier(SiegePosture p) => p == SiegePosture.強襲 ? 0.1f : 0.4f;

        /// <summary>姿勢を切り替える（強襲↔包囲）。</summary>
        public static SiegePosture Toggle(SiegePosture p)
            => p == SiegePosture.強襲 ? SiegePosture.包囲 : SiegePosture.強襲;

        /// <summary>
        /// 四面楚歌による守備隊士気の崩壊（このtickの減少分・0..1）。物理包囲×心理孤立の崩壊加速度
        /// （<see cref="PsychologicalSiegeMoraleRules"/>）から戦意侵食を求め、姿勢倍率を掛ける。
        /// 包囲では大きく、強襲では小さい＝戦わずして折るのは包囲の道。
        /// </summary>
        public static float GarrisonMoraleErosion(float physicalEncirclement, float psychologicalIsolation,
                                                  SiegePosture posture, float dt,
                                                  PsychologicalSiegeMoraleParams mp)
        {
            if (dt <= 0f) return 0f;
            float accel = PsychologicalSiegeMoraleRules.MoraleCollapseAcceleration(physicalEncirclement, psychologicalIsolation, mp);
            float erosion = PsychologicalSiegeMoraleRules.WillToFightErosion(accel, dt, mp);
            return Mathf.Max(0f, erosion * MoraleErosionMultiplier(posture));
        }

        public static float GarrisonMoraleErosion(float physicalEncirclement, float psychologicalIsolation,
                                                  SiegePosture posture, float dt)
            => GarrisonMoraleErosion(physicalEncirclement, psychologicalIsolation, posture, dt, PsychologicalSiegeMoraleParams.Default);

        /// <summary>守備隊が降伏したか（士気が閾値以下＝四面楚歌で戦わずして崩れる）。</summary>
        public static bool GarrisonSurrendered(float garrisonMorale, float threshold)
            => Mathf.Clamp01(garrisonMorale) <= Mathf.Clamp01(threshold);
    }
}
