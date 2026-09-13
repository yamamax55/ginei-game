
## 2026-09-12 revision5 ChatGPT再検証

ClaudeがPlayMode試験の初期化順を修正。非アクティブで全部品追加後に有効化し、士気の実低下・下限到達・生存を検査する構成をレビュー。
ChatGPTがUnity 6000.6.0f1で確認:
- Fixture_DamageActuallyDrainsMorale 単独1/1合格。
- WithoutLock_NormalRoutStillHappens 単独1/1合格。
- MoraleLockRoutPlayModeTests 一括7/7合格（1.428秒）。被弾直後・フレーム間反転防止・陣形保持・支援継続・効果終了後の通常敗走を、実際に士気が下限まで減る試験で確認。
- 前回revision3の4合格は空振りだったため、今回の証跡で置き換える。
- 既存FleetFormationHoldPlayModeTests 一括16合格1失敗（7.336秒）。WithoutHold_RealAiDoesChangeFormation が LastFormationSource=なし で失敗（158行）。同試験の単独再実行も同じ失敗（1.075秒）。保持なし対照でAI指定が一度も観測されず、AI周期を跨いだ保持試験の前提も再確認が必要。
- 修正後の通常会戦での不退転再現確認は未実施。仕様1最終承認・仕様2開始は保留。Unity Play OFF。

証跡: docs/ops/morale-lock-r5-fixture-20260912.xml、morale-lock-r5-control-20260912.xml、morale-lock-r5-all-20260912.xml、formation-hold-r5-regression-20260912.xml、formation-hold-r5-control-20260912.xml。

次のClaude依頼: 既存対照試験の失敗原因を切り分ける。製品回帰とはまだ断定しない。敵検出・部品参照・実AIの自動陣形判定周期・選択条件が成立しているか確認し、保持なしなら実AIが判断する対照と、保持ありならそれでも守る検証を成立させる。期待緩和・AI経路の迂回・結果直書きは不可。必要最小限の修正後に編集停止して返す。仕様2には触れない。

不退転中の自動総退却について: 実際には敗走していないため commanderRouted=false と扱い、艦艇比による総退却判断は継続する現行仕様で整合する。明示的な軍団総退却命令を不退転で拒否しないことは維持する。