# 士気原因監査 revision2 Unityレビュー（2026-09-12）
Unity PlayMode MoraleSourcePlayModeTests実行: 全6件、4合格2失敗。
証跡: docs/ops/morale-source-r2-all-20260912.xml
合格: Fixture_RealDamageIsRecordedAsDamage, AfterFireStops_FirstRecoveryIsPastTheDelay, BattleEventGain_IsRecordedAsEventAndClearsRout, RealFlagshipKill_GivesRecordedElationToTheEnemy。
失敗: Idle_OnlyNaturalRecovery_AndRateMatches line188 自然回復件数 Expected >1 Actual1。
失敗: RoutedUnderFire_NoNaturalRecoveryAtAll line249 被弾件数 Expected >1 Actual0。
レビュー: MoraleAuditLog.ClearがMinAbsDeltaを既定.01へ戻すため、SetUpで0を指定しても観測開始時Clearで失われる。通常回復の細かい刻みを落とす可能性。敗走後0の継続被弾は士気変化0なので、変化台帳件数は被弾の証明として不適切。実ダメージ/艦艇数減少等で継続被弾を証明し、自然回復0・生存・不退転なしを維持して検証すること。断定せず実装も確認して最小修正。
本番の回復計算/待ち時間/イベント量は変更しない。ゲームコードとテスト変更はClaude担当。
報告の新規試験数5は実際6へ訂正すること。旧会戦t=278.41/280.34の原因は新しい会戦の記録から遡って確定できない。新規再現例の原因と旧ログの仮説を区別すること。
イベント試験はApplyMoraleDeltaを直接呼ぶためBattleEventManagerの自然発火は未検証。通常会戦の原因付き観測と回帰9+17件はまだ残る。
仕様1未承認、仕様2保留。保存データとv5と既存dirty treeを保護。commit/pushなし。
