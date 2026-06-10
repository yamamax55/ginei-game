using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 世代を跨ぐ国家的大事業（PIL-1 #1090）を固定する：段階遷移（基礎→構造→完成）、資金比例の進捗、
    /// 中断/再開（財政ドレイン停止）、発起人死亡時の頓挫判定（制度化×roll 決定論＝#812 の式の事業適用）、
    /// 種別別の完成効果、埋没費用。既定 Params（buildSeconds=3600/upkeep=1/構造閾値0.35/頓挫係数0.8/回収0.2）で期待値固定。
    /// </summary>
    public class MegaProjectRulesTests
    {
        // ===== 段階遷移 =====

        [Test]
        public void StageOf_FoundationToStructureToComplete()
        {
            Assert.AreEqual(ProjectStage.基礎, MegaProjectRules.StageOf(0f));
            Assert.AreEqual(ProjectStage.基礎, MegaProjectRules.StageOf(0.34f)); // 閾値0.35未満
            Assert.AreEqual(ProjectStage.構造, MegaProjectRules.StageOf(0.35f)); // 閾値ちょうどで構造
            Assert.AreEqual(ProjectStage.構造, MegaProjectRules.StageOf(0.99f));
            Assert.AreEqual(ProjectStage.完成, MegaProjectRules.StageOf(1f));
            Assert.AreEqual(ProjectStage.完成, MegaProjectRules.StageOf(5f)); // 過大入力はクランプ
        }

        // ===== 進捗（資金比例・決定論） =====

        [Test]
        public void ProgressTick_FundingProportional_AndCompletes()
        {
            var monument = new MegaProject(1, Faction.帝国, MegaProjectKind.記念碑); // 規模倍率1.0
            // 満額資金で半分の時間 → 進捗0.5
            MegaProjectRules.ProgressTick(monument, funding: 1f, dt: 1800f);
            Assert.AreEqual(0.5f, monument.progress, 1e-4f);
            // 残りを満額で進めると完成（1にクランプ）
            MegaProjectRules.ProgressTick(monument, 1f, 99999f);
            Assert.AreEqual(1f, monument.progress, 1e-4f);
            Assert.IsTrue(monument.IsComplete);
            // 完成後はもう進まない
            Assert.AreEqual(0f, MegaProjectRules.ProgressTick(monument, 1f, 100f), 1e-6f);
        }

        [Test]
        public void ProgressTick_ZeroFundingStalls_AndKindScaleSlowsCapital()
        {
            var monument = new MegaProject(1, Faction.帝国, MegaProjectKind.記念碑);
            Assert.AreEqual(0f, MegaProjectRules.ProgressTick(monument, 0f, 1000f), 1e-6f); // 資金0＝停滞

            var capital = new MegaProject(2, Faction.帝国, MegaProjectKind.遷都); // 規模倍率2.0
            MegaProjectRules.ProgressTick(capital, 1f, 3600f);
            Assert.AreEqual(0.5f, capital.progress, 1e-4f); // 記念碑なら完成する時間で半分＝数十年スケールの種別差
        }

        // ===== 中断/再開＝財政ドレインの決断 =====

        [Test]
        public void Suspend_StopsProgressAndUpkeep_ResumeRestarts()
        {
            var fortress = new MegaProject(1, Faction.同盟, MegaProjectKind.要塞);
            Assert.AreEqual(1.5f, MegaProjectRules.UpkeepDrain(fortress), 1e-4f); // 1×規模1.5

            MegaProjectRules.Suspend(fortress);
            Assert.AreEqual(0f, MegaProjectRules.UpkeepDrain(fortress), 1e-6f);   // 中断で支出停止
            Assert.AreEqual(0f, MegaProjectRules.ProgressTick(fortress, 1f, 1000f), 1e-6f); // 進捗も停止

            MegaProjectRules.Resume(fortress);
            Assert.Greater(MegaProjectRules.ProgressTick(fortress, 1f, 1000f), 0f); // 再開で進む
        }

        // ===== 発起人死亡＝頓挫判定（roll 決定論） =====

        [Test]
        public void SuccessionOnFounderDeath_LowInstitutionalization_Abandons()
        {
            var p = new MegaProject(1, Faction.帝国, MegaProjectKind.遷都);
            // 制度化0 → 頓挫確率 0.8。roll 0.5 < 0.8 → 頓挫（不可逆）
            Assert.IsTrue(MegaProjectRules.SuccessionOnFounderDeath(p, institutionalization: 0f, roll: 0.5f));
            Assert.IsTrue(p.abandoned);
            // 頓挫後は進まず、再開もできない
            Assert.AreEqual(0f, MegaProjectRules.ProgressTick(p, 1f, 99999f), 1e-6f);
            MegaProjectRules.Resume(p);
            Assert.IsTrue(p.abandoned);
        }

        [Test]
        public void SuccessionOnFounderDeath_HighInstitutionalization_Continues()
        {
            var p = new MegaProject(1, Faction.帝国, MegaProjectKind.遷都);
            // 制度化1 → 頓挫確率0＝どんな roll でも続行（カリスマの日常化）
            Assert.IsFalse(MegaProjectRules.SuccessionOnFounderDeath(p, 1f, 0f));
            Assert.IsFalse(p.abandoned);
            // 制度化0.5 → 頓挫確率 0.4。roll が境界を跨いで決定論的に分かれる
            Assert.IsFalse(MegaProjectRules.SuccessionOnFounderDeath(p, 0.5f, 0.41f)); // 0.41 ≥ 0.4 → 続行
            Assert.IsTrue(MegaProjectRules.SuccessionOnFounderDeath(p, 0.5f, 0.39f));  // 0.39 < 0.4 → 頓挫
        }

        // ===== 完成効果（種別別） =====

        [Test]
        public void CompletionEffect_PerKind()
        {
            Assert.AreEqual(MegaProjectRules.EffectFortress, MegaProjectRules.CompletionEffect(MegaProjectKind.要塞), 1e-4f);
            Assert.AreEqual(MegaProjectRules.EffectShipyard, MegaProjectRules.CompletionEffect(MegaProjectKind.大シップヤード), 1e-4f);
            Assert.AreEqual(MegaProjectRules.EffectCapital, MegaProjectRules.CompletionEffect(MegaProjectKind.遷都), 1e-4f);
            Assert.AreEqual(MegaProjectRules.EffectMonument, MegaProjectRules.CompletionEffect(MegaProjectKind.記念碑), 1e-4f);
            // 建艦力増（大シップヤード）が最大の投資対効果
            Assert.Greater(MegaProjectRules.CompletionEffect(MegaProjectKind.大シップヤード), MegaProjectRules.CompletionEffect(MegaProjectKind.記念碑));
        }

        // ===== 埋没費用 =====

        [Test]
        public void AbandonmentSunkCost_GrowsWithProgress_AndClamps()
        {
            Assert.AreEqual(0f, MegaProjectRules.AbandonmentSunkCost(0f), 1e-4f);
            Assert.AreEqual(0.4f, MegaProjectRules.AbandonmentSunkCost(0.5f), 1e-4f); // 0.5×(1-0.2)
            Assert.AreEqual(0.8f, MegaProjectRules.AbandonmentSunkCost(1f), 1e-4f);
            Assert.AreEqual(0.8f, MegaProjectRules.AbandonmentSunkCost(9f), 1e-4f);   // 過大入力はクランプ
            // 進んだ事業ほど畳む痛みが大きい＝決断材料
            Assert.Greater(MegaProjectRules.AbandonmentSunkCost(0.9f), MegaProjectRules.AbandonmentSunkCost(0.1f));
        }
    }
}
