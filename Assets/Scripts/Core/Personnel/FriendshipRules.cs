using UnityEngine;

namespace Ginei
{
    /// <summary>盟友（友情・僚友の絆）の調整係数（盟友システム）。</summary>
    public readonly struct FriendshipParams
    {
        /// <summary>平時の紐帯成長率（per dt・1=完全な信頼へ向けてゆっくり深まる速度）。</summary>
        public readonly float bondGrowthBase;
        /// <summary>修羅場による成長加速倍率（sharedTrials=1 で成長率が (1+この値) 倍）。</summary>
        public readonly float trialMultiplier;
        /// <summary>紐帯→共同作戦ボーナスの最大幅（あうんの呼吸）。</summary>
        public readonly float jointBonusScale;
        /// <summary>紐帯→喪失悲嘆の最大深さ（深い紐帯ほど急所）。</summary>
        public readonly float griefScale;
        /// <summary>悲嘆の回復速度（per dt・喪失の傷はゆっくりとしか癒えない）。</summary>
        public readonly float griefRecoveryRate;
        /// <summary>悲嘆のうち決して癒えない残滓の比率（0..1・キルヒアイス喪失型＝完全には戻らない）。</summary>
        public readonly float griefResidualRatio;
        /// <summary>紐帯による反目耐性（0..1・深い友情は裂け目に抗うが、完全には防げない）。</summary>
        public readonly float estrangementResist;

        public FriendshipParams(float bondGrowthBase, float trialMultiplier, float jointBonusScale,
                                float griefScale, float griefRecoveryRate, float griefResidualRatio,
                                float estrangementResist)
        {
            this.bondGrowthBase = Mathf.Max(0f, bondGrowthBase);
            this.trialMultiplier = Mathf.Max(0f, trialMultiplier);
            this.jointBonusScale = Mathf.Max(0f, jointBonusScale);
            this.griefScale = Mathf.Max(0f, griefScale);
            this.griefRecoveryRate = Mathf.Max(0f, griefRecoveryRate);
            this.griefResidualRatio = Mathf.Clamp01(griefResidualRatio);
            this.estrangementResist = Mathf.Clamp01(estrangementResist);
        }

        /// <summary>既定＝成長0.02/修羅場×4/共同0.3/悲嘆0.6/回復0.01/残滓0.25/耐性0.6。</summary>
        public static FriendshipParams Default => new FriendshipParams(0.02f, 4f, 0.3f, 0.6f, 0.01f, 0.25f, 0.6f);
    }

    /// <summary>
    /// 盟友の純ロジック（キルヒアイス/双璧型の個人的紐帯）。深い信頼の僚友は共同作戦で「あうんの呼吸」の
    /// ボーナスを生み、喪失は深い紐帯ほど深い悲嘆（長期の能力・士気ペナルティ）となり、野心の差と政治圧力は
    /// 深い友情すら裂きうる（反目）。「深い紐帯は力であり急所」＝共同ボーナスと喪失悲嘆をともに紐帯の二乗で
    /// スケールさせ、絆を深めるほど得るものも失うものも大きくする。
    /// <see cref="CommandStaffRules"/>（副提督/参謀＝職制上の補佐・配置と能力反映）とは別系統＝こちらは
    /// 役職に依らない個人の友情を扱う。乱数なし決定論・全入力クランプ・基準値非破壊（実効値パターン）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FriendshipRules
    {
        /// <summary>
        /// 紐帯の時間発展（0..1）。共有体験で深まる：平時（sharedTrials=0）は bondGrowthBase でゆっくり、
        /// 共に潜った修羅場（sharedTrials 0..1）が濃いほど (1 + trialMultiplier×sharedTrials) 倍で速く深まる。
        /// </summary>
        public static float BondTick(float bond, float sharedTrials, float dt, FriendshipParams p)
        {
            float b = Mathf.Clamp01(bond);
            float trials = Mathf.Clamp01(sharedTrials);
            float rate = p.bondGrowthBase * (1f + p.trialMultiplier * trials);
            return Mathf.MoveTowards(b, 1f, rate * Mathf.Max(0f, dt));
        }

        public static float BondTick(float bond, float sharedTrials, float dt)
            => BondTick(bond, sharedTrials, dt, FriendshipParams.Default);

        /// <summary>
        /// 共同作戦の調整ボーナス（0..jointBonusScale）。あうんの呼吸＝通信を超える連携。紐帯の二乗で増える
        /// ＝浅い知己はほぼ無益、深い盟友（双璧）で初めて真価が出る。
        /// </summary>
        public static float JointOperationBonus(float bond, FriendshipParams p)
        {
            float b = Mathf.Clamp01(bond);
            return b * b * p.jointBonusScale;
        }

        public static float JointOperationBonus(float bond) => JointOperationBonus(bond, FriendshipParams.Default);

        /// <summary>
        /// 盟友喪失の悲嘆（0..griefScale）。紐帯の二乗でスケール＝深い紐帯ほど不釣り合いに深い痛手
        /// （キルヒアイス喪失型＝長期の能力・士気ペナルティの初期値として使う）。
        /// </summary>
        public static float LossGrief(float bond, FriendshipParams p)
        {
            float b = Mathf.Clamp01(bond);
            return b * b * p.griefScale;
        }

        public static float LossGrief(float bond) => LossGrief(bond, FriendshipParams.Default);

        /// <summary>悲嘆の癒えない残滓（下限）。喪失時の紐帯から「決して戻らない」床を出す。</summary>
        public static float GriefFloor(float bondAtLoss, FriendshipParams p)
            => LossGrief(bondAtLoss, p) * p.griefResidualRatio;

        public static float GriefFloor(float bondAtLoss) => GriefFloor(bondAtLoss, FriendshipParams.Default);

        /// <summary>
        /// 悲嘆の遅い回復。floor（既定0・<see cref="GriefFloor"/> を渡せば完全には戻らない）へ向けて
        /// griefRecoveryRate×dt で漸減する。floor を下回らない（MoveTowards）。
        /// </summary>
        public static float GriefRecoveryTick(float grief, float dt, FriendshipParams p, float floor = 0f)
        {
            float g = Mathf.Clamp01(grief);
            float fl = Mathf.Clamp01(floor);
            if (g <= fl) return g;
            return Mathf.MoveTowards(g, fl, p.griefRecoveryRate * Mathf.Max(0f, dt));
        }

        public static float GriefRecoveryTick(float grief, float dt)
            => GriefRecoveryTick(grief, dt, FriendshipParams.Default);

        /// <summary>
        /// 反目リスク（0..1・双璧の悲劇型）。裂け目の力＝野心の差(ambitionGap 0..1)×政治圧力(politicalPressure 0..1)
        /// の積（どちらか一方だけでは裂けない）。深い紐帯は estrangementResist の割合まで抗うが、耐性は1未満
        /// ＝最深の友情でも野心と政治がともに極まれば裂けうる。
        /// </summary>
        public static float EstrangementRisk(float bond, float ambitionGap, float politicalPressure, FriendshipParams p)
        {
            float b = Mathf.Clamp01(bond);
            float strain = Mathf.Clamp01(ambitionGap) * Mathf.Clamp01(politicalPressure);
            return Mathf.Clamp01(strain * (1f - b * p.estrangementResist));
        }

        public static float EstrangementRisk(float bond, float ambitionGap, float politicalPressure)
            => EstrangementRisk(bond, ambitionGap, politicalPressure, FriendshipParams.Default);
    }
}
