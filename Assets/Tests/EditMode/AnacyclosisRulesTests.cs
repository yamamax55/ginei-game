using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// AnacyclosisRules（POLY-1 #1442・ポリュビオスの政体循環論）の純ロジック検証。
    /// 既定 Params（腐落率0.1/打倒閾値0.6/循環基準速度0.5/堕落形態固有不安定度0.4）の具体値で期待値を固定し、
    /// 六政体の循環遷移・正/堕落の判定・腐落の進行・打倒圧力・形態安定度・循環速度・位相写像・一巡判定を担保する。
    /// </summary>
    public class AnacyclosisRulesTests
    {
        /// <summary>循環遷移は王政→僭主政→貴族政→寡頭政→民主政→衆愚政→王政と一周する。</summary>
        [Test]
        public void NextForm_六政体が循環する()
        {
            Assert.AreEqual(RegimeForm.僭主政, AnacyclosisRules.NextForm(RegimeForm.王政));
            Assert.AreEqual(RegimeForm.貴族政, AnacyclosisRules.NextForm(RegimeForm.僭主政));
            Assert.AreEqual(RegimeForm.寡頭政, AnacyclosisRules.NextForm(RegimeForm.貴族政));
            Assert.AreEqual(RegimeForm.民主政, AnacyclosisRules.NextForm(RegimeForm.寡頭政));
            Assert.AreEqual(RegimeForm.衆愚政, AnacyclosisRules.NextForm(RegimeForm.民主政));
            Assert.AreEqual(RegimeForm.王政, AnacyclosisRules.NextForm(RegimeForm.衆愚政)); // 一周
        }

        /// <summary>正しい3形態（王政・貴族政・民主政）と堕落3形態（僭主政・寡頭政・衆愚政）を弁別する。</summary>
        [Test]
        public void IsLegitimateForm_正と堕落を弁別()
        {
            Assert.IsTrue(AnacyclosisRules.IsLegitimateForm(RegimeForm.王政));
            Assert.IsTrue(AnacyclosisRules.IsLegitimateForm(RegimeForm.貴族政));
            Assert.IsTrue(AnacyclosisRules.IsLegitimateForm(RegimeForm.民主政));
            Assert.IsFalse(AnacyclosisRules.IsLegitimateForm(RegimeForm.僭主政));
            Assert.IsFalse(AnacyclosisRules.IsLegitimateForm(RegimeForm.寡頭政));
            Assert.IsFalse(AnacyclosisRules.IsLegitimateForm(RegimeForm.衆愚政));
            // 正しい形態は対応する堕落形態へ腐落する
            Assert.AreEqual(RegimeForm.僭主政, AnacyclosisRules.CorruptedForm(RegimeForm.王政));
            Assert.AreEqual(RegimeForm.寡頭政, AnacyclosisRules.CorruptedForm(RegimeForm.貴族政));
            Assert.AreEqual(RegimeForm.衆愚政, AnacyclosisRules.CorruptedForm(RegimeForm.民主政));
        }

        /// <summary>正しい形態は徳が低いほど速く腐落し、堕落形態は腐落しない（0を返す）。</summary>
        [Test]
        public void DegenerationTick_徳の喪失で腐落が進む()
        {
            // 王政・徳0.2＝0.1*(1-0.2)*1=0.08進む→0.08
            Assert.AreEqual(0.08f, AnacyclosisRules.DegenerationTick(0.2f, RegimeForm.王政, 0f, 1f), 1e-4f);
            // 徳が高いほど遅い：王政・徳0.9＝0.1*0.1*1=0.01
            Assert.AreEqual(0.01f, AnacyclosisRules.DegenerationTick(0.9f, RegimeForm.王政, 0f, 1f), 1e-4f);
            // 1.0でクランプ：徳0・既に0.95＝+0.1→1.0
            Assert.AreEqual(1f, AnacyclosisRules.DegenerationTick(0f, RegimeForm.民主政, 0.95f, 1f), 1e-4f);
            // 堕落形態は腐落しない
            Assert.AreEqual(0f, AnacyclosisRules.DegenerationTick(0f, RegimeForm.僭主政, 0.5f, 1f), 1e-4f);
        }

        /// <summary>堕落形態は抑圧×不満で打倒圧力が高まり、閾値0.6で打倒の機が熟す。正しい形態は0。</summary>
        [Test]
        public void OverthrowPressure_堕落形態が不満で打倒される()
        {
            // 僭主政・抑圧0.8×不満0.8=0.64
            Assert.AreEqual(0.64f, AnacyclosisRules.OverthrowPressure(RegimeForm.僭主政, 0.8f, 0.8f), 1e-4f);
            // 正しい形態には打倒圧力が掛からない
            Assert.AreEqual(0f, AnacyclosisRules.OverthrowPressure(RegimeForm.王政, 1f, 1f), 1e-4f);
            // 0.64>=0.6で打倒の機が熟す
            Assert.IsTrue(AnacyclosisRules.IsOverthrowReady(RegimeForm.僭主政, 0.8f, 0.8f));
            // 抑圧0.7×不満0.7=0.49<0.6で未熟
            Assert.IsFalse(AnacyclosisRules.IsOverthrowReady(RegimeForm.寡頭政, 0.7f, 0.7f));
        }

        /// <summary>正しい形態は徳で安定し、堕落形態は固有不安定度ぶん安定が削られる（同条件で堕落のほうが脆い）。</summary>
        [Test]
        public void FormStability_正しい形態が堕落形態より安定()
        {
            // 王政・徳1・制度1＝1*Lerp(0.5,1,1)=1.0
            Assert.AreEqual(1f, AnacyclosisRules.FormStability(RegimeForm.王政, 1f, 1f), 1e-4f);
            // 僭主政・徳1・制度1＝(1*(1-0.4))*Lerp(0.6,1,1)=0.6
            Assert.AreEqual(0.6f, AnacyclosisRules.FormStability(RegimeForm.僭主政, 1f, 1f), 1e-4f);
            // 同条件で正しい形態のほうが安定
            float legit = AnacyclosisRules.FormStability(RegimeForm.貴族政, 1f, 1f);
            float corrupt = AnacyclosisRules.FormStability(RegimeForm.寡頭政, 1f, 1f);
            Assert.Greater(legit, corrupt);
            // 制度的歯止めが弱いと正しい形態も下がる：王政・徳1・制度0＝Lerp(0.5,1,0)=0.5
            Assert.AreEqual(0.5f, AnacyclosisRules.FormStability(RegimeForm.王政, 1f, 0f), 1e-4f);
        }

        /// <summary>徳が速く失われ制度的歯止めが無いほど循環が速い。</summary>
        [Test]
        public void CycleVelocity_徳の喪失と歯止め欠如で速まる()
        {
            // 徳喪失1・ブレーキ0＝0.5*1*(1-0)*2=1.0
            Assert.AreEqual(1f, AnacyclosisRules.CycleVelocity(1f, 0f), 1e-4f);
            // ブレーキ0.5で循環が遅れる＝0.5*1*0.5*2=0.5
            Assert.AreEqual(0.5f, AnacyclosisRules.CycleVelocity(1f, 0.5f), 1e-4f);
            // 徳喪失が遅いと循環も遅い＝0.5*0.2*1*2=0.2
            Assert.AreEqual(0.2f, AnacyclosisRules.CycleVelocity(0.2f, 0f), 1e-4f);
        }

        /// <summary>循環位置（0..1）が6形態へ段階的に写る。</summary>
        [Test]
        public void PhaseOf_循環位置を六政体へ写す()
        {
            Assert.AreEqual(RegimeForm.王政, AnacyclosisRules.PhaseOf(0.05f));
            Assert.AreEqual(RegimeForm.僭主政, AnacyclosisRules.PhaseOf(0.2f));
            Assert.AreEqual(RegimeForm.貴族政, AnacyclosisRules.PhaseOf(0.4f));
            Assert.AreEqual(RegimeForm.寡頭政, AnacyclosisRules.PhaseOf(0.6f));
            Assert.AreEqual(RegimeForm.民主政, AnacyclosisRules.PhaseOf(0.7f));
            Assert.AreEqual(RegimeForm.衆愚政, AnacyclosisRules.PhaseOf(0.9f));
            // 末端1.0は衆愚政へクランプ
            Assert.AreEqual(RegimeForm.衆愚政, AnacyclosisRules.PhaseOf(1f));
        }

        /// <summary>NextFormを6回たどると起点へ戻る＝アナキュクローシスが一巡する。</summary>
        [Test]
        public void IsAnacyclosisComplete_六遷移で起点へ回帰()
        {
            RegimeForm start = RegimeForm.王政;
            RegimeForm cur = start;
            for (int i = 0; i < 5; i++)
            {
                cur = AnacyclosisRules.NextForm(cur);
                Assert.IsFalse(AnacyclosisRules.IsAnacyclosisComplete(cur, start)); // 途中はまだ
            }
            cur = AnacyclosisRules.NextForm(cur); // 6回目＝衆愚政→王政
            Assert.IsTrue(AnacyclosisRules.IsAnacyclosisComplete(cur, start)); // 一巡完了
        }
    }
}
