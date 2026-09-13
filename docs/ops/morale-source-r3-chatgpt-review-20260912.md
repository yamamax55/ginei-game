# 士気原因監査 revision3 Unity検証（2026-09-12）
ChatGPTがUnity Test RunnerのPlayModeで実行し、XMLを読んで確認。
- MoraleSourcePlayModeTests: 6/6合格、終了16:02:08 JST。証跡 morale-source-r3-all-20260912.xml。
- MoraleLockRoutPlayModeTests: 9/9合格、終了16:02:56 JST。証跡 morale-source-r3-lock-regression-20260912.xml。
- FleetFormationHoldPlayModeTests: 17/17合格、終了16:03:56 JST。証跡 morale-source-r3-formation-regression-20260912.xml。
合計32件、失敗0。revision2の2失敗は修正後に解消。Clearが観測閾値を保持し、継続被弾は艦艇数減少と実際の発数で証明する変更をレビュー済み。
自然回復率0.5と経過時間の一致、敗走中の継続被弾では自然回復なし、被弾停止後4秒の待ち時間、イベント相当+6の直接加算と敗走解除、実旗艦撃墜による高揚を原因別に確認。
限界: イベント試験はApplyMoraleDeltaの直接呼出。BattleEventManagerの自然発火、通常会戦での原因付き記録は未実施。新しい記録で旧会戦の+6/+4.4を遡って確定できない。
仕様1全体は未承認。仕様2保留。通常会戦の原因付き観測を続行する。
関連: https://github.com/yamamax55/ginei-game/issues/2253 https://github.com/yamamax55/ginei-game/issues/2175
