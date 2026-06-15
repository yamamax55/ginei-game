using UnityEngine;

namespace Ginei
{
    /// <summary>エスカレーション・コミットメント＝サンクコストへの固執（#1378）の調整係数。</summary>
    public readonly struct EscalationCommitmentParams
    {
        /// <summary>投じた犠牲の重み（investedLoss=1 で固執の重さがこの幅まで立ち上がる）。</summary>
        public readonly float investedLossWeight;
        /// <summary>面子・自己像の投資の重み（identityInvestment=1 で固執の重さがこの幅まで上乗せ＝面子がかかると止められない）。</summary>
        public readonly float identityWeight;
        /// <summary>合理的撤退価値が固執を緩める強さ（撤退すべき度合いがこの幅までロックを下げる）。</summary>
        public readonly float rationalPull;
        /// <summary>損の追い銭の最大幅（commitmentLock=1 で追加投入がこの幅まで膨らむ＝泥沼への追加投入）。</summary>
        public readonly float throwGoodScale;
        /// <summary>固執が時間で深まる速度（per dt・インパールの継続＝引き返せなくなる）。</summary>
        public readonly float spiralRate;
        /// <summary>公約が面子の駆動を押し上げる幅（publicCommitment=1 でこの幅まで撤退困難が増す）。</summary>
        public readonly float publicCommitScale;
        /// <summary>外的衝撃がロックを解除する強さ（externalShock=1 でこの幅までロックを崩す）。</summary>
        public readonly float shockBreak;
        /// <summary>指導者交代がロックを解除する強さ（newLeadership=1 でこの幅までロックを崩す＝損切りの決断）。</summary>
        public readonly float leadershipBreak;

        public EscalationCommitmentParams(float investedLossWeight, float identityWeight, float rationalPull,
                                          float throwGoodScale, float spiralRate, float publicCommitScale,
                                          float shockBreak, float leadershipBreak)
        {
            this.investedLossWeight = Mathf.Clamp01(investedLossWeight);
            this.identityWeight = Mathf.Clamp01(identityWeight);
            this.rationalPull = Mathf.Clamp01(rationalPull);
            this.throwGoodScale = Mathf.Max(0f, throwGoodScale);
            this.spiralRate = Mathf.Max(0f, spiralRate);
            this.publicCommitScale = Mathf.Clamp01(publicCommitScale);
            this.shockBreak = Mathf.Clamp01(shockBreak);
            this.leadershipBreak = Mathf.Clamp01(leadershipBreak);
        }

        /// <summary>
        /// 既定＝投下犠牲重み0.6・面子重み0.4・合理的撤退の引き0.6・追い銭幅0.5・スパイラル速度0.2・
        /// 公約幅0.5・外的衝撃解除0.7・指導者交代解除0.6。
        /// </summary>
        public static EscalationCommitmentParams Default => new EscalationCommitmentParams(
            0.6f, 0.4f, 0.6f, 0.5f, 0.2f, 0.5f, 0.7f, 0.6f);
    }

