using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace UnifiedLabels
{
    /// <summary>タスク一覧・一括発行タブ。汎用ラベル画面とは別。</summary>
    public sealed class BatchTaskForm : Form
    {
        readonly BatchTaskStore store;
        readonly Func<BatchTask,BatchTask> enrichFromPrinter;
        readonly IBatchJobSender sender;
        readonly TabControl tabs=new TabControl();
        readonly DataGridView grid=new DataGridView();
        readonly Label status=new Label();
        readonly TextBox issueLog=new TextBox();

        public BatchTaskForm(BatchTaskStore store, Func<BatchTask,BatchTask> enrichFromPrinter, IBatchJobSender sender)
        {
            this.store=store;
            this.enrichFromPrinter=enrichFromPrinter;
            this.sender=sender??new PrintDocumentBatchJobSender();
            Text="一括発行・タスク";
            Width=980; Height=640;
            StartPosition=FormStartPosition.CenterParent;
            Font=new Font("Yu Gothic UI",9f);

            tabs.Dock=DockStyle.Fill;
            TabPage listPage=new TabPage("タスク一覧");
            TabPage issuePage=new TabPage("一括発行");
            tabs.TabPages.Add(listPage);
            tabs.TabPages.Add(issuePage);
            Controls.Add(tabs);

            BuildListPage(listPage);
            BuildIssuePage(issuePage);
            RefreshGrid();
        }

        void BuildListPage(TabPage page)
        {
            Panel top=new Panel { Dock=DockStyle.Top, Height=44 };
            Button addPaste=Btn("表を貼付登録",8); addPaste.Click+=delegate { ImportPaste(); };
            Button addCsv=Btn("Excel/CSV取込",130); addCsv.Click+=delegate { ImportCsv(); };
            Button tmpl=Btn("テンプレート保存",260); tmpl.Click+=delegate { SaveTemplate(); };
            Button edit=Btn("編集",400); edit.Click+=delegate { EditSelected(); };
            Button dup=Btn("複製",480); dup.Click+=delegate { DuplicateSelected(); };
            Button del=Btn("削除",560); del.Click+=delegate { DeleteSelected(); };
            Button hist=Btn("印刷履歴",640); hist.Click+=delegate { ShowHistory(); };
            Button reprint=Btn("再印刷",740); reprint.Click+=delegate { ReprintSelected(); };
            Button confirm=Btn("完了確認",820); confirm.Click+=delegate { ConfirmSelected(); };
            top.Controls.AddRange(new Control[]{addPaste,addCsv,tmpl,edit,dup,del,hist,reprint,confirm});
            page.Controls.Add(top);

            status.Dock=DockStyle.Bottom; status.Height=28;
            status.TextAlign=ContentAlignment.MiddleLeft;
            page.Controls.Add(status);

            grid.Dock=DockStyle.Fill;
            grid.AllowUserToAddRows=false;
            grid.AllowUserToDeleteRows=false;
            grid.ReadOnly=true;
            grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect=true;
            grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
            grid.RowHeadersVisible=false;
            page.Controls.Add(grid);
            page.Controls.Add(top);
        }

        void BuildIssuePage(TabPage page)
        {
            Panel top=new Panel { Dock=DockStyle.Top, Height=80 };
            Label guide=new Label {
                Left=8, Top=8, Width=920, Height=36,
                Text="未発行タスクを色別にまとめて発行します。同色は顧客・大／小が違っても連続送信し、色が変わるときだけ用紙確認で一時停止します。Print送信成功だけでは完了にしません。"
            };
            Button run=Btn("一括発行を開始",8); run.Top=48; run.Width=160;
            run.Click+=delegate { RunIssue(false); };
            Button refresh=Btn("一覧を更新",180); refresh.Top=48;
            refresh.Click+=delegate { RefreshGrid(); tabs.SelectedIndex=0; };
            top.Controls.Add(guide); top.Controls.Add(run); top.Controls.Add(refresh);
            page.Controls.Add(top);
            issueLog.Dock=DockStyle.Fill;
            issueLog.Multiline=true;
            issueLog.ScrollBars=ScrollBars.Vertical;
            issueLog.ReadOnly=true;
            issueLog.Font=new Font("Consolas",9f);
            page.Controls.Add(issueLog);
            issueLog.BringToFront();
        }

        static Button Btn(string text,int left)
        {
            return new Button { Text=text, Left=left, Top=8, Width=110, Height=28 };
        }

        void RefreshGrid()
        {
            grid.Columns.Clear();
            grid.Rows.Clear();
            grid.Columns.Add("status","状態");
            grid.Columns.Add("customer","顧客");
            grid.Columns.Add("format","形式");
            grid.Columns.Add("size","大/小");
            grid.Columns.Add("color","色");
            grid.Columns.Add("person","氏名");
            grid.Columns.Add("number","番号");
            grid.Columns.Add("group","区分");
            grid.Columns.Add("copies","部数");
            grid.Columns.Add("updated","更新");
            grid.Columns.Add("id","Id");
            grid.Columns["id"].Visible=false;
            foreach(BatchTask t in store.Tasks) {
                int i=grid.Rows.Add(
                    BatchValidation.StatusText(t.Status),
                    t.CustomerName, t.FormatName, t.Small?"小":"大",
                    t.ColorGroup, t.Data.Person, t.Data.Number, t.Data.ColorDepartment,
                    t.Copies.ToString(), t.UpdatedAt.ToString("yyyy-MM-dd HH:mm"), t.Id);
                grid.Rows[i].Tag=t;
            }
            status.Text="タスク "+store.Tasks.Count+" 件　保存: "+BatchTaskStore.DefaultPath();
        }

        List<BatchTask> SelectedTasks()
        {
            List<BatchTask> list=new List<BatchTask>();
            foreach(DataGridViewRow row in grid.SelectedRows) {
                string id=Convert.ToString(row.Cells["id"].Value);
                BatchTask t=store.Find(id);
                if(t!=null) list.Add(t);
            }
            return list;
        }

        void ImportPaste()
        {
            string text=Clipboard.ContainsText()?Clipboard.GetText():"";
            if(string.IsNullOrWhiteSpace(text)) {
                MessageBox.Show(this,"クリップボードに表データがありません。Excelからコピーしてください。","貼付登録");
                return;
            }
            BatchImportResult result=BatchImport.FromClipboardText(text,enrichFromPrinter);
            ApplyImport(result);
        }

        void ImportCsv()
        {
            using(OpenFileDialog dlg=new OpenFileDialog()) {
                dlg.Filter="CSV / テキスト (*.csv;*.txt)|*.csv;*.txt|すべてのファイル|*.*";
                dlg.Title="一括発行用 CSV（Excelテンプレート）を選択";
                if(dlg.ShowDialog(this)!=DialogResult.OK) return;
                BatchImportResult result=BatchImport.FromCsvFile(dlg.FileName,enrichFromPrinter);
                ApplyImport(result);
            }
        }

        void ApplyImport(BatchImportResult result)
        {
            if(result.HasErrors) {
                MessageBox.Show(this,string.Join("\r\n",result.Errors.ToArray())+"\r\n\r\n不正行があるため1件も登録していません。","取込エラー",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
            if(result.Tasks.Count==0) {
                MessageBox.Show(this,"登録できる行がありません。","取込");
                return;
            }
            foreach(BatchTask t in result.Tasks) store.Add(t);
            RefreshGrid();
            MessageBox.Show(this,result.Tasks.Count+" 件を登録しました。","取込完了");
        }

        void SaveTemplate()
        {
            using(SaveFileDialog dlg=new SaveFileDialog()) {
                dlg.Filter="CSV (*.csv)|*.csv";
                dlg.FileName="一括発行テンプレート.csv";
                if(dlg.ShowDialog(this)!=DialogResult.OK) return;
                BatchImport.WriteTemplate(dlg.FileName);
                MessageBox.Show(this,"Excel で開いて記入し、CSVのまま保存して取込むか、表をコピーして「表を貼付登録」してください。\r\n列: "+string.Join(" / ",BatchImport.TemplateHeaders),"テンプレート");
            }
        }

        void EditSelected()
        {
            List<BatchTask> sel=SelectedTasks();
            if(sel.Count!=1) { MessageBox.Show(this,"編集するタスクを1件選んでください。"); return; }
            using(BatchTaskEditDialog dlg=new BatchTaskEditDialog(sel[0])) {
                if(dlg.ShowDialog(this)!=DialogResult.OK) return;
                string err=BatchValidation.ValidateForTask(sel[0].Kind,sel[0].Data.ToLabelData());
                if(err!=null) { MessageBox.Show(this,err,"入力エラー"); return; }
                sel[0].ColorGroup=BatchValidation.IssueColorGroup(sel[0].Kind,sel[0].Data.ToLabelData());
                sel[0].UpdatedAt=DateTime.Now;
                store.Save();
                RefreshGrid();
            }
        }

        void DuplicateSelected()
        {
            List<BatchTask> sel=SelectedTasks();
            if(sel.Count==0) return;
            foreach(BatchTask t in sel) {
                BatchTask c=t.CloneTask(true);
                c.Status=BatchTaskStatus.Pending;
                c.LastError="";
                store.Add(c);
            }
            RefreshGrid();
        }

        void DeleteSelected()
        {
            List<BatchTask> sel=SelectedTasks();
            if(sel.Count==0) return;
            if(MessageBox.Show(this,sel.Count+" 件を削除しますか？","削除",MessageBoxButtons.OKCancel)!=DialogResult.OK) return;
            foreach(BatchTask t in sel) store.Remove(t.Id);
            RefreshGrid();
        }

        void ShowHistory()
        {
            List<BatchTask> sel=SelectedTasks();
            if(sel.Count!=1) { MessageBox.Show(this,"履歴を見るタスクを1件選んでください。"); return; }
            BatchTask t=sel[0];
            StringBuilder sb=new StringBuilder();
            sb.AppendLine(t.CustomerName+" / "+t.FormatName+" / "+t.Data.Person);
            sb.AppendLine("現在状態: "+BatchValidation.StatusText(t.Status));
            sb.AppendLine("内容: 区分="+t.Data.ColorDepartment+" 部署="+t.Data.Header+" 番号="+t.Data.Number+" 色="+t.ColorGroup);
            sb.AppendLine("--- 履歴 ---");
            if(t.History.Count==0) sb.AppendLine("（なし）");
            foreach(BatchPrintHistoryEntry h in t.History) {
                sb.AppendLine(h.At.ToString("yyyy-MM-dd HH:mm:ss")+" ["+BatchValidation.StatusText(h.StatusAfter)+"] "
                    +(h.IsReprint?"再印刷 ":"")+h.Note+" 色="+h.ColorGroup
                    +" 氏名="+h.Content.Person+" 番号="+h.Content.Number);
            }
            MessageBox.Show(this,sb.ToString(),"印刷履歴");
        }

        void ConfirmSelected()
        {
            List<BatchTask> sel=SelectedTasks();
            int n=0;
            foreach(BatchTask t in sel) {
                if(t.Status==BatchTaskStatus.SentAwaitingConfirm) {
                    t.Status=BatchTaskStatus.CompletedConfirmed;
                    t.UpdatedAt=DateTime.Now;
                    t.History.Add(new BatchPrintHistoryEntry {
                        Note="一覧から作業者確認", StatusAfter=BatchTaskStatus.CompletedConfirmed,
                        ColorGroup=t.ColorGroup, Content=t.Data.Clone()
                    });
                    n++;
                }
            }
            if(n>0) store.Save();
            RefreshGrid();
            MessageBox.Show(this,n+" 件を作業者確認済み完了にしました。");
        }

        void ReprintSelected()
        {
            List<BatchTask> sel=SelectedTasks();
            if(sel.Count==0) { MessageBox.Show(this,"再印刷するタスクを選んでください。"); return; }
            if(MessageBox.Show(this,"選択 "+sel.Count+" 件を再印刷します。元の履歴は残ります。続行しますか？","再印刷",MessageBoxButtons.OKCancel)!=DialogResult.OK) return;
            foreach(BatchTask t in sel) {
                t.Status=BatchTaskStatus.Pending;
                t.LastError="";
                t.UpdatedAt=DateTime.Now;
            }
            store.Save();
            RunIssueCore(sel,true);
            RefreshGrid();
        }

        void RunIssue(bool reprint)
        {
            List<BatchTask> targets=new List<BatchTask>();
            foreach(BatchTask t in store.Tasks)
                if(t.Status==BatchTaskStatus.Pending || t.Status==BatchTaskStatus.Failed) targets.Add(t);
            if(targets.Count==0) { MessageBox.Show(this,"発行対象の未発行／要確認タスクがありません。"); return; }
            RunIssueCore(targets,reprint);
            RefreshGrid();
        }

        void RunIssueCore(IList<BatchTask> targets, bool reprint)
        {
            issueLog.Text="";
            BatchIssueRunner runner=new BatchIssueRunner { Sender=sender };
            runner.Report=delegate(string m) {
                issueLog.AppendText(m+"\r\n");
                Application.DoEvents();
            };
            runner.Confirm=delegate(string title,string message) {
                return MessageBox.Show(this,message,title,MessageBoxButtons.OKCancel,MessageBoxIcon.Information)==DialogResult.OK;
            };
            tabs.SelectedIndex=1;
            runner.Run(targets,store,reprint);
            string summary="送信ジョブ数: "+runner.JobsSent
                +(runner.Cancelled?"\r\n中断: "+runner.StopReason:"\r\n完了")
                +(string.IsNullOrEmpty(runner.StopReason)?"":("\r\n"+runner.StopReason));
            issueLog.AppendText(summary+"\r\n");
            MessageBox.Show(this,summary,"一括発行");
        }
    }

    sealed class BatchTaskEditDialog : Form
    {
        readonly BatchTask task;
        readonly TextBox header=new TextBox();
        readonly TextBox role=new TextBox();
        readonly TextBox person=new TextBox();
        readonly TextBox number=new TextBox();
        readonly ComboBox color=new ComboBox();
        readonly NumericUpDown copies=new NumericUpDown();

        public BatchTaskEditDialog(BatchTask task)
        {
            this.task=task;
            Text="タスク編集 — "+task.CustomerName+" / "+task.FormatName;
            Width=480; Height=320; FormBorderStyle=FormBorderStyle.FixedDialog;
            StartPosition=FormStartPosition.CenterParent; MaximizeBox=false; MinimizeBox=false;
            Font=new Font("Yu Gothic UI",9f);
            int y=16;
            AddLabel("部署/見出し",y); header.SetBounds(140,y,280,24); header.Text=task.Data.Header; Controls.Add(header); y+=32;
            AddLabel("役職/期生など",y); role.SetBounds(140,y,280,24); role.Text=task.Data.Role; Controls.Add(role); y+=32;
            AddLabel("氏名",y); person.SetBounds(140,y,280,24); person.Text=task.Data.Person; Controls.Add(person); y+=32;
            AddLabel("番号",y); number.SetBounds(140,y,280,24); number.Text=task.Data.Number; Controls.Add(number); y+=32;
            AddLabel("区分（色確認）",y); color.SetBounds(140,y,280,24); color.DropDownStyle=ComboBoxStyle.DropDownList;
            string[] groups=LabelCatalog.ColorGroups(task.Kind);
            if(groups.Length>0) { color.Items.AddRange(groups); color.SelectedItem=task.Data.ColorDepartment; if(color.SelectedIndex<0 && groups.Length>0) color.SelectedIndex=-1; }
            else { color.Items.Add("(色選択なし)"); color.SelectedIndex=0; color.Enabled=false; }
            Controls.Add(color); y+=32;
            AddLabel("部数",y); copies.SetBounds(140,y,80,24); copies.Minimum=1; copies.Maximum=30; copies.Value=Math.Max(1,Math.Min(30,(int)task.Copies)); Controls.Add(copies); y+=40;
            Button ok=new Button { Text="保存", Left=200, Top=y, Width=100, Height=30, DialogResult=DialogResult.None };
            Button cancel=new Button { Text="キャンセル", Left=310, Top=y, Width=100, Height=30, DialogResult=DialogResult.Cancel };
            ok.Click+=delegate {
                task.Data.Header=header.Text.Replace('　',' ');
                task.Data.Role=role.Text.Replace('　',' ');
                task.Data.Person=person.Text.Replace('　',' ');
                task.Data.Number=number.Text.Replace('　',' ');
                if(color.Enabled) task.Data.ColorDepartment=color.SelectedIndex>=0?color.Text:"";
                task.Copies=(short)copies.Value;
                string err=BatchValidation.ValidateForTask(task.Kind,task.Data.ToLabelData());
                if(err!=null) { MessageBox.Show(this,err,"入力エラー"); return; }
                DialogResult=DialogResult.OK;
            };
            Controls.Add(ok); Controls.Add(cancel); CancelButton=cancel;
        }
        void AddLabel(string text,int y) { Controls.Add(new Label { Text=text, Left=16, Top=y+4, Width=120 }); }
    }
}
