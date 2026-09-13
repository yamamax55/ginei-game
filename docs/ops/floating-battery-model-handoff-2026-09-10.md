# 浮遊砲台3Dモデル受渡し（2026-09-10 / ChatGPT制作）

Blender 5.2で制作。出力フォルダ：C:/Users/htccj/Documents/Codex/2026-09-09/new-chat/outputs/floating-battery
- FloatingBattery.blend：制作元。旋回→仰角→反動の90フレーム/30fpsデモ付き。
- FloatingBattery.fbx：ニュートラル姿勢、ゲーム組込用。3メッシュ・7,624三角形。カメラ/ライトなし。
- FloatingBattery_AimFireDemo.fbx：旋回と発砲反動デモ。戦闘中にこの固定旋回を自動再生して狙いを上書きしないこと。
- floating-battery-preview.png：Blenderレンダー確認画像。
- export-verification.json：別のBlenderプロセスでFBX再読込、階層/3メッシュ/カメラライトなし/アニメーション3アクションを確認。

## 階層と向き
FloatingBattery > TurretYaw > GunPitch > BarrelRecoil > MuzzleLeft/MuzzleRight。
AimForward/AimUpはGunPitchの子で、原点からの方向を示す基準。Blenderは前+Y、上+Z。FBXはforward=-Z/up=Yで書出し。Unityインポート軸の推測に依存せず、これらマーカーと砲口を使って戦術XY平面への姿勢を合わせる。
TurretYawはZ軸旋回、GunPitchはX軸仰角、BarrelRecoilはY負方向へ最大0.16の後退（Blender基準）。デモは1〜90F、発砲反動は32〜41Fと67〜77F。
マテリアル：Armor_Silver/Armor_Light/Recess_Graphite/Gunmetal/Reactor_Cyan/Windows_Amber。既存要塞のSelfLit材質へのマッピングを再利用可能。

## Claude担当のゲーム統合
FBXをResources/Models/Fortress等へ追加して既存FortressTurretの丸いスプライトと差し替える。配置・半径・ダメージ・制圧ルールは維持。必要なUnityマテリアル/スケール/軸調整はClaudeが行う。
砲台根元のtransform.upは既存射界の固定基準なので旋回で動かさない。子のモデル砲塔だけを実対象に滑らかに向け、向いてから砲口を起点として射撃し反動。照準許容角内で射撃し、敵消滅/範囲外/ポーズ/沈黙で不正射撃しない。射界が追尾に連れて拡大しないこと。
沈黙時は発光/射撃/旋回を止め残骸として分かる暗い3D表示にする。通常の砲台制圧を要塞本体の特殊破壊に変えない。別会戦の敵を狙わない。
実画面検証はまだ。モデル制作とFBX検証済みであってUnity統合完了ではない。
