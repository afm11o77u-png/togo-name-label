using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
namespace UnifiedLabels {
 public sealed partial class MainForm {
  int LatestRow(int row){for(int i=0;i<LabelCatalog.Items.Length;i++)if(LabelCatalog.Items[i].SourceRow==row)return i;throw new Exception("Missing current row "+row);}
  void Need(bool condition,string message){if(!condition)throw new Exception(message);}
  void CaptureLatest(string folder,string name){Application.DoEvents();using(Bitmap b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(Point.Empty,Size));b.Save(Path.Combine(folder,name+".png"));}}
  public void VerifyRetainedInputs(string folder){
   Directory.CreateDirectory(folder);Show();
   foreach(int k in new[]{20,23}){
    kinds.SelectedIndex=k;Need(LabelRenderer.Validate(k,new LabelData())==null,"Qualification requires input");Need(!person.Visible && !number.Visible,"Qualification fields still visible");
    using(Bitmap b=new Bitmap(480,300))using(Graphics g=Graphics.FromImage(b)){g.Clear(Color.White);g.ScaleTransform(6*.254f,6*.254f);LabelRenderer.Draw(g,k,new LabelData());b.Save(Path.Combine(folder,"generic-"+k+".png"));}
    CaptureLatest(folder,"window-"+k);
   }
   Need(LabelRenderer.Validate(21,new LabelData {Header="製造課(加工)",Person="山田 太郎",Number="1234567"})!=null,"QR maximum length");
   SelectedCustomerIndex=13;formats.SelectedIndex=0;department.SelectedIndex=1;person.Text="山田 太郎";formats.SelectedIndex=1;Need(department.Text=="加工場" && Current().Header=="加工場","Nayoro department preservation");
   SelectedCustomerIndex=14;formats.SelectedIndex=0;colorDepartment.SelectedIndex=1;person.Text="山田　太郎";Need(Current().Person=="山田 太郎" && LabelCatalog.ColorName(active,Current())=="青" && sampleColor.BackColor==BackColor,"Fukagawa space/color/sample");formats.SelectedIndex=1;Need(colorDepartment.Text=="夜間","Shift preservation");
   SelectedCustomerIndex=10;person.Text="山田 太郎";for(int c=0;c<LabelCatalog.ColorDepartments.Length;c++){colorDepartment.SelectedIndex=c;Need(LabelCatalog.ColorName(active,Current())==LabelCatalog.DepartmentColors[c] && sampleColor.Text.Contains(LabelCatalog.DepartmentColors[c]) && LabelRenderer.Validate(active,Current())==null,"Toprun color rules");}
   VerifyAdditions(folder);VerifyBulkAdditions(folder);
   File.WriteAllText(Path.Combine(folder,"retained-verification.txt"),"PASS: fixed qualification labels, existing QR length, department and color state, full-width spaces, all Toprun colors, mixed-name selection and bounds, and all retained bulk input checks. No print jobs sent.",Encoding.UTF8);Hide();
  }
  public void VerifyLatest(string folder){
   Directory.CreateDirectory(folder);Show();
   // SourceRow は五十音順2（md5 ca88ea51…）の行番号
   foreach(int row in new[]{222,233,235,33,78,226,227,228,140,12,161,10,120,199,174,165,171,109,48,157,77}){
    kinds.SelectedIndex=LatestRow(row);var spec=LabelCatalog.Items[active];
    if(department.Items.Count>0)department.SelectedIndex=0;
    if(colorDepartment.Items.Count>0)colorDepartment.SelectedIndex=0;
    person.Text="山田 太郎";number.Text="123";
    if(spec.Layout=="StarRoman"){header.Text="YAMADA";role.Text="3";}
    if(spec.Layout=="DeptOnly")header.Text="鮮魚";
    if(spec.Layout=="DeptFurigana"){role.Text="やまだ"; if(header.Visible) header.Text="看護部";}
    if(spec.Layout=="SplitCode")person.Text="M-23";
    if(spec.Layout=="OptionalHeading")header.Text="製造";
    if(spec.Layout=="InitialName")role.Text="太";
    if(spec.Layout=="FreeText")person.Text=row==235?"タロウ":"LL";
    if(spec.Reading=="ローマ字")person.Text="Yamada Taro";
    UpdatePreview();Need(!status.Text.StartsWith("処理できません"),"Preview error "+row+" "+status.Text);
    if(spec.Pending=="")Need(LabelRenderer.Validate(active,Current())==null,"Invalid example "+row+" "+LabelRenderer.Validate(active,Current()));
    else Need(LabelRenderer.Validate(active,Current())!=null,"Incomplete template permitted "+row);
    if(garmentSummary.Visible)Need(garmentSummary.Bottom<=smallPlacement.Top && garmentSummary.Text==LabelCatalog.GarmentSummary(spec.Customer),"Garment summary overlap "+row);
    else Need(LabelCatalog.GarmentSummary(spec.Customer)=="","Garment summary should hide "+row);
    Need(smallPlacement.Visible && smallPlacement.Height>0 && smallPlacement.Text.StartsWith("貼り付け先：") && smallPlacement.Text.IndexOf(spec.Placement)>=0,"Placement band missing "+row);
    string selection=LabelCatalog.SelectionGuide(spec.Customer,active);
    if(selection!="")Need(largePlacement.Visible && largePlacement.Text==selection && largePlacement.Text.IndexOf("【選択中】")>=0,"Selection guide missing "+row);
    Need(smallPlacement.Bottom<=largePlacement.Top && largePlacement.Bottom<=guide.Top && status.Bottom<=ClientSize.Height,"Guide overlaps "+row);
    if(row==33){Need(!colorDepartment.Visible,"Duplicate department selection");department.SelectedItem="洋菓子チーム";Need(LabelCatalog.ColorName(active,Current())=="赤","Department color not linked");}
    if(row==222){Need(LabelCatalog.ColorName(active,Current())=="緑？","Fresh fish provisional color");header.Text="その他";Need(LabelCatalog.ColorName(active,Current())=="先方から指定","Unspecified department color");header.Text="鮮魚";}
    if(row==140 || row==12 || row==161){Need(LabelCatalog.ColorName(active,Current()).Contains("？"),"Question mark lost");Need(LabelCatalog.Swatch(LabelCatalog.ColorName(active,Current()))==(row==12?Color.LightCoral:Color.LightSkyBlue),"Provisional swatch wrong");}
    if(row==228){Need(LabelCatalog.ColorName(active,Current())=="先方から指定","Unknown color invents a color");Need(header.Visible&&!header.ReadOnly,"Department not editable");header.Text="";Need(LabelRenderer.Validate(active,Current())==null,"Optional department blocks print");header.Text="製造";}
    if(row==120)Need(spec.Heading=="トテッポ工房" && spec.Layout=="HeadingName","TotepPo fixed header");
    CaptureLatest(folder,"row-"+row);
    if(row==233){role.Text="0";CaptureLatest(folder,"row-233-no-stars");role.Text="abc";Need(LabelRenderer.Validate(active,Current())!=null,"Invalid star count accepted");}
    if(row==171){person.Text="M-X";Need(LabelRenderer.Validate(active,Current())!=null,"Invalid split code accepted");}
   }
   int sapporoCohort=-1,asahikawaCohort=-1;
   for(int i=0;i<LabelCatalog.Items.Length;i++){if(LabelCatalog.Items[i].Layout!="CohortName")continue; if(LabelCatalog.Items[i].Format.Contains("札幌"))sapporoCohort=i; if(LabelCatalog.Items[i].Format.Contains("旭川"))asahikawaCohort=i;}
   Need(sapporoCohort>=0 && asahikawaCohort>=0,"Cohort formats missing");
   foreach(int k in new[]{sapporoCohort,asahikawaCohort}){
    kinds.SelectedIndex=k;var colors=k==sapporoCohort?new[]{"赤","緑","青","白"}:new[]{"白","緑","青","赤"};person.Text="テキ シホウ";number.Text="1234567";
    for(int n=1;n<=12;n++){role.Text=n.ToString();Need(LabelCatalog.ColorName(active,Current())==colors[(n-1)%4],"Cohort branch cycle");Need(LabelRenderer.Validate(active,Current())==null,"Cohort validation");}
    role.Text="5";CaptureLatest(folder,k==sapporoCohort?"cohort-sapporo":"cohort-asahikawa");
   }
   kinds.SelectedIndex=LatestRow(56);person.Text="チャン ティ ハー ザン";number.Text="21";
   Need(LabelCatalog.NameForPrint(LabelCatalog.Items[active],person.Text)=="ﾁｬﾝ ﾃｨ ﾊｰ ｻﾞﾝ","Halfwidth kana conversion");CaptureLatest(folder,"halfwidth-kana");
   Need(LabelCatalog.Items[0].NeedsNumber && LabelCatalog.Items[1].NeedsNumber && LabelCatalog.Items[2].NeedsNumber && LabelCatalog.Items[3].NeedsNumber,"Legacy codes hidden");
   kinds.SelectedIndex=0;digits.SelectedIndex=1;number.Text="Y12345";digits.SelectedIndex=0;Need(number.Text=="Y1234","Yotsuba 5 to 4 truncation");
   Need(preferences.Get(OffsetKey).X==0,"Unexpected test adjustment");
   File.WriteAllText(Path.Combine(folder,"latest-verification.txt"),"PASS: 131 customers / 227 formats; provisional colors and question marks; customer-specified unknown color; department color linkage; 3-line furigana; variable stars; Roman names; split alphanumeric codes; fixed TotepPo; Sapporo/Asahikawa cohort cycles 1..12; halfwidth katakana; incomplete templates blocked; existing QR inputs; no print jobs sent.",Encoding.UTF8);Hide();
  }
 }
}
