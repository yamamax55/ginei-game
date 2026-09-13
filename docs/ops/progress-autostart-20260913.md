# 進捗ページ自動起動 2026-09-13
タスクスケジューラ GineiProgressPage を登録。htccjのWindowsサインイン時、非表示PowerShellから既存Python標準http.serverを起動。公開フォルダはCodex作業フォルダoutputs/progressのみ、ポート8765、IPv4/IPv6対応。多重起動無視、実行時間制限なし、電池時も継続。パスワード保存なし。
リンク: http://DESKTOP-LI17SQH:8765/status.txt
現IPリンク: http://10.140.246.198:8765/status.txt
ChatGPT確認: タスク起動、停止/再起動後のHTTP200をこのPCから確認。OS再起動を実際に行った試験と別PCからの接続は未実施。再起動前のIPリンクは更新が必要。PC名の名前解決は別PC環境に依存する。
既存Publicプロファイルのpython.exe受信ブロック規則を検出。限定許可への変更はWindowsの規則制約で失敗し、変更未適用。別PCからブロックされる場合はファイアウォール設定の解決が必要。ローカルHTTP200はLAN疎通の証明ではない。
自動起動はサインイン後。電源OFF/サインイン前に配信しない。状態ファイルはディスク保持、内容の更新はCodexの監視が動作している間のみ。
