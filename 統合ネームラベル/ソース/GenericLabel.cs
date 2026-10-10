using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace UnifiedLabels
{
    /// <summary>自由入力の汎用ラベル（顧客別仕様とは独立）。</summary>
    public sealed class GenericLabelData
    {
        public bool Small = true;
        public string Family = "HG丸ｺﾞｼｯｸM-PRO";
        public int Rows = 1;
        public readonly string[] Texts = new string[] { "", "", "" };
        public readonly float[] Sizes = new float[] { 26f, 26f, 26f };
        /// <summary>段内の左右オフセット(mm)。0＝横中央。正で右へ。</summary>
        public readonly float[] OffsetXMm = new float[] { 0f, 0f, 0f };
        /// <summary>段内の上下オフセット(mm)。0＝縦中央。正で下へ。</summary>
        public readonly float[] OffsetYMm = new float[] { 0f, 0f, 0f };

        public GenericLabelData Clone()
        {
            GenericLabelData c = new GenericLabelData { Small = Small, Family = Family, Rows = Rows };
            for (int i = 0; i < 3; i++)
            {
                c.Texts[i] = Texts[i];
                c.Sizes[i] = Sizes[i];
                c.OffsetXMm[i] = OffsetXMm[i];
                c.OffsetYMm[i] = OffsetYMm[i];
            }
            return c;
        }
    }

    public static class GenericLabelRenderer
    {
        public const string SettingsIdentity = "汎用ラベル";
        const float MarginMm = 1f;
        /// <summary>段内位置の調整上限(mm)。帯／枠内に収まる範囲でさらに検証する。</summary>
        public const float MaxRowOffsetMm = 20f;

        public static Size Millimetres(bool small) { return small ? new Size(50, 24) : new Size(80, 50); }
        /// <summary>ラベルサイズごとの既定pt（段数によらない）。大48／小26。</summary>
        public static float DefaultSizePt(bool small) { return small ? 26f : 48f; }
        public static string FamilyName(string display) { return display == "MS Pゴシック" ? "MS PGothic" : "HG丸ｺﾞｼｯｸM-PRO"; }
        public static string FamilyDisplay(string family) { return family == "MS PGothic" ? "MS Pゴシック" : "丸ゴシック"; }
        public static string OffsetKey(string printer, bool small)
        {
            return printer + "|" + SettingsIdentity + "|" + (small ? "小" : "大");
        }
        static RectangleF Mm(float x, float y, float w, float h)
        {
            return new RectangleF(x / 0.254f, y / 0.254f, w / 0.254f, h / 0.254f);
        }
        static string RowText(GenericLabelData d, int i)
        {
            return (d.Texts[i] ?? "").Replace('　', ' ');
        }
        static Font MakeFont(string family, float points)
        {
            return new Font(family, points * 100f / 72f, LabelCatalog.StyleFor(family), GraphicsUnit.Pixel);
        }

        /// <summary>ラベル高さを段数で等分した帯（mm）。1段=全高、2段=各50%、3段=各1/3。</summary>
        public static float BandHeightMm(bool small, int rows)
        {
            int n = Math.Max(1, Math.Min(3, rows));
            return Millimetres(small).Height / (float)n;
        }

        /// <summary>段の描画・Fit用領域（左右は余白込み、高さは等分帯）。</summary>
        public static RectangleF BandArea(bool small, int rows, int index)
        {
            Size mm = Millimetres(small);
            int n = Math.Max(1, Math.Min(3, rows));
            float bandHmm = mm.Height / (float)n;
            return Mm(MarginMm, index * bandHmm, mm.Width - 2 * MarginMm, bandHmm);
        }

        /// <summary>指定ptを上限に、顧客ラベルと同じ LabelCatalog.Fit で段帯へ収める。</summary>
        public static float FittedSize(Graphics g, GenericLabelData d, int index)
        {
            string text = RowText(d, index);
            return LabelCatalog.Fit(g, text, d.Family, BandArea(d.Small, d.Rows, index), d.Sizes[index]);
        }

        /// <summary>画面プレビュー・印刷プレビュー・印刷で共有する描画。</summary>
        public static void Draw(Graphics g, GenericLabelData d)
        {
            int n = Math.Max(1, Math.Min(3, d.Rows));
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                // Fit と同じく NoWrap のみ（顧客 Line と揃える）。PointF 描画は Mono 向け。
                format.FormatFlags |= StringFormatFlags.NoWrap;
                for (int i = 0; i < n; i++)
                {
                    string text = RowText(d, i);
                    if (String.IsNullOrWhiteSpace(text)) continue;
                    RectangleF band = BandArea(d.Small, d.Rows, i);
                    float size = FittedSize(g, d, i);
                    using (Font f = MakeFont(d.Family, size))
                    {
                        SizeF measured = g.MeasureString(text, f, new PointF(), format);
                        float ox = d.OffsetXMm[i] / 0.254f;
                        float oy = d.OffsetYMm[i] / 0.254f;
                        float x = band.Left + (band.Width - measured.Width) / 2f + ox;
                        float y = band.Top + (band.Height - measured.Height) / 2f + oy;
                        g.DrawString(text, f, Brushes.Black, new PointF(x, y), format);
                    }
                }
            }
        }

        public static string Validate(GenericLabelData d)
        {
            int n = Math.Max(1, Math.Min(3, d.Rows));
            for (int i = 0; i < n; i++)
            {
                if (String.IsNullOrWhiteSpace(RowText(d, i)))
                    return (i + 1) + "段目の文字を入力してください。";
                if (d.Sizes[i] < 6f || d.Sizes[i] > 120f)
                    return (i + 1) + "段目の文字サイズは6～120ptで指定してください。";
                if (Math.Abs(d.OffsetXMm[i]) > MaxRowOffsetMm || Math.Abs(d.OffsetYMm[i]) > MaxRowOffsetMm)
                    return (i + 1) + "段目の位置調整は±" + MaxRowOffsetMm.ToString("0") + "mm以内にしてください。";
            }
            using (Bitmap probe = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(probe))
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.FormatFlags |= StringFormatFlags.NoWrap;
                for (int i = 0; i < n; i++)
                {
                    string text = RowText(d, i);
                    RectangleF band = BandArea(d.Small, d.Rows, i);
                    float size = FittedSize(g, d, i);
                    using (Font f = MakeFont(d.Family, size))
                    {
                        SizeF measured = g.MeasureString(text, f, new PointF(), format);
                        float ox = d.OffsetXMm[i] / 0.254f;
                        float oy = d.OffsetYMm[i] / 0.254f;
                        float x = band.Left + (band.Width - measured.Width) / 2f + ox;
                        float y = band.Top + (band.Height - measured.Height) / 2f + oy;
                        // 自動縮小後でも段内オフセットではみ出す場合のみ拒否（長い文字自体は Fit で吸収）。
                        if (x < band.Left - 0.5f || x + measured.Width > band.Right + 0.5f)
                            return (i + 1) + "段目の文字が左右にはみ出します。左右位置を調整するか文字を短くしてください。";
                        if (y < band.Top - 0.5f || y + measured.Height > band.Bottom + 0.5f)
                            return (i + 1) + "段目の文字が段の上下にはみ出します。上下位置を調整してください。";
                    }
                }
            }
            return null;
        }
    }

    public sealed class GenericLabelForm : Form
    {
        readonly UserSettings preferences;
        readonly ComboBox sizeBox = new ComboBox(), fontBox = new ComboBox(), rowsBox = new ComboBox();
        readonly ComboBox printers = new ComboBox(), papers = new ComboBox();
        readonly TextBox[] rowTexts = { new TextBox(), new TextBox(), new TextBox() };
        readonly NumericUpDown[] rowSizes = { new NumericUpDown(), new NumericUpDown(), new NumericUpDown() };
        readonly NumericUpDown[] rowOffsetX = { new NumericUpDown(), new NumericUpDown(), new NumericUpDown() };
        readonly NumericUpDown[] rowOffsetY = { new NumericUpDown(), new NumericUpDown(), new NumericUpDown() };
        readonly Label[] rowCaptions = { new Label(), new Label(), new Label() };
        readonly Label[] sizeCaptions = { new Label(), new Label(), new Label() };
        readonly Label[] offsetXCaptions = { new Label(), new Label(), new Label() };
        readonly Label[] offsetYCaptions = { new Label(), new Label(), new Label() };
        readonly Panel[] rowPanels = { new Panel(), new Panel(), new Panel() };
        readonly NumericUpDown copies = new NumericUpDown();
        readonly PictureBox preview = new PictureBox();
        readonly Label status = new Label(), guide = new Label(), offsetSummary = new Label();
        readonly PrintDocument document = new PrintDocument();
        readonly GenericLabelData draft = new GenericLabelData();
        PrintOffset temporaryOffset;
        bool changing;

        public GenericLabelForm(UserSettings settings)
        {
            preferences = settings;
            Text = "汎用ラベル";
            Font = new Font("Yu Gothic UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(980, 720);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label { Text = "ラベルサイズ", Left = 20, Top = 18, Width = 140, Height = 24 });
            sizeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            sizeBox.Items.AddRange(new object[] { "大（80×50mm）", "小（50×24mm）" });
            sizeBox.SetBounds(20, 44, 280, 30);
            Controls.Add(sizeBox);

            Controls.Add(new Label { Text = "フォント（全体）", Left = 20, Top = 88, Width = 200, Height = 24 });
            fontBox.DropDownStyle = ComboBoxStyle.DropDownList;
            fontBox.Items.AddRange(new object[] { "丸ゴシック", "MS Pゴシック" });
            fontBox.SetBounds(20, 114, 280, 30);
            Controls.Add(fontBox);

            Controls.Add(new Label { Text = "段数", Left = 20, Top = 158, Width = 140, Height = 24 });
            rowsBox.DropDownStyle = ComboBoxStyle.DropDownList;
            rowsBox.Items.AddRange(new object[] { "1段", "2段", "3段" });
            rowsBox.SetBounds(20, 184, 280, 30);
            Controls.Add(rowsBox);

            for (int i = 0; i < 3; i++)
            {
                int top = 224 + i * 96;
                rowPanels[i].SetBounds(20, top, 350, 90);
                Controls.Add(rowPanels[i]);
                rowCaptions[i].Text = (i + 1) + "段目";
                rowCaptions[i].SetBounds(0, 0, 80, 22);
                rowPanels[i].Controls.Add(rowCaptions[i]);
                rowTexts[i].SetBounds(0, 22, 220, 28);
                rowTexts[i].MaxLength = 80;
                rowPanels[i].Controls.Add(rowTexts[i]);
                sizeCaptions[i].Text = "pt";
                sizeCaptions[i].SetBounds(230, 0, 40, 22);
                rowPanels[i].Controls.Add(sizeCaptions[i]);
                rowSizes[i].SetBounds(230, 22, 100, 28);
                rowSizes[i].Minimum = 6;
                rowSizes[i].Maximum = 120;
                rowSizes[i].DecimalPlaces = 0;
                rowSizes[i].Value = 26;
                rowPanels[i].Controls.Add(rowSizes[i]);

                offsetXCaptions[i].Text = "左右 mm";
                offsetXCaptions[i].SetBounds(0, 54, 70, 22);
                rowPanels[i].Controls.Add(offsetXCaptions[i]);
                ConfigureOffsetSpinner(rowOffsetX[i]);
                rowOffsetX[i].SetBounds(70, 52, 90, 28);
                rowPanels[i].Controls.Add(rowOffsetX[i]);

                offsetYCaptions[i].Text = "上下 mm";
                offsetYCaptions[i].SetBounds(170, 54, 70, 22);
                rowPanels[i].Controls.Add(offsetYCaptions[i]);
                ConfigureOffsetSpinner(rowOffsetY[i]);
                rowOffsetY[i].SetBounds(240, 52, 90, 28);
                rowPanels[i].Controls.Add(rowOffsetY[i]);
            }

            Controls.Add(new Label { Text = "ラベルの内容（拡大表示）", Left = 380, Top = 18, Width = 300, Height = 24 });
            preview.SetBounds(380, 44, 560, 300);
            preview.BackColor = Color.White;
            preview.BorderStyle = BorderStyle.FixedSingle;
            preview.SizeMode = PictureBoxSizeMode.Zoom;
            Controls.Add(preview);

            guide.SetBounds(20, 520, 940, 42);
            guide.BackColor = Color.White;
            guide.BorderStyle = BorderStyle.FixedSingle;
            guide.Padding = new Padding(6, 3, 0, 0);
            Controls.Add(guide);

            Controls.Add(new Label { Text = "プリンター", Left = 20, Top = 575, Width = 120, Height = 24 });
            printers.DropDownStyle = ComboBoxStyle.DropDownList;
            printers.SetBounds(20, 600, 320, 30);
            Controls.Add(printers);
            Controls.Add(new Label { Text = "用紙サイズ", Left = 360, Top = 575, Width = 120, Height = 24 });
            papers.DropDownStyle = ComboBoxStyle.DropDownList;
            papers.SetBounds(360, 600, 250, 30);
            Controls.Add(papers);
            Controls.Add(new Label { Text = "部数", Left = 630, Top = 575, Width = 60, Height = 24 });
            copies.SetBounds(630, 600, 80, 30);
            copies.Minimum = 1; copies.Maximum = 30; copies.Value = 1;
            Controls.Add(copies);

            Button page = new Button { Text = "用紙・方向設定", Left = 20, Top = 650, Width = 150, Height = 36 };
            Button proof = new Button { Text = "印刷プレビュー", Left = 185, Top = 650, Width = 150, Height = 36 };
            Button adjust = new Button { Text = "印字位置調整", Left = 350, Top = 650, Width = 150, Height = 36 };
            Button print = new Button { Text = "印刷", Left = 720, Top = 645, Width = 150, Height = 42 };
            Button close = new Button { Text = "閉じる", Left = 880, Top = 650, Width = 80, Height = 36, DialogResult = DialogResult.Cancel };
            Controls.AddRange(new Control[] { page, proof, adjust, print, close });
            CancelButton = close;
            offsetSummary.SetBounds(510, 650, 190, 40);
            Controls.Add(offsetSummary);
            status.SetBounds(380, 355, 560, 150);
            Controls.Add(status);

            sizeBox.SelectedIndex = 1;
            fontBox.SelectedIndex = 0;
            rowsBox.SelectedIndex = 0;
            ApplyDraftToControls();
            UpdateRowVisibility();

            sizeBox.SelectedIndexChanged += delegate { if (!changing) { SaveControlsToDraft(); ApplySizeDefaults(); LoadPapers(true); UpdatePreview(); } };
            fontBox.SelectedIndexChanged += delegate { if (!changing) { SaveControlsToDraft(); UpdatePreview(); } };
            rowsBox.SelectedIndexChanged += delegate { if (!changing) { SaveControlsToDraft(); UpdateRowVisibility(); UpdatePreview(); } };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                rowTexts[i].TextChanged += delegate { if (!changing) { draft.Texts[index] = rowTexts[index].Text; UpdatePreview(); } };
                rowSizes[i].ValueChanged += delegate { if (!changing) { draft.Sizes[index] = (float)rowSizes[index].Value; UpdatePreview(); } };
                rowOffsetX[i].ValueChanged += delegate { if (!changing) { draft.OffsetXMm[index] = (float)rowOffsetX[index].Value; UpdatePreview(); } };
                rowOffsetY[i].ValueChanged += delegate { if (!changing) { draft.OffsetYMm[index] = (float)rowOffsetY[index].Value; UpdatePreview(); } };
            }
            printers.SelectedIndexChanged += delegate { SelectPrinter(); };
            papers.SelectedIndexChanged += delegate
            {
                if (!changing && papers.SelectedItem != null)
                {
                    document.DefaultPageSettings.PaperSize = (PaperSize)papers.SelectedItem;
                    UpdatePreview();
                }
            };
            page.Click += delegate { Safe(delegate { using (PageSetupDialog dlg = new PageSetupDialog { Document = document, EnableMetric = true }) { dlg.ShowDialog(this); SyncPrinter(); } }); };
            proof.Click += delegate { if (Check()) Safe(delegate { using (PrintPreviewDialog dlg = new PrintPreviewDialog { Document = document, Width = 1050, Height = 760 }) dlg.ShowDialog(this); }); };
            print.Click += delegate
            {
                if (!Check()) return;
                Safe(delegate
                {
                    print.Enabled = false;
                    try
                    {
                        document.PrinterSettings.Copies = (short)copies.Value;
                        document.Print();
                        status.Text = "印刷データをプリンターへ送信しました。";
                    }
                    finally { print.Enabled = true; }
                });
            };
            adjust.Click += delegate { Safe(OpenOffset); };
            document.PrintPage += PrintPage;
            FormClosed += delegate { if (preview.Image != null) preview.Image.Dispose(); document.Dispose(); };

            Safe(delegate
            {
                foreach (string p in PrinterSettings.InstalledPrinters) printers.Items.Add(p);
                SyncPrinter(true);
            });
            UpdatePreview();
        }

        static void ConfigureOffsetSpinner(NumericUpDown box)
        {
            box.Minimum = (decimal)(-GenericLabelRenderer.MaxRowOffsetMm);
            box.Maximum = (decimal)GenericLabelRenderer.MaxRowOffsetMm;
            box.DecimalPlaces = 1;
            box.Increment = 0.1m;
            box.Value = 0;
        }

        void ApplySizeDefaults()
        {
            // 大/小切替時は各段の上限ptを既定（大48／小26）へ戻す。段数には依存しない。
            float pt = GenericLabelRenderer.DefaultSizePt(draft.Small);
            for (int i = 0; i < 3; i++) draft.Sizes[i] = pt;
            changing = true;
            for (int i = 0; i < 3; i++) rowSizes[i].Value = (decimal)pt;
            changing = false;
        }

        void ApplyDraftToControls()
        {
            changing = true;
            sizeBox.SelectedIndex = draft.Small ? 1 : 0;
            fontBox.SelectedIndex = draft.Family == "MS PGothic" ? 1 : 0;
            rowsBox.SelectedIndex = Math.Max(0, Math.Min(2, draft.Rows - 1));
            for (int i = 0; i < 3; i++)
            {
                rowTexts[i].Text = draft.Texts[i];
                rowSizes[i].Value = (decimal)Math.Max(6, Math.Min(120, draft.Sizes[i]));
                decimal ox = (decimal)draft.OffsetXMm[i];
                decimal oy = (decimal)draft.OffsetYMm[i];
                rowOffsetX[i].Value = Math.Max(rowOffsetX[i].Minimum, Math.Min(rowOffsetX[i].Maximum, ox));
                rowOffsetY[i].Value = Math.Max(rowOffsetY[i].Minimum, Math.Min(rowOffsetY[i].Maximum, oy));
            }
            changing = false;
        }

        void SaveControlsToDraft()
        {
            draft.Small = sizeBox.SelectedIndex != 0;
            draft.Family = GenericLabelRenderer.FamilyName(fontBox.SelectedItem != null ? fontBox.SelectedItem.ToString() : "丸ゴシック");
            draft.Rows = rowsBox.SelectedIndex + 1;
            for (int i = 0; i < 3; i++)
            {
                draft.Texts[i] = rowTexts[i].Text;
                draft.Sizes[i] = (float)rowSizes[i].Value;
                draft.OffsetXMm[i] = (float)rowOffsetX[i].Value;
                draft.OffsetYMm[i] = (float)rowOffsetY[i].Value;
            }
            document.DefaultPageSettings.Landscape = false;
        }

        void UpdateRowVisibility()
        {
            int n = rowsBox.SelectedIndex + 1;
            for (int i = 0; i < 3; i++) rowPanels[i].Visible = i < n;
            float band = GenericLabelRenderer.BandHeightMm(draft.Small, n);
            string bandNote = n == 1
                ? ("1段＝高さ全体 " + band.ToString("0.###") + "mm")
                : (n + "段＝各 " + band.ToString("0.###") + "mm（等分）");
            guide.Text = (draft.Small
                ? "サイズ：小（幅50mm・カット長24mm）　｜　"
                : "サイズ：大（幅50mm・カット長80mm／表示は80×50mm）　｜　")
                + bandNote + "　｜　左右・上下 0＝段の中央　｜　ptは上限（枠内へ自動縮小）　｜　既定 "
                + GenericLabelRenderer.DefaultSizePt(draft.Small).ToString("0") + "pt";
        }

        string CurrentOffsetKey()
        {
            return GenericLabelRenderer.OffsetKey(document.PrinterSettings.PrinterName, draft.Small);
        }
        PrintOffset CurrentOffset() { return temporaryOffset ?? preferences.Get(CurrentOffsetKey()); }

        void UpdatePreview()
        {
            Safe(delegate
            {
                SaveControlsToDraft();
                UpdateRowVisibility();
                PrintOffset offset = CurrentOffset();
                Bitmap bmp = CreatePreview(offset);
                Image old = preview.Image;
                preview.Image = bmp;
                if (old != null) old.Dispose();
                offsetSummary.Text = "左右：" + offset.X.ToString("0.0") + " mm\r\n上下：" + offset.Y.ToString("0.0") + " mm";
                status.Text = PaperError() ?? GenericLabelRenderer.Validate(draft) ?? "印刷できます。用紙サイズと印刷方向を確認してください。";
            });
        }

        Bitmap CreatePreview(PrintOffset offset)
        {
            Bitmap bmp = new Bitmap(1120, 600);
            try
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(235, 238, 242));
                    g.ScaleTransform(2, 2);
                    Size mm = GenericLabelRenderer.Millimetres(draft.Small);
                    float mmWidth = mm.Width, mmHeight = mm.Height;
                    float scale = Math.Min(520f / mmWidth, 250f / mmHeight), width = mmWidth * scale, height = mmHeight * scale;
                    float left = (560 - width) / 2, top = 25 + (250 - height) / 2;
                    using (Font caption = new Font("Yu Gothic UI", 10, FontStyle.Bold, GraphicsUnit.Pixel))
                        g.DrawString(draft.Small ? "小：50 × 24 mm" : "大：80 × 50 mm（用紙幅50・カット長80 mm）", caption, Brushes.Black, 12, 8);
                    g.FillRectangle(Brushes.White, left, top, width, height);
                    GraphicsState state = g.Save();
                    g.SetClip(new RectangleF(left, top, width, height));
                    g.TranslateTransform(left, top);
                    g.ScaleTransform(scale * 0.254f, scale * 0.254f);
                    g.TranslateTransform((float)offset.X / .254f, (float)offset.Y / .254f);
                    GenericLabelRenderer.Draw(g, draft);
                    g.Restore(state);
                    using (Pen outline = new Pen(Color.FromArgb(90, 100, 110), 1))
                        g.DrawRectangle(outline, left, top, width, height);
                }
            }
            catch { bmp.Dispose(); throw; }
            return bmp;
        }

        void OpenOffset()
        {
            SaveControlsToDraft();
            string key = CurrentOffsetKey();
            string title = document.PrinterSettings.PrinterName + "\r\n汎用ラベル ／ " + (draft.Small ? "小" : "大");
            using (OffsetDialog dlg = new OffsetDialog(title, preferences.Get(key), CreatePreview,
                delegate(PrintOffset p)
                {
                    if (!Check()) return;
                    short oldCopies = document.PrinterSettings.Copies;
                    try { temporaryOffset = p; document.PrinterSettings.Copies = 1; document.Print(); }
                    finally { temporaryOffset = null; document.PrinterSettings.Copies = oldCopies; }
                },
                delegate(PrintOffset p) { preferences.Set(key, p); }))
                dlg.ShowDialog(this);
            UpdatePreview();
        }

        void SelectPrinter()
        {
            if (changing || printers.SelectedItem == null) return;
            Safe(delegate
            {
                bool landscape = document.DefaultPageSettings.Landscape;
                document.PrinterSettings.PrinterName = printers.SelectedItem.ToString();
                document.DefaultPageSettings = document.PrinterSettings.DefaultPageSettings;
                document.DefaultPageSettings.Landscape = landscape;
                LoadPapers();
            });
        }

        void SyncPrinter(bool initial = false)
        {
            changing = true;
            printers.SelectedItem = document.PrinterSettings.PrinterName;
            changing = false;
            LoadPapers(initial);
        }

        void LoadPapers() { LoadPapers(true); }
        void LoadPapers(bool initial)
        {
            changing = true;
            try
            {
                papers.Items.Clear();
                papers.DisplayMember = "PaperName";
                if (!document.PrinterSettings.IsValid) { status.Text = "プリンターがありません。ラベルの内容は確認できます。"; return; }
                foreach (PaperSize p in document.PrinterSettings.PaperSizes) papers.Items.Add(p);
                PaperSize current = document.DefaultPageSettings.PaperSize;
                PaperSize chosen = initial ? ChoosePaper(papers.Items, draft.Small) : null;
                if (chosen == null)
                {
                    foreach (PaperSize p in papers.Items)
                        if (p.PaperName == current.PaperName && p.Width == current.Width && p.Height == current.Height) { chosen = p; break; }
                }
                if (chosen == null) { chosen = current; papers.Items.Add(chosen); }
                if (chosen != null) { papers.SelectedItem = chosen; document.DefaultPageSettings.PaperSize = chosen; }
            }
            finally { changing = false; }
            UpdatePreview();
        }

        static PaperSize ChoosePaper(System.Collections.IEnumerable available, bool small)
        {
            string length = small ? "24" : "80";
            foreach (PaperSize paper in available)
            {
                string name = paper.PaperName.Normalize(NormalizationForm.FormKC);
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"(?<!\d)50(?!\d)")
                    && System.Text.RegularExpressions.Regex.IsMatch(name, @"(?<!\d)" + length + @"(?!\d)"))
                    return paper;
            }
            return null;
        }

        string PaperError()
        {
            if (!document.PrinterSettings.IsValid) return "プリンターを選択してください。";
            if (papers.SelectedItem == null) return "用紙を選択してください。";
            return null;
        }

        bool Check()
        {
            SaveControlsToDraft();
            string error = GenericLabelRenderer.Validate(draft);
            if (error == null) error = PaperError();
            if (error != null)
            {
                MessageBox.Show(this, error, "入力の確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            document.DocumentName = "ネームラベル - 汎用ラベル - " + (draft.Small ? "小" : "大");
            return true;
        }

        void PrintPage(object sender, PrintPageEventArgs e)
        {
            GraphicsState state = e.Graphics.Save();
            try
            {
                e.Graphics.PageUnit = GraphicsUnit.Inch;
                e.Graphics.ScaleTransform(0.01f, 0.01f);
                // 大ネームは既存顧客ラベルと同様に -90° 回転。
                if (!draft.Small)
                {
                    e.Graphics.RotateTransform(-90);
                    e.Graphics.TranslateTransform(-e.PageBounds.Height, 0);
                }
                PrintOffset offset = CurrentOffset();
                e.Graphics.TranslateTransform((float)offset.X / .254f, (float)offset.Y / .254f);
                GenericLabelRenderer.Draw(e.Graphics, draft);
                e.HasMorePages = false;
            }
            finally { e.Graphics.Restore(state); }
        }

        void Safe(Action action)
        {
            try { action(); }
            catch (Exception ex) { status.Text = "処理できませんでした: " + ex.Message; }
        }

        /// <summary>実印刷なしで描画・検証・オフセットキー分離を確認する。</summary>
        public void Verify(string folder)
        {
            Directory.CreateDirectory(folder);
            string keySmall = GenericLabelRenderer.OffsetKey("TestPrinter", true);
            string keyLarge = GenericLabelRenderer.OffsetKey("TestPrinter", false);
            if (keySmall == keyLarge) throw new Exception("大小のオフセットキーが同一です");
            if (!keySmall.Contains(GenericLabelRenderer.SettingsIdentity)) throw new Exception("汎用オフセット識別子がありません");
            // 顧客キーと衝突しないこと（顧客 SettingsIdentity は「顧客|形式|…」形式）。
            if (keySmall.Split('|').Length < 3) throw new Exception("オフセットキー形式が不正です");

            // 等分帯の高さ（大＝50mm基準）
            if (Math.Abs(GenericLabelRenderer.BandHeightMm(false, 1) - 50f) > 0.001f) throw new Exception("大1段帯が高さ50mmではありません");
            if (Math.Abs(GenericLabelRenderer.BandHeightMm(false, 2) - 25f) > 0.001f) throw new Exception("大2段帯が25mmではありません");
            if (Math.Abs(GenericLabelRenderer.BandHeightMm(false, 3) - (50f / 3f)) > 0.001f) throw new Exception("大3段帯が等分ではありません");
            if (Math.Abs(GenericLabelRenderer.BandHeightMm(true, 2) - 12f) > 0.001f) throw new Exception("小2段帯が12mmではありません");

            foreach (bool small in new[] { true, false })
            foreach (string family in new[] { "HG丸ｺﾞｼｯｸM-PRO", "MS PGothic" })
            for (int rows = 1; rows <= 3; rows++)
            {
                draft.Small = small;
                draft.Family = family;
                draft.Rows = rows;
                float def = GenericLabelRenderer.DefaultSizePt(small);
                for (int i = 0; i < 3; i++)
                {
                    draft.Texts[i] = i < rows ? ("テスト" + (i + 1)) : "保持確認" + (i + 1);
                    draft.Sizes[i] = def;
                    draft.OffsetXMm[i] = 0f;
                    draft.OffsetYMm[i] = 0f;
                }
                ApplyDraftToControls();
                UpdateRowVisibility();
                if (GenericLabelRenderer.Validate(draft) != null)
                    throw new Exception("標準サンプルが不正: " + GenericLabelRenderer.Validate(draft));
                using (Bitmap b = CreatePreview(new PrintOffset()))
                    b.Save(Path.Combine(folder, "preview-" + (small ? "s" : "l") + "-" + (family.Contains("Gothic") && !family.Contains("丸") ? "pg" : "maru") + "-" + rows + ".png"));
                Size mm = GenericLabelRenderer.Millimetres(small);
                using (Bitmap b = new Bitmap(mm.Width * 6, mm.Height * 6))
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.Clear(Color.White);
                    g.ScaleTransform(6 * 0.254f, 6 * 0.254f);
                    GenericLabelRenderer.Draw(g, draft);
                    b.Save(Path.Combine(folder, "draw-" + (small ? "s" : "l") + "-" + rows + "-" + (family == "MS PGothic" ? "pg" : "maru") + ".png"));
                }
                using (Bitmap b = new Bitmap(1200, 1200))
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.Clear(Color.White);
                    PrintPage(this, new PrintPageEventArgs(g, new Rectangle(0, 0, 400, 400), new Rectangle(0, 0, 400, 400), document.DefaultPageSettings));
                    b.Save(Path.Combine(folder, "print-" + (small ? "s" : "l") + "-" + rows + ".png"));
                }
            }

            // 大/小切替で各段ptが既定（48/26）へ戻ること
            draft.Small = true;
            draft.Rows = 2;
            draft.Sizes[0] = 12f; draft.Sizes[1] = 14f; draft.Sizes[2] = 16f;
            ApplyDraftToControls();
            sizeBox.SelectedIndex = 0; // 大
            if (draft.Sizes[0] != 48f || draft.Sizes[1] != 48f || draft.Sizes[2] != 48f
                || rowSizes[0].Value != 48 || rowSizes[1].Value != 48 || rowSizes[2].Value != 48)
                throw new Exception("大へ切替時の既定48pt反映失敗");
            sizeBox.SelectedIndex = 1; // 小
            if (draft.Sizes[0] != 26f || draft.Sizes[1] != 26f || draft.Sizes[2] != 26f
                || rowSizes[0].Value != 26 || rowSizes[1].Value != 26 || rowSizes[2].Value != 26)
                throw new Exception("小へ切替時の既定26pt反映失敗");

            // 長い文字は自動縮小で通る（従来のはみ出し拒否はしない）
            draft.Small = true;
            draft.Rows = 1;
            draft.Family = "MS PGothic";
            draft.Texts[0] = "はみ出し確認用のとても長い文字列ABCDEFG";
            draft.Sizes[0] = 26f;
            draft.OffsetXMm[0] = 0; draft.OffsetYMm[0] = 0;
            if (GenericLabelRenderer.Validate(draft) != null)
                throw new Exception("自動縮小後も長い文字が拒否された: " + GenericLabelRenderer.Validate(draft));
            using (Bitmap probe = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(probe))
            {
                float fitted = GenericLabelRenderer.FittedSize(g, draft, 0);
                if (fitted >= 26f - 0.01f) throw new Exception("長い文字で自動縮小されていません: " + fitted);
            }

            // 段内位置：初期は中央(0,0)、変更がプレビューに反映、非表示段は保持
            draft.Small = false;
            draft.Rows = 2;
            draft.Family = "MS PGothic";
            draft.Texts[0] = "上段"; draft.Texts[1] = "下段"; draft.Texts[2] = "隠";
            draft.Sizes[0] = 20f; draft.Sizes[1] = 20f; draft.Sizes[2] = 14f;
            draft.OffsetXMm[0] = 0; draft.OffsetYMm[0] = 0;
            draft.OffsetXMm[1] = 0; draft.OffsetYMm[1] = 0;
            draft.OffsetXMm[2] = 1.5f; draft.OffsetYMm[2] = -1f;
            ApplyDraftToControls();
            if (rowOffsetX[0].Value != 0 || rowOffsetY[0].Value != 0 || rowOffsetX[1].Value != 0 || rowOffsetY[1].Value != 0)
                throw new Exception("初期位置が中央(0,0)ではありません");
            using (Bitmap centered = CreatePreview(new PrintOffset()))
            {
                draft.OffsetXMm[0] = 3f;
                draft.OffsetYMm[1] = -2f;
                ApplyDraftToControls();
                using (Bitmap moved = CreatePreview(new PrintOffset()))
                {
                    bool changed = false;
                    for (int x = 0; x < centered.Width && !changed; x += 3)
                        for (int y = 0; y < centered.Height; y += 3)
                            if (centered.GetPixel(x, y) != moved.GetPixel(x, y)) { changed = true; break; }
                    if (!changed) throw new Exception("段内位置調整がプレビューに反映されていません");
                }
            }
            rowsBox.SelectedIndex = 0; // 1段（2・3段目を隠す）
            if (draft.OffsetXMm[2] != 1.5f || draft.OffsetYMm[2] != -1f) throw new Exception("非表示段の位置保持失敗");
            rowsBox.SelectedIndex = 2; // 3段
            if (rowOffsetX[2].Value != 1.5m || rowOffsetY[2].Value != -1m) throw new Exception("段数復帰で位置復元失敗");

            // 段数切替で非表示行の入力が復元されること
            draft.Rows = 3;
            draft.Texts[0] = "上"; draft.Texts[1] = "中"; draft.Texts[2] = "下";
            draft.Sizes[0] = 18; draft.Sizes[1] = 16; draft.Sizes[2] = 14;
            draft.OffsetXMm[0] = 0; draft.OffsetYMm[0] = 0;
            draft.OffsetXMm[1] = 0; draft.OffsetYMm[1] = 0;
            draft.OffsetXMm[2] = 0; draft.OffsetYMm[2] = 0;
            ApplyDraftToControls();
            rowsBox.SelectedIndex = 0; // 1段
            if (draft.Texts[1] != "中" || draft.Texts[2] != "下") throw new Exception("非表示行の保持失敗");
            rowsBox.SelectedIndex = 2; // 3段
            if (rowTexts[1].Text != "中" || rowTexts[2].Text != "下") throw new Exception("段数復帰で入力復元失敗");

            // 空行／段内オフセットによるはみ出し
            draft.Rows = 2; draft.Texts[0] = "あり"; draft.Texts[1] = "";
            draft.Sizes[0] = 26; draft.Sizes[1] = 26;
            if (GenericLabelRenderer.Validate(draft) == null || !GenericLabelRenderer.Validate(draft).Contains("2段目"))
                throw new Exception("空行検証失敗");
            draft.Texts[1] = "下段";
            draft.Small = true;
            draft.OffsetXMm[1] = 0; draft.OffsetYMm[1] = 0;
            if (GenericLabelRenderer.Validate(draft) != null)
                throw new Exception("通常2段が不正: " + GenericLabelRenderer.Validate(draft));
            draft.OffsetXMm[1] = 20f;
            if (GenericLabelRenderer.Validate(draft) == null)
                throw new Exception("段内オフセットはみ出し検証失敗");
            draft.OffsetXMm[1] = 0;

            // 印字位置オフセット反映（顧客と別キー）
            draft.Rows = 1;
            draft.Small = true;
            draft.Texts[0] = "オフセット";
            draft.Sizes[0] = 26;
            draft.OffsetXMm[0] = 0; draft.OffsetYMm[0] = 0;
            ApplyDraftToControls();
            using (Bitmap a = CreatePreview(new PrintOffset()))
            using (Bitmap b = CreatePreview(new PrintOffset(1, 1)))
            {
                bool changed = false;
                for (int x = 0; x < a.Width && !changed; x += 3)
                    for (int y = 0; y < a.Height; y += 3)
                        if (a.GetPixel(x, y) != b.GetPixel(x, y)) { changed = true; break; }
                if (!changed) throw new Exception("汎用ラベルの印字位置調整がプレビューに反映されていません");
            }

            // 顧客オフセットキーと混ざらないこと
            string customerLike = "TestPrinter|よつ葉乳業㈱十勝工場|小ネーム（QR・黒下用）|True|黒下：左前";
            if (keySmall == customerLike || keyLarge == customerLike) throw new Exception("顧客キーと衝突");
            preferences.Set(keySmall, new PrintOffset(1.5m, -0.5m));
            if (preferences.Get(customerLike).X != 0) throw new Exception("顧客オフセットへ混入");
            if (preferences.Get(keySmall).X != 1.5m || preferences.Get(keyLarge).X != 0)
                throw new Exception("汎用オフセットの大小分離失敗");

            File.WriteAllText(Path.Combine(folder, "generic-verification.txt"),
                "PASS: 自動縮小(Fit)・既定pt大48/小26・等分帯・段内位置・大/小×フォント×1～3段・印刷経路・段数復帰・空行/オフセットはみ出し・印字オフセットキー分離。実プリンター印字・カットは未実施。",
                Encoding.UTF8);
        }
    }
}
