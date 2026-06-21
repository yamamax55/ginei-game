---
type: ops
tags: [ops]
---

# 音楽・音声の調達ガイド（無料＋商用利用可）

> ②音楽・音声（銀英伝の魂）。`AudioManager`（器）に**ファイルを置くだけで鳴る**よう Resources 規約を用意した。
> 本ガイドは**無料かつ商用利用可**のソースに限定し、各ライセンスの要点と注意を明記する。最終的な可否は各ファイルのライセンス表示で必ず確認すること（特に Musopen は曲ごとに異なる）。

## 1. 置き場所の規約（ファイルを置くだけで鳴る）
`Assets/Resources/` 直下に下記の名前で音源（`.ogg` 推奨＝BGM、`.wav`＝SE）を置けば、Inspector 割当なしで `AudioManager` が自動ロードする（`Resources.Load`）。Inspector で明示割当すればそちらが優先。

| 用途 | ファイル名（拡張子任意） | 再生箇所 |
|---|---|---|
| タイトルBGM | `bgm_title` | `TitleManager.Start` |
| 会戦BGM | `bgm_battle` | `BattleManager.Start` |
| 戦略マップBGM | `bgm_strategy` | `GalaxyView.Start` |
| 結果BGM | `bgm_result` | `ResultManager.Start` |
| ビームSE | `shot_1`（既存） | `AudioManager.PlayBeam` |
| 被弾SE | `se_hit` | `PlayHit` |
| 爆発SE | `se_explosion` | `PlayExplosion` |
| UIクリックSE | `se_uiclick` | `PlayUIClick` |

- BGM切替は `bgmCrossfade`（既定1.2秒）でフェードアウト→フェードイン（曲間の唐突さを緩和）。0で即時。
- 形式：BGM は **`.ogg`**（圧縮・ループ向き）、SE は **`.wav`**（短尺・低遅延）。Unity が自動インポート。

## 2. BGM ─ 銀英伝の魂＝クラシック（パブリックドメイン）
原作の重厚さはクラシック。**楽曲（作曲）自体は作曲者の没後70年でPD**だが、**録音（演奏）には別途著作隣接権**がある。よって「**PD作品の、PD/CC0録音**」を使う。

### 推奨ソース（商用可）
- **[Musopen](https://musopen.org/)** … PD/CC0 のクラシック録音・楽譜の最大級リポジトリ。**royalty/copyright-free だが一部は商用不可のものが混在**＝**各録音のライセンスアイコンを必ず確認**（ユーザー投稿はPD保証なし）。CC0/PD 表示のものだけ使う。
- **[IMSLP / Petrucci 楽譜ライブラリ](https://imslp.org/)** … 楽譜中心。一部PD録音あり（自分で演奏/打ち込みする場合の原典）。
- **[archive.org Audio](https://archive.org/details/audio)** … 歴史的PD録音（録音年代が古くPDのもの）。ライセンス欄を確認。
- **[FreePD](https://freepd.com/)** … CC0 のオリジナル曲（クラシック風アレンジも）。帰属不要・商用可。

### 銀英伝の空気に合うPD作品（作品自体はPD・録音はPD/CC0を選ぶ）
- 荘厳/帝国：ベートーヴェン交響曲（第3「英雄」/第7）、ブルックナー、ワーグナー（楽劇の管弦楽部）。
- 哀感/喪失：ドヴォルザーク「新世界より」第2楽章、マーラー（交響曲のアダージョ）。
- 行進/会戦：ホルスト「惑星」より（録音のPD性に注意）、エルガー。
> ※作曲者の没年で作品PDかを確認（マーラー1911・ドヴォルザーク1904・エルガー1934＝多くの国でPD。ホルスト1934・ラヴェル1937＝国により最近PD化）。

### オリジナル曲を無料＋商用で使うなら
- **[Pixabay Music](https://pixabay.com/music/)** … Pixabay License＝**商用可・帰属不要**（DSP配信時のみ別途要件）。ゲーム内利用は帰属なしで可。
- **[incompetech（Kevin MacLeod）](https://incompetech.com/)** … CC-BY＝**商用可だが帰属（クレジット）必須**。オーケストラ風が豊富。

## 3. SE（効果音）─ CC0/ロイヤリティフリー
- **[Kenney.nl](https://kenney.nl/assets?q=audio)** … **CC0**（帰属不要・商用可）。UI/インパクト/宇宙/レトロ音。40,000+アセット。**最優先**。
- **[Sonniss GameAudioGDC](https://sonniss.com/gameaudiogdc/)** … プロ音響の巨大バンドル。**ロイヤリティフリー・帰属不要・商用可・生涯利用**。爆発/ビーム素材に。
- **[OpenGameArt](https://opengameart.org/)** … ライセンス混在＝**CC0 で絞って使う**（[CC0 Sound Effects](https://opengameart.org/content/cc0-sound-effects)）。CC-BY は帰属必須。
- **[Freesound](https://freesound.org/)** … ライセンス混在＝**CC0 フィルタ**で検索。CC-BY は帰属必須。
- **[jsfxr](https://sfxr.me/) / Bfxr** … レトロSEを自作生成（生成物は自由に使える）。ビーム/小爆発の量産に。

## 4. ライセンス順守チェックリスト（商用前提）
1. 各ファイルの**ライセンス表記を保存**（CC0/Pixabay/CC-BY 等）。Musopen/OGA/Freesound は曲ごとに違う。
2. **CC-BY は帰属（クレジット）必須**＝ゲーム内クレジット画面に「曲名／作者／ソース／ライセンス」を列挙。
3. クラシックは**作品PD＋録音PD/CC0**の二重確認（録音の権利を見落とさない）。
4. 商標・実在ロゴ・既存ゲーム/アニメの音源は使わない（PD作品の自家演奏/PD録音のみ）。
5. 帰属が要るものをまとめる `Assets/Resources/CREDITS`（または結果画面/設定のクレジット）を用意。

## 5. 導入手順（最短）
1. 上記から音源を入手（まず Kenney=SE と Pixabay/Musopen=BGM が手早い）。
2. `.ogg`(BGM)/`.wav`(SE) に変換（Audacity 等）し、§1の名前で `Assets/Resources/` に置く。
3. エディタ再生で各シーンの BGM・SE を確認。GameCI スモーク（`unity-test`）で Game 層コンパイルも担保。
4. CC-BY を使ったらクレジットに追記。

> 注：`AudioManager` の本対応（Resources自動ロード・クロスフェード・Strategy/Result枠・各シーン配線）は実装済み。あとは**素材を置くだけ**。

## 関連
- [[components-catalog]] — AudioManager の責務・API 索引
- [[comfyui-setup]] — 画像素材生成の調達パイプライン
- [[portrait-pipeline]] — ポートレート素材の調達手順
- [[game-introduction]] — 制作の全体像・素材の位置づけ
