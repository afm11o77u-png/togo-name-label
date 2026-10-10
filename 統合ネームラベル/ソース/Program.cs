using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Windows.Forms;
using System.IO;
using System.Text;

namespace UnifiedLabels
{
    public sealed class LabelData
    {
        public int Digits = 4;
        public int EmphasisStart = -1, EmphasisLength;
        public string ColorDepartment = "";
        public string Header = "", Role = "", Person = "", Number = "";
        // 欄ごとのフォントサイズ(pt)。*FontSet=false または無効値ならカタログ／自動調整の既定を使う。
        public float HeaderFontPt, RoleFontPt, PersonFontPt, NumberFontPt;
        public bool HeaderFontSet, RoleFontSet, PersonFontSet, NumberFontSet;
    }

    public static class LabelRenderer
    {
        public static readonly string[] Titles = { "よつ葉乳業（小ネーム）", "よつ葉乳業（大ネーム）", "だるま食品", "Umios Hokkaido 札幌工場" };
        public static readonly int[] NumberLimits = { 5, 5, 8, 6 };
        public static Size Canvas(int kind) { return kind == 0 ? new Size(222, 120) : new Size(370, 200); }
        static void Text(Graphics g, string value, string family, float points, float x, float y)
        {
            // Coordinates are hundredths of an inch, matching the original PrintDocument.
            using (Font font = new Font(family, points * 100f / 72f, FontStyle.Bold, GraphicsUnit.Pixel))
                g.DrawString(value, font, Brushes.Black, x, y);
        }
        public static string Validate(int kind, LabelData d)
        {
            if(kind>=4) return LabelCatalog.Validate(kind,d);
            if (kind==3 && String.IsNullOrWhiteSpace(d.Header)) return "部署を選択してください。";
            if (String.IsNullOrWhiteSpace(d.Person)) return "名前を入力してください。";
            if (String.IsNullOrWhiteSpace(d.Number)) return "社員番号を入力してください。";
            if (d.Number.Length > (kind<2?d.Digits+1:NumberLimits[kind])) return "社員番号が長すぎます。選択した桁数に合わせて修正してください。";
            if (kind == 2)
            {
                if (String.IsNullOrWhiteSpace(d.Header)) return "従業員Noを入力してください。";
                foreach (char c in d.Number)
                    if ("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%".IndexOf(c) < 0)
                        return "だるまの社員番号には半角数字・英大文字・記号（- . 空白 $ / + %）を使用してください。";
            }
            return null;
        }
        public static void Draw(Graphics g, int kind, LabelData d)
        {
            if(kind>=4) { LabelCatalog.Draw(g,kind,d); return; }
            string rounded = "HG丸ｺﾞｼｯｸM-PRO", gothic = "MS PGothic";
            string person = d.Person.Replace("　", " ");
            int n = d.Person.Length;
            float x = 0, y, size;
            if (kind < 2)
            {
                bool small = kind == 0;
                size = n <= 5 ? (small ? 24 : 38) : n == 6 ? (small ? 20 : 32) : n == 7 ? (small ? 16 : 26) : (small ? 13 : 25);
                size = LabelCatalog.PickFont(size, d.PersonFontSet, d.PersonFontPt);
                y = n <= 5 ? (small ? 53 : 113) : n <= 7 ? (small ? 58 : 118) : (small ? 65 : 125);
                // Large 2-row (kind 1): shift company/header down ~2.5mm (10 units of 0.01"); keep small and name Y unchanged.
                Text(g, d.Header, rounded, LabelCatalog.PickFont(small ? 16 : 26, d.HeaderFontSet, d.HeaderFontPt), 0, small ? 19 : 43);
            }
            else
            {
                int[] sizes = {44,44,44,42,40,36,32,28,32,30,28,26,24,22,20,19,18,17,16};
                int[] xs = kind == 2 ? new int[] {80,80,60,50,40,15,15,10} : new int[] {104,130,110,100,90,65,65,60};
                int[] ys = kind == 2 ? new int[] {35,35,37,38,39,43,48,53} : new int[] {105,105,107,108,109,113,118,123};
                size = LabelCatalog.PickFont(sizes[Math.Min(n,18)], d.PersonFontSet, d.PersonFontPt); x = xs[Math.Min(n,7)]; y = ys[Math.Min(n,7)];
                if (kind == 2)
                {
                    Text(g, d.Header, gothic, LabelCatalog.PickFont(14, d.HeaderFontSet, d.HeaderFontPt), 74, 10);
                    Text(g, "従業員No.", gothic, 10, 0, 16);
                }
                else
                {
                    Text(g, d.Header, rounded, LabelCatalog.PickFont(26, d.HeaderFontSet, d.HeaderFontPt), 0, 25);
                    Text(g, d.Role, rounded, LabelCatalog.PickFont(22, d.RoleFontSet, d.RoleFontPt), 63, 71);
                }
            }
            // Keep original sizes where they fit; shrink long names to prevent overlap/clipping.
            // ユーザーが氏名ptを明示したときはその値を上限に縮小（未指定時は従来どおり）。
            float right = kind == 0 ? 150 : kind == 1 ? 249 : 310;
            string nameFont = LabelCatalog.Items[kind].Family;
            while (size > 5)
            {
                using (Font f = new Font(nameFont, size * 100f / 72f, FontStyle.Bold, GraphicsUnit.Pixel))
                    if (g.MeasureString(person, f).Width <= right - x) break;
                size -= 0.5f;
            }
            Text(g, person, nameFont, size, x, y);
            if (kind == 0) Text(g, d.Number, "Bahnschrift", LabelCatalog.PickFont(d.Digits==5?7:8, d.NumberFontSet, d.NumberFontPt), 154, 80);
            if (kind == 1) Text(g, d.Number, gothic, LabelCatalog.PickFont(d.Digits==5?9:10, d.NumberFontSet, d.NumberFontPt), 254, 160);
            if (kind == 2) Text(g, d.Number, gothic, LabelCatalog.PickFont(10, d.NumberFontSet, d.NumberFontPt), 110, 160);
            if (kind == 3) Text(g, d.Number, gothic, LabelCatalog.PickFont(11, d.NumberFontSet, d.NumberFontPt), 7, 150);
            if (String.IsNullOrWhiteSpace(d.Number)) return;
            if (kind == 2)
                foreach (char c in d.Number) if ("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%".IndexOf(c) < 0) return;
            DotNetBarcode barcode = new DotNetBarcode(kind == 2 ? DotNetBarcode.Types.Code39 : DotNetBarcode.Types.QRCode);
            barcode.QRQuitZone = 0;
            if (kind == 2) barcode.WriteBar(d.Number, 15, 100, 260, 60, g);
            else if (kind == 3) barcode.WriteBar(d.Number, 15, 100, 60, 60, g);
            else
            {
                using (Bitmap bmp = new Bitmap(200,200))
                {
                    using (Graphics q = Graphics.FromImage(bmp)) { q.Clear(Color.White); barcode.WriteBar(d.Number,0,0,200,200,q); }
                    GraphicsState state = g.Save();
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(bmp, kind == 0 ? new Rectangle(156,50,30,30) : new Rectangle(255,115,45,45));
                    g.Restore(state);
                }
            }
        }
    }

    public sealed partial class MainForm : Form
    {
        readonly ComboBox kinds = new ComboBox(), printers = new ComboBox(), papers = new ComboBox();
        readonly ComboBox department = new ComboBox();
        readonly ComboBox colorDepartment = new ComboBox();
        readonly Label sampleColor = new Label();
        readonly Label[] colorLegend = {new Label(),new Label(),new Label(),new Label()};
        readonly System.Collections.Generic.List<Control> footerControls=new System.Collections.Generic.List<Control>();
        int legendHeight;
        int emphasisStart=-1, emphasisLength;
        readonly Button emphasize=new Button(), clearEmphasis=new Button();
        readonly Label emphasisGuide=new Label();
        readonly ComboBox digits = new ComboBox();
        readonly ComboBox customers = new ComboBox(), formats = new ComboBox();
        bool selecting;
        /// <summary>顧客コンボの項目を query で絞り込み（空なら全件）。入力中の文字とキャレットを維持する。</summary>
        void FillCustomerItems(string query) {
            string typed=customers.Text; int caret=customers.SelectionStart;
            bool prev=selecting; selecting=true;
            customers.BeginUpdate();
            customers.Items.Clear();
            foreach(string n in LabelCatalog.SortedCustomers())
                if(UserSettings.Matches(n,query)) customers.Items.Add(n);
            customers.EndUpdate();
            customers.Text=typed;
            if(caret>=0 && caret<=typed.Length) customers.SelectionStart=caret;
            selecting=prev;
        }
        /// <summary>顧客コンボの選択をラベル形式・入力欄へ反映（SelectedIndexChanged 本体）。kinds が同値でも SwitchKind する。</summary>
        void ApplyCustomerSelection() {
            if(SelectedCustomerIndex<0) return;
            int customer=SelectedCustomerIndex;
            int[] ids=LabelCatalog.ForCustomer(customer);
            int next=ids[lastFormats[customer]];
            if(kinds.SelectedIndex!=next) kinds.SelectedIndex=next;
            else SwitchKind();
            Safe(delegate {preferences.Visit(customers.Text);});
        }
        /// <summary>Enter：絞り込み先頭（または完全一致）を確定し、内容切替のうえフォーカスとキャレットを顧客欄に残す。</summary>
        void CommitCustomerOnEnter() {
            if(selecting) return;
            string typed=customers.Text??"";
            string match=null;
            int kept=SelectedCustomerIndex;
            if(kept>=0) match=LabelCatalog.Customers[kept];
            else {
                FillCustomerItems(typed);
                if(customers.Items.Count>0) match=customers.Items[0].ToString();
            }
            try {
                if(customers.DroppedDown) customers.DroppedDown=false;
            } catch { }
            if(match!=null) {
                FillCustomerItems("");
                // FillCustomerItems 内の Text 復元で既に同項目が選ばれていると SelectedIndexChanged が飛ばない。
                // 表示の選択は selecting 中に済ませ、内容切替は必ず ApplyCustomerSelection で行う。
                selecting=true;
                try { customers.SelectedItem=match; }
                finally { selecting=false; }
                ApplyCustomerSelection();
            } else if(active>=0) SelectedCustomerIndex=LabelCatalog.Items[active].Customer;
            // ドロップダウン閉鎖後にフォーカス／キャレットを戻す（消えないようにする）
            BeginInvoke(new Action(delegate {
                if(customers.IsDisposed) return;
                customers.Focus();
                string text=customers.Text??"";
                customers.SelectionStart=text.Length;
                customers.SelectionLength=0;
            }));
        }
        int SelectedCustomerIndex {
            get { return Array.IndexOf(LabelCatalog.Customers,customers.Text); }
            set {
                FillCustomerItems("");
                customers.SelectedItem=value<0?null:LabelCatalog.Customers[value];
            }
        }
        readonly int[] lastFormats=new int[LabelCatalog.Customers.Length];
        readonly TextBox header = new TextBox(), role = new TextBox(), person = new TextBox(), number = new TextBox();
        readonly Label headerLabel = new Label(), roleLabel = new Label(), status = new Label(), guide = new Label();
        readonly Label smallPlacement = new Label(), largePlacement = new Label(), garmentSummary = new Label();
        readonly Label personLabel = new Label(), numberLabel = new Label(), personSpaceNote = new Label();
        readonly NumericUpDown headerFont = new NumericUpDown(), roleFont = new NumericUpDown(), personFont = new NumericUpDown(), numberFont = new NumericUpDown();
        readonly Label headerFontUnit = new Label(), roleFontUnit = new Label(), personFontUnit = new Label(), numberFontUnit = new Label();
        readonly Font placementBold = new Font("Yu Gothic UI",10,FontStyle.Bold);
        int garmentSummarySlot;
        readonly NumericUpDown copies = new NumericUpDown();
        // 既定の小型レイアウト（目標スクショ ~812×784 相当）。リサイズ／均一スケールは使わない。
        const int FormClientWidth = 800, FormClientHeight = 560;
        const int FieldWidth = 200, FontBoxLeft = 218, FontBoxWidth = 48, FontUnitLeft = 268;
        const int LeftX = 12, PreviewX = 310, PreviewWidth = 470;
        const int LegendTop = 336, BandTop = 382, GuideTop = 430, ButtonTop = 478, StatusTop = 518;

