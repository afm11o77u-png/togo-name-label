using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace UnifiedLabels
{
    /// <summary>タスク／履歴の状態。Print() 成功だけでは完了にしない。</summary>
    public enum BatchTaskStatus
    {
        Pending = 0,           // 未発行
        Sending = 1,           // 送信中
        SentAwaitingConfirm = 2, // 送信済み・完了確認待ち
        CompletedConfirmed = 3,  // 作業者確認済み完了
        Failed = 4             // 失敗／要確認
    }

    public sealed class LabelDataSnapshot
    {
        public int Digits = 4;
        public int EmphasisStart = -1, EmphasisLength;
        public string ColorDepartment = "";
        public string Header = "", Role = "", Person = "", Number = "", Title = "";
        public float HeaderFontPt, RoleFontPt, PersonFontPt, NumberFontPt;
        public bool HeaderFontSet, RoleFontSet, PersonFontSet, NumberFontSet;

        public static LabelDataSnapshot From(LabelData d)
        {
            if(d==null) return new LabelDataSnapshot();
            return new LabelDataSnapshot {
                Digits=d.Digits, EmphasisStart=d.EmphasisStart, EmphasisLength=d.EmphasisLength,
                ColorDepartment=d.ColorDepartment??"", Header=d.Header??"", Role=d.Role??"",
                Person=d.Person??"", Number=d.Number??"", Title=d.Title??"",
                HeaderFontPt=d.HeaderFontPt, RoleFontPt=d.RoleFontPt, PersonFontPt=d.PersonFontPt, NumberFontPt=d.NumberFontPt,
                HeaderFontSet=d.HeaderFontSet, RoleFontSet=d.RoleFontSet, PersonFontSet=d.PersonFontSet, NumberFontSet=d.NumberFontSet
            };
        }
        public LabelData ToLabelData()
        {
            return new LabelData {
                Digits=Digits, EmphasisStart=EmphasisStart, EmphasisLength=EmphasisLength,
                ColorDepartment=ColorDepartment??"", Header=Header??"", Role=Role??"",
                Person=Person??"", Number=Number??"", Title=Title??"",
                HeaderFontPt=HeaderFontPt, RoleFontPt=RoleFontPt, PersonFontPt=PersonFontPt, NumberFontPt=NumberFontPt,
                HeaderFontSet=HeaderFontSet, RoleFontSet=RoleFontSet, PersonFontSet=PersonFontSet, NumberFontSet=NumberFontSet
            };
        }
        public LabelDataSnapshot Clone() { return From(ToLabelData()); }
    }

    public sealed class BatchPrintHistoryEntry
    {
        public string Id = Guid.NewGuid().ToString("N");
        public DateTime At = DateTime.Now;
        public BatchTaskStatus StatusAfter = BatchTaskStatus.SentAwaitingConfirm;
        public string Note = "";
        public string ColorGroup = "";
        public bool IsReprint;
        public LabelDataSnapshot Content = new LabelDataSnapshot();
    }

    /// <summary>登録時点の顧客・形式・印字内容・プリンタ設定を保持（後の画面変更の影響を受けない）。</summary>
    public sealed class BatchTask
    {
        public string Id = Guid.NewGuid().ToString("N");
        public DateTime CreatedAt = DateTime.Now;
        public DateTime UpdatedAt = DateTime.Now;
        public BatchTaskStatus Status = BatchTaskStatus.Pending;
        public int Kind;
        public string CustomerName = "";
        public string FormatName = "";
        public bool Small;
        public LabelDataSnapshot Data = new LabelDataSnapshot();
        public string PrinterName = "";
        public string PaperName = "";
        public int PaperWidth, PaperHeight;
        public bool Landscape;
        public decimal OffsetX, OffsetY;
        public short Copies = 1;
        public string ColorGroup = "";
        public string LastError = "";
        public readonly List<BatchPrintHistoryEntry> History = new List<BatchPrintHistoryEntry>();

        public BatchTask CloneTask(bool asNew)
        {
            BatchTask t=new BatchTask {
                Id=asNew?Guid.NewGuid().ToString("N"):Id,
                CreatedAt=asNew?DateTime.Now:CreatedAt,
                UpdatedAt=DateTime.Now,
                Status=asNew?BatchTaskStatus.Pending:Status,
                Kind=Kind, CustomerName=CustomerName, FormatName=FormatName, Small=Small,
                Data=Data.Clone(), PrinterName=PrinterName, PaperName=PaperName,
                PaperWidth=PaperWidth, PaperHeight=PaperHeight, Landscape=Landscape,
                OffsetX=OffsetX, OffsetY=OffsetY, Copies=Copies, ColorGroup=ColorGroup,
                LastError=asNew?"":LastError
            };
            if(!asNew) foreach(BatchPrintHistoryEntry h in History) {
                t.History.Add(new BatchPrintHistoryEntry {
                    Id=h.Id, At=h.At, StatusAfter=h.StatusAfter, Note=h.Note,
                    ColorGroup=h.ColorGroup, IsReprint=h.IsReprint, Content=h.Content.Clone()
                });
            }
            return t;
        }
    }

    public static class BatchValidation
    {
        /// <summary>通常印刷用 Validate に加え、色確認用 Groups がある形式は区分選択を必須にする。</summary>
        public static string ValidateForTask(int kind, LabelData d)
        {
            if(kind<0 || kind>=LabelCatalog.Items.Length) return "ラベル形式が不正です。";
            string error=LabelRenderer.Validate(kind,d);
            if(error!=null) return error;
            LabelSpec s=LabelCatalog.Items[kind];
            string[] groups=LabelCatalog.ColorGroups(kind);
            if(groups.Length>0 && !LabelCatalog.ColorGroupAffectsPrint(kind)) {
                if(Array.IndexOf(groups,LabelCatalog.SelectedGroup(kind,d))<0)
                    return (s.ColorCaption==""?"区分":s.ColorCaption)+"を選択してラベルの色を確認してください。";
            }
            return null;
        }

        /// <summary>区分コンボが無く、カタログ色が先方指定のとき（タスク用に手入力が必要）。</summary>
        public static bool NeedsClientColorInput(int kind, LabelData d)
        {
            if(kind<0 || kind>=LabelCatalog.Items.Length) return false;
            if(LabelCatalog.ColorGroups(kind).Length>0) return false;
            string fixedColor=LabelCatalog.Items[kind].FixedColor??"";
            if(fixedColor.IndexOf("先方から指定",StringComparison.Ordinal)>=0) return true;
            string color=LabelCatalog.ColorName(kind,d??new LabelData());
            return !string.IsNullOrEmpty(color) && color.IndexOf("先方から指定",StringComparison.Ordinal)>=0;
        }

        /// <summary>発行色名を正規化。未指定・先方指定・？付きは白と分離し「色未指定」。</summary>
        public static string NormalizeIssueColor(string color)
        {
            if(string.IsNullOrEmpty(color) || color=="未指定" || color=="先方から指定") return "色未指定";
            if(color.IndexOf('？')>=0 || color.IndexOf('?')>=0) return "色未指定";
            return color;
        }

        /// <summary>発行グループ名。未指定は白と分離し「色未指定」にする（自動で白にしない）。</summary>
        public static string IssueColorGroup(int kind, LabelData d)
        {
            return IssueColorGroup(kind,d,null);
        }

        /// <summary>先方指定形式で手入力色があるときはそれを発行グループにする。</summary>
        public static string IssueColorGroup(int kind, LabelData d, string clientColorOverride)
        {
            if(NeedsClientColorInput(kind,d) && !string.IsNullOrWhiteSpace(clientColorOverride))
                return NormalizeIssueColor(clientColorOverride.Trim().Replace('　',' '));
            return NormalizeIssueColor(LabelCatalog.ColorName(kind,d));
        }

        public static string StatusText(BatchTaskStatus s)
        {
            switch(s) {
                case BatchTaskStatus.Pending: return "未発行";
                case BatchTaskStatus.Sending: return "送信中";
                case BatchTaskStatus.SentAwaitingConfirm: return "送信済み・完了確認待ち";
                case BatchTaskStatus.CompletedConfirmed: return "作業者確認済み完了";
                case BatchTaskStatus.Failed: return "失敗／要確認";
                default: return s.ToString();
            }
        }

        /// <summary>再起動後に送信中のまま残ったタスクは要確認へ。送信済みは自動再送しない。</summary>
        public static void RecoverInterrupted(IList<BatchTask> tasks)
        {
            foreach(BatchTask t in tasks) {
                if(t.Status==BatchTaskStatus.Sending) {
                    t.Status=BatchTaskStatus.Failed;
                    t.LastError="送信中に中断されました。二重印刷を避けるため自動再送しません。状態を確認してください。";
                    t.UpdatedAt=DateTime.Now;
                }
            }
        }
    }

    public sealed class BatchTaskStore
    {
        readonly string path;
        public readonly List<BatchTask> Tasks=new List<BatchTask>();
        public BatchTaskStore(string file) {
            path=file;
            if(File.Exists(path)) Load();
            BatchValidation.RecoverInterrupted(Tasks);
        }
        public static string DefaultPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"UnifiedNameLabels","batch-tasks.xml");
        }
        public void Add(BatchTask task) { Tasks.Add(task); Save(); }
        public void Remove(string id) {
            for(int i=Tasks.Count-1;i>=0;i--) if(Tasks[i].Id==id) Tasks.RemoveAt(i);
            Save();
        }
        public BatchTask Find(string id) {
            foreach(BatchTask t in Tasks) if(t.Id==id) return t;
            return null;
        }
        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            XmlDocument d=new XmlDocument();
            XmlElement root=d.CreateElement("batchTasks");
            d.AppendChild(root);
            foreach(BatchTask t in Tasks) root.AppendChild(WriteTask(d,t));
            string tmp=path+".tmp";
            d.Save(tmp);
            if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path);
        }
        void Load()
        {
            XmlDocument d=new XmlDocument();
            d.Load(path);
            Tasks.Clear();
            foreach(XmlElement n in d.SelectNodes("/batchTasks/task")) Tasks.Add(ReadTask(n));
        }
        static XmlElement WriteTask(XmlDocument d, BatchTask t)
        {
            XmlElement n=d.CreateElement("task");
            n.SetAttribute("id",t.Id);
            n.SetAttribute("created",t.CreatedAt.ToString("o"));
            n.SetAttribute("updated",t.UpdatedAt.ToString("o"));
            n.SetAttribute("status",((int)t.Status).ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("kind",t.Kind.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("customer",t.CustomerName);
            n.SetAttribute("format",t.FormatName);
            n.SetAttribute("small",t.Small?"1":"0");
            n.SetAttribute("printer",t.PrinterName??"");
            n.SetAttribute("paper",t.PaperName??"");
            n.SetAttribute("paperW",t.PaperWidth.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("paperH",t.PaperHeight.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("landscape",t.Landscape?"1":"0");
            n.SetAttribute("ox",t.OffsetX.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("oy",t.OffsetY.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("copies",t.Copies.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("color",t.ColorGroup??"");
            n.SetAttribute("error",t.LastError??"");
            n.AppendChild(WriteData(d,t.Data));
            foreach(BatchPrintHistoryEntry h in t.History) {
                XmlElement he=d.CreateElement("history");
                he.SetAttribute("id",h.Id);
                he.SetAttribute("at",h.At.ToString("o"));
                he.SetAttribute("status",((int)h.StatusAfter).ToString(CultureInfo.InvariantCulture));
                he.SetAttribute("note",h.Note??"");
                he.SetAttribute("color",h.ColorGroup??"");
                he.SetAttribute("reprint",h.IsReprint?"1":"0");
                he.AppendChild(WriteData(d,h.Content));
                n.AppendChild(he);
            }
            return n;
        }
        static XmlElement WriteData(XmlDocument d, LabelDataSnapshot s)
        {
            XmlElement n=d.CreateElement("data");
            n.SetAttribute("digits",s.Digits.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("emphS",s.EmphasisStart.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("emphL",s.EmphasisLength.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("colorDept",s.ColorDepartment??"");
            n.SetAttribute("header",s.Header??"");
            n.SetAttribute("role",s.Role??"");
            n.SetAttribute("title",s.Title??"");
            n.SetAttribute("person",s.Person??"");
            n.SetAttribute("number",s.Number??"");
            n.SetAttribute("hPt",s.HeaderFontPt.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("rPt",s.RoleFontPt.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("pPt",s.PersonFontPt.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("nPt",s.NumberFontPt.ToString(CultureInfo.InvariantCulture));
            n.SetAttribute("hSet",s.HeaderFontSet?"1":"0");
            n.SetAttribute("rSet",s.RoleFontSet?"1":"0");
            n.SetAttribute("pSet",s.PersonFontSet?"1":"0");
            n.SetAttribute("nSet",s.NumberFontSet?"1":"0");
            return n;
        }
        static BatchTask ReadTask(XmlElement n)
        {
            BatchTask t=new BatchTask {
                Id=n.GetAttribute("id"),
                CreatedAt=ParseDate(n.GetAttribute("created")),
                UpdatedAt=ParseDate(n.GetAttribute("updated")),
                Status=(BatchTaskStatus)Int32.Parse(n.GetAttribute("status"),CultureInfo.InvariantCulture),
                Kind=Int32.Parse(n.GetAttribute("kind"),CultureInfo.InvariantCulture),
                CustomerName=n.GetAttribute("customer"),
                FormatName=n.GetAttribute("format"),
                Small=n.GetAttribute("small")=="1",
                PrinterName=n.GetAttribute("printer"),
                PaperName=n.GetAttribute("paper"),
                PaperWidth=ParseInt(n.GetAttribute("paperW")),
                PaperHeight=ParseInt(n.GetAttribute("paperH")),
                Landscape=n.GetAttribute("landscape")=="1",
                OffsetX=ParseDec(n.GetAttribute("ox")),
                OffsetY=ParseDec(n.GetAttribute("oy")),
                Copies=(short)Math.Max(1,ParseInt(n.GetAttribute("copies"))),
                ColorGroup=n.GetAttribute("color"),
                LastError=n.GetAttribute("error"),
                Data=ReadData(n.SelectSingleNode("data") as XmlElement)
            };
            foreach(XmlElement he in n.SelectNodes("history")) {
                t.History.Add(new BatchPrintHistoryEntry {
                    Id=he.GetAttribute("id"),
                    At=ParseDate(he.GetAttribute("at")),
                    StatusAfter=(BatchTaskStatus)Int32.Parse(he.GetAttribute("status"),CultureInfo.InvariantCulture),
                    Note=he.GetAttribute("note"),
                    ColorGroup=he.GetAttribute("color"),
                    IsReprint=he.GetAttribute("reprint")=="1",
                    Content=ReadData(he.SelectSingleNode("data") as XmlElement)
                });
            }
            return t;
        }
        static LabelDataSnapshot ReadData(XmlElement n)
        {
            if(n==null) return new LabelDataSnapshot();
            return new LabelDataSnapshot {
                Digits=ParseInt(n.GetAttribute("digits")),
                EmphasisStart=ParseInt(n.GetAttribute("emphS")),
                EmphasisLength=ParseInt(n.GetAttribute("emphL")),
                ColorDepartment=n.GetAttribute("colorDept"),
                Header=n.GetAttribute("header"),
                Role=n.GetAttribute("role"),
                Title=n.GetAttribute("title"),
                Person=n.GetAttribute("person"),
                Number=n.GetAttribute("number"),
                HeaderFontPt=ParseFloat(n.GetAttribute("hPt")),
                RoleFontPt=ParseFloat(n.GetAttribute("rPt")),
                PersonFontPt=ParseFloat(n.GetAttribute("pPt")),
                NumberFontPt=ParseFloat(n.GetAttribute("nPt")),
                HeaderFontSet=n.GetAttribute("hSet")=="1",
                RoleFontSet=n.GetAttribute("rSet")=="1",
                PersonFontSet=n.GetAttribute("pSet")=="1",
                NumberFontSet=n.GetAttribute("nSet")=="1"
            };
        }
        static DateTime ParseDate(string s) {
            DateTime dt; return DateTime.TryParse(s,null,DateTimeStyles.RoundtripKind,out dt)?dt:DateTime.Now;
        }
        static int ParseInt(string s) { int v; return Int32.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:0; }
        static decimal ParseDec(string s) { decimal v; return Decimal.TryParse(s,NumberStyles.Number,CultureInfo.InvariantCulture,out v)?v:0m; }
        static float ParseFloat(string s) { float v; return float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:0f; }
    }

    /// <summary>実機印刷の代わりに記録できる送信口（検証用）。</summary>
    public interface IBatchJobSender
    {
        /// <summary>ジョブ送信。成功で true。例外または false は失敗。</summary>
        bool Send(BatchTask task);
    }

    public sealed class RecordingBatchJobSender : IBatchJobSender
    {
        public readonly List<BatchTask> Sent=new List<BatchTask>();
        public bool ThrowOnSend;
        public int FailAfter=-1;
        public bool Send(BatchTask task)
        {
            if(ThrowOnSend) throw new InvalidOperationException("simulated send failure");
            if(FailAfter>=0 && Sent.Count>=FailAfter) return false;
            Sent.Add(task.CloneTask(false));
            return true;
        }
    }

    public sealed class PrintDocumentBatchJobSender : IBatchJobSender
    {
        public bool Send(BatchTask task)
        {
            using(PrintDocument doc=new PrintDocument()) {
                doc.PrinterSettings.PrinterName=task.PrinterName;
                if(!doc.PrinterSettings.IsValid) throw new InvalidOperationException("プリンターが無効です: "+task.PrinterName);
                doc.PrinterSettings.Copies=task.Copies;
                doc.DefaultPageSettings.Landscape=task.Landscape;
                if(!string.IsNullOrEmpty(task.PaperName)) {
                    foreach(PaperSize p in doc.PrinterSettings.PaperSizes) {
                        if(p.PaperName==task.PaperName && (task.PaperWidth==0 || (p.Width==task.PaperWidth && p.Height==task.PaperHeight))) {
                            doc.DefaultPageSettings.PaperSize=p; break;
                        }
                    }
                }
                doc.DocumentName="一括ネームラベル - "+task.CustomerName+" - "+task.FormatName;
                LabelData data=task.Data.ToLabelData();
                int kind=task.Kind;
                PrintOffset offset=new PrintOffset(task.OffsetX,task.OffsetY);
                doc.PrintPage+=delegate(object sender,PrintPageEventArgs e) {
                    GraphicsState state=e.Graphics.Save();
                    try {
                        e.Graphics.PageUnit=GraphicsUnit.Inch;
                        e.Graphics.ScaleTransform(0.01f,0.01f);
                        if(kind==1 || (kind>=4 && !LabelCatalog.Items[kind].Small && LabelCatalog.Items[kind].Layout!="QR")) {
                            e.Graphics.RotateTransform(-90);
                            e.Graphics.TranslateTransform(-e.PageBounds.Height,0);
                        }
                        e.Graphics.TranslateTransform((float)offset.X/.254f,(float)offset.Y/.254f);
                        LabelRenderer.Draw(e.Graphics,kind,data);
                        e.HasMorePages=false;
                    } finally { e.Graphics.Restore(state); }
                };
                doc.Print();
            }
            return true;
        }
    }

    public delegate bool BatchConfirmHandler(string title, string message);
    public delegate void BatchStatusHandler(string message);

    /// <summary>色別グループ化・確認一時停止付きの一括発行。</summary>
    public sealed class BatchIssueRunner
    {
        public BatchConfirmHandler Confirm=delegate(string t,string m) {
            return MessageBox.Show(m,t,MessageBoxButtons.OKCancel,MessageBoxIcon.Information)==DialogResult.OK;
        };
        public BatchStatusHandler Report=delegate(string m) { };
        public IBatchJobSender Sender=new PrintDocumentBatchJobSender();

        public int JobsSent { get; private set; }
        public bool Cancelled { get; private set; }
        public string StopReason { get; private set; }

        public List<List<BatchTask>> GroupByColor(IEnumerable<BatchTask> tasks)
        {
            Dictionary<string,List<BatchTask>> map=new Dictionary<string,List<BatchTask>>();
            List<string> order=new List<string>();
            foreach(BatchTask t in tasks) {
                string key=string.IsNullOrEmpty(t.ColorGroup)?BatchValidation.IssueColorGroup(t.Kind,t.Data.ToLabelData()):t.ColorGroup;
                t.ColorGroup=key;
                List<BatchTask> list;
                if(!map.TryGetValue(key,out list)) { list=new List<BatchTask>(); map[key]=list; order.Add(key); }
                list.Add(t);
            }
            List<List<BatchTask>> groups=new List<List<BatchTask>>();
            foreach(string k in order) groups.Add(map[k]);
            return groups;
        }

        /// <summary>発行対象を再検証し、不正は除外して errors に行情報を返す。</summary>
        public List<BatchTask> FilterReady(IEnumerable<BatchTask> source, List<string> errors)
        {
            List<BatchTask> ready=new List<BatchTask>();
            int row=0;
            foreach(BatchTask t in source) {
                row++;
                if(t.Status==BatchTaskStatus.SentAwaitingConfirm || t.Status==BatchTaskStatus.CompletedConfirmed || t.Status==BatchTaskStatus.Sending) {
                    errors.Add("行"+row+"（"+t.CustomerName+"／"+t.FormatName+"）: 送信済みまたは送信中のため対象外（二重印刷防止）");
                    continue;
                }
                string err=BatchValidation.ValidateForTask(t.Kind,t.Data.ToLabelData());
                if(err!=null) { errors.Add("行"+row+"（"+t.CustomerName+"／"+t.Data.Person+"）: "+err); continue; }
                ready.Add(t);
            }
            return ready;
        }

        public void Run(IList<BatchTask> tasks, BatchTaskStore store, bool reprint)
        {
            JobsSent=0; Cancelled=false; StopReason="";
            List<string> errors=new List<string>();
            List<BatchTask> ready=FilterReady(tasks,errors);
            if(errors.Count>0 && ready.Count==0) {
                StopReason=string.Join("\r\n",errors.ToArray());
                return;
            }
            if(errors.Count>0) {
                if(!Confirm("入力エラー",string.Join("\r\n",errors.ToArray())+"\r\n\r\nエラー行を除いて続行しますか？")) {
                    Cancelled=true; StopReason="入力エラーで中止"; return;
                }
            }
            List<List<BatchTask>> groups=GroupByColor(ready);
            for(int gi=0;gi<groups.Count;gi++) {
                List<BatchTask> group=groups[gi];
                string color=group[0].ColorGroup;
                string paperMsg=color=="色未指定"
                    ?"【色未指定】グループです。白ラベルと混ぜず、作業者が用紙色を明示確認してください。\r\n前色の出力が終わっていることを確認し、用紙をセットしてから OK を押してください。"
                    :"ラベル色【"+color+"】の用紙をセットしてください。\r\n前色の出力が終わっていることを確認してから OK を押してください（最初の色も確認が必要です）。";
                if(!Confirm("用紙・色の確認（"+(gi+1)+"/"+groups.Count+"）",paperMsg+"\r\n対象 "+group.Count+" 件")) {
                    Cancelled=true; StopReason="色確認でキャンセル（以降のジョブは送信しません）"; return;
                }
                foreach(BatchTask t in group) {
                    string recheck=BatchValidation.ValidateForTask(t.Kind,t.Data.ToLabelData());
                    if(recheck!=null) {
                        t.Status=BatchTaskStatus.Failed; t.LastError=recheck; t.UpdatedAt=DateTime.Now;
                        if(store!=null) store.Save();
                        if(!Confirm("再検証エラー","タスク（"+t.Data.Person+"）: "+recheck+"\r\nこのタスクを飛ばして続行しますか？")) {
                            Cancelled=true; StopReason="再検証エラーで中断"; return;
                        }
                        continue;
                    }
                    t.Status=BatchTaskStatus.Sending; t.UpdatedAt=DateTime.Now; t.LastError="";
                    if(store!=null) store.Save();
                    Report("送信中: "+t.CustomerName+" / "+t.Data.Person+" ["+color+"]");
                    try {
                        if(!Sender.Send(t)) throw new InvalidOperationException("ジョブ送信が拒否されました");
                        t.Status=BatchTaskStatus.SentAwaitingConfirm;
                        t.History.Add(new BatchPrintHistoryEntry {
                            At=DateTime.Now, StatusAfter=BatchTaskStatus.SentAwaitingConfirm,
                            Note=reprint?"再印刷で送信":"一括発行で送信", ColorGroup=color, IsReprint=reprint,
                            Content=t.Data.Clone()
                        });
                        t.UpdatedAt=DateTime.Now;
                        JobsSent++;
                        if(store!=null) store.Save();
                    } catch(Exception ex) {
                        t.Status=BatchTaskStatus.Failed;
                        t.LastError=ex.Message;
                        t.History.Add(new BatchPrintHistoryEntry {
                            At=DateTime.Now, StatusAfter=BatchTaskStatus.Failed,
                            Note=ex.Message, ColorGroup=color, IsReprint=reprint, Content=t.Data.Clone()
                        });
                        t.UpdatedAt=DateTime.Now;
                        if(store!=null) store.Save();
                        if(!Confirm("送信失敗",ex.Message+"\r\n以降のジョブ送信を中止しますか？（はい＝中止）")) {
                            // No = continue
                        } else {
                            Cancelled=true; StopReason="送信失敗後に中断"; return;
                        }
                    }
                }
                if(!Confirm("完了確認","色【"+color+"】のジョブ送信が終わりました。\r\nプリンター出力が終わっていることを確認したら OK。\r\nまだの場合はキャンセルで中断（次色は送りません）。")) {
                    Cancelled=true; StopReason="完了確認で中断（次色未送信）"; return;
                }
                foreach(BatchTask t in group) {
                    if(t.Status==BatchTaskStatus.SentAwaitingConfirm) {
                        t.Status=BatchTaskStatus.CompletedConfirmed;
                        t.UpdatedAt=DateTime.Now;
                        t.History.Add(new BatchPrintHistoryEntry {
                            At=DateTime.Now, StatusAfter=BatchTaskStatus.CompletedConfirmed,
                            Note="作業者確認済み", ColorGroup=color, IsReprint=reprint, Content=t.Data.Clone()
                        });
                    }
                }
                if(store!=null) store.Save();
            }
        }
    }

    public sealed class BatchImportResult
    {
        public readonly List<BatchTask> Tasks=new List<BatchTask>();
        public readonly List<string> Errors=new List<string>();
        public bool HasErrors { get { return Errors.Count>0; } }
    }

    public static class BatchImport
    {
        public static readonly string[] TemplateHeaders=new string[] {
            "顧客","形式","大/小","部署","区分","役職","ふりがな","氏名","番号","部数"
        };

        public static string TemplateCsv()
        {
            StringBuilder sb=new StringBuilder();
            sb.AppendLine(string.Join(",",TemplateHeaders));
            // 列: 顧客,形式,大/小,部署,区分,役職,ふりがな,氏名,番号,部数
            sb.AppendLine("ふか河,小ネーム（フルネーム）,小,,日中,,,山田 太郎,,1");
            sb.AppendLine("ふか河,小ネーム（フルネーム）,小,,夜間,,,佐藤 花子,,1");
            return sb.ToString();
        }

        public static void WriteTemplate(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            // Excel で開ける UTF-8 BOM CSV
            File.WriteAllText(path,TemplateCsv(),new UTF8Encoding(true));
        }

        public static BatchImportResult FromClipboardText(string text, Func<BatchTask,BatchTask> enrich)
        {
            return FromRows(ParseDelimited(text),enrich);
        }

        public static BatchImportResult FromCsvFile(string path, Func<BatchTask,BatchTask> enrich)
        {
            string text=File.ReadAllText(path,DetectEncoding(path));
            return FromRows(ParseDelimited(text),enrich);
        }

        static Encoding DetectEncoding(string path)
        {
            byte[] bom=new byte[3];
            using(FileStream fs=File.OpenRead(path)) { int n=fs.Read(bom,0,3); if(n>=3 && bom[0]==0xEF && bom[1]==0xBB && bom[2]==0xBF) return new UTF8Encoding(true); }
            return Encoding.GetEncoding(932);
        }

        public static List<string[]> ParseDelimited(string text)
        {
            List<string[]> rows=new List<string[]>();
            if(string.IsNullOrEmpty(text)) return rows;
            string[] lines=text.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
            char sep='\0';
            foreach(string line in lines) {
                if(line.Trim().Length==0) continue;
                if(sep=='\0') sep=line.IndexOf('\t')>=0?'\t':',';
                rows.Add(SplitLine(line,sep));
            }
            return rows;
        }

        static string[] SplitLine(string line, char sep)
        {
            List<string> cells=new List<string>();
            StringBuilder cur=new StringBuilder();
            bool q=false;
            for(int i=0;i<line.Length;i++) {
                char c=line[i];
                if(q) {
                    if(c=='"') {
                        if(i+1<line.Length && line[i+1]=='"') { cur.Append('"'); i++; }
                        else q=false;
                    } else cur.Append(c);
                } else {
                    if(c=='"') q=true;
                    else if(c==sep) { cells.Add(cur.ToString()); cur.Length=0; }
                    else cur.Append(c);
                }
            }
            cells.Add(cur.ToString());
            return cells.ToArray();
        }

        public static BatchImportResult FromRows(List<string[]> rows, Func<BatchTask,BatchTask> enrich)
        {
            BatchImportResult result=new BatchImportResult();
            if(rows.Count==0) { result.Errors.Add("データがありません。"); return result; }
            int start=0;
            Dictionary<string,int> map=new Dictionary<string,int>();
            if(LooksLikeHeader(rows[0])) {
                for(int i=0;i<rows[0].Length;i++) map[NormalizeHeader(rows[0][i])]=i;
                start=1;
            } else {
                for(int i=0;i<TemplateHeaders.Length;i++) map[NormalizeHeader(TemplateHeaders[i])]=i;
            }
            for(int r=start;r<rows.Count;r++) {
                string[] row=rows[r];
                int displayRow=r+1;
                try {
                    string customer=Cell(row,map,"顧客");
                    string format=Cell(row,map,"形式");
                    string size=Cell(row,map,"大/小");
                    string dept=Cell(row,map,"部署");
                    string group=Cell(row,map,"区分");
                    string role=Cell(row,map,"役職");
                    string furigana=Cell(row,map,"ふりがな");
                    string person=Cell(row,map,"氏名");
                    string number=Cell(row,map,"番号");
                    string copiesText=Cell(row,map,"部数");
                    if(string.IsNullOrWhiteSpace(customer) && string.IsNullOrWhiteSpace(person) && string.IsNullOrWhiteSpace(number))
                        continue;
                    int kind=ResolveKind(customer,format,size);
                    if(kind<0) { result.Errors.Add("行"+displayRow+": 顧客・形式を特定できません（顧客="+customer+", 形式="+format+"）"); continue; }
                    LabelSpec importSpec=LabelCatalog.Items[kind];
                    // 明日佳・東苗穂など：役職＝肩書(Title)、ふりがな＝Role。旧CSV（ふりがな列なし）は役職をRoleのまま。
                    string title="", roleValue=role;
                    if(importSpec.Titles!=null) {
                        title=role;
                        roleValue=furigana!=""?furigana:"";
                    } else if(furigana!="") roleValue=furigana;
                    LabelData d=new LabelData {
                        Header=dept, Role=roleValue, Title=title, Person=person, Number=number,
                        ColorDepartment=group, Digits=4
                    };
                    if(kind<2 && !string.IsNullOrEmpty(number) && number.Length>=6) d.Digits=5;
                    string err=BatchValidation.ValidateForTask(kind,d);
                    if(err!=null) { result.Errors.Add("行"+displayRow+"・項目検証: "+err); continue; }
                    short copies=1;
                    if(!string.IsNullOrWhiteSpace(copiesText)) {
                        int c; if(!Int32.TryParse(copiesText.Trim(),out c) || c<1 || c>30) {
                            result.Errors.Add("行"+displayRow+"・部数: 1～30の半角数字で入力してください。"); continue;
                        }
                        copies=(short)c;
                    }
                    LabelSpec spec=LabelCatalog.Items[kind];
                    BatchTask task=new BatchTask {
                        Kind=kind,
                        CustomerName=LabelCatalog.Customers[spec.Customer],
                        FormatName=spec.Format,
                        Small=spec.Small,
                        Data=LabelDataSnapshot.From(d),
                        Copies=copies,
                        ColorGroup=BatchValidation.IssueColorGroup(kind,d),
                        Status=BatchTaskStatus.Pending
                    };
                    if(enrich!=null) task=enrich(task);
                    result.Tasks.Add(task);
                } catch(Exception ex) {
                    result.Errors.Add("行"+displayRow+": "+ex.Message);
                }
            }
            if(result.HasErrors) result.Tasks.Clear(); // 不正行を黙って登録しない
            return result;
        }

        static bool LooksLikeHeader(string[] row)
        {
            foreach(string c in row) if(NormalizeHeader(c)=="顧客" || NormalizeHeader(c)=="氏名") return true;
            return false;
        }
        static string NormalizeHeader(string s) { return (s??"").Trim().Replace("　",""); }
        static string Cell(string[] row, Dictionary<string,int> map, string key)
        {
            int i; if(!map.TryGetValue(key,out i) || i<0 || i>=row.Length) return "";
            return (row[i]??"").Trim();
        }
        static int ResolveKind(string customer, string format, string size)
        {
            int customerIndex=-1;
            for(int i=0;i<LabelCatalog.Customers.Length;i++) {
                if(UserSettings.Matches(LabelCatalog.Customers[i],customer) || LabelCatalog.Customers[i]==customer) { customerIndex=i; break; }
            }
            if(customerIndex<0) {
                for(int i=0;i<LabelCatalog.Customers.Length;i++)
                    if(LabelCatalog.Customers[i].IndexOf(customer,StringComparison.Ordinal)>=0) { customerIndex=i; break; }
            }
            if(customerIndex<0) return -1;
            if(LabelCatalog.IsHiddenCustomer(customerIndex)) return -1;
            int[] ids=LabelCatalog.ForCustomer(customerIndex);
            bool wantSmall=size.IndexOf("小",StringComparison.Ordinal)>=0;
            bool wantLarge=size.IndexOf("大",StringComparison.Ordinal)>=0;
            int best=-1;
            foreach(int id in ids) {
                LabelSpec s=LabelCatalog.Items[id];
                bool formatOk=string.IsNullOrWhiteSpace(format)
                    || s.Format==format
                    || s.Format.IndexOf(format,StringComparison.Ordinal)>=0
                    || LabelCatalog.ShortFormName(s.Format).IndexOf(format,StringComparison.Ordinal)>=0;
                if(!formatOk) continue;
                if(wantSmall && !s.Small) continue;
                if(wantLarge && s.Small) continue;
                best=id; break;
            }
            if(best>=0) return best;
            if(!string.IsNullOrWhiteSpace(format)) return -1;
            return ids.Length>0?ids[0]:-1;
        }
    }

    /// <summary>--verify-batch 用。実プリンターへは送らない。</summary>
    public static class BatchVerification
    {
        public static void Run(string folder)
        {
            Directory.CreateDirectory(folder);
            string storePath=Path.Combine(folder,"batch-tasks.xml");
            if(File.Exists(storePath)) File.Delete(storePath);
            BatchTaskStore store=new BatchTaskStore(storePath);

            // 1) 通常印刷は色確認専用未選択可、タスクは必須
            int fuka=KindForRow(47);
            LabelData open=new LabelData { Person="山田 太郎" };
            if(LabelRenderer.Validate(fuka,open)!=null) throw new Exception("通常印刷の色未選択が拒否された");
            if(BatchValidation.ValidateForTask(fuka,open)==null) throw new Exception("タスク登録で色未選択を許可している");
            open.ColorDepartment="日中";
            if(BatchValidation.ValidateForTask(fuka,open)!=null) throw new Exception("タスク登録で色選択後も失敗: "+BatchValidation.ValidateForTask(fuka,open));
            if(BatchValidation.IssueColorGroup(fuka,new LabelData { Person="a" })!="色未指定") throw new Exception("未指定が色未指定グループにならない");
            if(BatchValidation.IssueColorGroup(fuka,open)!="緑") throw new Exception("日中色グループ失敗");
            // 先方から指定：手入力色を発行グループへ。区分コンボ付き形式は手入力不要。
            int andersen=KindForRow(11); // アンデルセン小（PreviousSourceRow=11, FixedColor=先方から指定）
            LabelData andersenData=new LabelData { Person="山田 太郎", Header="菓子工房アンデルセン" };
            if(LabelCatalog.Items[andersen].FixedColor!="先方から指定") throw new Exception("アンデルセンのFixedColorが先方から指定でない");
            string[] andersenChoices=LabelCatalog.Items[andersen].ClientColorChoices;
            if(andersenChoices==null || andersenChoices.Length!=2 || andersenChoices[0]!="赤" || andersenChoices[1]!="緑")
                throw new Exception("アンデルセンの色選択肢が赤／緑でない");
            if(LabelCatalog.ColorName(andersen,andersenData)!="先方から指定") throw new Exception("アンデルセン表示色が先方から指定でない");
            if(!BatchValidation.NeedsClientColorInput(andersen,andersenData)) throw new Exception("アンデルセンが先方指定の手入力対象でない");
            if(BatchValidation.IssueColorGroup(andersen,andersenData)!="色未指定") throw new Exception("先方指定の既定が色未指定でない");
            if(BatchValidation.IssueColorGroup(andersen,andersenData,"赤")!="赤") throw new Exception("先方指定の手入力色が反映されない");
            if(BatchValidation.IssueColorGroup(andersen,andersenData,"緑")!="緑") throw new Exception("先方指定の緑が反映されない");
            if(BatchValidation.IssueColorGroup(andersen,andersenData,"先方から指定")!="色未指定") throw new Exception("手入力の先方指定が色未指定にならない");
            if(BatchValidation.NeedsClientColorInput(fuka,open)) throw new Exception("区分付き形式を先方指定手入力対象にしている");
            if(BatchValidation.NormalizeIssueColor("白")!="白" || BatchValidation.NormalizeIssueColor("")!="色未指定")
                throw new Exception("NormalizeIssueColor 失敗");
            // タンゼン：男性は白／青の2択。トップラン品質保証室は白。
            int tanzen=-1, tanzenSmall=-1;
            for(int i=0;i<LabelCatalog.Items.Length;i++) {
                if(LabelCatalog.Customers[LabelCatalog.Items[i].Customer]!="タンゼンテクニカルプロダクト") continue;
                if(LabelCatalog.Items[i].Small) tanzenSmall=i; else tanzen=i;
            }
            if(tanzen<0 || tanzenSmall<0) throw new Exception("タンゼンの大小形式が見つからない");
            string[] tanzenGroups=LabelCatalog.ColorGroups(tanzen);
            string[] tanzenColors=LabelCatalog.GroupColors(tanzen);
            if(tanzenGroups.Length!=4 || tanzenGroups[0]!="男性（白）" || tanzenGroups[1]!="男性（青）")
                throw new Exception("タンゼン大の男性色選択肢が白／青でない");
            if(tanzenColors[0]!="白" || tanzenColors[1]!="青") throw new Exception("タンゼン大の男性色が白／青でない");
            if(LabelCatalog.ColorGroups(tanzenSmall)[0]!="男性（白）" || LabelCatalog.GroupColors(tanzenSmall)[1]!="青")
                throw new Exception("タンゼン小の男性色が白／青でない");
            int toprun=KindForRow(33);
            int qa=Array.IndexOf(LabelCatalog.ColorGroups(toprun),"品質保証室");
            if(qa<0 || LabelCatalog.GroupColors(toprun)[qa]!="白") throw new Exception("トップラン品質保証室が白でない");
            if(LabelCatalog.DepartmentColors[Array.IndexOf(LabelCatalog.ColorDepartments,"品質保証室")]!="白")
                throw new Exception("DepartmentColorsの品質保証室が白でない");

            // 2) SizeNumber / 部署必須は維持
            int sizeKind=KindForRow(20);
            if(BatchValidation.ValidateForTask(sizeKind,new LabelData { Number="60" })==null) throw new Exception("サイズ未選択をタスク許可");
            int deptKind=KindForRow(45);
            if(BatchValidation.ValidateForTask(deptKind,new LabelData { Person="山田 太郎" })==null) throw new Exception("必須部署なしをタスク許可");

            // 3) 保存・再読込・編集・複製・削除・履歴
            BatchTask t1=MakeTask(fuka,"山田 太郎","日中","緑");
            store.Add(t1);
            BatchTask dup=t1.CloneTask(true); dup.Data.Person="複製 太郎"; store.Add(dup);
            store.Remove(dup.Id);
            BatchTaskStore reload=new BatchTaskStore(storePath);
            if(reload.Tasks.Count!=1) throw new Exception("保存再読込件数失敗");
            if(reload.Tasks[0].Data.Person!="山田 太郎" || reload.Tasks[0].ColorGroup!="緑") throw new Exception("内容保持失敗");

            // 4) 送信中断リカバリ（二重送信防止）
            reload.Tasks[0].Status=BatchTaskStatus.Sending; reload.Save();
            BatchTaskStore recovered=new BatchTaskStore(storePath);
            if(recovered.Tasks[0].Status!=BatchTaskStatus.Failed) throw new Exception("送信中断の要確認化失敗");

            // 5) 色別連続・色変更前は次色0件
            RecordingBatchJobSender rec=new RecordingBatchJobSender();
            BatchIssueRunner runner=new BatchIssueRunner { Sender=rec };
            List<string> confirms=new List<string>();
            runner.Confirm=delegate(string title,string msg) {
                confirms.Add(title+"|"+msg);
                // 最初の色確認後、完了確認でキャンセル → 次色を送らない
                if(title.StartsWith("完了確認") && confirms.FindAll(delegate(string s){return s.StartsWith("完了確認");}).Count==1)
                    return false;
                return true;
            };
            List<BatchTask> mix=new List<BatchTask>();
            mix.Add(MakeTask(fuka,"A","日中","緑")); // 緑
            mix.Add(MakeTask(KindForRow(121),"B","男性","青")); // 青（山川）— 別顧客
            // 同色異顧客: もう一つ緑
            int fukaSmall=KindForCustomerFormat("ふか河",true);
            if(fukaSmall>=0) mix.Add(MakeTask(fukaSmall,"C","日中","緑"));
            recovered.Tasks.Clear();
            foreach(BatchTask t in mix) { t.Status=BatchTaskStatus.Pending; recovered.Add(t); }
            runner.Run(new List<BatchTask>(recovered.Tasks),recovered,false);
            if(rec.Sent.Count==0) throw new Exception("最初の色グループが送信されていない");
            foreach(BatchTask s in rec.Sent) if(s.ColorGroup!="緑") throw new Exception("色確認キャンセル前に別色が送信された");
            if(!runner.Cancelled) throw new Exception("キャンセルが記録されていない");

            // 6) 同色は顧客・大小混在でも連続（色確認は1回）。めむろフーズ 派遣=緑。
            rec=new RecordingBatchJobSender();
            runner=new BatchIssueRunner { Sender=rec };
            int colorConfirms=0;
            runner.Confirm=delegate(string title,string msg) {
                if(title.StartsWith("用紙・色の確認")) colorConfirms++;
                return true;
            };
            List<BatchTask> sameColor=new List<BatchTask>();
            sameColor.Add(MakeTask(KindForRow(53),"大側","派遣","緑")); // 大
            sameColor.Add(MakeTask(KindForRow(54),"小側","派遣","緑")); // 小
            sameColor.Add(MakeTask(fuka,"別顧客","日中","緑")); // 異顧客同色
            if(!sameColor[0].Small && sameColor[1].Small) { /* 大小混在 */ }
            else throw new Exception("大小混在テストの前提が崩れた");
            runner.Run(sameColor,null,false);
            if(colorConfirms!=1) throw new Exception("同色で色確認が複数回: "+colorConfirms);
            if(rec.Sent.Count!=sameColor.Count) throw new Exception("同色連続送信数失敗");

            // 7) 送信済みは再送しない／再印刷は明示
            BatchTask sent=MakeTask(fuka,"送信済","日中","緑");
            sent.Status=BatchTaskStatus.SentAwaitingConfirm;
            List<string> skipErr=new List<string>();
            List<BatchTask> filtered=runner.FilterReady(new BatchTask[]{sent},skipErr);
            if(filtered.Count!=0 || skipErr.Count==0) throw new Exception("送信済み再送防止失敗");
            sent.Status=BatchTaskStatus.Pending;
            sent.History.Add(new BatchPrintHistoryEntry { Note="過去", IsReprint=false, Content=sent.Data.Clone() });
            int histBefore=sent.History.Count;
            rec=new RecordingBatchJobSender();
            runner=new BatchIssueRunner { Sender=rec, Confirm=delegate(string a,string b){return true;} };
            runner.Run(new BatchTask[]{sent},null,true);
            if(sent.History.Count<=histBefore) throw new Exception("再印刷で履歴が増えない");
            if(sent.Status!=BatchTaskStatus.CompletedConfirmed) throw new Exception("確認後完了にならない");

            // 8) Excel/貼付：不正行は登録しない、先頭ゼロ・空欄
            string csv=BatchImport.TemplateCsv()+"\n不正顧客,大ネーム,大,,,太郎,,1\n";
            BatchImportResult bad=BatchImport.FromRows(BatchImport.ParseDelimited(csv),null);
            if(!bad.HasErrors || bad.Tasks.Count!=0) throw new Exception("不正行黙認登録を検出");
            string paste="顧客\t形式\t大/小\t部署\t区分\t役職\tふりがな\t氏名\t番号\t部数\nふか河\t小ネーム（フルネーム）\t小\t\t日中\t\t\t山田 太郎\t\t1\n";
            BatchImportResult ok=BatchImport.FromClipboardText(paste,null);
            if(ok.HasErrors || ok.Tasks.Count!=1) throw new Exception("貼付取込失敗: "+string.Join(";",ok.Errors.ToArray()));
            // 先頭ゼロ番号（石狩 4桁）
            int numKind=KindForRow(27);
            LabelData zero=new LabelData { Number="0123" };
            if(BatchValidation.ValidateForTask(numKind,zero)!=null) throw new Exception("先頭ゼロ番号がタスク検証で失敗: "+BatchValidation.ValidateForTask(numKind,zero));
            LabelData shortNum=new LabelData { Number="123" };
            if(BatchValidation.ValidateForTask(numKind,shortNum)==null) throw new Exception("桁不足番号をタスクで許可している");

            // 9) 描画経路：スナップショット描画が例外なく動く
            using(Bitmap b=new Bitmap(400,200))
            using(Graphics g=Graphics.FromImage(b)) {
                g.Clear(Color.White); g.ScaleTransform(2.54f,2.54f);
                LabelRenderer.Draw(g,fuka,open);
            }

            string templatePath=Path.Combine(folder,"一括発行テンプレート.csv");
            BatchImport.WriteTemplate(templatePath);
            if(!File.Exists(templatePath)) throw new Exception("テンプレート出力失敗");

            File.WriteAllText(Path.Combine(folder,"batch-verification.txt"),
                "PASS: task color required vs print optional; persist/reload/clone/delete; interrupt no-resend; same-color multi-customer continuous; next-color blocked until confirm; sent vs confirmed; reprint history; import rejects bad rows. Real printer not used.",
                Encoding.UTF8);
        }

        static int KindForRow(int previousSourceRow)
        {
            for(int i=0;i<LabelCatalog.Items.Length;i++) if(LabelCatalog.Items[i].PreviousSourceRow==previousSourceRow) return i;
            throw new Exception("旧登録のExcel行が見つかりません "+previousSourceRow);
        }
        static int KindForCustomerFormat(string customerName, bool small)
        {
            for(int i=0;i<LabelCatalog.Items.Length;i++) {
                LabelSpec s=LabelCatalog.Items[i];
                if(LabelCatalog.Customers[s.Customer]==customerName && s.Small==small && LabelCatalog.ColorGroups(i).Length>0)
                    return i;
            }
            for(int i=0;i<LabelCatalog.Items.Length;i++) {
                if(LabelCatalog.Customers[LabelCatalog.Items[i].Customer]==customerName && LabelCatalog.Items[i].Small==small)
                    return i;
            }
            return -1;
        }
        static BatchTask MakeTask(int kind, string person, string group, string expectColor)
        {
            LabelSpec s=LabelCatalog.Items[kind];
            LabelData d=new LabelData {
                Person=s.NeedsName?person:"",
                ColorDepartment=group??"",
                Header=s.Heading??"",
                Role="",
                Number=""
            };
            if(s.NeedsNumber) d.Number=s.NumberLength>0?new string('0',s.NumberLength):"1234";
            string[] deps=LabelCatalog.DepartmentNames(kind);
            if(deps!=null && deps.Length>0) d.Header=deps[0];
            if(s.Layout=="CohortName") d.Role="1";
            if(s.Layout=="SizeNumber" && string.IsNullOrEmpty(d.ColorDepartment) && LabelCatalog.ColorGroups(kind).Length>0)
                d.ColorDepartment=LabelCatalog.ColorGroups(kind)[0];
            string err=BatchValidation.ValidateForTask(kind,d);
            if(err!=null) throw new Exception("MakeTask失敗 kind="+kind+" row="+s.SourceRow+": "+err);
            string color=BatchValidation.IssueColorGroup(kind,d);
            if(!string.IsNullOrEmpty(expectColor) && color!=expectColor)
                throw new Exception("期待色 "+expectColor+" に対し "+color+" (kind="+kind+")");
            return new BatchTask {
                Kind=kind,
                CustomerName=LabelCatalog.Customers[s.Customer],
                FormatName=s.Format,
                Small=s.Small,
                Data=LabelDataSnapshot.From(d),
                ColorGroup=color,
                PrinterName="VERIFY",
                PaperName="50x80",
                Copies=1,
                Status=BatchTaskStatus.Pending
            };
        }
    }
}
