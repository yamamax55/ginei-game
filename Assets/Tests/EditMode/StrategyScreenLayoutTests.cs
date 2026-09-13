using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦略画面の割り付けの不変条件を固定する。実機QAで出た重なり（勝敗メーターが MAP 右上に被って
    /// 星名を隠す／通知が MAP 下部に被って星と凡例を隠す）を、数値の取り決めの段階で捕まえる。
    /// 割合で持つので 1920x1080 と 2560x1440（同じ16:9）では同じ結果になる＝両解像度ぶんの検証になる。
    /// </summary>
    public class StrategyScreenLayoutTests
    {
        // 実測の設計ピクセル（参照解像度 1080 基準）。
        // 通知＝タイトル30＋タブ28＋5行×26＝130＋余白14 で約202、下余白40。
        private const float NotificationHeightPx = 202f;
        private const float NotificationMarginPx = 40f;
        private const float MenuBarFrac = 0.10f;

        [Test]
        public void Default_MapTakesAboutThreeQuartersOfWidth()
        {
            var l = StrategyScreenLayout.Default;
            Assert.GreaterOrEqual(l.mapWidth, 0.70f, "MAP が画面の主役になっていない");
            Assert.LessOrEqual(l.mapWidth, 0.78f, "MAP が広すぎて右カラムが潰れる");
        }

        [Test]
        public void RightColumn_DoesNotOverlapMap()
        {
            var l = StrategyScreenLayout.Default;
            Assert.IsTrue(StrategyScreenLayoutRules.RightColumnClearsMap(l),
                "右カラム（勝敗メーター/決裁デスク）が MAP に被る＝星名が隠れる");
            Assert.Greater(l.RightColumnLeft, l.MapRight, "右カラムの左端が MAP の右端より内側にある");
        }

        [Test]
        public void RightColumn_HasRoomForTheMeter()
        {
            var l = StrategyScreenLayout.Default;
            float columnPx = StrategyScreenLayoutRules.ToDesignWidth(l.RightColumnWidth);
            // メーターは 3本＋2本の2段。1段=3バー＋間隔＋余白 が収まる幅が要る。
            Assert.Greater(columnPx, 380f, $"右カラムが狭すぎてメーターが入らない（{columnPx:F0}px）");
            // 5本を1段に並べる旧構成（660px）は入らないこと＝2段化が必要だった根拠を固定する。
            Assert.Less(columnPx, 660f, "この幅なら1段でも入るはずで、2段化の前提が崩れている");
        }

        [Test]
        public void Notification_FitsBelowTheMap()
        {
            var l = StrategyScreenLayout.Default;
            float hFrac = StrategyScreenLayoutRules.HeightFracOfDesign(NotificationHeightPx);
            float mFrac = StrategyScreenLayoutRules.HeightFracOfDesign(NotificationMarginPx);
            Assert.IsTrue(StrategyScreenLayoutRules.BottomPanelClearsMap(l, hFrac, mFrac),
                $"通知（高さ{NotificationHeightPx}px＋余白{NotificationMarginPx}px）が MAP 下端({l.mapBottom:F3})に収まらない");
        }

        [Test]
        public void Map_DoesNotOverlapMenuBar()
        {
            var l = StrategyScreenLayout.Default;
            Assert.IsTrue(StrategyScreenLayoutRules.MapClearsMenuBar(l, MenuBarFrac),
                "MAP の上端が上メニューに食い込む");
        }

        [Test]
        public void Map_KeepsAUsableHeight()
        {
            var l = StrategyScreenLayout.Default;
            Assert.Greater(l.MapHeight, 0.55f, "MAP が低すぎる（銀河が縦に潰れる）");
        }

        [Test]
        public void Rules_RejectAnOverlappingLayout()
        {
            // 取り決めを崩した場合はちゃんと false になること（テスト自体が効いている確認）。
            var bad = new StrategyScreenLayout(0.01f, 0.95f, 0.245f, 0.885f, 0.008f, 0.006f);
            Assert.IsFalse(StrategyScreenLayoutRules.RightColumnClearsMap(bad));

            var shallow = new StrategyScreenLayout(0.012f, 0.740f, 0.10f, 0.885f, 0.008f, 0.006f);
            float hFrac = StrategyScreenLayoutRules.HeightFracOfDesign(NotificationHeightPx);
            float mFrac = StrategyScreenLayoutRules.HeightFracOfDesign(NotificationMarginPx);
            Assert.IsFalse(StrategyScreenLayoutRules.BottomPanelClearsMap(shallow, hFrac, mFrac));
        }

        // 実機で想定する画面サイズ（16:9 / 4:3 / 21:9 / 縦長・小窓）。
        private static readonly (int w, int h, string name)[] Screens =
        {
            (1920, 1080, "16:9 FHD"),
            (2560, 1440, "16:9 QHD"),
            (1366, 768, "16:9 ノート"),
            (1024, 768, "4:3"),
            (2560, 1080, "21:9 ウルトラワイド"),
            (900, 1200, "縦長"),
            (1280, 800, "16:10 小窓"),
        };

        [Test]
        public void ForScreen_KeepsTheNotificationOnScreenAtEveryAspect()
        {
            for (int i = 0; i < Screens.Length; i++)
            {
                var (w, h, name) = Screens[i];
                var l = StrategyScreenLayoutRules.ForScreen(w, h);
                Assert.IsTrue(StrategyScreenLayoutRules.BottomPanelFitsOnScreen(l, w, h),
                    $"{name}({w}x{h}) で通知が MAP 下端に収まらない＝画面外へ出る");
            }
        }

        [Test]
        public void ForScreen_FixedLayoutOverflowsOnUltrawide_WhichIsWhyWeAdapt()
        {
            // 固定の 0.245 は 21:9 で破綻する（適応が必要な根拠を固定する）。
            var fixedLayout = StrategyScreenLayout.Default;
            Assert.IsFalse(StrategyScreenLayoutRules.BottomPanelFitsOnScreen(fixedLayout, 2560, 1080),
                "21:9 で固定レイアウトが破綻しない＝適応の前提が崩れている");

            var adapted = StrategyScreenLayoutRules.ForScreen(2560, 1080);
            Assert.IsTrue(StrategyScreenLayoutRules.BottomPanelFitsOnScreen(adapted, 2560, 1080));
            Assert.Greater(adapted.mapBottom, fixedLayout.mapBottom, "21:9 では下端を上げる必要がある");
        }

        [Test]
        public void ForScreen_GivesTheMapMoreHeightOnTallScreens()
        {
            var tall = StrategyScreenLayoutRules.ForScreen(900, 1200);
            var wide = StrategyScreenLayoutRules.ForScreen(1920, 1080);
            Assert.Less(tall.mapBottom, wide.mapBottom, "縦長画面で下の余白が無駄に残っている");
            Assert.Greater(tall.MapHeight, wide.MapHeight);
        }

        [Test]
        public void ForScreen_AlwaysKeepsAUsableMapAndNoOverlap()
        {
            for (int i = 0; i < Screens.Length; i++)
            {
                var (w, h, name) = Screens[i];
                var l = StrategyScreenLayoutRules.ForScreen(w, h);
                Assert.IsTrue(StrategyScreenLayoutRules.RightColumnClearsMap(l), $"{name}: 右カラムが MAP に被る");
                Assert.IsTrue(StrategyScreenLayoutRules.MapClearsMenuBar(l, MenuBarFrac), $"{name}: MAP が上メニューに食い込む");
                Assert.Greater(l.MapHeight, 0.35f, $"{name}: MAP が低すぎて銀河が潰れる");
                Assert.Less(l.mapBottom, 0.50f, $"{name}: 下帯が広すぎる");
            }
        }

        [Test]
        public void ForScreen_LeavesAVisibleGapAboveTheNotification()
        {
            // 実機QA（フルHD）で MAP 下辺が通知上辺に 16px 被った。どの画面でも実ピクセルの隙間が残ること。
            for (int i = 0; i < Screens.Length; i++)
            {
                var (w, h, name) = Screens[i];
                var l = StrategyScreenLayoutRules.ForScreen(w, h);
                float uiScale = w / StrategyScreenLayoutRules.ReferenceWidth;
                float bandPx = (StrategyScreenLayoutRules.NotificationDesignHeight
                              + StrategyScreenLayoutRules.NotificationDesignMargin) * uiScale;
                float gapPx = l.mapBottom * h - bandPx;
                Assert.GreaterOrEqual(gapPx, 16f, $"{name}({w}x{h}) の隙間が {gapPx:F0}px しかない（重なりの再発）");
            }
        }

        [Test]
        public void ForScreen_IsSafeForDegenerateSizes()
        {
            Assert.AreEqual(StrategyScreenLayout.Default.mapBottom, StrategyScreenLayoutRules.ForScreen(0, 0).mapBottom, 1e-4f);
            Assert.AreEqual(StrategyScreenLayout.Default.mapBottom, StrategyScreenLayoutRules.ForScreen(-5, 100).mapBottom, 1e-4f);
        }

        [Test]
        public void MinDesignForActual_KeepsTextReadableOnNarrowScreens()
        {
            // 縦長 900x1200 では幅基準スケールが 0.469 倍＝設計18ptが実8pxまで縮む。
            // 最低14px を要求すると設計値が引き上げられ、実ピクセルで読める大きさになる。
            const float minPx = 14f;
            for (int i = 0; i < Screens.Length; i++)
            {
                var (w, h, name) = Screens[i];
                float design = StrategyScreenLayoutRules.MinDesignForActual(18f, minPx, w);
                float actual = design * (w / StrategyScreenLayoutRules.ReferenceWidth);
                Assert.GreaterOrEqual(actual, minPx - 0.01f, $"{name}({w}x{h}) で文字が {actual:F1}px まで縮む");
            }
        }

        [Test]
        public void MinDesignForActual_DoesNotShrinkOnWideScreens()
        {
            // 広い画面では設計値をそのまま使う（無駄に大きくしない）。
            Assert.AreEqual(18f, StrategyScreenLayoutRules.MinDesignForActual(18f, 14f, 2560f), 1e-3f);
            Assert.AreEqual(18f, StrategyScreenLayoutRules.MinDesignForActual(18f, 14f, 1920f), 1e-3f);
            // 退化入力でも落ちない。
            Assert.AreEqual(18f, StrategyScreenLayoutRules.MinDesignForActual(18f, 14f, 0f), 1e-3f);
        }

        [Test]
        public void DesignPixelConversion_IsConsistent()
        {
            Assert.AreEqual(1920f * 0.25f, StrategyScreenLayoutRules.ToDesignWidth(0.25f), 1e-3f);
            Assert.AreEqual(1080f * 0.25f, StrategyScreenLayoutRules.ToDesignHeight(0.25f), 1e-3f);
            Assert.AreEqual(0.25f, StrategyScreenLayoutRules.HeightFracOfDesign(270f), 1e-4f);
        }
    }
}
