
## 2026-09-12 ChatGPT revision3レビュー・Unity試験結果（差戻し）

Claude報告: IsRouted と被弾/イベントの士気下限に不退転を反映。6アセンブリコンパイル・TestHarness 9686件合格はClaude報告。ChatGPTは差分の判定・下限共通化を確認。ただし全機能承認ではない。

ChatGPT実行: Unity 6000.6.0f1 Test Runnerで MoraleLockRoutPlayModeTests 全6件を一括実行。4合格・2失敗（8.198秒）。証跡 docs/ops/morale-lock-r3-playmode-20260912.xml。
- 合格: 被弾直後、フレーム間の反転防止、陣形保持、支援継続。
- 失敗: AfterLockExpires_NormalRoutResumes（217行、効果終了後に通常の敗走が起きない）。WithoutLock_NormalRoutStillHappens（241行、不退転なしの通常敗走が起きない）。いずれも IsRouted の期待Trueに対しFalse。
- 通常被弾による士気低下量・被弾上限・艦艇数・初期化・試験時間を切り分ける必要あり。現時点で製品回帰とは断定しない。合格4件も対照試験が失敗しているため、十分に士気を削って不具合条件を踏めているか再評価が必要。
- 新規試験の個別実行、既存陣形17件の再実行、修正後の通常会戦での再現確認は未実施。

Claudeへの差戻し: 上記2失敗の原因を確認し、結果の直書きや期待値緩和をせず、本物の被弾経路で効果なしなら敗走する条件を証明する。効果中は同等の条件で敗走しないことを確認し、終了後も通常判定へ戻ること。生存・士気下限到達等の前提をassertし、未到達や死亡で試験を空振り合格にしない。ゲーム側に原因があれば最小修正。仕様2は着手禁止。Unity Play OFFを確認済み。