using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 主人公の執務机（軍人立身伝の一人称UIシェル・TKO-8 #2485・<b>Alt+J</b>）。god-view から「一人の軍人」へ視点を落とし、
    /// 自分の<b>階級・拝命中の主命（君主→指揮系統→自分のカスケード）・武勲と次の昇進・君主との人脈・一代記</b>を一望する。
    /// 状態は <see cref="ProtagonistCareerDirector"/> が回す立身出世ループから読むだけ（観測）。加えて<b>上官へ具申する</b>ボタン（TKO-4）を持ち、
    /// 押すと <see cref="ProtagonistCareerDirector.SubmitPetition"/> が稟議を起案＝稟議オブザーバ（Alt+I）に起案者=自分・決裁者=上官として現れる。
    /// `RingiObserverOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class ProtagonistDeskOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1115;
        public float dimAlpha = 0.55f;
        public float panelWidth = 1000f;
        public float panelMaxHeight = 900f;
        public Color panelColor = new Color(0.05f, 0.06f, 0.08f, 0.96f);
        public float bodyFontSize = 20f;
        public int barWidth = 14;

        [Header("階級ピラミッド（グラフィカル）")]
        [Tooltip("最下級（二等兵）の帯の最大幅(px)。上の階級ほど細くなりピラミッド型になる")]
        public float pyramidMaxBarWidth = 860f;
        [Tooltip("ピラミッド頂点の幅(px)。小さいほど尖った三角形になる")]
        public float pyramidMinBarWidth = 64f;
        [Tooltip("各階級の行の高さ(px)")]
        public float pyramidRowHeight = 28f;
        [Tooltip("帯の上のラベル文字サイズ")]
        public float pyramidLabelFontSize = 16f;
        [Tooltip("階級章を画像で帯内に表示する最小の帯上辺幅(px)。これより細い帯ではラベル文字と被るため、画像をやめてラベル先頭にインライン記号で出す")]
        public float pyramidInsigniaMinBandWidth = 160f;

        private GameObject overlayRoot;
        private GameObject panel;
        private TextMeshProUGUI bodyLabel;       // 上段テキスト（人物〜武勲）
        private TextMeshProUGUI bodyLabelLower;  // 下段テキスト（人脈〜一代記）
        private GameObject pyramidRoot;          // 階級ピラミッド（帯＋ラベルのグラフィカル表示）
        private RankPyramidRow[] pyramidRows;    // 階級ごとの帯（幅は固定・人数/現在地のみ毎フレーム更新）
        private TextMeshProUGUI pyramidFooter;   // 次階級の定員空き
        private object escWindowToken;

        // 士官段（少尉1〜元帥10）のネームド人数を毎フレーム数えるための再利用バッファ（GC回避）。
        private readonly int[] namedCountByTier = new int[11];
        private readonly HashSet<object> namedDedup = new HashSet<object>();

        // 階級別ネームド人物ウィンドウ（帯クリックで開く）。
        private GameObject rankWindowRoot;
        private TextMeshProUGUI rankWindowTitle;
        private TextMeshProUGUI rankWindowBody;
        private object rankWindowEscToken;

        // 1階級ぶんの帯（色付き帯＝Image＋階級章スプライト＝Image＋帯の上に重なるラベル＝TMP）。
        private class RankPyramidRow
        {
            public MilitaryGrade grade;
            public Graphic bar; // 台形バンド（PyramidBand）。色のみ毎フレーム更新
            public Color baseColor;
            public TextMeshProUGUI label;
            public Image insignia; // 階級章スプライト（Gemini生成）。無ければ非表示＝ラベルのUnicode記号にフォールバック
            public bool insigniaInline; // 細い帯＝画像をやめラベル先頭にインライン記号で出す（中央ラベルとの被り防止）
        }

        // 階級章スプライトのキャッシュ（"帝国/元帥" 等のキー→Sprite。Resources.Load の連打回避）。
        private readonly Dictionary<string, Sprite> insigniaCache = new Dictionary<string, Sprite>();

        // ネームド士官1名（OOB司令／艦隊台帳の司令・副提督・参謀／主人公）。
        private struct NamedOfficer
        {
            public string name;
            public int tier;
            public string role;
            public AdmiralData admiral; // null=主人公（Person）
        }

        private static readonly MeritRecordRules.MeritRecordParams MeritP = MeritRecordRules.MeritRecordParams.Default;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Strategy" && scene.name != "Battle") return;
            if (Object.FindAnyObjectByType<ProtagonistDeskOverlay>() != null) return;
            new GameObject("ProtagonistDeskOverlay").AddComponent<ProtagonistDeskOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "執務机");
        }

        private void OnDestroy()
        {
            UIWindowStack.Unregister(escWindowToken);
            UIWindowStack.Unregister(rankWindowEscToken);
        }

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.執務机切替)) Toggle();
            if (panel == null || !panel.activeSelf) return;
            if (bodyLabel != null) bodyLabel.text = BuildUpperDump();
            if (bodyLabelLower != null) bodyLabelLower.text = BuildLowerDump();
            UpdatePyramid();
        }

        public void Toggle() => SetVisible(panel != null && !panel.activeSelf);
        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
            if (!visible) CloseRankWindow(); // 執務机を閉じたら階級の人物ウィンドウも畳む
        }

        // ===== ダンプ本体 =====

        // 上段テキスト：人物〜武勲（この下にグラフィカルな階級ピラミッド、さらに下に下段テキストが並ぶ）。
        private string BuildUpperDump()
        {
            var sb = new StringBuilder(1536);
            sb.Append("<b>執務机</b>　階級・主命・武勲・人脈・一代記　(Alt+J で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            var d = ProtagonistCareerDirector.Instance;
            if (d == null || d.Protagonist == null)
            {
                sb.Append("\n<color=#ffcc66>立身出世ループはまだ起動していません。</color>\n");
                sb.Append("戦略マップ（Strategy）で ProtagonistCareerDirector が主人公を士官学校から任官させると、\n");
                sb.Append("ここに階級・拝命中の主命・武勲・人脈・一代記がライブ表示されます。");
                return sb.ToString();
            }

            Person me = d.Protagonist;
            sb.Append("\n<color=#e7e0b0>◤ 人物</color>\n");
            sb.Append("  <color=#ffe08a>").Append(me.name).Append("</color>　")
              .Append("<color=#9ad0ff>").Append(d.RankName(me.rankTier)).Append("</color>\n");
            sb.Append("  <color=#9aa7b2>").Append(OriginRules.Title(d.Origin)).Append("</color>\n");
            sb.Append("  <color=#9aa7b2>武名</color> ");
            AppendBar(sb, Mathf.Clamp01(d.Fame / 100f), "#d0b060");
            sb.Append("　<color=#6f8a9a>(政界転身の資本)</color>\n");

            // 能力（会戦成長 P1-b を反映した実効値＝基準＋GrowthRegistry）
            Growth g = d.HeroGrowth;
            sb.Append("\n<color=#e7e0b0>◤ 能力（会戦成長を反映）</color>\n");
            AppendStat(sb, "統率", me.leadership, g);
            AppendStat(sb, "攻撃", me.attack, g);
            AppendStat(sb, "防御", me.defense, g);
            AppendStat(sb, "機動", me.mobility, g);

            // 主命（カスケード）
            sb.Append("\n<color=#e7e0b0>◤ 主命（君主 → 指揮系統 → あなた）</color>\n");
            SovereignMandate m = d.ActiveMandate;
            if (m == null)
                sb.Append("  <color=#9aa7b2>（拝命中の主命なし）</color>\n");
            else
            {
                sb.Append("  発令 <color=#ffcc66>").Append(d.ResolveName(m.issuerId)).Append("</color>")
                  .Append("　→　拝命 <color=#ffe08a>").Append(me.name).Append("</color>\n");
                sb.Append("  <color=#9ad0ff>").Append(m.kind).Append("</color>　状態 ").Append(m.status)
                  .Append("　期限(通算月) ").Append(m.dueMonth).Append('\n');
                AppendCascade(sb, d);
            }

            // 武勲
            sb.Append("\n<color=#e7e0b0>◤ 武勲</color>\n");
            MeritRecord mr = d.Merit;
            if (mr != null)
            {
                float toNext = MeritP.pointsPerPromotion * (mr.meritPromotionsApplied + 1) - mr.points;
                if (toNext < 0f) toNext = 0f;
                sb.Append("  累積 <color=#ffe08a>").Append(Mathf.RoundToInt(mr.points)).Append("</color> 点");
                sb.Append("　次の昇進まで ").Append(Mathf.CeilToInt(toNext)).Append(" 点\n  実力 ");
                AppendBar(sb, MeritRecordRules.MeritScore01(mr, MeritP), "#8ce08c");
                sb.Append('\n');
            }

            sb.Append("\n<color=#e7e0b0>◤ 階級ピラミッド（自勢力）</color>");
            return sb.ToString();
        }

        // 下段テキスト：人脈〜一代記（グラフィカルな階級ピラミッドの下に並ぶ）。
        private string BuildLowerDump()
        {
            var d = ProtagonistCareerDirector.Instance;
            if (d == null || d.Protagonist == null) return "";
            Person me = d.Protagonist;
            var sb = new StringBuilder(1024);

            // 人脈（君主との縁）
            sb.Append("<color=#e7e0b0>◤ 人脈（君主との縁）</color>\n");
            if (d.Relations != null && d.Sovereign != null)
            {
                float favor = PersonRelationRules.NetAffinity(d.Relations, me.id, d.Sovereign.id);
                sb.Append("  ").Append(me.name).Append(" → ").Append(d.Sovereign.CharacterName)
                  .Append("　正味の親密度 <color=").Append(favor >= 0f ? "#8ce08c" : "#ff9a8a").Append('>')
                  .Append(favor.ToString("+0.00;-0.00;0.00")).Append("</color>\n");
            }
            else sb.Append("  <color=#9aa7b2>（記録なし）</color>\n");

            // 岐路（開かれた進路・TKO-7・政界転身を含む）
            sb.Append("\n<color=#e7e0b0>◤ 岐路（開かれた進路）</color>\n  ");
            List<CareerFork> forks = d.AvailableForks();
            for (int i = 0; i < forks.Count; i++)
            {
                if (i > 0) sb.Append("　/　");
                sb.Append(ForkColor(forks[i])).Append(forks[i].ToString()).Append("</color>");
            }
            sb.Append("\n  <color=#9aa7b2>不満 </color>");
            AppendBar(sb, d.Grievance, "#e0a06a");
            sb.Append('\n');

            // 一代記
            sb.Append("\n<color=#e7e0b0>◤ 一代記（新しい順）</color>\n");
            var recent = ProtagonistChronicleRules.Recent(d.Chronicle, 8);
            if (recent.Count == 0) sb.Append("  <color=#9aa7b2>（記録なし）</color>\n");
            for (int i = 0; i < recent.Count; i++)
            {
                ChronicleEntry e = recent[i];
                sb.Append("  <color=#6f8a9a>第").Append(e.monthIndex).Append("月</color> 【")
                  .Append(e.kind).Append("】 ").Append(e.note).Append('\n');
            }

            sb.Append("\n<color=#6f8a9a>※ 主命は君主の大方針が指揮系統で噛み砕かれた末端目標（MBO）。達成で武勲と恩義、月次評定で昇進。</color>");
            return sb.ToString();
        }

        private void AppendCascade(StringBuilder sb, ProtagonistCareerDirector d)
        {
            var c = d.Cascade;
            if (c == null || c.Count == 0) return;
            sb.Append("  <color=#9fb0c0>◇ 主命の落とし込み（MBO）</color>\n");
            for (int i = 0; i < c.Count; i++)
            {
                CascadeLevel lv = c[i];
                sb.Append("    ");
                for (int t = 0; t < i; t++) sb.Append("　");
                sb.Append("└ <color=#9ad0ff>").Append(d.RankName(lv.tier)).Append("</color> ")
                  .Append(d.ResolveName(lv.holderId)).Append("  規模 ").Append(Mathf.RoundToInt(lv.scope));
                if (lv.parentIndex >= 0 && lv.parentIndex < c.Count)
                {
                    float contrib = MandateCascadeRules.Contribution(lv, c[lv.parentIndex]);
                    sb.Append("  <color=#6f8a9a>(寄与 ").Append(Mathf.RoundToInt(contrib * 100f)).Append("%)</color>");
                }
                sb.Append('\n');
            }
        }

        // ===== 階級ピラミッド（グラフィカル）=====
        // 全階級を上（元帥＝頂点／細い）→下（二等兵＝底辺／太い）へ色付きの帯で積み、JSDF階級体系図のような
        // ピラミッドにする。帯の幅は階級序列で固定（人数ではなく階層）＝図と同じ。人数・現在地は毎フレーム更新。
        // 区分色：将官（赤系/青系）／士官（青）／准士官（金）／下士官（鋼）／兵卒（紺）。

        private void BuildPyramid(Transform parent)
        {
            pyramidRoot = new GameObject("RankPyramid");
            pyramidRoot.transform.SetParent(parent, false);
            pyramidRoot.AddComponent<RectTransform>();
            VerticalLayoutGroup vlg = pyramidRoot.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 4, 6);
            vlg.spacing = 0f; // 帯どうしを密着＝台形が繋がり滑らかな三角形になる
            vlg.childAlignment = TextAnchor.UpperCenter; // 帯を中央寄せ＝左右対称のピラミッド
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            int n = RankDistributionRules.GradeCount;
            pyramidRows = new RankPyramidRow[n];
            // 上（元帥＝index n-1）から下（二等兵＝index 0）へ並べる。
            for (int g = n - 1; g >= 0; g--)
                pyramidRows[g] = BuildPyramidRow(pyramidRoot.transform, (MilitaryGrade)g);

            // 次階級の定員空き（脚注）
            GameObject footObj = new GameObject("PyramidFooter");
            footObj.transform.SetParent(pyramidRoot.transform, false);
            footObj.AddComponent<RectTransform>();
            pyramidFooter = footObj.AddComponent<TextMeshProUGUI>();
            pyramidFooter.fontSize = bodyFontSize * 0.8f;
            pyramidFooter.color = new Color(0.44f, 0.54f, 0.60f);
            pyramidFooter.alignment = TextAlignmentOptions.Center;
            pyramidFooter.raycastTarget = false;
            ApplyJapaneseFont(pyramidFooter);
        }

        private RankPyramidRow BuildPyramidRow(Transform parent, MilitaryGrade grade)
        {
            int n = RankDistributionRules.GradeCount;
            // 滑らかな三角形にするため、帯を台形で描く。帯の上辺・下辺の幅を「ピラミッドの
            // その高さでの幅」にすると、隣の帯と辺が一致して階段にならない。
            // 縦位置 p（0=頂点＝元帥, n-1=底辺＝二等兵）。f=0 頂点/ f=1 底辺の幅 = lerp(apex, base, f)。
            int p = (n - 1) - (int)grade;
            float topW = Mathf.Lerp(pyramidMinBarWidth, pyramidMaxBarWidth, (float)p / n);
            float bottomW = Mathf.Lerp(pyramidMinBarWidth, pyramidMaxBarWidth, (float)(p + 1) / n);
            float midW = Mathf.Lerp(pyramidMinBarWidth, pyramidMaxBarWidth, (p + 0.5f) / n);

            // 行（レイアウトの子＝高さ固定・全幅）。中で帯を中央に置く。
            GameObject rowObj = new GameObject("Row_" + grade);
            rowObj.transform.SetParent(parent, false); // ★親(pyramidRoot)へ接続＝帯が描画される
            rowObj.AddComponent<RectTransform>();
            LayoutElement le = rowObj.AddComponent<LayoutElement>();
            le.minHeight = pyramidRowHeight; le.preferredHeight = pyramidRowHeight; le.flexibleHeight = 0f;

            // 色付き台形（中央寄せ・下辺幅でRT確保・上辺は内側へ）。行いっぱいの高さで密着＝三角形面。
            GameObject barObj = new GameObject("Bar");
            barObj.transform.SetParent(rowObj.transform, false);
            RectTransform barRT = barObj.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.5f, 0f);
            barRT.anchorMax = new Vector2(0.5f, 1f);
            barRT.pivot = new Vector2(0.5f, 0.5f);
            barRT.sizeDelta = new Vector2(bottomW, 0f);
            barRT.anchoredPosition = Vector2.zero;
            // PyramidBand は Graphic 派生＝CanvasRenderer 必須。RequireComponent の自動付与に依存せず明示的に先付け
            // （RectTransform を手動追加済みのGameObjectでは自動付与が効かずMissingComponentExceptionになる環境があるため）。
            barObj.AddComponent<CanvasRenderer>();
            PyramidBand bar = barObj.AddComponent<PyramidBand>();
            bar.SetTrapezoid(topW, bottomW);
            Color baseColor = CategoryColor(grade);
            bar.color = baseColor;
            // 帯をクリックでこの階級のネームド人物ウィンドウを開く（raycast 対象は台形）。
            Button barBtn = barObj.AddComponent<Button>();
            // 注意：本プロジェクトには Ginei.Selectable（艦隊選択）があるため UnityEngine.UI.Selectable を明示。
            barBtn.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            barBtn.targetGraphic = bar;
            ColorBlock cb = barBtn.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.fadeDuration = 0.06f;
            barBtn.colors = cb;
            MilitaryGrade captured = grade;
            barBtn.onClick.AddListener(() => OpenRankWindow(captured));

            // 階級章スプライト（帯の左端に配置）。既定は非表示＝スプライトが見つかった時だけ UpdatePyramid で出す。
            GameObject insObj = new GameObject("Insignia");
            insObj.transform.SetParent(barObj.transform, false);
            RectTransform insRT = insObj.AddComponent<RectTransform>();
            // 台形は上に向かって細くなる。アイコンを帯からはみ出させない（被り防止）ため、
            // 配置・サイズとも台形の最も狭い上辺(topW)を基準にする＝全高で帯の内側に収まる。
            float insSize = Mathf.Min(pyramidRowHeight - 6f, topW - 8f);
            insRT.anchorMin = new Vector2(0.5f, 0.5f);
            insRT.anchorMax = new Vector2(0.5f, 0.5f);
            insRT.pivot = new Vector2(0f, 0.5f);
            insRT.sizeDelta = new Vector2(insSize, insSize);
            insRT.anchoredPosition = new Vector2(-topW * 0.5f + 4f, 0f);
            Image insImg = insObj.AddComponent<Image>();
            insImg.raycastTarget = false;
            insImg.preserveAspect = true;
            insImg.enabled = false;

            // ラベル（帯の上に重ねる・全幅中央＝細い頂点でも文字が切れない）。
            GameObject lblObj = new GameObject("Label");
            lblObj.transform.SetParent(rowObj.transform, false);
            RectTransform lblRT = lblObj.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
            lblRT.sizeDelta = Vector2.zero; lblRT.anchoredPosition = Vector2.zero;
            TextMeshProUGUI lbl = lblObj.AddComponent<TextMeshProUGUI>();
            lbl.fontSize = pyramidLabelFontSize;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.color = Color.white;
            lbl.raycastTarget = false;
            lbl.richText = true;
            ApplyJapaneseFont(lbl);

            // 帯が細すぎて画像だと中央ラベルと被る階級（頂点付近）は、画像をやめてラベル先頭のインライン記号で出す。
            bool insigniaInline = topW < pyramidInsigniaMinBandWidth;
            return new RankPyramidRow { grade = grade, bar = bar, baseColor = baseColor, label = lbl, insignia = insImg, insigniaInline = insigniaInline };
        }

        // 毎フレーム：人数・現在地ハイライト・脚注を更新（行の生成はしない＝GC/再生成なし）。
        private void UpdatePyramid()
        {
            if (pyramidRoot == null || pyramidRows == null) return;
            var d = ProtagonistCareerDirector.Instance;
            Person me = d?.Protagonist;
            bool ready = me != null && MilitaryRankRegistry.Has(me.faction)
                         && RankDistributionRules.TotalForce(MilitaryRankRegistry.Get(me.faction)) > 0;
            pyramidRoot.SetActive(ready);
            if (!ready) return;

            RankDistribution dist = MilitaryRankRegistry.Get(me.faction);
            // 士官段（少尉〜元帥）の人数はネームド人物（OOB司令／艦隊台帳／主人公）の実数に合わせる。
            // 兵卒〜准尉はネームドが居ない＝マクロ集計（員数）をそのまま使う。
            CountNamedByTier(me.faction);
            MilitaryGrade myGrade = RankDistributionRules.GradeForOfficerTier(me.rankTier);
            for (int i = 0; i < pyramidRows.Length; i++)
            {
                RankPyramidRow row = pyramidRows[i];
                if (row == null) continue;
                bool mine = row.grade == myGrade;
                int tier = RankDistributionRules.ToOfficerTier(row.grade);
                int count = tier >= 1 ? namedCountByTier[tier] : dist.Get(row.grade);
                // 現在地は帯を金色寄りに明るく＋文字を金色＋（現在地）。
                row.bar.color = mine ? Color.Lerp(row.baseColor, new Color(1f, 0.84f, 0.2f), 0.45f) : row.baseColor;
                // 階級章：勢力別スプライト（Gemini生成）があれば画像で出し、無ければラベルのUnicode記号で代替。
                // ただし細い帯（頂点付近）は画像をやめ、ラベル先頭のインライン記号にして中央ラベルとの被りを防ぐ。
                Sprite sprite = row.insigniaInline ? null : LoadInsignia(me.faction, row.grade);
                if (row.insignia != null)
                {
                    row.insignia.enabled = sprite != null;
                    if (sprite != null) row.insignia.sprite = sprite;
                }
                string ins = sprite != null ? "" : InsigniaGlyph(row.grade);
                string nameCol = mine ? "<color=#fff0b0><b>" : "<color=#eef2f6>";
                string tail = mine ? "　<color=#ffe07a>◀ 現在地</color></b></color>" : "</color>";
                row.label.text = ins + " " + nameCol + row.grade.ToString()
                    + "</color>　<color=#cfe0ee>" + count + "</color><color=#9fb3c2>名</color>"
                    + (mine ? tail : "");
            }

            int total = RankDistributionRules.TotalForce(dist);
            int nextTier = Mathf.Min(10, me.rankTier + 1);
            int[] target = RankDistributionRules.PyramidTarget(total, RankDistributionRules.PyramidParams.Default);
            int vac = RankDistributionRules.Vacancy(dist, RankDistributionRules.GradeForOfficerTier(nextTier), target);
            bool can = RankPyramidDirector.Instance != null && RankPyramidDirector.Instance.CanPromote(me.faction, nextTier);
            pyramidFooter.text = "次階級 <color=#cfe0ee>" + d.RankName(nextTier) + "</color> の定員空き " + vac
                + (can ? "　<color=#8ce08c>（昇進余地あり）</color>" : "　<color=#e0a06a>（狭き門＝空き待ち）</color>");
        }

        // 区分ごとの帯色（銀英伝風のダーク基調・JSDF体系図の色帯を踏襲）。
        private static Color CategoryColor(MilitaryGrade g)
        {
            int tier = RankDistributionRules.ToOfficerTier(g);
            if (tier >= 8) return new Color(0.70f, 0.20f, 0.20f);   // 大将以上＝赤の頂点
            if (tier >= 5) return new Color(0.32f, 0.42f, 0.64f);   // 准将〜中将＝将官の青
            if (tier >= 1) return new Color(0.22f, 0.30f, 0.47f);   // 尉官・佐官＝士官の青
            if (g == MilitaryGrade.准尉) return new Color(0.60f, 0.48f, 0.20f); // 准士官＝金帯
            if (RankDistributionRules.IsNco(g)) return new Color(0.27f, 0.33f, 0.36f); // 下士官＝鋼
            return new Color(0.16f, 0.20f, 0.29f);                  // 兵卒＝紺
        }

        // 階級章（プレースホルダ＝Unicode記号。将官=金★／士官=銀✦／准士官=◈／下士官=シェブロン«／兵卒=▮）。
        // ★将来：Resources/RankInsignia/<grade>.png に銀英伝風スプライト（Gemini生成）を置き、TMP SpriteAsset 化して差し替える。
        private static string InsigniaGlyph(MilitaryGrade g)
        {
            int tier = RankDistributionRules.ToOfficerTier(g);
            if (tier >= 5) return "<color=#ffd24a>" + Repeat('★', tier - 4) + "</color>";   // 将官：1〜6★
            if (tier >= 1) return "<color=#d6dde6>" + Repeat('✦', tier) + "</color>";        // 士官：1〜4✦
            if (g == MilitaryGrade.准尉) return "<color=#e0c060>◈</color>";                   // 准士官
            if (RankDistributionRules.IsNco(g))
                return "<color=#c8ccd0>" + Repeat('«', (int)g - (int)MilitaryGrade.伍長 + 1) + "</color>"; // 下士官：1〜3
            return "<color=#7e8a98>" + Repeat('▮', (int)g - (int)MilitaryGrade.二等兵 + 1) + "</color>";   // 兵卒：1〜4
        }

        private static string Repeat(char c, int n)
        {
            if (n <= 0) return "";
            return new string(c, Mathf.Clamp(n, 1, 6));
        }

        // 勢力別の階級章スプライトを読む（Resources/RankInsignia/<勢力>/<階級>.png＝Gemini生成）。
        // 例：Resources/RankInsignia/帝国/元帥.png ／ Resources/RankInsignia/同盟/中将.png。
        // 見つからなければ null（→ラベルのUnicode記号にフォールバック）。結果はキャッシュ（null も）。
        private Sprite LoadInsignia(Faction faction, MilitaryGrade grade)
        {
            string key = faction + "/" + grade;
            if (insigniaCache.TryGetValue(key, out var cached)) return cached;
            Sprite s = Resources.Load<Sprite>("RankInsignia/" + key);
            insigniaCache[key] = s;
            return s;
        }

        // ===== ネームド人物の集約（OOB司令＋艦隊台帳の司令/副提督/参謀＋主人公）=====

        // 勢力の士官 tier 別ネームド人数を namedCountByTier に集計（再利用バッファ＝GC回避）。
        private void CountNamedByTier(Faction f)
        {
            System.Array.Clear(namedCountByTier, 0, namedCountByTier.Length);
            EnumerateNamed(f, (name, tier, role, adm) =>
            {
                if (tier >= 1 && tier <= 10) namedCountByTier[tier]++;
            });
        }

        // 勢力のネームド士官を走査して visit を呼ぶ（重複は AdmiralData/Person 参照で排除）。
        private void EnumerateNamed(Faction f, System.Action<string, int, string, AdmiralData> visit)
        {
            namedDedup.Clear();

            // 主人公（Person・名鑑には居ないことが多いので明示的に加える）
            var dir = ProtagonistCareerDirector.Instance;
            Person me = dir != null ? dir.Protagonist : null;
            if (me != null && me.faction == f && namedDedup.Add(me))
                visit(me.name, me.rankTier, "あなた", null);

            // 編制ツリー OrderOfBattle の各梯団司令
            var formations = OrderOfBattle.AllFormations(f);
            if (formations != null)
                for (int i = 0; i < formations.Count; i++)
                {
                    var node = formations[i];
                    if (node?.commander != null && namedDedup.Add(node.commander))
                        visit(node.commander.FullName, node.commander.rankTier, node.echelon + "司令", node.commander);
                }

            // 艦隊台帳 FleetRoster の司令・副提督・参謀
            var fleets = FleetRoster.AllFleets(f);
            if (fleets != null)
                for (int i = 0; i < fleets.Count; i++)
                {
                    var u = fleets[i];
                    if (u == null) continue;
                    VisitAdmiral(u.assignedAdmiral, u.DisplayName + " 司令", visit);
                    VisitAdmiral(u.viceCommander, u.DisplayName + " 副提督", visit);
                    VisitAdmiral(u.chiefOfStaff, u.DisplayName + " 参謀", visit);
                }
        }

        private void VisitAdmiral(AdmiralData a, string role, System.Action<string, int, string, AdmiralData> visit)
        {
            if (a == null || !namedDedup.Add(a)) return;
            visit(a.FullName, a.rankTier, role, a);
        }

        // ===== 階級別ネームド人物ウィンドウ（帯クリックで開く）=====

        private void OpenRankWindow(MilitaryGrade grade)
        {
            if (rankWindowRoot == null) BuildRankWindow();
            var dir = ProtagonistCareerDirector.Instance;
            Person me = dir != null ? dir.Protagonist : null;
            int tier = RankDistributionRules.ToOfficerTier(grade);

            rankWindowTitle.text = "<b>" + grade + "</b> の人物";

            var sb = new StringBuilder(512);
            if (tier < 1)
            {
                // 兵卒〜准尉はネームド人物を持たない（員数のみ）。
                int n = me != null && MilitaryRankRegistry.Has(me.faction)
                    ? MilitaryRankRegistry.Get(me.faction).Get(grade) : 0;
                sb.Append("<color=#9aa7b2>この階級にネームド人物はいません（員数のみ）。</color>\n\n");
                sb.Append("<color=#8aa0b0>員数 </color><color=#cfe0ee>").Append(n.ToString("#,0")).Append("</color> 名");
            }
            else if (me == null)
            {
                sb.Append("<color=#9aa7b2>（立身出世ループ未起動）</color>");
            }
            else
            {
                var list = new List<NamedOfficer>();
                EnumerateNamed(me.faction, (name, t, role, adm) =>
                {
                    if (t == tier) list.Add(new NamedOfficer { name = name, tier = t, role = role, admiral = adm });
                });
                list.Sort((x, y) => string.CompareOrdinal(x.role, y.role));

                if (list.Count == 0)
                    sb.Append("<color=#9aa7b2>この階級のネームド人物はいません。</color>");
                else
                {
                    sb.Append("<color=#8aa0b0>計 ").Append(list.Count).Append(" 名</color>\n");
                    for (int i = 0; i < list.Count; i++)
                    {
                        NamedOfficer o = list[i];
                        bool isMe = o.admiral == null;
                        sb.Append('\n').Append(isMe ? "<color=#ffe07a>★ " : "<color=#bfe9c0>◆ ")
                          .Append(o.name).Append("</color>");
                        sb.Append("　<color=#7f93a6>").Append(o.role).Append("</color>\n");
                        if (o.admiral != null)
                            sb.Append("   <color=#9fb0c0>統率</color> ").Append(o.admiral.EffectiveLeadership)
                              .Append(" ／ <color=#9fb0c0>攻撃</color> ").Append(o.admiral.EffectiveAttack)
                              .Append(" ／ <color=#9fb0c0>防御</color> ").Append(o.admiral.EffectiveDefense)
                              .Append(" ／ <color=#9fb0c0>機動</color> ").Append(o.admiral.EffectiveMobility).Append('\n');
                    }
                }
            }
            rankWindowBody.text = sb.ToString();
            rankWindowRoot.SetActive(true);
            rankWindowRoot.transform.SetAsLastSibling(); // 最前面へ
        }

        private void CloseRankWindow() { if (rankWindowRoot != null) rankWindowRoot.SetActive(false); }

        private void BuildRankWindow()
        {
            GameObject frame = new GameObject("RankPersonWindow");
            frame.transform.SetParent(overlayRoot.transform, false);
            RectTransform frameRT = frame.AddComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0.5f, 0.5f);
            frameRT.anchorMax = new Vector2(0.5f, 0.5f);
            frameRT.pivot = new Vector2(0.5f, 0.5f);
            frameRT.anchoredPosition = new Vector2(260f, 0f);
            frameRT.sizeDelta = new Vector2(460f, 560f);
            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = new Color(0.06f, 0.07f, 0.10f, 0.98f);

            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 10, 12);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            WindowChrome.AddTitleBarLayout(frameRT, "階級の人物", CloseRankWindow);

            // タイトル（クリックした階級名）
            GameObject titleObj = new GameObject("RankTitle");
            titleObj.transform.SetParent(frame.transform, false);
            titleObj.AddComponent<RectTransform>();
            rankWindowTitle = titleObj.AddComponent<TextMeshProUGUI>();
            rankWindowTitle.fontSize = bodyFontSize;
            rankWindowTitle.color = new Color(0.92f, 0.95f, 1f);
            rankWindowTitle.raycastTarget = false;
            ApplyJapaneseFont(rankWindowTitle);

            // 本文（スクロール）
            GameObject scrollObj = new GameObject("RankScroll");
            scrollObj.transform.SetParent(frame.transform, false);
            scrollObj.AddComponent<RectTransform>();
            LayoutElement scrollLE = scrollObj.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;
            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false; scrollRect.vertical = true; scrollRect.scrollSensitivity = 24f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero; viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero; viewportRT.anchoredPosition = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f); contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero; contentRT.sizeDelta = Vector2.zero;
            VerticalLayoutGroup cVlg = content.AddComponent<VerticalLayoutGroup>();
            cVlg.childControlWidth = true; cVlg.childControlHeight = true;
            cVlg.childForceExpandWidth = true; cVlg.childForceExpandHeight = false;
            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRT;

            GameObject bodyObj = new GameObject("RankBody");
            bodyObj.transform.SetParent(content.transform, false);
            rankWindowBody = bodyObj.AddComponent<TextMeshProUGUI>();
            rankWindowBody.fontSize = bodyFontSize * 0.85f;
            rankWindowBody.color = new Color(0.9f, 0.93f, 0.96f);
            rankWindowBody.alignment = TextAlignmentOptions.TopLeft;
            rankWindowBody.richText = true; rankWindowBody.raycastTarget = false;
            ApplyJapaneseFont(rankWindowBody);

            // Esc で（執務机より先に）この小窓を閉じる。
            rankWindowEscToken = UIWindowStack.Register(
                () => rankWindowRoot != null && rankWindowRoot.activeSelf, CloseRankWindow,
                canvasSortingOrder + 5, "階級の人物");

            rankWindowRoot = frame;
            rankWindowRoot.SetActive(false);
        }

        // 会戦成長（GrowthRegistry）を反映した実効能力を1行表示（基準→実効・成長分を+表示）。
        private void AppendStat(StringBuilder sb, string label, int baseStat, Growth g)
        {
            int grown = AdmiralGrowthRules.GrownStat(baseStat, g);
            sb.Append("  ").Append(label).Append("  <color=#9ad0ff>").Append(grown).Append("</color>");
            if (grown > baseStat) sb.Append(" <color=#8ce08c>(+").Append(grown - baseStat).Append(")</color>");
            sb.Append('\n');
        }

        private static string ForkColor(CareerFork f)
        {
            switch (f)
            {
                case CareerFork.忠勤: return "<color=#9ad0ff>";
                case CareerFork.政界転身: return "<color=#c9a0ff>";
                case CareerFork.独立: return "<color=#ffd700>";
                case CareerFork.亡命: return "<color=#ff9a8a>";
                default: return "<color=#e0c060>"; // 下野
            }
        }

        private void AppendBar(StringBuilder sb, float v01, string colorHex)
        {
            v01 = Mathf.Clamp01(v01);
            int filled = Mathf.RoundToInt(v01 * barWidth);
            sb.Append("<color=").Append(colorHex).Append('>');
            for (int i = 0; i < barWidth; i++) sb.Append(i < filled ? '█' : '░');
            sb.Append("</color> ").Append(v01.ToString("0.00"));
        }

        // 上段テキスト・階級ピラミッド・下段テキストをまとめて即時更新（ボタン操作の直後など）。
        private void RefreshNow()
        {
            if (bodyLabel != null) bodyLabel.text = BuildUpperDump();
            if (bodyLabelLower != null) bodyLabelLower.text = BuildLowerDump();
            UpdatePyramid();
        }

        private void OnSubmitPetition()
        {
            var d = ProtagonistCareerDirector.Instance;
            if (d != null) d.SubmitPetition();
            RefreshNow();
        }

        private void OnSubmitFleetBudgetPetition()
        {
            var d = ProtagonistCareerDirector.Instance;
            if (d != null) d.SubmitFleetBudgetPetition();
            RefreshNow();
        }

        // 岐路の実行ボタン（TKO-7・一人称の自由意志）。押すと director.ExecuteFork が開かれた進路か＋前提を確認して実行。
        private void BuildForkButtons(Transform parent)
        {
            GameObject row = new GameObject("ForkButtonRow");
            row.transform.SetParent(parent, false);
            row.AddComponent<RectTransform>();
            LayoutElement le = row.AddComponent<LayoutElement>();
            // 高さは行を 38px に固定（flexibleHeight=0 で縦に伸びないようにする＝ボタンが巨大化しない）。
            le.minHeight = 38f; le.preferredHeight = 38f; le.flexibleHeight = 0f;
            HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            CareerFork[] forks = { CareerFork.忠勤, CareerFork.下野, CareerFork.政界転身, CareerFork.亡命, CareerFork.独立 };
            for (int i = 0; i < forks.Length; i++) BuildForkButton(row.transform, forks[i]);
        }

        private void BuildForkButton(Transform parent, CareerFork fork)
        {
            GameObject go = new GameObject("Fork_" + fork);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.18f, 0.24f, 1f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnFork(fork));

            GameObject lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lrt = lblGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = Vector2.zero; lrt.anchoredPosition = Vector2.zero;
            TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = fork.ToString();
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.fontSize = 16f;
            lbl.color = new Color(0.9f, 0.93f, 1f);
            lbl.raycastTarget = false;
            ApplyJapaneseFont(lbl);
        }

        private void OnFork(CareerFork fork)
        {
            var d = ProtagonistCareerDirector.Instance;
            if (d != null) d.ExecuteFork(fork);
            RefreshNow();
        }

        // ===== UI 構築（RingiObserverOverlay と同型＋具申ボタン） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("ProtagonistDeskCanvas");
            overlayRoot.transform.SetParent(transform);
            Canvas canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            overlayRoot.AddComponent<GraphicRaycaster>();

            panel = new GameObject("DeskPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero;
            panelRT.anchoredPosition = Vector2.zero;
            Image dimImage = panel.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, dimAlpha);
            WindowChrome.MakeNonModal(dimImage);

            BuildContentPanel(panel.transform);
        }

        private void BuildContentPanel(Transform parent)
        {
            GameObject frame = new GameObject("DeskFrame");
            frame.transform.SetParent(parent, false);
            RectTransform frameRT = frame.AddComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0f, 0.5f);
            frameRT.anchorMax = new Vector2(0f, 0.5f);
            frameRT.pivot = new Vector2(0f, 0.5f);
            frameRT.anchoredPosition = new Vector2(24f, 0f);
            frameRT.sizeDelta = new Vector2(panelWidth, panelMaxHeight);

            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = panelColor;

            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 12, 12);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            WindowChrome.AddTitleBarLayout(frameRT, "執務机", () => SetVisible(false));
            BuildPetitionButton(frame.transform);
            BuildFleetBudgetPetitionButton(frame.transform);
            BuildForkButtons(frame.transform);
            BuildScrollBody(frame.transform);
        }

        private void BuildPetitionButton(Transform parent)
        {
            GameObject go = new GameObject("SubmitPetitionButton");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 42f; le.preferredHeight = 42f;
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.30f, 0.46f, 1f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnSubmitPetition);

            GameObject lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lrt = lblGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = Vector2.zero; lrt.anchoredPosition = Vector2.zero;
            TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = "上官へ具申する（建白を起案）";
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.fontSize = 18f;
            lbl.color = new Color(0.92f, 0.95f, 1f);
            lbl.raycastTarget = false;
            ApplyJapaneseFont(lbl);
        }

        /// <summary>自艦隊への予算増額を上官へ具申するボタン（TKO-4 の具体例＝effectKey に直列化して稟議へ）。</summary>
        private void BuildFleetBudgetPetitionButton(Transform parent)
        {
            GameObject go = new GameObject("SubmitFleetBudgetPetitionButton");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 42f; le.preferredHeight = 42f;
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.20f, 0.34f, 0.30f, 1f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnSubmitFleetBudgetPetition);

            GameObject lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lrt = lblGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = Vector2.zero; lrt.anchoredPosition = Vector2.zero;
            TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = "自艦隊への予算増額を具申する";
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.fontSize = 18f;
            lbl.color = new Color(0.9f, 1f, 0.95f);
            lbl.raycastTarget = false;
            ApplyJapaneseFont(lbl);
        }

        private void BuildScrollBody(Transform parent)
        {
            GameObject scrollObj = new GameObject("DeskScrollRect");
            scrollObj.transform.SetParent(parent, false);
            scrollObj.AddComponent<RectTransform>();
            LayoutElement scrollLE = scrollObj.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;

            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero;
            viewportRT.anchoredPosition = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.padding = new RectOffset(8, 8, 4, 4);
            contentVlg.childAlignment = TextAnchor.UpperLeft;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;

            // 上段テキスト → グラフィカル階級ピラミッド → 下段テキスト、の順に縦に並べる。
            bodyLabel = BuildBodyLabel(content.transform, "BodyUpper");
            BuildPyramid(content.transform);
            bodyLabelLower = BuildBodyLabel(content.transform, "BodyLower");
        }

        private TextMeshProUGUI BuildBodyLabel(Transform parent, string name)
        {
            GameObject bodyObj = new GameObject(name);
            bodyObj.transform.SetParent(parent, false);
            TextMeshProUGUI lbl = bodyObj.AddComponent<TextMeshProUGUI>();
            lbl.text = "";
            lbl.fontSize = bodyFontSize;
            lbl.color = new Color(0.9f, 0.93f, 0.96f);
            lbl.alignment = TextAlignmentOptions.TopLeft;
            lbl.richText = true;
            lbl.raycastTarget = false;
            ApplyJapaneseFont(lbl);
            return lbl;
        }

        private static void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }
}