        readonly PictureBox preview = new PictureBox();
        readonly PictureBox sample = new PictureBox();
        readonly PrintDocument document = new PrintDocument();
        readonly LabelData[] drafts = new LabelData[LabelCatalog.Items.Length];
        int active = -1;
        bool changing;
        static Label Caption(string s, int x, int y) { return new Label {Text=s,Left=x,Top=y,Width=280,Height=18}; }
        // ラベルは欄より低くし、会社名などでラベル背景が TextBox を覆って潰れて見えるのを防ぐ。
        const int FieldHeight = 26, LabelFieldHeight = 18, HeaderFieldTop = 108, HeaderLabelTop = 88;
        const int RoleFieldTop = 148, PersonFieldTop = 188;
        void Field(Control c, int x, int y, int width) {
            int h = (c is Label) ? LabelFieldHeight : FieldHeight;
            c.SetBounds(x,y,width,h); Controls.Add(c);
        }
        readonly UserSettings preferences;
readonly Label offsetSummary=new Label(); PrintOffset temporaryOffset;
string OffsetKey { get { var spec=LabelCatalog.Items[active]; return document.PrinterSettings.PrinterName+"|"+(spec.SettingsIdentity!=""?spec.SettingsIdentity:LabelCatalog.Customers[spec.Customer]+"|"+spec.Format+"|"+spec.Small+"|"+spec.Placement); } }
PrintOffset CurrentOffset() { return temporaryOffset??preferences.Get(OffsetKey); }
public MainForm(string settingsFile)
        {
            preferences=new UserSettings(settingsFile);
            preferences.RenameCustomer("弁釜札幌","弁釜本社/札幌/八軒/旭川/函館/ローズD/");
            preferences.RenameCustomer("よつ葉根釧工場","よつ葉根釧/宗谷工場");
            preferences.RenameCustomer("コープフーズ石狩食品工場","コープフーズ / 石狩食品工場");
            for(int i=0;i<drafts.Length;i++) drafts[i]=new LabelData {Header=LabelCatalog.Items[i].Heading};
            Text = "統合ネームラベル"; Font = new Font("Yu Gothic UI", 9);
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(FormClientWidth, FormClientHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            Label customerCaption=Caption("顧客",LeftX,6);customerCaption.Width=80;Controls.Add(customerCaption); Field(customers,LeftX,22,280);
            Button generic=new Button {Text="汎用ラベル"}; Field(generic,630,6,150);
            generic.Click+=delegate {using(GenericLabelForm dlg=new GenericLabelForm(preferences)) dlg.ShowDialog(this); UpdatePreview();};
            // メイン顧客欄：DropDown で直接入力し、Matches（名前・読み）で候補を絞る（別ダイアログ不要）
            formats.DropDownStyle=ComboBoxStyle.DropDownList;
            customers.DropDownStyle=ComboBoxStyle.DropDown;
            customers.AutoCompleteMode=AutoCompleteMode.None; customers.AutoCompleteSource=AutoCompleteSource.None;
            FillCustomerItems("");
            customers.MaxDropDownItems=20; customers.IntegralHeight=false; customers.DropDownHeight=320;
            kinds.DropDownStyle = printers.DropDownStyle = papers.DropDownStyle = ComboBoxStyle.DropDownList;
            kinds.Items.AddRange(LabelRenderer.Titles);
            for(int i=4;i<LabelCatalog.Items.Length;i++) kinds.Items.Add(LabelCatalog.Items[i].Format);
            formats.DropDownWidth=480;
            Controls.Add(Caption("ラベル形式",LeftX,48)); Field(formats,LeftX,64,280);
            headerLabel.Text="会社名 / 部署"; Field(headerLabel,LeftX,HeaderLabelTop,280); Field(header,LeftX,HeaderFieldTop,FieldWidth);
            department.DropDownStyle=ComboBoxStyle.DropDownList; department.Items.AddRange(new object[] {"製造課 冷食", "製造課 ハムソー", "工務課", "商品開発課", "生産管理課", "総務課", "調達課", "品質保証部"}); Field(department,LeftX,HeaderFieldTop,FieldWidth); department.Visible=false;
            colorDepartment.DropDownStyle=ComboBoxStyle.DropDownList; colorDepartment.Items.AddRange(LabelCatalog.ColorDepartments); Field(colorDepartment,LeftX,HeaderFieldTop,FieldWidth); colorDepartment.Visible=false;
            roleLabel.Text="役職"; Field(roleLabel,LeftX,128,280); Field(role,LeftX,RoleFieldTop,FieldWidth);
            personLabel.Text="名前"; Field(personLabel,LeftX,174,280); Field(person,LeftX,PersonFieldTop,FieldWidth); person.MaxLength=20;
            // 氏名欄の直下。既存の全角→半角変換（Current / LabelRenderer）と同じ趣旨。
            personSpaceNote.Text="※全角スペースも半角スペースとして出力されます";
            personSpaceNote.ForeColor=Color.FromArgb(80,80,80);
            Field(personSpaceNote,LeftX,214,280); personSpaceNote.Height=14;
            numberLabel.Text="社員番号（コードに変換する内容）"; Field(numberLabel,LeftX,228,280); Field(number,LeftX,244,FieldWidth);
            SetupFontBox(headerFont, headerFontUnit); SetupFontBox(roleFont, roleFontUnit); SetupFontBox(personFont, personFontUnit); SetupFontBox(numberFont, numberFontUnit);
            headerFont.Top=HeaderFieldTop; headerFontUnit.Top=HeaderFieldTop;
            roleFont.Top=RoleFieldTop; roleFontUnit.Top=RoleFieldTop;
            personFont.Top=PersonFieldTop; personFontUnit.Top=PersonFieldTop;
            numberFont.Top=244; numberFontUnit.Top=244;
            digits.DropDownStyle=ComboBoxStyle.DropDownList; digits.Items.AddRange(new object[]{"数字4桁（Y含め5文字）","数字5桁（Y含め6文字）"}); Field(digits,LeftX,274,280); digits.SelectedIndex=0;
            Controls.Add(Caption("ラベルの内容（拡大表示）",PreviewX,6));
            preview.SetBounds(PreviewX,22,PreviewWidth,185); preview.BackColor=Color.White; preview.BorderStyle=BorderStyle.FixedSingle;
            preview.SizeMode=PictureBoxSizeMode.Zoom; Controls.Add(preview);
            Controls.Add(Caption("完成見本（サンプル）",PreviewX,212));
            sample.SetBounds(PreviewX,228,150,72); sample.BackColor=Color.White; sample.BorderStyle=BorderStyle.FixedSingle;
            sample.SizeMode=PictureBoxSizeMode.Zoom; Controls.Add(sample);
            Label sampleNote=new Label {Text="見本の名前・番号は印刷されません。\r\n表示・印刷時、全角スペースはすべて半角に変換します。\r\n各入力欄右のptでサイズを変更できます。\r\n未変更時はカタログ／自動調整の既定のままです。",Left=470,Top=228,Width=310,Height=72};
            Controls.Add(sampleNote);
            sampleColor.SetBounds(470,302,310,20); sampleColor.TextAlign=ContentAlignment.MiddleLeft; Controls.Add(sampleColor);
            // 期生色帯は見本の下・プリンター欄の上。表示時だけ footer を押し下げて重なりを防ぐ。
            for(int n=0;n<4;n++) { colorLegend[n].SetBounds(LeftX+(n%2)*390,LegendTop+(n/2)*36,380,34); colorLegend[n].BorderStyle=BorderStyle.FixedSingle; colorLegend[n].Padding=new Padding(4,1,0,0); colorLegend[n].Visible=false; Controls.Add(colorLegend[n]); }
            Controls.Add(Caption("プリンター",LeftX,LegendTop)); Field(printers,LeftX,LegendTop+16,340);
            Controls.Add(Caption("用紙サイズ",360,LegendTop)); Field(papers,360,LegendTop+16,250);
            Label copiesCaption=Caption("部数",620,LegendTop); Controls.Add(copiesCaption); Field(copies,620,LegendTop+16,80); copies.Minimum=1; copies.Maximum=30; copies.Value=1;
            // 貼り付け先帯の直上。高さ0で開始し、複数形式時だけ UpdateGarmentSummary で押し広げ。
            garmentSummary.SetBounds(LeftX,BandTop,FormClientWidth-24,0); garmentSummary.Visible=false;
            garmentSummary.Padding=new Padding(6,2,0,0); garmentSummary.BackColor=Color.FromArgb(232,245,220);
            smallPlacement.SetBounds(LeftX,BandTop,FormClientWidth-24,22); largePlacement.SetBounds(LeftX,BandTop+22,FormClientWidth-24,22);
            Controls.Add(garmentSummary); Controls.Add(smallPlacement); Controls.Add(largePlacement);
            guide.SetBounds(LeftX,GuideTop,FormClientWidth-24,40); guide.BackColor=Color.White; guide.BorderStyle=BorderStyle.FixedSingle; guide.Padding=new Padding(6,2,0,0); Controls.Add(guide);
            Button page = new Button {Text="用紙・方向設定"}, proof = new Button {Text="印刷プレビュー"}, print = new Button {Text="印刷"};
            Field(page,LeftX,ButtonTop,150); Field(proof,170,ButtonTop,140); Field(print,630,ButtonTop,150); print.Height=36;
            Button adjust=new Button {Text="印字位置調整"};Field(adjust,320,ButtonTop,140);offsetSummary.SetBounds(470,ButtonTop,150,36);Controls.Add(offsetSummary);adjust.Click+=delegate {Safe(OpenOffset);};
            status.SetBounds(LeftX,StatusTop,FormClientWidth-24,32); Controls.Add(status);
            // 色帯自体は押し下げ対象外（LegendTop に固定し、プリンター以降だけ動かす）
            foreach(Control control in Controls) {
                if(control.Top<LegendTop) continue;
                bool legend=false;
                for(int n=0;n<colorLegend.Length;n++) if(control==colorLegend[n]) { legend=true; break; }
                if(!legend) footerControls.Add(control);
            }
            person.HideSelection=false;
            emphasize.Text="選択部分を大きく"; Field(emphasize,LeftX,274,170);
            clearEmphasis.Text="解除"; Field(clearEmphasis,188,274,80);
            emphasisGuide.SetBounds(LeftX,302,280,30); Controls.Add(emphasisGuide);
            emphasize.Click+=delegate { SetEmphasis(); };
            clearEmphasis.Click+=delegate { emphasisStart=-1; emphasisLength=0; UpdateEmphasisGuide(); UpdatePreview(); };
            person.TextChanged+=delegate { if(!changing) { emphasisStart=-1; emphasisLength=0; UpdateEmphasisGuide(); } };
            customers.SelectedIndexChanged += delegate {
                if(selecting || SelectedCustomerIndex<0) return;
                ApplyCustomerSelection();
            };
            // 打ち込み中：ドロップダウン候補を一致顧客だけに更新（読み・全半角可）
            customers.TextUpdate += delegate {
                if(selecting) return;
                string q=customers.Text;
                FillCustomerItems(q);
                if(q.Length==0 || customers.Items.Count==0) return;
                try {
                    string typed=customers.Text; int caret=customers.SelectionStart;
                    selecting=true;
                    if(!customers.DroppedDown) customers.DroppedDown=true;
                    customers.Text=typed;
                    if(caret>=0 && caret<=typed.Length) customers.SelectionStart=caret;
                } catch { } finally { selecting=false; }
            };
            // Enter をダイアログキーにせず受け取り、先頭候補を確定（フォーカス維持）
            customers.PreviewKeyDown += delegate(object sender, PreviewKeyDownEventArgs e) {
                if(e.KeyCode==Keys.Enter) e.IsInputKey=true;
            };
            customers.KeyDown += delegate(object sender, KeyEventArgs e) {
                if(e.KeyCode!=Keys.Enter) return;
                e.Handled=true; e.SuppressKeyPress=true;
                CommitCustomerOnEnter();
            };
            // クリック等で開いたとき、確定済み顧客なら全件を見せる（TextUpdate中は絞り込みを維持）
            customers.DropDown += delegate {
                if(selecting || SelectedCustomerIndex<0) return;
                FillCustomerItems("");
            };
            // 未確定入力のままフォーカスが外れたら一致顧客へ寄せる（なければ現在形式の顧客へ戻す）
            customers.Leave += delegate {
                if(selecting) return;
                int kept=SelectedCustomerIndex;
                if(kept>=0) { FillCustomerItems(""); customers.SelectedItem=LabelCatalog.Customers[kept]; return; }
                string typed=customers.Text; string match=null;
                if(!string.IsNullOrEmpty(typed))
                    foreach(string n in LabelCatalog.SortedCustomers())
                        if(UserSettings.Matches(n,typed)) { match=n; break; }
                if(match!=null) { FillCustomerItems(""); customers.SelectedItem=match; }
                else if(active>=0) SelectedCustomerIndex=LabelCatalog.Items[active].Customer;
            };
            formats.SelectedIndexChanged += delegate {
                if(selecting || SelectedCustomerIndex<0 || formats.SelectedIndex<0) return;
                lastFormats[SelectedCustomerIndex]=formats.SelectedIndex;
                kinds.SelectedIndex=LabelCatalog.ForCustomer(SelectedCustomerIndex)[formats.SelectedIndex];
            };
            kinds.SelectedIndexChanged += delegate { SwitchKind(); };
            digits.SelectedIndexChanged += delegate { if(!changing && active>=0 && active<2) { UpdateNumberGuide(); RefreshUnsetFontDefaults(); Safe(UpdateSample); UpdatePreview(); } };
            department.SelectedIndexChanged += delegate { if(!changing) {if(active>=0 && LabelCatalog.Items[active].ColorFromDepartment){UpdateNumberGuide();Safe(UpdateSample);}UpdatePreview();} };
            colorDepartment.SelectedIndexChanged += delegate { if(!changing && active>=0) { UpdateNumberGuide(); Safe(UpdateSample); UpdatePreview(); } };
            foreach (TextBox box in new TextBox[] {header,role,person,number}) box.TextChanged += delegate { if(!changing) { if(active>=0 && (LabelCatalog.Items[active].Layout=="CohortName" || LabelCatalog.Items[active].Layout=="DeptOnly")) {UpdateNumberGuide(); Safe(UpdateSample);} RefreshUnsetFontDefaults(); UpdatePreview(); } };
            headerFont.ValueChanged += delegate { if(changing || active<0) return; drafts[active].HeaderFontSet=true; drafts[active].HeaderFontPt=(float)headerFont.Value; UpdatePreview(); };
            roleFont.ValueChanged += delegate { if(changing || active<0) return; drafts[active].RoleFontSet=true; drafts[active].RoleFontPt=(float)roleFont.Value; UpdatePreview(); };
            personFont.ValueChanged += delegate { if(changing || active<0) return; drafts[active].PersonFontSet=true; drafts[active].PersonFontPt=(float)personFont.Value; UpdatePreview(); };
            numberFont.ValueChanged += delegate { if(changing || active<0) return; drafts[active].NumberFontSet=true; drafts[active].NumberFontPt=(float)numberFont.Value; UpdatePreview(); };
            printers.SelectedIndexChanged += delegate { SelectPrinter(); };
            papers.SelectedIndexChanged += delegate { if(!changing && papers.SelectedItem != null) { document.DefaultPageSettings.PaperSize=(PaperSize)papers.SelectedItem; UpdatePreview(); } };
            page.Click += delegate { Safe(delegate { using(PageSetupDialog dlg = new PageSetupDialog {Document=document,EnableMetric=true}) { dlg.ShowDialog(this); SyncPrinter(); } }); };
            proof.Click += delegate { if(Check()) Safe(delegate { using(PrintPreviewDialog dlg = new PrintPreviewDialog {Document=document,Width=1050,Height=760}) dlg.ShowDialog(this); }); };
            print.Click += delegate { if(Check()) Safe(delegate { print.Enabled=false; try { document.PrinterSettings.Copies=(short)copies.Value; document.Print(); status.Text="印刷データをプリンターへ送信しました。"; } finally { print.Enabled=true; } }); };
            document.PrintPage += PrintPage;
            // 既に SelectedIndex==0 だと SelectedIndexChanged が飛ばず SwitchKind 未実行のまま起動することがある
            kinds.SelectedIndex=0;
            if(active<0) SwitchKind();
            Safe(delegate { foreach(string p in PrinterSettings.InstalledPrinters) printers.Items.Add(p); SyncPrinter(true); });
            FormClosed += delegate { if(preview.Image!=null) preview.Image.Dispose(); if(sample.Image!=null) sample.Image.Dispose(); document.Dispose(); placementBold.Dispose(); };
            // 固定サイズ。起動直後から緑／青帯を出し、入力欄の PreferredHeight と前面化だけ整える。
            if(active>=0) UpdateNumberGuide();
            NormalizeInputHeights();
            BringInputRowsToFront();
            garmentSummary.BringToFront();
            smallPlacement.BringToFront();
            largePlacement.BringToFront();
            bool startupPlacementSynced=false;
            Shown += delegate {
                if(startupPlacementSynced || active<0) return;
                startupPlacementSynced=true;
                UpdateNumberGuide();
                NormalizeInputHeights();
                BringInputRowsToFront();
                garmentSummary.BringToFront();
                smallPlacement.BringToFront();
                largePlacement.BringToFront();
            };
        }
        void EnsurePreferredHeight(Control c) {
            TextBoxBase tb = c as TextBoxBase;
            if(tb!=null && !tb.Multiline) { int ph=tb.PreferredHeight; if(c.Height<ph) c.Height=ph; return; }
            ComboBox cb = c as ComboBox;
            if(cb!=null) { int ph=cb.PreferredHeight; if(c.Height<ph) c.Height=ph; return; }
            NumericUpDown nud = c as NumericUpDown;
            if(nud!=null) { int ph=nud.PreferredHeight; if(c.Height<ph) c.Height=ph; }
        }
        void NormalizeInputHeights() {
            foreach(Control c in Controls) EnsurePreferredHeight(c);
        }
        void BringInputRowsToFront() {
            // ラベル背景が入力欄を覆って潰れて見えるのを防ぐ（会社名行が典型）。
            foreach(Control c in new Control[]{headerLabel,roleLabel,personLabel,numberLabel}) c.SendToBack();
            foreach(Control c in new Control[]{header,role,person,number,department,colorDepartment,
                headerFont,roleFont,personFont,numberFont,headerFontUnit,roleFontUnit,personFontUnit,numberFontUnit})
                c.BringToFront();
        }
        void SetupFontBox(NumericUpDown box, Label unit) {
            box.Minimum=6; box.Maximum=120; box.DecimalPlaces=0; box.Increment=1; box.Value=18;
            Field(box, FontBoxLeft, HeaderFieldTop, FontBoxWidth);
            unit.Text="pt"; unit.TextAlign=ContentAlignment.MiddleLeft;
            Field(unit, FontUnitLeft, HeaderFieldTop, 28); unit.Width=28;
        }
        void PlaceFontBox(NumericUpDown box, Label unit, Control anchor, bool visible) {
            box.Visible=unit.Visible=visible;
            if(!visible || anchor==null) return;
            box.Top=anchor.Top; unit.Top=anchor.Top;
        }
        void SyncFontBox(NumericUpDown box, bool set, float pt, float fallback) {
            decimal value=(decimal)(set?LabelCatalog.PickFont(fallback,true,pt):fallback);
            if(value<box.Minimum) value=box.Minimum;
            if(value>box.Maximum) value=box.Maximum;
            box.Value=value;
        }
        void RefreshUnsetFontDefaults() {
            if(changing || active<0) return;
            LabelSpec spec=LabelCatalog.Items[active];
            LabelData d=Current();
            bool prev=changing; changing=true;
            if(headerFont.Visible && !d.HeaderFontSet) SyncFontBox(headerFont,false,0,LabelCatalog.DefaultHeaderPt(spec,d));
            if(roleFont.Visible && !d.RoleFontSet) SyncFontBox(roleFont,false,0,LabelCatalog.DefaultRolePt(spec));
            if(personFont.Visible && !d.PersonFontSet) SyncFontBox(personFont,false,0,LabelCatalog.DefaultPersonPt(spec,d));
            if(numberFont.Visible && !d.NumberFontSet) SyncFontBox(numberFont,false,0,LabelCatalog.DefaultNumberPt(spec,d));
            changing=prev;
        }
        void UpdateFontControls() {
            LabelSpec spec=LabelCatalog.Items[active];
            LabelData d=drafts[active];
            // 色区分だけの見出し（入力テキストを印字しない）にはptを出さない。
            bool colorOnlyHeader=colorDepartment.Visible && !header.Visible && !department.Visible && headerLabel.Visible;
            bool colorOnlyRole=colorDepartment.Visible && !role.Visible && roleLabel.Visible && LabelCatalog.DepartmentNames(active)!=null;
            bool showHeader=spec.UsesHeaderFont && (header.Visible || department.Visible) && !colorOnlyHeader;
            bool showRole=spec.UsesRoleFont && role.Visible && !colorOnlyRole;
            bool showPerson=spec.UsesPersonFont && person.Visible;
            bool showNumber=spec.UsesNumberFont && number.Visible;
            Control headerAnchor=department.Visible?(Control)department:(Control)header;
            PlaceFontBox(headerFont, headerFontUnit, headerAnchor, showHeader);
            PlaceFontBox(roleFont, roleFontUnit, role, showRole);
            PlaceFontBox(personFont, personFontUnit, person, showPerson);
            PlaceFontBox(numberFont, numberFontUnit, number, showNumber);
            SyncFontBox(headerFont, d.HeaderFontSet, d.HeaderFontPt, LabelCatalog.DefaultHeaderPt(spec,d));
            SyncFontBox(roleFont, d.RoleFontSet, d.RoleFontPt, LabelCatalog.DefaultRolePt(spec));
            SyncFontBox(personFont, d.PersonFontSet, d.PersonFontPt, LabelCatalog.DefaultPersonPt(spec,d));
            SyncFontBox(numberFont, d.NumberFontSet, d.NumberFontPt, LabelCatalog.DefaultNumberPt(spec,d));
        }
        LabelData Current() {
            LabelData d=new LabelData {
                Header=(LabelCatalog.DepartmentNames(active)!=null?department.Text:header.Text).Replace('　',' '),
                Role=role.Text.Replace('　',' '), Person=person.Text.Replace('　',' '), Number=number.Text.Replace('　',' '),
                Digits=digits.SelectedIndex==1?5:4,
                ColorDepartment=LabelCatalog.ColorGroups(active).Length>0?colorDepartment.Text:drafts[active].ColorDepartment,
                EmphasisStart=emphasisStart, EmphasisLength=emphasisLength,
                HeaderFontSet=drafts[active].HeaderFontSet, HeaderFontPt=drafts[active].HeaderFontPt,
                RoleFontSet=drafts[active].RoleFontSet, RoleFontPt=drafts[active].RoleFontPt,
                PersonFontSet=drafts[active].PersonFontSet, PersonFontPt=drafts[active].PersonFontPt,
                NumberFontSet=drafts[active].NumberFontSet, NumberFontPt=drafts[active].NumberFontPt
            };
            if(headerFont.Visible && d.HeaderFontSet) d.HeaderFontPt=(float)headerFont.Value;
            if(roleFont.Visible && d.RoleFontSet) d.RoleFontPt=(float)roleFont.Value;
            if(personFont.Visible && d.PersonFontSet) d.PersonFontPt=(float)personFont.Value;
            if(numberFont.Visible && d.NumberFontSet) d.NumberFontPt=(float)numberFont.Value;
            return d;
        }
        void SetEmphasis() {
            if(person.SelectionLength==0 || String.IsNullOrWhiteSpace(person.SelectedText)) {
                emphasisGuide.Text="名前の中で、大きくする文字を\r\nドラッグして選択してください。"; return;
            }
            emphasisStart=person.SelectionStart; emphasisLength=person.SelectionLength;
            UpdateEmphasisGuide(); UpdatePreview();
        }
        void UpdateEmphasisGuide() {
            LabelData d=Current();
            emphasisGuide.Text=LabelCatalog.HasEmphasis(d)?"大きく印字："+d.Person.Substring(d.EmphasisStart,d.EmphasisLength)+"\r\n名前を編集した場合は、指定し直してください。":"名前の中で大きくする文字を選択し、\r\n「選択部分を大きく」を押してください。";
        }
        void SwitchKind()
        {
            if(active>=0) {
                drafts[active]=Current();
                foreach(int id in LabelCatalog.ForCustomer(LabelCatalog.Items[active].Customer)) {
                    drafts[id].Person=drafts[active].Person; drafts[id].Number=drafts[active].Number; drafts[id].Digits=drafts[active].Digits;
                    drafts[id].PersonFontSet=drafts[active].PersonFontSet; drafts[id].PersonFontPt=drafts[active].PersonFontPt;
                    drafts[id].NumberFontSet=drafts[active].NumberFontSet; drafts[id].NumberFontPt=drafts[active].NumberFontPt;
                    if(String.Join("|",LabelCatalog.ColorGroups(active))==String.Join("|",LabelCatalog.ColorGroups(id))) drafts[id].ColorDepartment=drafts[active].ColorDepartment;
                    drafts[id].EmphasisStart=drafts[active].EmphasisStart; drafts[id].EmphasisLength=drafts[active].EmphasisLength;
                    if(LabelCatalog.DepartmentNames(active)!=null && LabelCatalog.DepartmentNames(id)!=null) { drafts[id].Header=drafts[active].Header; drafts[id].Role=drafts[active].Role; }
                    if(LabelCatalog.Items[active].Layout==LabelCatalog.Items[id].Layout) {
                        drafts[id].Role=drafts[active].Role;
                        drafts[id].HeaderFontSet=drafts[active].HeaderFontSet; drafts[id].HeaderFontPt=drafts[active].HeaderFontPt;
                        drafts[id].RoleFontSet=drafts[active].RoleFontSet; drafts[id].RoleFontPt=drafts[active].RoleFontPt;
                        if(LabelCatalog.Items[active].Layout=="DeptNumberName" || LabelCatalog.Items[active].Layout=="OptionalHeading" || LabelCatalog.Items[active].Layout=="StarRoman") drafts[id].Header=drafts[active].Header;
                    }
                }
            }
            active=kinds.SelectedIndex; changing=true;
            LabelSpec spec=LabelCatalog.Items[active];
            selecting=true;
            SelectedCustomerIndex=spec.Customer;
            formats.Items.Clear();
            int[] options=LabelCatalog.ForCustomer(spec.Customer);
            for(int n=0;n<options.Length;n++) { formats.Items.Add(LabelCatalog.Items[options[n]].Format); if(options[n]==active) { formats.SelectedIndex=n; lastFormats[spec.Customer]=n; } }
            formats.Enabled=options.Length>1;
            selecting=false;
            LabelData d=drafts[active]; department.Visible=active==3; header.Visible=active!=3; department.SelectedItem=d.Header; header.Text=d.Header; role.Text=d.Role; person.Text=d.Person; number.Text=d.Number;
            person.MaxLength=spec.Customer==18 || spec.Reading=="ローマ字" || spec.Reading=="半角カタカナ"?100:spec.Layout=="FreeText"?40:20;
            emphasisStart=d.EmphasisStart; emphasisLength=d.EmphasisLength;
            emphasize.Visible=clearEmphasis.Visible=emphasisGuide.Visible=spec.Layout=="MixedName";
            UpdateEmphasisGuide();
            header.ReadOnly=active<2; header.MaxLength=active==2?4:12; headerLabel.Text=active==2?"従業員No":active==3?"部署":"会社名";
            role.Visible=roleLabel.Visible=active==3; role.MaxLength=10; digits.Visible=active<2; digits.SelectedIndex=d.Digits==5?1:0;
            if(active>=4) { header.Visible=headerLabel.Visible=spec.Layout=="HeadingName"; header.ReadOnly=true; header.Text=spec.Heading; headerLabel.Text="会社名（定型）"; }
            else headerLabel.Visible=true;
            string[] departments=LabelCatalog.DepartmentNames(active);
            department.Items.Clear(); department.Visible=departments!=null;
            if(departments!=null) { department.Items.AddRange(departments); department.SelectedItem=d.Header; header.Visible=false; headerLabel.Visible=true; headerLabel.Text="部署"; }
            role.Visible=roleLabel.Visible=active==3 || spec.Layout=="QR";
            roleLabel.Text="役職"; role.MaxLength=20;
            if(spec.Layout=="DeptRoleName") {role.Visible=roleLabel.Visible=true;roleLabel.Text="役職（ない場合は空欄）";}
            if(spec.Layout=="RoleName") { role.Visible=roleLabel.Visible=true; roleLabel.Text="役職（空欄なら氏名のみ）"; }
            if(spec.Layout=="InitialName" || spec.Layout=="InitialMixed") { role.Visible=roleLabel.Visible=true; roleLabel.Text="名前の1文字目（同姓の方のみ）"; role.MaxLength=1; }
            if(spec.Layout=="CohortName") { role.Visible=roleLabel.Visible=true; roleLabel.Text="期生（例：5）"; role.MaxLength=3; }
            if(spec.Layout=="DeptFurigana") {role.Visible=roleLabel.Visible=true;roleLabel.Text="苗字のふりがな（ひらがな）";role.MaxLength=40; if(departments==null){header.Visible=headerLabel.Visible=true;header.ReadOnly=false;header.MaxLength=40;header.Text=d.Header;headerLabel.Text="部署名";}}
            if(spec.Layout=="StarRoman") {header.Visible=headerLabel.Visible=true;header.ReadOnly=false;header.MaxLength=100;header.Text=d.Header;headerLabel.Text="苗字（ローマ字）";role.Visible=roleLabel.Visible=true;roleLabel.Text="☆の数（なし＝0）";role.MaxLength=2;if(role.Text=="")role.Text="0";}
            if(spec.Layout=="DeptOnly" || spec.Layout=="OptionalHeading") {header.Visible=headerLabel.Visible=true;header.ReadOnly=false;header.MaxLength=40;header.Text=d.Header;headerLabel.Text=spec.Layout=="DeptOnly"?"部署名（印字する内容）":"部署名（空欄なら氏名のみ）";}
            if(spec.Layout=="DeptNumberName") { header.Visible=headerLabel.Visible=true; header.ReadOnly=false; header.MaxLength=30; header.Text=d.Header; headerLabel.Text="部署（印字する内容）"; }
            colorDepartment.Items.Clear();
            string[] groups=LabelCatalog.ColorGroups(active);
            colorDepartment.Items.AddRange(groups);
            colorDepartment.Visible=groups.Length>0 && spec.Layout!="CohortName" && !spec.ColorFromDepartment; colorDepartment.SelectedItem=d.ColorDepartment;
            colorDepartment.Top=HeaderFieldTop;
            if(groups.Length>0 && spec.Layout!="CohortName" && !spec.ColorFromDepartment) {
                string caption=spec.ColorCaption+"（ラベル色）";
                if(departments!=null) { colorDepartment.Top=PersonFieldTop; role.Visible=false; roleLabel.Visible=true; roleLabel.Text=caption; }
                else { header.Visible=false; headerLabel.Visible=true; headerLabel.Text=caption; }
            }
            if(spec.Layout=="SizeNumber") { role.Visible=false; roleLabel.Visible=true; roleLabel.Text="その他サイズ（例：10L）"; header.Visible=true; header.Top=PersonFieldTop; header.ReadOnly=false; header.MaxLength=5; header.Text=d.Header; }
            else {
                header.Top=headerLabel.Visible?headerLabel.Top+(HeaderFieldTop-HeaderLabelTop):HeaderFieldTop;
                EnsurePreferredHeight(header);
            }
            EnsurePreferredHeight(person); EnsurePreferredHeight(role); EnsurePreferredHeight(number);
            BringInputRowsToFront();
            person.Visible=personLabel.Visible=personSpaceNote.Visible=spec.NeedsName;
            personLabel.Text=(spec.SurnameOnly || spec.Layout=="Surname"?"苗字":"名前")+(spec.Reading!=""?"（"+spec.Reading+"で入力）":spec.SurnameOnly || spec.Layout=="Surname"?"（苗字のみ印字）":"（フルネーム）");
            if(spec.InputCaption!="")personLabel.Text=spec.InputCaption;
            number.Visible=numberLabel.Visible=spec.NeedsNumber;
            numberLabel.Text=active<4 || spec.Layout=="QR"?"社員番号（コードに変換する内容）":"管理番号（印字する番号）";
            if(spec.NumberLength>0) numberLabel.Text=(spec.Layout=="NumberName" || spec.Layout=="CohortName"?"社員番号":"番号")+"（数字"+spec.NumberLength+"桁）";
            if(spec.Layout=="PrefixName") numberLabel.Text="丸数字の番号（1～50）";

            UpdateNumberGuide();
            UpdateFontControls();
            document.DefaultPageSettings.Landscape=active==2 || active==3 || spec.Layout=="QR";
            changing=false; Safe(UpdateSample); Safe(LoadPapers); UpdatePreview();
        }
        void UpdateNumberGuide()
        {
            LabelSpec spec=LabelCatalog.Items[active];
            UpdateColorLegend();
            Color bandBlue=Color.FromArgb(218,237,255);
            int count=digits.SelectedIndex==1?5:4;
            number.MaxLength=active<2?count+1:active<4?LabelRenderer.NumberLimits[active]:spec.Layout=="QR"?6:spec.NumberLength>0?spec.NumberLength:10;
            if(active<2 && number.Text.Length>number.MaxLength) number.Text=number.Text.Substring(0,number.MaxLength);
            guide.Text="使用するラベル：白　｜　サイズ："+(active==0?"小（幅50mm・カット長24mm）":"大（幅50mm・カット長80mm）")+(active<2?"\r\nよつ葉のQRコード：Y＋数字"+count+"桁（例："+(count==5?"Y12345":"Y1234")+"／合計"+(count+1)+"文字）":"");
            // 青帯: 形式を選んだら常に詳細な貼り付け先を出す（よつ葉の【選択中】行とは別枠）
            smallPlacement.Visible=true;
            smallPlacement.Text="貼り付け先："+spec.Placement;
            smallPlacement.Font=placementBold;
            smallPlacement.BackColor=bandBlue;
            // 【選択中】対応表は active<2 限定にせず、同一顧客の全形式（夏服 HeadingName 含む）で出す
            string selection=LabelCatalog.SelectionGuide(spec.Customer,active);
            if(selection!="") {
                largePlacement.Visible=true;
                largePlacement.Text=selection;
                largePlacement.Font=Font;
                largePlacement.BackColor=BackColor;
            } else {
                largePlacement.Text=spec.Pending!=""?"要確認："+spec.Pending:spec.Note+(spec.ColorGuide!=""?(spec.Note!=""?" ／ ":"")+"色の指定："+spec.ColorGuide:"");
                largePlacement.Visible=largePlacement.Text!="" && !(spec.Customer==10 || spec.Customer==14);
                largePlacement.Font=Font;
                largePlacement.BackColor=BackColor;
            }
            if(active>=4) {
                string color=LabelCatalog.ColorName(active,Current());
                guide.Text=(color!=""?"使用するラベル："+color+"　｜　":"")+"サイズ："+(spec.UnknownSize?"未指定":spec.Small?"小（幅50mm・カット長24mm）":"大（幅50mm・カット長80mm）")+"\r\n"+(spec.Family=="MS PGothic"?"Pゴシック":spec.Family=="MS PMincho"?(spec.HeaderFamily!=""?"上段："+spec.HeaderFamily+"／下段P明朝":"P明朝"):"HG丸ゴシック")+(spec.StrictFont?"／氏名は20pt固定（収まらない場合は印刷できません）":"／文字の大きさは印字範囲に合わせて自動調整")+"／各欄右のptで変更可";
            }
            UpdateGarmentSummary();
            ResizePlacementGuides();
            // 高さ確定後に同Y・重なりを矯正（Resize より前だと青帯が緑帯に隠れることがある）
            EnsurePlacementBandStack();
        }
        /// <summary>複数形式の顧客だけ、貼り付け先帯の上に商品→大/小対応の色違い帯を出す。</summary>
        void UpdateGarmentSummary() {
            string text=active<0?"":LabelCatalog.GarmentSummary(LabelCatalog.Items[active].Customer);
            bool show=text!="";
            garmentSummary.Text=show?text:"";
            garmentSummary.Font=Font;
            garmentSummary.BackColor=Color.FromArgb(232,245,220);
            garmentSummary.Visible=show;
            int measured=show?TextRenderer.MeasureText(text,garmentSummary.Font,new Size(Math.Max(40,garmentSummary.Width-10),1000),TextFormatFlags.WordBreak).Height:0;
            int unwrapped=show?Math.Max(TextRenderer.MeasureText(text,garmentSummary.Font).Width,text.Length*garmentSummary.Font.Height):0;
            int conservative=show?(int)Math.Ceiling(unwrapped/(double)Math.Max(40,garmentSummary.Width-10))*garmentSummary.Font.Height:0;
            int needed=show?Math.Max(23,Math.Max(measured,conservative)+4):0;
            int delta=needed-garmentSummarySlot;
            if(delta!=0) {
                foreach(Control control in footerControls)
                    if(control!=garmentSummary && control.Top>=garmentSummary.Top) control.Top+=delta;
                ClientSize=new Size(ClientSize.Width,ClientSize.Height+delta);
                garmentSummarySlot=needed;
            }
            garmentSummary.Height=needed;
        }
        /// <summary>緑帯と青帯が同Y・高さ0で消えたように見えないよう、積み順を矯正する。</summary>
        void EnsurePlacementBandStack() {
            int minSmall=garmentSummary.Top+garmentSummary.Height;
            if(smallPlacement.Top<minSmall) {
                int delta=minSmall-smallPlacement.Top;
                foreach(Control control in footerControls)
                    if(control!=garmentSummary && control.Top>=smallPlacement.Top) control.Top+=delta;
                ClientSize=new Size(ClientSize.Width,ClientSize.Height+delta);
            }
            if(largePlacement.Visible && largePlacement.Top<smallPlacement.Bottom) {
                int delta=smallPlacement.Bottom-largePlacement.Top;
                foreach(Control control in footerControls)
                    if(control!=smallPlacement && control!=garmentSummary && control.Top>=largePlacement.Top) control.Top+=delta;
                ClientSize=new Size(ClientSize.Width,ClientSize.Height+delta);
            }
        }
        void ResizePlacementGuides() {
            foreach(Label label in new Label[]{smallPlacement,largePlacement}) {
                int measured=label.Visible?TextRenderer.MeasureText(label.Text,label.Font,new Size(Math.Max(40,label.Width-10),1000),TextFormatFlags.WordBreak).Height:0;
                int unwrapped=label.Visible?Math.Max(TextRenderer.MeasureText(label.Text,label.Font).Width,label.Text.Length*label.Font.Height):0;
                int conservative=label.Visible?(int)Math.Ceiling(unwrapped/(double)Math.Max(40,label.Width-10))*label.Font.Height:0;
                // 非表示帯は高さ0にして隙間・重なりの誤表示を防ぐ
                int needed=label.Visible?Math.Max(23,Math.Max(measured,conservative)+4):0;
                int delta=needed-label.Height;
                if(delta==0) continue;
                foreach(Control control in footerControls) if(control.Top>label.Top) control.Top+=delta;
                label.Height=needed; ClientSize=new Size(ClientSize.Width,ClientSize.Height+delta);
            }
        }
        void UpdateColorLegend()
        {
            string[] groups=LabelCatalog.ColorGroups(active), groupColors=LabelCatalog.GroupColors(active);
            System.Collections.Generic.List<string> colors=new System.Collections.Generic.List<string>();
            foreach(string color in groupColors) if(!colors.Contains(color)) colors.Add(color);
            int cardWidth=Math.Max(180,(ClientSize.Width-LeftX*2-10)/2);
            int gap=Math.Max(8,ClientSize.Width-LeftX*2-cardWidth*2);
            int cardHeight=34;
            foreach(string color in colors) {
                string list="";
                for(int i=0;i<groups.Length;i++) if(groupColors[i]==color) list+="【"+groups[i]+"】 ／ ";
                cardHeight=Math.Max(cardHeight,TextRenderer.MeasureText(list,Font,new Size(Math.Max(120,cardWidth-12),1000),TextFormatFlags.WordBreak).Height+20);
            }
            // 見本・注意書きの下に確保。footer（プリンター以降）だけ押し下げ、入力／見本とは重ねない。
            int targetHeight=colors.Count==0?0:((colors.Count+1)/2)*(cardHeight+4)+4;
            if(targetHeight!=legendHeight) {
                int delta=targetHeight-legendHeight;
                foreach(Control control in footerControls) control.Top+=delta;
                ClientSize=new Size(ClientSize.Width,ClientSize.Height+delta); legendHeight=targetHeight;
            }
            for(int n=0;n<4;n++) {
                colorLegend[n].Visible=n<colors.Count;
                if(n>=colors.Count) continue;
                colorLegend[n].SetBounds(LeftX+(n%2)*(cardWidth+gap),LegendTop+(n/2)*(cardHeight+4),cardWidth,cardHeight);
                colorLegend[n].BackColor=LabelCatalog.Swatch(colors[n]);
                string list="";
                for(int d=0;d<groups.Length;d++) if(groupColors[d]==colors[n]) {
                    if(list!="") list+=" ／ ";
                    string name=groups[d];
                    list+=name==LabelCatalog.SelectedGroup(active,Current())?"【"+name+"】":name;
                }
                colorLegend[n].Text=colors[n]+"ラベル"+(LabelCatalog.ColorName(active,Current())==colors[n]?"（【 】は選択中の区分）":"")+"\r\n"+list;
                colorLegend[n].BringToFront();
            }
        }
        void UpdateSample()
        {
            string colorName=LabelCatalog.ColorName(active,Current());
            sampleColor.Visible=colorName!=""; sampleColor.Text="ラベル色："+colorName; sampleColor.BackColor=BackColor;
            LabelData example=new LabelData { Digits=digits.SelectedIndex==1?5:4, Header=active<2?"よつ葉乳業㈱":active==2?"1234":"製造課 冷食",Role=active==3?"主任":"",Person="山田 太郎",Number=active<2?(digits.SelectedIndex==1?"Y12345":"Y1234"):active==2?"12345678":"123456" };
            if(active>=4) { example.Header=LabelCatalog.Items[active].Heading; example.Number="101"; if(active==4) example.Person="山田"; }
            string[] departments=LabelCatalog.DepartmentNames(active);
            if(departments!=null) example.Header=department.SelectedIndex>=0?department.Text:departments[0];
            if(LabelCatalog.Items[active].Layout=="QR") { example.Number="123456"; example.Role="主任"; }
            if(LabelCatalog.Items[active].Layout=="MixedName") { example.Person="ブイ ティ ツオン アン"; example.EmphasisStart=example.Person.LastIndexOf("アン"); example.EmphasisLength=2; }
            LabelSpec spec=LabelCatalog.Items[active];
            if(spec.Reading=="カタカナ") example.Person="ヤマダ タロウ";
            if(spec.Reading=="ひらがな") example.Person="やまだ たろう";
            if(spec.Reading=="半角カタカナ")example.Person="ﾔﾏﾀﾞ ﾀﾛｳ";
            if(spec.Reading=="ローマ字")example.Person="Yamada Taro";
            if(spec.Layout=="DeptOnly")example.Header="鮮魚";
            if(spec.Layout=="DeptFurigana"){example.Role="やまだ";example.Person="山田";}
            if(spec.Layout=="StarRoman"){example.Header="YAMADA";example.Role="3";example.Person="山田";}
            if(spec.Layout=="OptionalHeading")example.Header="製造";
            if(spec.Layout=="SplitCode")example.Person="M-23";
            if(spec.Layout=="FreeText")example.Person=spec.InputCaption.Contains("ファースト")?"タロウ":"M";
            if(spec.NumberLength>0) example.Number="1234567890".Substring(0,spec.NumberLength);
            if(spec.Layout=="PrefixName") example.Number="13";
            if(spec.Layout=="DeptNumberName") example.Header="製造";
            if(spec.Layout=="RoleName" || spec.Layout=="DeptRoleName") example.Role="主任";
            if(spec.Layout=="InitialName" || spec.Layout=="InitialMixed") example.Role="太";
            if(spec.Layout=="CohortName") example.Role=role.Text==""?"1":role.Text;
            if(spec.Layout=="SizeNumber") { example.ColorDepartment=colorDepartment.Text==""?"M":colorDepartment.Text; example.Header=header.Text==""?"10L":header.Text; example.Number="60"; }
            Size size=LabelCatalog.Millimetres(active);
            Bitmap bitmap=new Bitmap(size.Width*6,size.Height*6);
            try { using(Graphics g=Graphics.FromImage(bitmap)) { g.Clear(LabelCatalog.Swatch(colorName)); g.ScaleTransform(6*0.254f,6*0.254f); LabelRenderer.Draw(g,active,example); } }
            catch { bitmap.Dispose(); throw; }
            sample.BackColor=LabelCatalog.Swatch(colorName);
            // 小型レイアウトの見本枠を維持（ClientSize 変更で色帯へ食い込ませない）
            sample.SetBounds(PreviewX,228,LabelCatalog.Items[active].Small?150:140,72);
            Image old=sample.Image; sample.Image=bitmap; if(old!=null) old.Dispose();
        }
        void UpdatePreview() {
            Safe(delegate { PrintOffset offset=CurrentOffset();Bitmap bmp=CreatePreview(offset);Image old=preview.Image;preview.Image=bmp;if(old!=null)old.Dispose();
                offsetSummary.Text="左右："+offset.X.ToString("0.0")+" mm\r\n上下："+offset.Y.ToString("0.0")+" mm";
                status.Text=PaperError()??LabelRenderer.Validate(active,Current())??"印刷できます。用紙サイズと印刷方向を確認してください。";
            });
        }
        Bitmap CreatePreview(PrintOffset offset) {
                Bitmap bmp=new Bitmap(1160,620);
                try { using(Graphics g=Graphics.FromImage(bmp)) {
                    g.Clear(Color.FromArgb(235,238,242));
                    g.ScaleTransform(2,2);
                    // Fit each label independently while preserving its physical aspect ratio.
                    Size mm=LabelCatalog.Millimetres(active); float mmWidth=mm.Width, mmHeight=mm.Height;
                    float scale=Math.Min(550f/mmWidth,275f/mmHeight), width=mmWidth*scale, height=mmHeight*scale;
                    float left=(580-width)/2, top=30+(275-height)/2;
                    using(Font caption=new Font("Yu Gothic UI",10,FontStyle.Bold,GraphicsUnit.Pixel))
                        g.DrawString(LabelCatalog.Items[active].UnknownSize?"サイズ未指定（仕様確認中）":LabelCatalog.Items[active].Small?"小：50 × 24 mm":"大：80 × 50 mm（用紙幅50・カット長80 mm）",caption,Brushes.Black,12,8);
                    string colorName=LabelCatalog.ColorName(active,Current());
                    if(colorName!="") {
                        using(Brush colorBrush=new SolidBrush(LabelCatalog.Swatch(colorName))) g.FillRectangle(colorBrush,430,4,138,22);
                        g.DrawRectangle(Pens.Gray,430,4,138,22);
                        using(Font colorFont=new Font("Yu Gothic UI",11,FontStyle.Bold,GraphicsUnit.Pixel)) g.DrawString("ラベル色："+colorName,colorFont,Brushes.Black,438,8);
                    }
                    using(Brush paperBrush=new SolidBrush(LabelCatalog.Swatch(colorName))) g.FillRectangle(paperBrush,left,top,width,height);
                    GraphicsState state=g.Save();
                    g.SetClip(new RectangleF(left,top,width,height));
                    g.TranslateTransform(left,top); g.ScaleTransform(scale*0.254f,scale*0.254f);
                    g.TranslateTransform((float)offset.X/.254f,(float)offset.Y/.254f);
                    LabelRenderer.Draw(g,active,Current());
                    g.Restore(state);
                    using(Pen outline=new Pen(Color.FromArgb(90,100,110),1)) g.DrawRectangle(outline,left,top,width,height);
                } }
                catch { bmp.Dispose(); throw; }

            return bmp;
        }
        void OpenOffset() {
            string key=OffsetKey; using(OffsetDialog dlg=new OffsetDialog(document.PrinterSettings.PrinterName+"\r\n"+customers.Text+" ／ "+formats.Text+" ／ "+LabelCatalog.Items[active].Placement,preferences.Get(key),CreatePreview,delegate(PrintOffset p) {
                if(!Check()) return; short oldCopies=document.PrinterSettings.Copies; try {temporaryOffset=p;document.PrinterSettings.Copies=1;document.Print();} finally {temporaryOffset=null;document.PrinterSettings.Copies=oldCopies;}
            },delegate(PrintOffset p) {preferences.Set(key,p);})) dlg.ShowDialog(this); UpdatePreview();
        }
        void SelectPrinter()
        {
            if(changing || printers.SelectedItem==null) return;
            Safe(delegate { bool landscape=document.DefaultPageSettings.Landscape; document.PrinterSettings.PrinterName=printers.SelectedItem.ToString(); document.DefaultPageSettings=document.PrinterSettings.DefaultPageSettings; document.DefaultPageSettings.Landscape=landscape; LoadPapers(); });
        }
        void SyncPrinter(bool initial=false)
        {
            changing=true; printers.SelectedItem=document.PrinterSettings.PrinterName; changing=false; LoadPapers(initial);
        }
        void LoadPapers() { LoadPapers(true); }
        void LoadPapers(bool initial)
        {
            changing=true;
            try {
                papers.Items.Clear(); papers.DisplayMember="PaperName";
                if(!document.PrinterSettings.IsValid) { status.Text="プリンターがありません。ラベルの内容は確認できます。"; return; }
                foreach(PaperSize p in document.PrinterSettings.PaperSizes) papers.Items.Add(p);
                PaperSize current=document.DefaultPageSettings.PaperSize;
                PaperSize chosen=initial?ChoosePaper(papers.Items,active):null;
                if(chosen==null) { foreach(PaperSize p in papers.Items) if(p.PaperName==current.PaperName && p.Width==current.Width && p.Height==current.Height) { chosen=p; break; } }
                if(chosen==null) { chosen=current; papers.Items.Add(chosen); }
                if(chosen!=null) { papers.SelectedItem=chosen; document.DefaultPageSettings.PaperSize=chosen; }
            } finally { changing=false; }
            UpdatePreview();
        }
        static PaperSize ChoosePaper(System.Collections.IEnumerable available,int kind)
        {
            string length=LabelCatalog.Items[kind].Small?"24":"80";
            foreach(PaperSize paper in available) {
                string name=paper.PaperName.Normalize(NormalizationForm.FormKC);
                if(System.Text.RegularExpressions.Regex.IsMatch(name,@"(?<!\d)50(?!\d)") && System.Text.RegularExpressions.Regex.IsMatch(name,@"(?<!\d)"+length+@"(?!\d)")) return paper;
            }
            return null;
        }
        string PaperError()
        {
            if(!document.PrinterSettings.IsValid) return "プリンターを選択してください。";
            if(papers.SelectedItem==null) return "用紙を選択してください。";
            return null;
        }        bool Check()
        {
            string error=LabelRenderer.Validate(active,Current());
            if(error==null) error=PaperError();
            if(error!=null) { MessageBox.Show(this,error,"入力の確認",MessageBoxButtons.OK,MessageBoxIcon.Information); return false; }
            document.DocumentName="ネームラベル - "+LabelCatalog.Customers[LabelCatalog.Items[active].Customer]+" - "+LabelCatalog.Items[active].Format; return true;
        }
        void PrintPage(object sender, PrintPageEventArgs e)
        {
            GraphicsState state=e.Graphics.Save();
            try {
                e.Graphics.PageUnit=GraphicsUnit.Inch;
                e.Graphics.ScaleTransform(0.01f,0.01f);
                if(active==1 || (active>=4 && !LabelCatalog.Items[active].Small && LabelCatalog.Items[active].Layout!="QR")) { e.Graphics.RotateTransform(-90); e.Graphics.TranslateTransform(-e.PageBounds.Height,0); }
                PrintOffset offset=CurrentOffset();e.Graphics.TranslateTransform((float)offset.X/.254f,(float)offset.Y/.254f);
                LabelRenderer.Draw(e.Graphics,active,Current()); e.HasMorePages=false;
            } finally { e.Graphics.Restore(state); }
        }
        void Safe(Action action)
        {
            try { action(); } catch(Exception ex) { status.Text="処理できませんでした: "+ex.Message; }
        }
        // Runs without sending a print job. Exercises every layout and input length.
        public void VerifyFeatures(string folder) {
            Directory.CreateDirectory(folder);
            var keys=new System.Collections.Generic.HashSet<string>();
            for(int k=0;k<LabelCatalog.Items.Length;k++){kinds.SelectedIndex=k;if(!keys.Add(OffsetKey))throw new Exception("印字位置の保存キーが重複しています");}kinds.SelectedIndex=0;
            foreach(bool rotate in new[]{false,true}) using(Matrix m=new Matrix()) {
                m.Scale(.01f,.01f);if(rotate){m.Rotate(-90);m.Translate(-400,0);}
                PointF[] before={new PointF(0,0)};m.TransformPoints(before);m.Translate(1/.254f,2/.254f);PointF[] after={new PointF(0,0)};m.TransformPoints(after);
                float dx=after[0].X-before[0].X,dy=after[0].Y-before[0].Y;
                if(Math.Abs(dx-(rotate?2:1)/25.4f)>.00001f||Math.Abs(dy-(rotate?-1:2)/25.4f)>.00001f)throw new Exception("回転・mm方向の変換失敗");
            }
            string testPath=Path.Combine(Path.GetFullPath(folder),"persistence.xml");
            UserSettings s=new UserSettings(testPath);s.Favorites.Clear();s.Recent.Clear();s.Toggle("よつ葉乳業");s.Visit("だるま食品");s.Visit("よつ葉乳業");s.Visit("だるま食品");s.Set("printer-A|small",new PrintOffset(1.2m,-.3m));s.Set("printer-A|big",new PrintOffset(-2m,3m));
            UserSettings r=new UserSettings(testPath);
            if(!r.Favorites.Contains("よつ葉乳業")||r.Recent.Count!=2||r.Recent[0]!="だるま食品"||r.Get("printer-A|small").X!=1.2m||r.Get("printer-A|big").Y!=3||r.Get("printer-B|small").X!=0)throw new Exception("設定保存の検証失敗");
            if(!UserSettings.Matches("Umios Hokkaido 札幌工場","ＵＭＩＯＳ")||!UserSettings.Matches("よつ葉乳業","よつ葉")||!UserSettings.Matches("よつ葉乳業㈱十勝工場","よつば")||!UserSettings.Matches("よつ葉乳業㈱十勝工場","ヨツバ")||!UserSettings.Matches("Umios Hokkaido 札幌工場","うみおす")||UserSettings.Matches("だるま食品","存在しない"))throw new Exception("検索の検証失敗");
            if(FormBorderStyle!=FormBorderStyle.FixedSingle)throw new Exception("メイン画面が固定サイズでない");
            if(MaximizeBox)throw new Exception("メイン画面の最大化が有効のまま");
            if(ClientSize.Width>FormClientWidth+40)throw new Exception("既定幅が小型レイアウトより大きい "+ClientSize.Width);
            if(LabelCatalog.GarmentSummary(76)!="白衣：大ネーム　白下：小ネーム")throw new Exception("十勝大福の商品対応帯テキストが想定と違う");
            if(LabelCatalog.GarmentSummary(1)!="")throw new Exception("1形式顧客に商品対応帯テキストが出る");
            // Excel 大きさの QR 区別が緑帯に出ること（よつ葉十勝）
            string yotsubaBand=LabelCatalog.GarmentSummary(0);
            if(yotsubaBand.IndexOf("小ネーム（QR）")<0 || yotsubaBand.IndexOf("大ネーム（QR）")<0)throw new Exception("よつ葉緑帯にQR区別が出ない "+yotsubaBand);
            if(LabelCatalog.ShortFormName("大ネーム（パート2）")!="パート・帽子")throw new Exception("パート2の緑帯短縮がパート・帽子でない");
            if(LabelCatalog.ShortFormName("大ネーム（資格）")!="大ネーム（資格）")throw new Exception("資格の緑帯短縮が想定と違う");
            // 起動直後（顧客・形式を触る前）: 初期選択の緑帯＋貼り付け先＋【選択中】
            if(active!=0 || SelectedCustomerIndex!=0)throw new Exception("起動時の初期選択がよつ葉小ネームでない");
            if(!garmentSummary.Visible || garmentSummary.Height<=0 || garmentSummary.Text.IndexOf("小ネーム（QR）")<0)throw new Exception("起動直後に商品対応帯が出ない");
            if(!smallPlacement.Visible || smallPlacement.Text!="貼り付け先：黒下：左前" || smallPlacement.Height<=0)throw new Exception("起動直後に貼り付け先帯が出ない");
            if(!largePlacement.Visible || largePlacement.Text.IndexOf("【選択中】")<0)throw new Exception("起動直後によつ葉の【選択中】行が出ない");
            if(garmentSummary.Bottom>smallPlacement.Top || smallPlacement.Bottom>largePlacement.Top || largePlacement.Bottom>guide.Top)throw new Exception("起動直後の帯レイアウトが重なっている");
            SelectedCustomerIndex=76; Application.DoEvents();
            if(!garmentSummary.Visible || garmentSummary.Text!="白衣：大ネーム　白下：小ネーム")throw new Exception("十勝大福で商品対応帯が表示されない");
            if(!smallPlacement.Visible || smallPlacement.Text.IndexOf("貼り付け先：")!=0 || smallPlacement.Height<=0)throw new Exception("十勝大福で貼り付け先帯が出ない");
            if(garmentSummary.Bottom>smallPlacement.Top)throw new Exception("商品対応帯が貼り付け先帯と重なっている");
            if(smallPlacement.Bottom>largePlacement.Top || largePlacement.Bottom>guide.Top)throw new Exception("貼り付け先帯レイアウトが崩れている");
            SelectedCustomerIndex=1; Application.DoEvents();
            if(garmentSummary.Visible || garmentSummary.Height!=0)throw new Exception("1形式顧客で商品対応帯が残っている");
            if(!smallPlacement.Visible || smallPlacement.Text.IndexOf("貼り付け先：")!=0)throw new Exception("1形式顧客で貼り付け先帯が出ない");
            // よつ葉: 緑帯＋青帯（貼り付け先）＋【選択中】行が共存すること（夏服 HeadingName でも消えない）
            SelectedCustomerIndex=0; formats.SelectedIndex=0; Application.DoEvents();
            if(!garmentSummary.Visible || garmentSummary.Height<=0 || garmentSummary.Text.IndexOf("小ネーム（QR）")<0)throw new Exception("よつ葉で商品対応帯（QR）が出ない");
            if(!smallPlacement.Visible || smallPlacement.Text!="貼り付け先：黒下：左前" || smallPlacement.Height<=0)throw new Exception("よつ葉小ネームで貼り付け先帯が出ない");
            if(!largePlacement.Visible || largePlacement.Text.IndexOf("小ネーム → 黒下【選択中】")<0)throw new Exception("よつ葉の【選択中】行が消えている");
            if(largePlacement.Text.IndexOf("ブルゾン夏")<0)throw new Exception("よつ葉対応表に夏服行が出ない");
            if(garmentSummary.Bottom>smallPlacement.Top || smallPlacement.Bottom>largePlacement.Top || largePlacement.Bottom>guide.Top)throw new Exception("よつ葉の帯レイアウトが重なっている");
            formats.SelectedIndex=1; Application.DoEvents();
            if(smallPlacement.Text!="貼り付け先：白衣：左胸" || largePlacement.Text.IndexOf("大ネーム → 白衣【選択中】")<0)throw new Exception("よつ葉大ネームで貼り付け先／選択中が更新されない");
            formats.SelectedIndex=2; Application.DoEvents();
            if(LabelCatalog.Items[active].Format.IndexOf("夏服")<0)throw new Exception("よつ葉3形式目が夏服用ではない");
            if(!smallPlacement.Visible || smallPlacement.Text.IndexOf("貼り付け先：ブルゾン夏")!=0)throw new Exception("よつ葉夏服で貼り付け先帯が出ない");
            if(!largePlacement.Visible || largePlacement.Text.IndexOf("ブルゾン夏【選択中】")<0)throw new Exception("よつ葉夏服で【選択中】が消える");
            if(largePlacement.Text.IndexOf("小ネーム → 黒下")<0 || largePlacement.Text.IndexOf("大ネーム → 白衣")<0)throw new Exception("よつ葉夏服でQR対応行が消える");
            if(garmentSummary.Bottom>smallPlacement.Top || smallPlacement.Bottom>largePlacement.Top || largePlacement.Bottom>guide.Top)throw new Exception("よつ葉夏服の帯レイアウトが重なっている");
            // よつ葉東京工場: Excel r212–214（社名つなぎ＋白＋通行服）。形式名は大きさ欄に合わせる。
            int[] tokyo=LabelCatalog.ForCustomer(73);
            if(tokyo.Length!=3) throw new Exception("よつ葉東京工場の形式数がExcelと違う "+tokyo.Length);
            if(LabelCatalog.Items[tokyo[0]].Layout!="FixedSingle" || LabelCatalog.Items[tokyo[0]].Format!="小ネーム（社名）" || LabelCatalog.Items[tokyo[0]].Placement!="つなぎ：左胸(片布)" || LabelCatalog.Items[tokyo[0]].MaximumFont!=24)
                throw new Exception("よつ葉東京・つなぎ定型がExcelと違う");
            if(LabelCatalog.Items[tokyo[1]].Layout!="HeadingName" || LabelCatalog.Items[tokyo[1]].FixedColor!="白" || LabelCatalog.Items[tokyo[1]].UpperBandMm!=12f || LabelCatalog.Items[tokyo[1]].LowerBandMm!=12f)
                throw new Exception("よつ葉東京・白ラベルがExcelと違う");
            if(LabelCatalog.Items[tokyo[2]].Layout!="HeadingName" || LabelCatalog.Items[tokyo[2]].Format!="小ネーム（通行服）" || LabelCatalog.Items[tokyo[2]].FixedColor!="青" || LabelCatalog.Items[tokyo[2]].UpperBandMm!=12f || LabelCatalog.Items[tokyo[2]].LowerBandMm!=12f)
                throw new Exception("よつ葉東京・青ラベルがExcelと違う");
            SelectedCustomerIndex=73; formats.SelectedIndex=1; Application.DoEvents();
            if(!personSpaceNote.Visible || personSpaceNote.Text.IndexOf("全角スペース")<0) throw new Exception("氏名欄下の全角スペース注意書きが出ていない");
            if(personSpaceNote.Top<person.Bottom || (numberLabel.Visible && personSpaceNote.Bottom>numberLabel.Top)) throw new Exception("全角スペース注意書きの位置が氏名欄直下でない");
            person.Text="山田　太郎"; Application.DoEvents();
            if(Current().Person!="山田 太郎") throw new Exception("全角スペース注意書きと変換ロジックが矛盾");
            SelectedCustomerIndex=0; Application.DoEvents();
            if(!IsHandleCreated) CreateControl();
            // 弁釜・期生色帯が見本／社員番号／注意書きと重ならないこと
            SelectedCustomerIndex=72; Application.DoEvents();
            int cohortFormat=-1;
            for(int i=0;i<formats.Items.Count;i++) if(formats.Items[i].ToString().IndexOf("札幌・実習生")>=0) { cohortFormat=i; break; }
            if(cohortFormat<0) throw new Exception("弁釜の札幌・実習生形式が見つからない");
            formats.SelectedIndex=cohortFormat; Application.DoEvents();
            if(LabelCatalog.Items[active].Layout!="CohortName") throw new Exception("弁釜札幌実習生がCohortNameでない");
            int visibleLegends=0;
            foreach(Label card in colorLegend) if(card.Visible) {
                visibleLegends++;
                if(card.Top<LegendTop) throw new Exception("色帯がLegendTopより上にある "+card.Top);
                if(card.Bounds.IntersectsWith(sample.Bounds)) throw new Exception("色帯が見本と重なっている");
                if(card.Bounds.IntersectsWith(number.Bounds) || card.Bounds.IntersectsWith(numberLabel.Bounds)) throw new Exception("色帯が社員番号欄と重なっている");
                if(card.Top<sample.Bottom+2) throw new Exception("色帯が見本直下の余白を確保していない");
                if(printers.Visible && card.Bottom>printers.Top) throw new Exception("色帯がプリンター欄と重なっている");
            }
            if(visibleLegends<4) throw new Exception("弁釜期生の色帯が4枚出ていない "+visibleLegends);
            SelectedCustomerIndex=0; Application.DoEvents();
            if(customers.DropDownStyle!=ComboBoxStyle.DropDown)throw new Exception("顧客欄が入力不可（DropDownList）のまま");
            customers.Text="よつば";FillCustomerItems("よつば");
            if(customers.Items.Count<1)throw new Exception("メイン顧客欄の読み絞り込み失敗");
            foreach(object item in customers.Items)if(!UserSettings.Matches(item.ToString(),"よつば"))throw new Exception("メイン顧客欄に不一致が残った");
            string topMatch=customers.Items[0].ToString();
            int topCustomer=Array.IndexOf(LabelCatalog.Customers,topMatch);
            int narrowed=customers.Items.Count;customers.Text="よつ";FillCustomerItems("よつ");
            if(customers.Items.Count<narrowed)throw new Exception("メイン顧客欄の入力中絞り込み失敗");
            FillCustomerItems("");if(customers.Items.Count!=LabelCatalog.Customers.Length)throw new Exception("メイン顧客欄の全件復帰失敗");
            // 別顧客へ移してから Enter 先頭確定し、内容切替（active）まで進むこと
            SelectedCustomerIndex=1; Application.DoEvents();
            if(LabelCatalog.Items[active].Customer==topCustomer)throw new Exception("Enter検証の前提（別顧客）にできない");
            customers.Text="よつば";FillCustomerItems("よつば");
            CommitCustomerOnEnter();
            if(customers.Text!=topMatch)throw new Exception("Enterで先頭候補が選ばれない");
            if(SelectedCustomerIndex!=topCustomer)throw new Exception("Enter確定後の顧客インデックス不正");
            if(customers.Items.Count!=LabelCatalog.Customers.Length)throw new Exception("Enter確定後の全件復帰失敗");
            if(LabelCatalog.Items[active].Customer!=topCustomer)throw new Exception("Enter確定後に内容切替が走らない");
            Show();person.Text="山田 太郎";number.Text="Y1234";UpdatePreview();Application.DoEvents();
            // 会社名欄が PreferredHeight 未満／氏名欄より明らかに低い／ラベルに埋もれるのを検出
            if(!header.Visible) throw new Exception("よつ葉小ネームで会社名欄が出ていない");
            if(header.Height<header.PreferredHeight) throw new Exception("会社名欄の高さがPreferredHeight未満で文字がクリップする "+header.Height+"<"+header.PreferredHeight);
            if(person.Visible && header.Height+2<person.Height) throw new Exception("会社名欄が氏名欄より低い "+header.Height+"<"+person.Height);
            if(headerLabel.Visible && header.Top+2<headerLabel.Bottom) throw new Exception("会社名ラベルが入力欄に重なって潰れて見える");
            if(headerFont.Visible && header.Height+4<headerFont.Height) throw new Exception("会社名欄がptスピナーより低い "+header.Height+"<"+headerFont.Height);
            if(!personFont.Visible || !headerFont.Visible || !numberFont.Visible) throw new Exception("よつ葉のフォントサイズUIが表示されていない");
            if(roleFont.Visible) throw new Exception("よつ葉に役職ptが出ている");
            decimal beforePerson=personFont.Value;
            if(LabelCatalog.PickFont(18,false,99)!=18 || LabelCatalog.PickFont(18,true,5)!=18 || LabelCatalog.PickFont(18,true,40)!=40) throw new Exception("PickFontの安全な既定復帰が壊れている");
            using(Bitmap a=CreatePreview(new PrintOffset())) {
                changing=false; personFont.Value=beforePerson>=20?beforePerson-8:beforePerson+8;
                if(!Current().PersonFontSet) throw new Exception("氏名pt変更が明示フラグになっていない");
                using(Bitmap b=CreatePreview(new PrintOffset())) {
                    bool changed=false; for(int x=0;x<a.Width&&!changed;x+=3) for(int y=0;y<a.Height;y+=3) if(a.GetPixel(x,y)!=b.GetPixel(x,y)){changed=true;break;}
                    if(!changed) throw new Exception("氏名フォントサイズ変更がプレビューに反映されていない");
                }
            }
            // Umios 大ネーム（QR）: Legacy（札幌）と Layout=QR（十勝）で役職ptが出てプレビューに効くこと
            SelectedCustomerIndex=2; formats.SelectedIndex=0; Application.DoEvents();
            if(active!=3 || LabelCatalog.Items[active].Layout!="Legacy") throw new Exception("Umios札幌・大ネーム（QR）の形式が違う");
            if(!role.Visible || !roleFont.Visible || !LabelCatalog.Items[active].UsesRoleFont) throw new Exception("Umios札幌の役職ptが出ていない");
            role.Text="主任"; department.SelectedIndex=0; person.Text="山田 太郎"; number.Text="123456"; UpdatePreview();
            using(Bitmap a=CreatePreview(new PrintOffset())) {
                changing=false; decimal beforeRole=roleFont.Value; roleFont.Value=beforeRole>=30?beforeRole-12:beforeRole+12;
                if(!Current().RoleFontSet || Current().RoleFontPt!=(float)roleFont.Value) throw new Exception("役職pt変更が明示フラグになっていない");
                using(Bitmap b=CreatePreview(new PrintOffset())) {
                    bool changed=false; for(int x=0;x<a.Width&&!changed;x+=3) for(int y=0;y<a.Height;y+=3) if(a.GetPixel(x,y)!=b.GetPixel(x,y)){changed=true;break;}
                    if(!changed) throw new Exception("Umios札幌の役職pt変更がプレビューに反映されていない");
                }
            }
            SelectedCustomerIndex=12; formats.SelectedIndex=0; Application.DoEvents();
            if(LabelCatalog.Items[active].Layout!="QR" || !role.Visible || !roleFont.Visible) throw new Exception("Umios十勝の役職ptが出ていない");
            role.Text="主任"; department.SelectedIndex=0; person.Text="山田 太郎"; number.Text="123456"; UpdatePreview();
            using(Bitmap a=CreatePreview(new PrintOffset())) {
                changing=false; decimal beforeRole=roleFont.Value; roleFont.Value=beforeRole>=30?beforeRole-12:beforeRole+12;
                if(!Current().RoleFontSet) throw new Exception("十勝の役職pt変更が明示フラグになっていない");
                using(Bitmap b=CreatePreview(new PrintOffset())) {
                    bool changed=false; for(int x=0;x<a.Width&&!changed;x+=3) for(int y=0;y<a.Height;y+=3) if(a.GetPixel(x,y)!=b.GetPixel(x,y)){changed=true;break;}
                    if(!changed) throw new Exception("Umios十勝の役職pt変更がプレビューに反映されていない");
                }
            }
            SelectedCustomerIndex=0; formats.SelectedIndex=0; person.Text="山田 太郎"; number.Text="Y1234"; Application.DoEvents();
            // 入力のない固定ネームではpt UIを出さない
            for(int k=0;k<LabelCatalog.Items.Length;k++) {
                LabelSpec spec=LabelCatalog.Items[k];
                if(spec.Layout!="Fixed" && spec.Layout!="FixedSingle" && spec.Layout!="Blank") continue;
                kinds.SelectedIndex=k;
                if(headerFont.Visible || roleFont.Visible || personFont.Visible || numberFont.Visible) throw new Exception("入力なし形式にフォントUIが出ている "+k);
            }
            kinds.SelectedIndex=0; person.Text="山田 太郎"; number.Text="Y1234";
            using(Bitmap b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(Point.Empty,Size));b.Save(Path.Combine(folder,"main.png"));}
            foreach(int kind in new[]{0,1,2,3,4}) {
                kinds.SelectedIndex=kind;person.Text="山田 太郎";number.Text=kind<2?"Y1234":kind==2?"12345678":"123456";
                using(Bitmap a=CreatePreview(new PrintOffset()))using(Bitmap b=CreatePreview(new PrintOffset(1,1))){bool changed=false;for(int x=0;x<a.Width&&!changed;x+=3)for(int y=0;y<a.Height;y+=3)if(a.GetPixel(x,y)!=b.GetPixel(x,y)){changed=true;break;}if(!changed)throw new Exception("調整未反映");b.Save(Path.Combine(folder,"offset-"+kind+".png"));}
                using(Bitmap b=new Bitmap(500,500))using(Graphics g=Graphics.FromImage(b)){temporaryOffset=new PrintOffset(1,1);try{PrintPage(this,new PrintPageEventArgs(g,new Rectangle(0,0,400,400),new Rectangle(0,0,400,400),document.DefaultPageSettings));}finally{temporaryOffset=null;}}
            }
            kinds.SelectedIndex=1;person.Text="山田 太郎";number.Text="Y1234";
            using(OffsetDialog dlg=new OffsetDialog("プリンターごと・ラベル形式ごとに保存\r\nよつ葉乳業 ／ 大ネーム",new PrintOffset(.5m,-.3m),CreatePreview,delegate{throw new Exception("実印刷は禁止");},delegate{})){dlg.Show();Application.DoEvents();using(Bitmap b=new Bitmap(dlg.Width,dlg.Height)){dlg.DrawToBitmap(b,new Rectangle(Point.Empty,dlg.Size));b.Save(Path.Combine(folder,"adjustment.png"));}dlg.Close();}
            File.WriteAllText(Path.Combine(folder,"features-verification.txt"),"メイン顧客欄の入力絞り込み（読み・全半角）・Enter先頭候補確定と内容切替・設定再読込・お気に入り・最近の重複除去・プリンター形式別分離・5形式の調整表示・印刷描画を確認。実印刷なし。",Encoding.UTF8);Hide();
        }
        public void Verify(string folder)
        {
            Directory.CreateDirectory(folder);
            PaperSize small=new PaperSize("50 × 24",197,94), large=new PaperSize("50 × 80",197,315), alias=new PaperSize("別名",197,315);
            if(ChoosePaper(new PaperSize[]{alias,small,large},1)!=large || ChoosePaper(new PaperSize[]{large,small},0)!=small || ChoosePaper(new PaperSize[]{alias},3)!=null || ChoosePaper(new PaperSize[]{large},0)!=null || ChoosePaper(new PaperSize[]{new PaperSize("ラベル 50 x 24 mm",315,197)},0)==null) throw new Exception("用紙自動選択検証失敗");
            int indented=0; for(int i=0;i<LabelCatalog.Items.Length;i++) if(LabelCatalog.Items[i].IndentUpper) indented++;
            if(indented!=19) throw new Exception("左インデントの件数が違います "+indented);
            int yama=0; for(int i=0;i<LabelCatalog.Items.Length;i++) if(LabelCatalog.Items[i].SourceRow==202) yama=i;
            using(Bitmap ink=new Bitmap(400,250)) using(Graphics g=Graphics.FromImage(ink)) {
                g.Clear(Color.White); LabelCatalog.Draw(g,yama,new LabelData{Person="山田 太郎"});
                int left=400; for(int y=8;y<70;y++) for(int x=0;x<400;x++) if(ink.GetPixel(x,y).R<128 && x<left) left=x;
                if(left>25) throw new Exception("上段が左に寄っていません "+left);
            }
            ShowInTaskbar=false; Opacity=0; Show(); Application.DoEvents();
            for(int k=0;k<4;k++) {
                kinds.SelectedIndex=k; header.Text=k<2?"よつ葉乳業㈱":k==2?"1234":"製造部"; if(k==3) department.SelectedIndex=0; role.Text="主任"; number.Text=k<2?"Y1234":"00123";
                for(int len=0;len<=20;len++) { person.Text=new string('山',len); if(preview.Image==null || status.Text.StartsWith("処理できません")) throw new Exception("描画失敗 "+k+" / "+len); }
                person.Text="山田 太郎"; preview.Image.Save(Path.Combine(folder,"label-"+k+".png"));
                using(Bitmap b=new Bitmap(1200,1200)) using(Graphics g=Graphics.FromImage(b)) {
                    g.Clear(Color.White); PrintPage(this,new PrintPageEventArgs(g,new Rectangle(0,0,400,400),new Rectangle(0,0,400,400),document.DefaultPageSettings));
                    b.Save(Path.Combine(folder,"print-"+k+".png"));
                }
            }
            if(LabelRenderer.Validate(2,new LabelData {Header="1",Person="山田",Number="abc"})==null) throw new Exception("Code39入力検証失敗");
            kinds.SelectedIndex=0; if(number.Text!="Y1234") throw new Exception("入力保持失敗");
            kinds.SelectedIndex=3;
            for(int d=0;d<department.Items.Count;d++) { department.SelectedIndex=d; if(Current().Header!=department.Items[d].ToString() || status.Text.StartsWith("処理できません")) throw new Exception("部署選択失敗"); }
            kinds.SelectedIndex=0; kinds.SelectedIndex=3; if(department.SelectedIndex!=7) throw new Exception("部署保持失敗");
            for(int kind=0;kind<2;kind++) {
                kinds.SelectedIndex=kind; digits.SelectedIndex=1; number.Text="Y12345";
                if(number.MaxLength!=6 || LabelRenderer.Validate(kind,Current())!=null) throw new Exception("5桁設定失敗");
                digits.SelectedIndex=0;
                if(number.MaxLength!=5 || LabelRenderer.Validate(kind,Current())!=null || number.Text!="Y1234") throw new Exception("4桁切替検証失敗");
                digits.SelectedIndex=1; kinds.SelectedIndex=3; kinds.SelectedIndex=kind;
                if(digits.SelectedIndex!=1 || number.MaxLength!=6) throw new Exception("桁数保持失敗");
            }
            SelectedCustomerIndex=0; digits.SelectedIndex=1; number.Text="Y12345"; person.Text="山田 太郎";
            formats.SelectedIndex=0;
            if(active!=0 || number.Text!="Y12345" || person.Text!="山田 太郎") throw new Exception("顧客形式切替失敗");
            formats.SelectedIndex=1;
            if(active!=1 || number.Text!="Y12345" || digits.SelectedIndex!=1) throw new Exception("大小入力共有失敗");
            SelectedCustomerIndex=1;
            if(active!=2 || formats.Enabled) throw new Exception("だるま選択失敗");
            SelectedCustomerIndex=2;
            if(active!=3 || !formats.Enabled) throw new Exception("Umios選択失敗");
            SelectedCustomerIndex=0;
            if(active!=1 || number.Text!="Y12345") throw new Exception("顧客入力保持失敗");
            for(int k=4;k<LabelCatalog.Items.Length;k++) {
                LabelSpec spec=LabelCatalog.Items[k];
                SelectedCustomerIndex=spec.Customer;
                int index=Array.IndexOf(LabelCatalog.ForCustomer(spec.Customer),k); formats.SelectedIndex=index;
                if(active!=k) throw new Exception("追加形式の切替失敗 "+k);
                if(number.Visible!=spec.NeedsNumber || person.Visible!=spec.NeedsName) throw new Exception("入力欄の切替失敗 "+k);
                if(personFont.Visible!=spec.UsesPersonFont || numberFont.Visible!=spec.UsesNumberFont) throw new Exception("フォントサイズUIの切替失敗 "+k);
                if(headerFont.Visible && !spec.UsesHeaderFont) throw new Exception("ヘッダーptが不要形式で表示 "+k);
                if(roleFont.Visible && !spec.UsesRoleFont) throw new Exception("役職ptが不要形式で表示 "+k);
                if(role.Visible && spec.UsesRoleFont && !roleFont.Visible) throw new Exception("役職欄があるのに役職ptが出ていない "+k);
                number.Text="101"; if(LabelCatalog.ColorGroups(k).Length>0) colorDepartment.SelectedIndex=0; if(LabelCatalog.DepartmentNames(k)!=null) department.SelectedIndex=0;
                for(int n=0;n<=20;n+=k<37?1:10) { person.Text=new string('山',n); if(status.Text.StartsWith("処理できません")) throw new Exception("新規描画失敗 "+k+"/"+n); }
                person.Text=k==4?"山田":"山田 太郎";
                if(spec.Reading=="カタカナ") person.Text="ヤマダ タロウ";
                if(spec.Reading=="ひらがな") person.Text="やまだ たろう";
                if(spec.NumberLength>0) number.Text="1234567890".Substring(0,spec.NumberLength);
                if(spec.Layout=="PrefixName") number.Text="13";
                if(spec.Layout=="CohortName") role.Text="5";
                if(spec.Layout=="DeptNumberName") header.Text="製造";
                if(spec.Layout=="InitialName" || spec.Layout=="InitialMixed") role.Text="太";
                if(spec.Layout=="MixedName") { person.Text="ブイ ティ ツオン アン"; person.Select(person.Text.LastIndexOf("アン"),2); SetEmphasis(); }
                if(spec.Layout=="DeptOnly")header.Text="鮮魚";
                if(spec.Layout=="DeptFurigana"){role.Text="やまだ"; if(header.Visible) header.Text="看護部";}
                if(spec.Layout=="StarRoman"){header.Text="YAMADA";role.Text="3";}
                if(spec.Layout=="SplitCode")person.Text="M-23";
                if(spec.Layout=="FreeText")person.Text="M";
                if(LabelRenderer.Validate(k,Current())!=null && spec.Pending=="" && spec.Layout!="Blank") throw new Exception("追加形式入力検証失敗 "+k+": "+LabelRenderer.Validate(k,Current()));
                Size mm=LabelCatalog.Millimetres(k);
                using(Bitmap b=new Bitmap(mm.Width*6,mm.Height*6)) using(Graphics g=Graphics.FromImage(b)) {
                    g.Clear(Color.White); g.ScaleTransform(6*0.254f,6*0.254f); LabelRenderer.Draw(g,k,Current()); b.Save(Path.Combine(folder,"generic-"+k+".png"));
                }
                using(Bitmap b=new Bitmap(1200,1200)) using(Graphics g=Graphics.FromImage(b)) {
                    g.Clear(Color.White); PrintPage(this,new PrintPageEventArgs(g,new Rectangle(0,0,400,400),new Rectangle(0,0,400,400),document.DefaultPageSettings)); b.Save(Path.Combine(folder,"print-"+k+".png"));
                }
                using(Bitmap b=new Bitmap(Width,Height)) { DrawToBitmap(b,new Rectangle(0,0,Width,Height)); b.Save(Path.Combine(folder,"window-"+k+".png")); }
            }
            if(LabelCatalog.NameForPrint(LabelCatalog.Items[4],"山田　太郎")!="山田") throw new Exception("苗字の印字失敗");
            if(LabelRenderer.Validate(13,new LabelData {Number="101"})!=null || LabelRenderer.Validate(12,new LabelData {Person="山田 太郎",Number="ABC"})==null) throw new Exception("番号入力検証失敗");
            SelectedCustomerIndex=7; formats.SelectedIndex=0; person.Text="佐藤 花子";
            formats.SelectedIndex=2; number.Text="123";
            formats.SelectedIndex=3; if(number.Text!="123") throw new Exception("札幌の番号共有失敗");
            formats.SelectedIndex=0; if(person.Text!="佐藤 花子") throw new Exception("札幌の氏名共有失敗");
            foreach(int fixedKind in new int[]{20,23}) if(LabelRenderer.Validate(fixedKind,new LabelData())!=null) throw new Exception("資格ネームに入力が要求されています");
            if(LabelRenderer.Validate(21,new LabelData {Header="製造課(加工)",Person="山田 太郎",Number="1234567"})==null) throw new Exception("十勝QRの上限検証失敗");
            SelectedCustomerIndex=13; formats.SelectedIndex=0; department.SelectedIndex=1; person.Text="山田 太郎";
            formats.SelectedIndex=1; if(department.Text!="加工場" || Current().Header!="加工場") throw new Exception("名寄部署引継ぎ失敗");
            SelectedCustomerIndex=14; formats.SelectedIndex=0; colorDepartment.SelectedIndex=1; person.Text="山田　太郎";
            if(Current().Person!="山田 太郎" || LabelCatalog.ColorName(active,Current())!="青" || sampleColor.BackColor!=BackColor) throw new Exception("スペース・夜間・見本色検証失敗");
            formats.SelectedIndex=1; if(colorDepartment.Text!="夜間") throw new Exception("時間帯引継ぎ失敗");
            SelectedCustomerIndex=10; person.Text="山田 太郎";
            for(int c=0;c<LabelCatalog.ColorDepartments.Length;c++) { colorDepartment.SelectedIndex=c; if(LabelCatalog.ColorName(active,Current())!=LabelCatalog.DepartmentColors[c] || !sampleColor.Text.Contains(LabelCatalog.DepartmentColors[c]) || LabelRenderer.Validate(active,Current())!=null) throw new Exception("部署色検証失敗"); }
            colorDepartment.SelectedIndex=0;
            VerifyAdditions(folder);
            VerifyBulkAdditions(folder);
            using(Bitmap sheet=new Bitmap(1200,((LabelCatalog.Items.Length-4+1)/2)*310)) using(Graphics g=Graphics.FromImage(sheet))
            using(Font title=new Font("Yu Gothic UI",15,FontStyle.Bold)) using(Font subtitle=new Font("Yu Gothic UI",11)) {
                g.Clear(Color.FromArgb(239,242,246));
                for(int k=4;k<LabelCatalog.Items.Length;k++) {
                    int x=((k-4)%2)*600, y=((k-4)/2)*310;
                    LabelSpec spec=LabelCatalog.Items[k];
                    g.DrawString(LabelCatalog.Customers[spec.Customer],title,Brushes.Black,x+18,y+8);
                    g.DrawString(spec.Format,subtitle,Brushes.Black,x+18,y+40);
                    using(Image label=Image.FromFile(Path.Combine(folder,"generic-"+k+".png"))) {
                        float zoom=Math.Min(550f/label.Width,215f/label.Height);
                        float w=label.Width*zoom,h=label.Height*zoom;
                        g.DrawImage(label,x+25+(550-w)/2,y+75+(215-h)/2,w,h);
                        g.DrawRectangle(Pens.Gray,x+25+(550-w)/2,y+75+(215-h)/2,w,h);
                    }
                }
                sheet.Save(Path.Combine(folder,"new-labels.png"));
            }
            DrawToBitmapAndSave(folder);
            Hide();
            File.WriteAllText(Path.Combine(folder,"verification.txt"),"PASS: "+LabelCatalog.Items.Length+" layouts; representative empty/short/long names (existing layouts 0..20, additions 0/10/20); print rendering; "+LabelCatalog.Customers.Length+" customers; all color groups and legends; surname spacing; mixed-size selection at start/middle/end and 100-character names, input editing clears selection, format/customer preservation; fixed QC; number validation and sharing; Yotsuba 4/5 digits and truncation; paper selection. No print job sent.",Encoding.UTF8);
        }
        void VerifyAdditions(string folder) {
            if(LabelCatalog.NameForPrint(LabelCatalog.Items[29],"山田　太郎")!="山 田" || LabelCatalog.NameForPrint(LabelCatalog.Items[30],"佐々木 太郎")!="佐々木") throw new Exception("ホッカン苗字間隔検証失敗");
            if(LabelRenderer.Validate(35,new LabelData())!=null || LabelCatalog.ColorName(35,new LabelData())!="赤") throw new Exception("QC固定ネーム検証失敗");
            foreach(int k in new int[]{16,27,28,31,33,34}) {
                kinds.SelectedIndex=k;
                string[] groups=LabelCatalog.ColorGroups(k), colors=LabelCatalog.GroupColors(k);
                for(int i=0;i<groups.Length;i++) {
                    colorDepartment.SelectedIndex=i;
                    bool found=false;
                    foreach(Label card in colorLegend) if(card.Visible && card.Text.Contains("【"+groups[i]+"】") && card.BackColor==LabelCatalog.Swatch(colors[i])) found=true;
                    if(!found || largePlacement.Visible || LabelCatalog.ColorName(k,Current())!=colors[i] || sampleColor.BackColor!=BackColor) throw new Exception("色付き一覧検証失敗 "+k);
                }
            }
            kinds.SelectedIndex=36; person.Text="アン ティ アン ツオン";
            if(LabelRenderer.Validate(36,Current())==null) throw new Exception("未指定の実習生名を許可しています");
            foreach(int start in new int[]{0,6,9}) {
                person.Select(start,2); SetEmphasis();
                if(Current().EmphasisStart!=start || Current().EmphasisLength!=2 || LabelRenderer.Validate(36,Current())!=null) throw new Exception("拡大部分の選択失敗");
                preview.Image.Save(Path.Combine(folder,"mixed-position-"+start+".png"));
            }
            person.Select(3,0); SetEmphasis(); if(emphasisStart!=9) throw new Exception("未選択クリックで指定が壊れました");
            person.Text="ブイ　ティ　ツオン アン"; if(emphasisStart!=-1 || LabelRenderer.Validate(36,Current())==null) throw new Exception("氏名編集時の指定解除失敗");
            person.Select(person.Text.LastIndexOf("アン"),2); SetEmphasis();
            kinds.SelectedIndex=33; colorDepartment.SelectedIndex=1; kinds.SelectedIndex=35; kinds.SelectedIndex=36;
            if(!LabelCatalog.HasEmphasis(Current()) || Current().Person!="ブイ ティ ツオン アン") throw new Exception("実習生形式間の指定保持失敗");
            SelectedCustomerIndex=0; kinds.SelectedIndex=36;
            if(!LabelCatalog.HasEmphasis(Current())) throw new Exception("実習生顧客間の指定保持失敗");
            kinds.SelectedIndex=33; if(colorDepartment.Text!="派遣") throw new Exception("めむろ区分保持失敗");
            kinds.SelectedIndex=36;
            foreach(int len in new int[]{2,20,50,100}) {
                person.Text=new string('山',len); person.Select(len/2,1); SetEmphasis();
                if(LabelRenderer.Validate(36,Current())!=null || status.Text.StartsWith("処理できません")) throw new Exception("長い実習生名失敗");
                using(Bitmap b=new Bitmap(800,500)) using(Graphics g=Graphics.FromImage(b)) {
                    g.Clear(Color.White); g.ScaleTransform(2.54f,2.54f); LabelRenderer.Draw(g,36,Current());
                    for(int x=0;x<b.Width;x++) for(int y=0;y<b.Height;y++) if(b.GetPixel(x,y).R<128 && (x<5 || x>=795 || y<5 || y>=495)) throw new Exception("実習生名の枠外印字");
                    b.Save(Path.Combine(folder,"mixed-length-"+len+".png"));
                }
            }
            person.Text="ブイ ティ ツオン アン"; person.Select(person.Text.LastIndexOf("アン"),2); SetEmphasis();
            using(Bitmap b=new Bitmap(Width,Height)) { DrawToBitmap(b,new Rectangle(0,0,Width,Height)); b.Save(Path.Combine(folder,"window-memuro.png")); }
        }
        void DrawToBitmapAndSave(string folder) { using(Bitmap b=new Bitmap(Width,Height)) { DrawToBitmap(b,new Rectangle(0,0,Width,Height)); b.Save(Path.Combine(folder,"window.png")); } }
        int KindForRow(int row) { for(int k=0;k<LabelCatalog.Items.Length;k++) if(LabelCatalog.Items[k].PreviousSourceRow==row) return k; throw new Exception("旧登録のExcel行が見つかりません "+row); }
        public void VerifyLayoutUpdates(string folder) {
            Directory.CreateDirectory(folder); ShowInTaskbar=false; Opacity=0; Show(); Application.DoEvents();
            // Shown 後も起動時の貼り付け先帯が残ること
            if(active!=0 || !smallPlacement.Visible || smallPlacement.Text.IndexOf("貼り付け先：")!=0 || smallPlacement.Height<=0)throw new Exception("表示直後に貼り付け先帯が出ない");
            if(!garmentSummary.Visible || garmentSummary.Height<=0)throw new Exception("表示直後に商品対応帯が出ない");
            if(!largePlacement.Visible || largePlacement.Text.IndexOf("【選択中】")<0)throw new Exception("表示直後に【選択中】行が出ない");
            int initialHeight=ClientSize.Height;
            foreach(int row in new int[]{45,74,90,146,152,153,99,20,144,129}) {
                kinds.SelectedIndex=KindForRow(row); LabelSpec spec=LabelCatalog.Items[active];
                if(department.Items.Count>0) department.SelectedIndex=0;
                if(colorDepartment.Items.Count>0) colorDepartment.SelectedIndex=0;
                person.Text=spec.Reading=="カタカナ"?"ヤマダ タロウ":"山田 太郎";
                if(spec.NumberLength>0) number.Text="1234567890".Substring(0,spec.NumberLength); else number.Text="60";
                if(spec.Layout=="CohortName") role.Text="5";
                if(garmentSummary.Visible && garmentSummary.Bottom>smallPlacement.Top) throw new Exception("商品対応帯が重なっています "+row);
                if(smallPlacement.Bottom>largePlacement.Top || largePlacement.Bottom>guide.Top || status.Bottom>ClientSize.Height) throw new Exception("ガイド配置が重なっています "+row);
                File.AppendAllText(Path.Combine(folder,"guide-sizes.txt"),row+": "+smallPlacement.Height+" / "+smallPlacement.Text.Length+" characters\r\n");
                using(Bitmap b=new Bitmap(Width,Height)) { DrawToBitmap(b,new Rectangle(0,0,Width,Height)); b.Save(Path.Combine(folder,"window-row-"+row+".png")); }
            }
            kinds.SelectedIndex=0;
            if(ClientSize.Height!=initialHeight) throw new Exception("ガイド切替後に画面高さが戻りません");
            Hide(); File.WriteAllText(Path.Combine(folder,"verification.txt"),"PASS: long placement wraps without overlap; color and cohort guides; fixed-font notice; changing formats restores original window height. No print job sent.",Encoding.UTF8);
        }
        void VerifyBulkAdditions(string folder) {
            if(LabelCatalog.Items.Length!=227 || LabelCatalog.Customers.Length!=131) throw new Exception("登録件数不一致");
            foreach(int row in new int[]{45,46,74,108+1,132+1}) {
                kinds.SelectedIndex=KindForRow(row); department.SelectedIndex=0;
                if(LabelCatalog.ColorGroups(active).Length>0) { colorDepartment.SelectedIndex=1; if(colorDepartment.Bounds.IntersectsWith(department.Bounds)) throw new Exception("部署と色の入力欄が重なっています"); }
                person.Text="山田 太郎"; if(LabelRenderer.Validate(active,Current())!=null) throw new Exception("部署と色の入力検証失敗");
            }
            kinds.SelectedIndex=KindForRow(19); colorDepartment.SelectedIndex=0; kinds.SelectedIndex=KindForRow(20); colorDepartment.SelectedIndex=1; number.Text="60";
            if(LabelCatalog.ColorName(active,Current())!="赤") throw new Exception("Mサイズ色失敗");
            kinds.SelectedIndex=KindForRow(19); if(colorDepartment.Text!="社員") throw new Exception("別の区分を持つ形式の入力保持失敗");
            kinds.SelectedIndex=KindForRow(20); if(colorDepartment.Text!="M") throw new Exception("サイズ保持失敗");
            colorDepartment.SelectedIndex=10; header.Text="10L"; if(LabelRenderer.Validate(active,Current())!=null || LabelCatalog.ColorName(active,Current())!="白") throw new Exception("その他サイズ検証失敗");
            kinds.SelectedIndex=KindForRow(27); number.Text="0123"; if(LabelRenderer.Validate(active,Current())!=null) throw new Exception("4桁の先頭ゼロ失敗"); number.Text="123"; if(LabelRenderer.Validate(active,Current())==null) throw new Exception("番号桁数検証失敗");
            kinds.SelectedIndex=KindForRow(146); person.Text="テキ シホウ"; number.Text="0123456";
            string[] cycle={"赤","緑","青","白"};
            for(int n=1;n<=12;n++) { role.Text=n.ToString(); if(LabelCatalog.ColorName(active,Current())!=cycle[(n-1)%4] || LabelRenderer.Validate(active,Current())!=null) throw new Exception("期生色ループ失敗"); }
            foreach(string bad in new string[]{"0","-1","abc","1000"}) { role.Text=bad; if(LabelRenderer.Validate(active,Current())==null) throw new Exception("期生入力検証失敗"); }
            if(LabelCatalog.Circled(13)!="⑬" || LabelCatalog.Circled(21)!="㉑" || LabelCatalog.Circled(50)!="㊿" || LabelCatalog.Circled(51)!="(51)") throw new Exception("丸数字変換失敗");
            kinds.SelectedIndex=KindForRow(73); colorDepartment.SelectedIndex=0; person.Text=new string('山',20); if(LabelRenderer.Validate(active,Current())==null) throw new Exception("固定文字サイズのはみ出し検証失敗");
            kinds.SelectedIndex=KindForRow(71); person.Text="山田 太郎"; role.Text="主任"; preview.Image.Save(Path.Combine(folder,"role-present.png")); role.Text=""; preview.Image.Save(Path.Combine(folder,"role-absent.png"));
            kinds.SelectedIndex=KindForRow(121); colorDepartment.SelectedIndex=0;person.Text="山田 太郎";if(LabelRenderer.Validate(active,Current())!=null || LabelCatalog.ColorName(active,Current())!="青")throw new Exception("山川牧場の記載済み男性用の検証失敗");
            kinds.SelectedIndex=KindForRow(129); if(LabelRenderer.Validate(active,Current())==null || person.Visible || number.Visible) throw new Exception("無地ガイド検証失敗");
            kinds.SelectedIndex=KindForRow(154); colorDepartment.SelectedIndex=0; person.Text="ヤマダ"; if(LabelRenderer.Validate(active,Current())==null) throw new Exception("上下氏名の区切り検証失敗"); person.Text="ヤマダ タロウ";
            using(Bitmap b=new Bitmap(Width,Height)) { DrawToBitmap(b,new Rectangle(0,0,Width,Height)); b.Save(Path.Combine(folder,"window-bulk.png")); }
            File.WriteAllText(Path.Combine(folder,"bulk-verification.txt"),"PASS: 131 customers / 227 formats; independent department and color controls; distinct color rules retain selections; numeric lengths including leading zero; cohort colors 1..12; invalid cohorts; circled numbers; fixed font overflow; optional role; blank label; specified male color; two-row surname/given name.",Encoding.UTF8);
        }
    }
    static class Program
    {
        [STAThread] static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try {
                string settings=args.Length==2 && args[0].StartsWith("--verify")?Path.Combine(Path.GetFullPath(args[1]),"test-settings.xml"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"UnifiedNameLabels","settings.xml");
                if(args.Length==2 && args[0]=="--verify-generic") {
                    using(GenericLabelForm generic=new GenericLabelForm(new UserSettings(settings))) generic.Verify(args[1]);
                    return 0;
                }
                using(MainForm form=new MainForm(settings)) {
                    if(args.Length==2 && args[0]=="--verify") form.Verify(args[1]);
                    else if(args.Length==2 && args[0]=="--verify-layout") form.VerifyLayoutUpdates(args[1]);
                    else if(args.Length==2 && args[0]=="--verify-features") form.VerifyFeatures(args[1]);
                    else if(args.Length==2 && args[0]=="--verify-latest") form.VerifyLatest(args[1]);
                    else if(args.Length==2 && args[0]=="--verify-retained") form.VerifyRetainedInputs(args[1]);
                    else Application.Run(form);
                }
                return 0;
            }
            catch(Exception ex) { if(args.Length>1) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1],"error.txt"),ex.ToString()); } else MessageBox.Show(ex.Message,"起動エラー"); return 1; }
        }
    }
}