    /// <summary>
    /// エスカレーション・コミットメント＝サンクコストへの固執（#1378・失敗の本質/行動経済学）の純ロジック。
    /// すでに投じた犠牲（埋没費用＝サンクコスト）が大きいほど、面子・自己像がかかるほど固執の重さが増し
    /// （<see cref="SunkCostWeight"/>）、合理的には撤退すべき状況でも「ここで止めたら今までの犠牲が無駄になる」と
    /// 固執して投入を続ける（<see cref="CommitmentLock"/>＝サンクコストが撤退を縛るロック）。損を取り返そうとさらに
    /// 投入し傷を深め（<see cref="ThrowGoodAfterBad"/>＝泥沼への追加投入）、固執は時間で深まり引き返せなくなる
    /// （<see cref="EscalationSpiral"/>＝インパールの継続）。面子・公約が撤退を一層難しくし（<see cref="FacePreservationDrive"/>）、
    /// 合理的な撤退の好機を固執で逃す（<see cref="RationalExitForegone"/>＝損切りすべき時に動けない）。だが外的衝撃や
    /// 指導者交代がそのロックを解除する（<see cref="LockBreaker"/>＝誰かが損切りを決断＝崩壊解除）。損切り不能で泥沼に
    /// 陥ったかは <see cref="IsSunkCostTrap"/> で判定する。
    /// 分担：<see cref="EscalationRules"/>（紛争の梯子＝相互作用で段が昇る）／<see cref="CommitmentRules"/>（背水の陣＝
    /// 意図的なコミット・生成済み）／<see cref="MegaProjectRules"/>（埋没費用＝事業の途中放棄）／<see cref="WarPoliticsRules"/>
    /// （出兵の博打）とは別＝本クラスは「サンクコストへの固執（インパール型の損切り不能）」。
    /// 乱数なし・全入力クランプ・決定論・基準非破壊。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EscalationCommitmentRules
    {
        /// <summary>
        /// 固執の重さ（0..1）＝サンクコストの重み。投じた犠牲（investedLoss 0..1）×investedLossWeight と
        /// 面子・自己像の投資（identityInvestment 0..1）×identityWeight の和＝犠牲が大きく面子がかかるほど止められない。
        /// 既定重み（0.6/0.4）の和は1.0＝両者最大で固執の重さ1.0。
        /// </summary>
        public static float SunkCostWeight(float investedLoss, float identityInvestment, EscalationCommitmentParams p)
        {
            float loss = Mathf.Clamp01(investedLoss);
            float identity = Mathf.Clamp01(identityInvestment);
            return Mathf.Clamp01(p.investedLossWeight * loss + p.identityWeight * identity);
        }

        public static float SunkCostWeight(float investedLoss, float identityInvestment)
            => SunkCostWeight(investedLoss, identityInvestment, EscalationCommitmentParams.Default);

        /// <summary>
        /// コミットメントのロック（0..1）＝合理的には撤退すべきなのに固執して投入を続ける度合い。固執の重さ
        /// （sunkCostWeight 0..1）からスタートし、合理的な撤退価値（rationalExitValue 0..1＝今こそ撤退すべき度合い）が
        /// rationalPull ぶんロックを下げる＝撤退すべきほど本来はロックが緩むはずだが、サンクコストが撤退を縛るので
        /// 完全には緩まない。sunkCostWeight − rationalExitValue×rationalPull を下限0で返す。
        /// </summary>
        public static float CommitmentLock(float sunkCostWeight, float rationalExitValue, EscalationCommitmentParams p)
        {
            float sunk = Mathf.Clamp01(sunkCostWeight);
            float exit = Mathf.Clamp01(rationalExitValue);
            return Mathf.Clamp01(sunk - exit * p.rationalPull);
        }

        public static float CommitmentLock(float sunkCostWeight, float rationalExitValue)
            => CommitmentLock(sunkCostWeight, rationalExitValue, EscalationCommitmentParams.Default);

        /// <summary>
        /// 損の追い銭（0..1）＝損を取り返そうとさらに投入し傷を深める。コミットメントのロック（commitmentLock 0..1）が
        /// 強いほど、追加投入の余地（additionalInvestment 0..1）を泥沼へ注ぎ込む。commitmentLock×additionalInvestment×
        /// throwGoodScale を返す。ロックがゼロ（撤退できる）なら追加投入も0＝損切りできるなら追い銭はしない。
        /// </summary>
        public static float ThrowGoodAfterBad(float commitmentLock, float additionalInvestment, EscalationCommitmentParams p)
        {
            float lockv = Mathf.Clamp01(commitmentLock);
            float add = Mathf.Clamp01(additionalInvestment);
            return Mathf.Clamp01(lockv * add * p.throwGoodScale);
        }

        public static float ThrowGoodAfterBad(float commitmentLock, float additionalInvestment)
            => ThrowGoodAfterBad(commitmentLock, additionalInvestment, EscalationCommitmentParams.Default);

        /// <summary>
        /// 固執スパイラルの1tick後のロック（0..1）＝固執が時間で深まり引き返せなくなる（インパールの継続）。
        /// ロック（commitmentLock 0..1）が強いほど自己強化で深まる＝lock + lock×spiralRate×dt。ロックがゼロなら
        /// 深まらない（撤退できる状態は泥沼化しない）。上限1。
        /// </summary>
        public static float EscalationSpiral(float commitmentLock, float dt, EscalationCommitmentParams p)
        {
            float lockv = Mathf.Clamp01(commitmentLock);
            float t = Mathf.Max(0f, dt);
            return Mathf.Clamp01(lockv + lockv * p.spiralRate * t);
        }

        public static float EscalationSpiral(float commitmentLock, float dt)
            => EscalationSpiral(commitmentLock, dt, EscalationCommitmentParams.Default);

        /// <summary>
        /// 面子の駆動（0..1）＝面子・公約が撤退を一層難しくする（引けない理由＝自己正当化）。面子・自己像の投資
        /// （identityInvestment 0..1）が基底で、公の約束（publicCommitment 0..1）が publicCommitScale ぶん上乗せする
        /// ＝公言したぶん引っ込みがつかない。identityInvestment + publicCommitment×publicCommitScale を上限1で返す。
        /// </summary>
        public static float FacePreservationDrive(float identityInvestment, float publicCommitment, EscalationCommitmentParams p)
        {
            float identity = Mathf.Clamp01(identityInvestment);
            float pub = Mathf.Clamp01(publicCommitment);
            return Mathf.Clamp01(identity + pub * p.publicCommitScale);
        }

        public static float FacePreservationDrive(float identityInvestment, float publicCommitment)
            => FacePreservationDrive(identityInvestment, publicCommitment, EscalationCommitmentParams.Default);

        /// <summary>
        /// 逃した合理的撤退（0..1）＝合理的な撤退の好機を固執で逃す（損切りすべき時に動けない）。撤退の好機
        /// （exitOpportunity 0..1＝今なら傷が浅く済む撤退の窓）のうち、コミットメントのロック（commitmentLock 0..1）が
        /// 強いぶんだけ逃す＝exitOpportunity×commitmentLock。ロックがゼロなら好機を逃さない（損切りできる）。
        /// </summary>
        public static float RationalExitForegone(float commitmentLock, float exitOpportunity, EscalationCommitmentParams p)
        {
            float lockv = Mathf.Clamp01(commitmentLock);
            float opp = Mathf.Clamp01(exitOpportunity);
            return Mathf.Clamp01(opp * lockv);
        }

        public static float RationalExitForegone(float commitmentLock, float exitOpportunity)
            => RationalExitForegone(commitmentLock, exitOpportunity, EscalationCommitmentParams.Default);

        /// <summary>
        /// ロック解除の強さ（0..1）＝外的衝撃や指導者交代が固執のロックを解除する（誰かが損切りを決断＝崩壊解除）。
        /// 外的衝撃（externalShock 0..1＝否応なき破局）×shockBreak と指導者交代（newLeadership 0..1＝サンクコストに縛られぬ
        /// 新たな決断者）×leadershipBreak を独立に合成し、いずれかが効けばロックが崩れる＝1 − (1−衝撃)(1−交代)。
        /// 両者ゼロなら解除されない（固執は内側からは解けない）。
        /// </summary>
        public static float LockBreaker(float externalShock, float newLeadership, EscalationCommitmentParams p)
        {
            float shock = Mathf.Clamp01(externalShock) * p.shockBreak;
            float lead = Mathf.Clamp01(newLeadership) * p.leadershipBreak;
            return Mathf.Clamp01(1f - (1f - shock) * (1f - lead));
        }

        public static float LockBreaker(float externalShock, float newLeadership)
            => LockBreaker(externalShock, newLeadership, EscalationCommitmentParams.Default);

        /// <summary>
        /// サンクコストの罠（損切り不能で泥沼）に陥ったか＝コミットメントのロック（commitmentLock 0..1）と固執の重さ
        /// （sunkCostWeight 0..1）がともに threshold 以上＝合理的撤退が縛られ、かつ投下犠牲が大きく止められない状態の判定。
        /// 一方だけでは罠ではない（ロックは強いが犠牲が浅ければ抜けられる）。<see cref="IsSunkCostTrap(float,float)"/> は
        /// 既定閾値0.5。
        /// </summary>
        public static bool IsSunkCostTrap(float commitmentLock, float sunkCostWeight, float threshold)
        {
            float th = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(commitmentLock) >= th && Mathf.Clamp01(sunkCostWeight) >= th;
        }

        public static bool IsSunkCostTrap(float commitmentLock, float sunkCostWeight)
            => IsSunkCostTrap(commitmentLock, sunkCostWeight, 0.5f);
    }
}
