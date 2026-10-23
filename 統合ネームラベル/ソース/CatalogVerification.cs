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
   // SourceRow は五十音順2（鱗幸=230/231）。削除したあすかHOUSE(10)は含めない。あすかベーカリー(12)は先方指定色へ変更済み。
   foreach(int row in new[]{222,235,237,33,78,226,227,230,140,161,11,120,199,174,165,171,109,48,157,77,136,250,251,252,253,254,217,41,42,43}){
    kinds.SelectedIndex=LatestRow(row);var spec=LabelCatalog.Items[active];
    if(department.Items.Count>0)department.SelectedIndex=0;
    if(colorDepartment.Items.Count>0)colorDepartment.SelectedIndex=0;
    if(jobTitle.Visible && jobTitle.Items.Count>0) jobTitle.SelectedIndex=0;
    person.Text="山田 太郎";number.Text="123";
    if(spec.Layout=="StarRoman"){header.Text="YAMADA";role.Text="3";}
    if(spec.Layout=="DeptOnly")header.Text="鮮魚";
    if(spec.Layout=="DeptFurigana"){role.Text="やまだ"; if(header.Visible) header.Text="看護部"; if(department.Visible && department.Items.Count>0) department.SelectedIndex=0;}
    if(spec.Layout=="DeptName" && spec.Titles!=null){person.Text="タロウ"; if(department.Items.Count>0) department.SelectedIndex=0;}
    if(spec.Layout=="SplitCode")person.Text="M-23";
    if(spec.Layout=="OptionalHeading")header.Text="製造";
    if(spec.Layout=="InitialName")role.Text="太";
    if(spec.Layout=="FreeText")person.Text=row==237?"タロウ":"LL";
    if(spec.Layout=="CompanyCodeName"){number.Text="01234"; person.Text="山田 太郎";}
    if(spec.Layout=="CohortName"){role.Text=spec.CohortColors!=null?"5":"1"; person.Text="テキ シホウ"; number.Text="1234567"; if(spec.Format.Contains("八軒")) role.Text="1";}
    if(spec.Layout=="RoleName" && spec.Reading=="ローマ字"){role.Text="ヤマダ"; person.Text="Yamada";}
    if(spec.Reading=="ローマ字" && spec.Layout!="RoleName")person.Text="Yamada Taro";
    if(spec.KatakanaOnly)person.Text="ヤマダ タロウ";
    if(spec.SurnameOnly || spec.Layout=="Surname")person.Text=spec.Reading=="ローマ字"?"Yamada":"山田";
    UpdatePreview();Need(!status.Text.StartsWith("処理できません"),"Preview error "+row+" "+status.Text);
    if(spec.Pending=="")Need(LabelRenderer.Validate(active,Current())==null,"Invalid example "+row+" "+LabelRenderer.Validate(active,Current()));
    else Need(LabelRenderer.Validate(active,Current())!=null,"Incomplete template permitted "+row);
    if(garmentSummary.Visible)Need(garmentSummary.Bottom<=smallPlacement.Top && garmentSummary.Text==LabelCatalog.GarmentSummary(spec.Customer),"Garment summary overlap "+row);
    else Need(LabelCatalog.GarmentSummary(spec.Customer)=="","Garment summary should hide "+row);
    Need(smallPlacement.Visible && smallPlacement.Height>0 && smallPlacement.Text.StartsWith("貼り付け先：") && smallPlacement.Text.IndexOf(spec.Placement)>=0,"Placement band missing "+row);
    Need(largePlacement.Text.IndexOf("【選択中】")<0,"Selection guide should stay hidden "+row);
    Need(smallPlacement.Bottom<=largePlacement.Top && largePlacement.Bottom<=guide.Top && status.Bottom<=ClientSize.Height,"Guide overlaps "+row);
    if(row==33){Need(!colorDepartment.Visible,"Duplicate department selection");department.SelectedItem="洋菓子チーム";Need(LabelCatalog.ColorName(active,Current())=="赤","Department color not linked");}
    if(row==222){Need(LabelCatalog.ColorName(active,Current())=="緑？","Fresh fish provisional color");header.Text="その他";Need(LabelCatalog.ColorName(active,Current())=="先方から指定","Unspecified department color");header.Text="鮮魚";}
    if(row==140 || row==161){Need(LabelCatalog.ColorName(active,Current()).Contains("？"),"Question mark lost");Need(LabelCatalog.Swatch(LabelCatalog.ColorName(active,Current()))==Color.LightSkyBlue,"Provisional swatch wrong");}
    if(row==11){
     Need(LabelCatalog.Customers[spec.Customer]=="あすかHOUSE東苗穂","Higashinaebo customer");
     Need(spec.Placement.IndexOf("左胸")>=0,"Higashinaebo placement");
     Need(spec.Departments!=null && spec.Departments.Length==6,"Higashinaebo departments");
     // 役職は空＋リーダー／主任／課長／施設長の5件（部署6件と混同しない）
     Need(spec.Titles!=null && spec.Titles.Length==5 && jobTitle.Visible && jobTitle.Items.Count==5,"Higashinaebo title combo");
     Need(spec.Titles[0]=="" && spec.Titles[1]=="リーダー" && spec.Titles[4]=="施設長","Higashinaebo title values");
     Need(roleLabel.Visible && roleLabel.Text.IndexOf("ふりがな")>=0,"Higashinaebo furigana field");
     Need(spec.UpperBandMm==12.8f && spec.MiddleBandMm==12.5f && spec.LowerBandMm==24.7f,"Higashinaebo bands");
     jobTitle.SelectedIndex=1; // リーダー
     Need(LabelCatalog.DeptWithTitle(Current()).IndexOf(" ")>=0,"Higashinaebo title join");
     jobTitle.SelectedIndex=0;
     Need(LabelCatalog.DeptWithTitle(Current()).IndexOf(" ")<0,"Higashinaebo empty title has no join");
     Need(LabelCatalog.IsHiddenCustomerName("あすかHOUSE") && LabelCatalog.IsHiddenCustomerName("ホワイトファーム"),"Deleted customers stay hidden");
    }
    if(row==78){
     Need(LabelCatalog.Customers[spec.Customer].IndexOf("桜台")>=0 && LabelCatalog.Customers[spec.Customer].IndexOf("宮の沢")>=0,"Ashita rename");
     Need(spec.SpaceFurigana,"Ashita furigana spacing");
     // 役職は空＋主任／師長／科長／部長の5件。ふりがな Role と独立。
     Need(spec.Layout=="DeptFurigana" && spec.Titles!=null && spec.Titles.Length==5 && jobTitle.Visible && jobTitle.Items.Count==5,"Ashita title combo");
     Need(spec.Titles[1]=="主任" && spec.Titles[4]=="部長","Ashita title values");
     Need(roleLabel.Visible && roleLabel.Text.IndexOf("ふりがな")>=0,"Ashita furigana field");
     role.Text="やまだ"; Need(LabelCatalog.SpaceFuriganaText(role.Text)=="や ま だ","Ashita space furigana");
     role.Text="や ま だ"; Need(LabelCatalog.SpaceFuriganaText(role.Text)=="や ま だ","Ashita no double space");
     jobTitle.SelectedIndex=1; Need(LabelCatalog.DeptWithTitle(Current()).IndexOf(" ")>=0,"Ashita title join");
     jobTitle.SelectedIndex=0;
    }
    if(row==253){
     Need(LabelCatalog.Customers[spec.Customer].IndexOf("桜台")>=0,"Ashita foreign customer");
     Need(spec.Layout=="DeptName" && spec.Reading=="カタカナ","Ashita foreign layout");
     Need(spec.Titles!=null && spec.Titles.Length==5 && jobTitle.Visible && jobTitle.Items.Count==5,"Ashita foreign title combo");
     Need(!roleLabel.Visible && !role.Visible,"Ashita foreign has no furigana Role");
     Need(spec.UpperBandMm==12.8f && spec.LowerBandMm==37.2f,"Ashita foreign bands");
    }
    if(row==230){
     Need(spec.Layout=="Name","Rinko became single-line Name");
     Need(LabelCatalog.ColorName(active,Current())=="赤","Rinko role color");
     Need(spec.ColorGuide.IndexOf("先方から指定？")>=0,"Rinko ColorGuide lost question mark");
     Need(!header.Visible,"Rinko upper heading removed");
     var uniq=new System.Collections.Generic.List<string>();
     foreach(string c in LabelCatalog.GroupColors(active)) if(!uniq.Contains(c)) uniq.Add(c);
     Need(uniq.Count==5 && uniq.Contains("黄") && uniq.Contains("白"),"Rinko shared 5-color legend");
     int legendVisible=0; bool yellowCard=false;
     foreach(Label card in colorLegend){ if(card.Visible){ legendVisible++; if(card.Text.StartsWith("黄")) yellowCard=true; } }
     Need(legendVisible==5 && yellowCard,"Rinko yellow belongs in shared legend cards");
     int yellowOnly=0; for(int i=0;i<LabelCatalog.Items.Length;i++) if(LabelCatalog.Items[i].Customer==81 && LabelCatalog.Items[i].FixedColor=="黄") yellowOnly++;
     Need(yellowOnly==0,"Rinko must not keep a yellow-only format");
     colorDepartment.SelectedIndex=System.Array.IndexOf(LabelCatalog.ColorGroups(active),"特殊色");
     Need(LabelCatalog.ColorName(active,Current())=="黄","Rinko special-color yellow");
     colorDepartment.SelectedIndex=0;
    }
    if(row==136){
     Need(spec.Layout=="CompanyCodeName" && spec.NumberLength==5,"Mikado CompanyCodeName");
     number.Text="01234"; UpdatePreview();
     Need(LabelRenderer.Validate(active,Current())==null,"Mikado valid number");
     // 描画文言はプレビュー経由で確認（固定〇を残さない）
     Need(spec.Heading=="函館みかど" && spec.Heading.IndexOf("〇")<0,"Mikado heading cleaned");
     number.Text="1234"; Need(LabelRenderer.Validate(active,Current())!=null,"Mikado length check");
     number.Text="01234";
    }
    if(row==120)Need(spec.Heading=="トテッポ工房" && spec.Layout=="HeadingName","TotepPo fixed header");
    if(row==217){
     Need(LabelCatalog.Customers[spec.Customer]=="よつ葉乳業㈱十勝工場","Yotsuba summer customer");
     Need(spec.FixedColor=="青" && spec.HeadingFont==18 && spec.Heading=="よつ葉乳業㈱","Yotsuba summer unchanged");
    }
    if(row==41 || row==42 || row==43){
     Need(spec.SurnameOnly,"GOOD NEWS surname only");
     if(row==41) Need(spec.SurnameSizing,"GOOD NEWS large sizing");
     if(row==42 || row==43) Need(spec.MaximumFont==36,"GOOD NEWS small 36pt");
    }
    if(row==250){
     string[] expect={"赤","緑","青","赤","青","白","赤","白","青","白"};
     person.Text="テキ シホウ"; number.Text="1234567";
     for(int n=1;n<=10;n++){role.Text=n.ToString();Need(LabelCatalog.ColorName(active,Current())==expect[n-1],"Asahikawa cohort "+n);Need(LabelRenderer.Validate(active,Current())==null,"Asahikawa valid "+n);}
     role.Text="11"; Need(LabelCatalog.ColorName(active,Current())=="未指定","Asahikawa unknown unset");
     Need(LabelRenderer.Validate(active,Current())!=null,"Asahikawa unknown blocked");
    }
    if(row==251){
     person.Text="テキ シホウ"; number.Text="1234567";
     foreach(int n in new[]{1,2,3,4}){role.Text=n.ToString();Need(LabelCatalog.ColorName(active,Current())=="未指定","Hakodate 1-4 unset");Need(LabelRenderer.Validate(active,Current())!=null,"Hakodate 1-4 blocked");}
     string[] expect=new string[]{"","","","","緑","青","赤","白"};
     for(int n=5;n<=8;n++){role.Text=n.ToString();Need(LabelCatalog.ColorName(active,Current())==expect[n-1],"Hakodate "+n);Need(LabelRenderer.Validate(active,Current())==null,"Hakodate valid "+n);}
    }
    if(row==252){
     person.Text="テキ シホウ"; number.Text="1234567"; role.Text="1";
     Need(LabelCatalog.ColorName(active,Current())=="赤","Hachiken 1");
     role.Text="2"; Need(LabelCatalog.ColorName(active,Current())=="未指定","Hachiken other unset");
    }
    if(row==254){
     Need(spec.UpperBandMm==20.8f && spec.LowerBandMm==29.2f && spec.HeadingFont==20 && spec.MaximumFont==22,"Kurisawa trainee bands/pt");
     role.Text="ヤマダ"; person.Text="Yamada";
     Need(LabelRenderer.Validate(active,Current())==null,"Kurisawa trainee valid");
    }
    CaptureLatest(folder,"row-"+row);
    if(row==235){role.Text="0";CaptureLatest(folder,"row-235-no-stars");role.Text="abc";Need(LabelRenderer.Validate(active,Current())!=null,"Invalid star count accepted");}
    if(row==171){person.Text="M-X";Need(LabelRenderer.Validate(active,Current())!=null,"Invalid split code accepted");}
   }
   int sapporoCohort=-1,asahikawaCohort=-1;
   for(int i=0;i<LabelCatalog.Items.Length;i++){if(LabelCatalog.Items[i].Layout!="CohortName")continue; if(LabelCatalog.Items[i].Format.Contains("札幌"))sapporoCohort=i; if(LabelCatalog.Items[i].Format.Contains("旭川"))asahikawaCohort=i;}
   Need(sapporoCohort>=0 && asahikawaCohort>=0,"Cohort formats missing");
   kinds.SelectedIndex=sapporoCohort;person.Text="テキ シホウ";number.Text="1234567";
   var sapporoColors=new[]{"赤","緑","青","白"};
   for(int n=1;n<=12;n++){role.Text=n.ToString();Need(LabelCatalog.ColorName(active,Current())==sapporoColors[(n-1)%4],"Sapporo cohort cycle");Need(LabelRenderer.Validate(active,Current())==null,"Sapporo cohort validation");}
   role.Text="5";CaptureLatest(folder,"cohort-sapporo");
   kinds.SelectedIndex=asahikawaCohort;person.Text="テキ シホウ";number.Text="1234567";
   var asahiExpect=new[]{"赤","緑","青","赤","青","白","赤","白","青","白"};
   for(int n=1;n<=10;n++){role.Text=n.ToString();Need(LabelCatalog.ColorName(active,Current())==asahiExpect[n-1],"Asahikawa map "+n);}
   role.Text="5";CaptureLatest(folder,"cohort-asahikawa");
   kinds.SelectedIndex=LatestRow(56);person.Text="チャン ティ ハー ザン";number.Text="21";
   Need(LabelCatalog.NameForPrint(LabelCatalog.Items[active],person.Text)=="ﾁｬﾝ ﾃｨ ﾊｰ ｻﾞﾝ","Halfwidth kana conversion");CaptureLatest(folder,"halfwidth-kana");
   Need(LabelCatalog.Items[0].NeedsNumber && LabelCatalog.Items[1].NeedsNumber && LabelCatalog.Items[2].NeedsNumber && LabelCatalog.Items[3].NeedsNumber,"Legacy codes hidden");
   kinds.SelectedIndex=0;digits.SelectedIndex=1;number.Text="Y12345";digits.SelectedIndex=0;Need(number.Text=="Y1234","Yotsuba 5 to 4 truncation");
   Need(preferences.Get(OffsetKey).X==0,"Unexpected test adjustment");
   Need(LabelCatalog.VisibleCustomerCount()==131 && LabelCatalog.Customers.Length==134 && LabelCatalog.Items.Length==233,"Catalog counts");
   File.WriteAllText(Path.Combine(folder,"latest-verification.txt"),"PASS: 131 visible / 134 slots / 233 formats; higashinaebo/ashita titles; mikado CompanyCodeName; Benkama cohort maps; GOOD NEWS surname; rinko single-line; yotsuba summer unchanged; deleted customers hidden; no print jobs sent.",Encoding.UTF8);Hide();
  }
 }
}
