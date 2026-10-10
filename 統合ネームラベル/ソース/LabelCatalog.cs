using System;
using System.Drawing;
using System.Collections.Generic;

namespace UnifiedLabels
{
    public sealed class LabelSpec
    {
        public int Customer;
        public string Format, Placement, Family, Heading, Note;
        public bool Small, White;
        public float MaximumFont, HeadingFont;
        public int SourceRow, NumberLength, PreviousSourceRow;
        public string FixedColor="", Reading="", HeaderFamily="", NumberPrefix="", Pending="", ColorCaption="区分";
        public string[] Groups=new string[0], Colors=new string[0], Departments;
        public bool SurnameOnly, SpaceSurname, StrictFont;
        public bool SurnameSizing, ColorFromDepartment, UnknownSize, IndentUpper;
        public float MiddleNudge;
        public string ColorGuide="", InputCaption="", SettingsIdentity="";
        // Legacy, Name, Surname, HeadingName, NumberName, SplitNumber
        public string Layout;
        public LabelSpec(int customer,string format,bool small,string layout,string family,string placement,string heading,string note,bool white)
        { Customer=customer; Format=format; Small=small; Layout=layout; Family=family; Placement=placement; Heading=heading; Note=note; White=white; }
        public bool NeedsNumber { get { return Layout=="Legacy" || Layout=="QR" || Layout=="NumberName" || Layout=="SplitNumber" || Layout=="NumberOnly" || Layout=="SizeNumber" || Layout=="PrefixName" || Layout=="DeptNumberName" || Layout=="CohortName" || Layout=="CompanyCodeName"; } }
        public bool NeedsName { get { return Layout!="DeptOnly" && Layout!="Unavailable" && Layout!="SplitNumber" && Layout!="Fixed" && Layout!="FixedSingle" && Layout!="NumberOnly" && Layout!="SizeNumber" && Layout!="Blank"; } }
    }
    public static partial class LabelCatalog
    {
        // Use the same style for measurement, baseline metrics and final rendering.
        public static FontStyle StyleFor(string family) {
            if(family==null) return FontStyle.Regular;
            string n=family.Normalize(System.Text.NormalizationForm.FormKC);
            if(n.StartsWith("HG丸ゴシック",StringComparison.OrdinalIgnoreCase)) return FontStyle.Bold;
            // MSP明朝 / MS PMincho (catalog + common Japanese spellings after FormKC).
            if(n.IndexOf("PMincho",StringComparison.OrdinalIgnoreCase)>=0
                || n.IndexOf("P明朝",StringComparison.Ordinal)>=0)
                return FontStyle.Bold;
            return FontStyle.Regular;
        }
        public static readonly string[] Customers=CreateCustomers();
        public static string[] DepartmentNames(int kind) {
            if(Items[kind].Departments!=null) return Items[kind].Departments;
            if(kind==3) return new string[]{"製造課 冷食","製造課 ハムソー","工務課","商品開発課","生産管理課","総務課","調達課","品質保証部"};
            if(kind==21) return new string[]{"製造課(加工)","製造課(食肉)"};
            if(kind==25 || kind==26) return new string[]{"と畜場","加工場"};
            return null;
        }
        public static readonly string[] ColorDepartments={"サラダ","仕分","トリミング","前洗浄","入荷・払出","計量","脱水洗浄","衛生","品質保証室","製造","事務","社長"};
        public static readonly string[] DepartmentColors={"青","青","緑","緑","緑","赤","赤","赤","青","白","白","白"};
        public static string[] ColorGroups(int kind) {
            return Items[kind].Groups;
        }
        /* Previous rules retained here as reference only.
        static string[] PreviousColorGroups(int kind) {
            int customer=Items[kind].Customer;
            if(customer==10) return ColorDepartments;
            if(customer==14) return new string[]{"日中","夜間"};
            if(customer==16) return new string[]{"社員","パート"};
            if(customer==18 && Items[kind].Layout=="Name") return new string[]{"職員","派遣"};
            return new string[0];
        }
        public static string[] GroupColors(int kind) {
            int customer=Items[kind].Customer;
            if(customer==10) return DepartmentColors;
            if(customer==14) return new string[]{"緑","青"};
            if(customer==16) return new string[]{"青","白"};
            if(customer==18 && Items[kind].Layout=="Name") return new string[]{"青","緑"};
            return new string[0];
        }
        */
        public static string[] GroupColors(int kind) { return Items[kind].Colors; }
        public static string SelectedGroup(int kind,LabelData d) {
            if(Items[kind].ColorFromDepartment) return d.Header;
            if(Items[kind].Layout=="CohortName") { int n; return Int32.TryParse(d.Role,out n) && n>0?Items[kind].Groups[(n-1)%4]:""; }
            return d.ColorDepartment;
        }
        public static string ColorName(int kind,LabelData d)
        {
            if(Items[kind].Layout=="DeptOnly" && Items[kind].FixedColor=="緑？")return d.Header.Replace(" ","").Replace("　","")=="鮮魚"?"緑？":"先方から指定";
            if(Items[kind].FixedColor!="") return Items[kind].FixedColor;
            string[] groups=ColorGroups(kind);
            if(groups.Length>0) { int n=Array.IndexOf(groups,SelectedGroup(kind,d)); return n<0?"区分未選択":GroupColors(kind)[n]; }
            if(kind==20 || kind==23 || kind==24) return "緑";
            return Items[kind].White?"白":"先方から指定";
        }
        public static Color Swatch(string name) { name=name.Replace("?","").Replace("？","").Trim();return name=="青"?Color.LightSkyBlue:name=="緑"?Color.LightGreen:name=="赤"?Color.LightCoral:name=="黄"?Color.LightYellow:name=="オレンジ"?Color.PeachPuff:Color.White; }
        const string R="HG丸ｺﾞｼｯｸM-PRO", P="MS PGothic";
        public static readonly LabelSpec[] Items=CreateItems();
        public static int[] ForCustomer(int customer)
        { List<int> found=new List<int>(); for(int i=0;i<Items.Length;i++) if(Items[i].Customer==customer) found.Add(i); return found.ToArray(); }
        public static Size Millimetres(int kind) { return Items[kind].Small?new Size(50,24):new Size(80,50); }
        public static string Validate(int kind,LabelData d)
        {
            LabelSpec s=Items[kind];
            if(s.Pending!="") return "要確認："+s.Pending;
            if(s.Layout=="Blank") return "無地ラベルです。印字せず、ガイドどおり切り分けて使用してください。";
            if(s.Layout=="QR") return LabelRenderer.Validate(3,d);
            if(DepartmentNames(kind)!=null && String.IsNullOrWhiteSpace(d.Header)) return "部署を選択してください。";
            string[] groups=ColorGroups(kind);
            if(groups.Length>0 && Array.IndexOf(groups,SelectedGroup(kind,d))<0) return s.Layout=="CohortName"?"期生を半角数字で入力してください。":"区分を選択してラベルの色を確認してください。";
            if(s.NeedsName && String.IsNullOrWhiteSpace(d.Person)) return s.Layout=="Surname"?"苗字を入力してください。":"名前を入力してください。";
            if(s.Layout=="DeptOnly" && String.IsNullOrWhiteSpace(d.Header))return "印字する部署名を入力してください。";
            if(s.Layout=="DeptFurigana" && String.IsNullOrWhiteSpace(d.Role))return "苗字のふりがなをひらがなで入力してください。";
            if(s.Layout=="DeptFurigana" && s.Departments==null && String.IsNullOrWhiteSpace(d.Header))return "部署名を入力してください。";
            if(s.Layout=="StarRoman") {
                int stars;if(!Int32.TryParse(d.Role,out stars)||stars<0||stars>99)return "☆の数を0～99で入力してください。";
                if(String.IsNullOrWhiteSpace(d.Header))return "苗字のローマ字を入力してください。";
            }
            if(s.Layout=="SplitCode" && !System.Text.RegularExpressions.Regex.IsMatch(d.Person.Trim(),@"^[A-Za-z]-[0-9]+$"))return "記号と番号を半角で入力してください（例：M-23）。";
            if(s.Layout=="MixedName" && !HasEmphasis(d)) return "名前の大きくする部分を選択し、「選択部分を大きく」を押してください。";
            if(s.NeedsNumber) {
                if(String.IsNullOrWhiteSpace(d.Number)) return "管理番号を入力してください。";
                foreach(char c in d.Number) if(c<'0'||c>'9') return "管理番号は半角数字で入力してください。";
                if(s.NumberLength>0 && d.Number.Length!=s.NumberLength) return "番号は半角数字"+s.NumberLength+"桁で入力してください。";
            }
            int numeric;
            if(s.Layout=="PrefixName" && (!Int32.TryParse(d.Number,out numeric) || numeric<1 || numeric>50)) return "丸数字の番号は1～50で入力してください。";
            if(s.Layout=="CohortName" && (!Int32.TryParse(d.Role,out numeric) || numeric<1 || numeric>999)) return "期生は1～999で入力してください。";
            if(s.Layout=="DeptNumberName" && String.IsNullOrWhiteSpace(d.Header)) return "部署を入力してください。";
            if(s.Layout=="SizeNumber" && d.ColorDepartment.StartsWith("その他") && !System.Text.RegularExpressions.Regex.IsMatch(d.Header,@"^[3-9]\d*L$|^[12]\d+L$")) return "サイズを半角で入力してください（例：10L）。";
            if((s.Layout=="InitialName" || s.Layout=="InitialMixed") && d.Role.Replace('　',' ').Trim().Length>1) return "追加する名前は1文字で入力してください。";
            if(s.Layout=="NameParts" && d.Person.Replace('　',' ').Trim().IndexOf(' ')<0) return "苗字と名前の間に半角スペースを入れてください。";
            if(s.StrictFont) {
                using(Bitmap b=new Bitmap(1,1)) using(Graphics g=Graphics.FromImage(b)) {
                    string text=NameForPrint(s,d.Person); Size mm=Millimetres(kind);
                    RectangleF area=s.Layout=="DeptName"?Mm(1,9,48,14):Mm(1,1,mm.Width-2,mm.Height-2);
                    if(Fit(g,text,s.Family,area,s.MaximumFont)<s.MaximumFont) return "固定"+s.MaximumFont+"ptでは名前が枠に収まりません。表記を確認して短くしてください。";
                }
            }
            return null;
        }
        public static string NameForPrint(LabelSpec s,string value)
        {
            string name=value.Replace('　',' ').Trim();
            if(s.Layout=="Surname" || s.SurnameOnly) { int space=name.IndexOf(' '); if(space>=0) name=name.Substring(0,space); }
            if(s.SpaceSurname && name.Length==2) name=name.Substring(0,1)+" "+name.Substring(1,1);
            if(s.Reading=="半角カタカナ")name=ToHalfWidthKana(name);
            return name;
        }
        static readonly Dictionary<string,string> HalfWidthKana=CreateKanaMap();
        static Dictionary<string,string> CreateKanaMap(){var map=new Dictionary<string,string>();for(int c=0xFF61;c<=0xFF9F;c++){string half=Char.ConvertFromUtf32(c);map[half.Normalize(System.Text.NormalizationForm.FormKC)]=half;foreach(int mark in new[]{0xFF9E,0xFF9F}){string pair=half+Char.ConvertFromUtf32(mark);string full=pair.Normalize(System.Text.NormalizationForm.FormKC);if(full.Length==1)map[full]=pair;}}return map;}
        public static string ToHalfWidthKana(string value){var result=new System.Text.StringBuilder();foreach(char c in value.Normalize(System.Text.NormalizationForm.FormKC)){string converted;result.Append(HalfWidthKana.TryGetValue(c.ToString(),out converted)?converted:c.ToString());}return result.ToString();}
        public static string Circled(int n) { return n>=1 && n<=20?Char.ConvertFromUtf32(0x2460+n-1):n<=35 && n>=21?Char.ConvertFromUtf32(0x3251+n-21):n<=50 && n>=36?Char.ConvertFromUtf32(0x32B1+n-36):"("+n+")"; }
        public static bool HasEmphasis(LabelData d) {
            return d.EmphasisStart>=0 && d.EmphasisLength>0 && d.EmphasisStart<=d.Person.Length-d.EmphasisLength && !String.IsNullOrWhiteSpace(d.Person.Substring(d.EmphasisStart,d.EmphasisLength));
        }
        // Measure and draw the exact same runs; shrink all runs by the same factor.
        public static void MixedLine(Graphics g,LabelData d,string family,RectangleF area) { MixedLine(g,d,family,area,24,42); }
        public static void MixedLine(Graphics g,LabelData d,string family,RectangleF area,float normal,float emphasized) {
            string name=d.Person.Replace('　',' ');
            bool selected=HasEmphasis(d);
            string[] runs=selected?new string[]{name.Substring(0,d.EmphasisStart),name.Substring(d.EmphasisStart,d.EmphasisLength),name.Substring(d.EmphasisStart+d.EmphasisLength)}:new string[]{name,"",""};
            float[] points={normal,emphasized,normal};
            using(StringFormat format=new StringFormat(StringFormat.GenericTypographic)) {
                format.FormatFlags|=StringFormatFlags.NoWrap|StringFormatFlags.MeasureTrailingSpaces;
                float width=0,height=0,ascent=0;
                for(int i=0;i<3;i++) if(runs[i].Length>0) using(Font f=new Font(family,points[i]*100f/72f,StyleFor(family),GraphicsUnit.Pixel)) {
                    width+=g.MeasureString(runs[i],f,new PointF(),format).Width;
                    height=Math.Max(height,f.GetHeight(g));
                    ascent=Math.Max(ascent,f.Size*f.FontFamily.GetCellAscent(StyleFor(family))/f.FontFamily.GetEmHeight(StyleFor(family)));
                }
                if(width==0 || height==0) return;
                float scale=Math.Min(1,Math.Min(area.Width/width,area.Height/height));
                float x=area.Left+(area.Width-width*scale)/2, baseline=area.Top+(area.Height-height*scale)/2+ascent*scale;
                for(int i=0;i<3;i++) if(runs[i].Length>0) using(Font f=new Font(family,points[i]*scale*100f/72f,StyleFor(family),GraphicsUnit.Pixel)) {
                    float a=f.Size*f.FontFamily.GetCellAscent(StyleFor(family))/f.FontFamily.GetEmHeight(StyleFor(family));
                    g.DrawString(runs[i],f,Brushes.Black,new PointF(x,baseline-a),format);
                    x+=g.MeasureString(runs[i],f,new PointF(),format).Width;
                }
            }
        }
        // Fit against both width and height, in 1/100-inch drawing units.
        public static float Fit(Graphics g,string text,string family,RectangleF area,float maximum)
        {
            using(StringFormat format=new StringFormat(StringFormat.GenericTypographic)) {
                format.FormatFlags|=StringFormatFlags.NoWrap;
                using(Font maximumFont=new Font(family,maximum*100f/72f,StyleFor(family),GraphicsUnit.Pixel)) {
                    SizeF maximumSize=g.MeasureString(text.Length==0?"国":text,maximumFont,new PointF(0,0),format);
                    if(maximumSize.Width<=area.Width && maximumSize.Height<=area.Height) return maximum;
                }
                float low=1,high=maximum;
                for(int n=0;n<20;n++) {
                    float p=(low+high)/2;
                    using(Font f=new Font(family,p*100f/72f,StyleFor(family),GraphicsUnit.Pixel)) {
                        SizeF measured=g.MeasureString(text.Length==0?"国":text,f,new PointF(0,0),format);
                        if(measured.Width<=area.Width && measured.Height<=area.Height) low=p; else high=p;
                    }
                }
                return (float)Math.Floor(low*4)/4;
            }
        }
        static RectangleF Mm(float x,float y,float w,float h) { return new RectangleF(x/0.254f,y/0.254f,w/0.254f,h/0.254f); }
        // Large 2-row (上下2段) — match ★★ネーム作成管理用.xls「大汎用２段」系:
        // tape width 50mm split into two equal columns (≈25mm each), text rotation -90° (BIFF 180).
        // After mapping columns → app Y bands: upper center 12.5mm, lower center 37.5mm.
        const float LargeTwoRowUpperYMm=0f, LargeTwoRowUpperHeightMm=25f;
        const float LargeTwoRowLowerYMm=25f, LargeTwoRowLowerHeightMm=25f;
        // Excel 弁釜列幅は mm ではなく 8.5+13.38=21.88 → 実寸50mm。上段≈19.42 / 下段≈30.58。
        // Customer 72 only (kinds 129/130/222). 大汎用２段の25+25は未変更。描画は上段下揃え・下段上揃え。
        const float BenGamaUpperHeightMm=19.42f, BenGamaLowerYMm=19.42f, BenGamaLowerHeightMm=30.58f;
        static void TwoRowBands(LabelSpec s,Size mm,out RectangleF upper,out RectangleF lower) {
            if(s.Small) { upper=Mm(1,2,mm.Width-2,7); lower=Mm(1,9,mm.Width-2,14); }
            else if(s.Customer==72) {
                upper=Mm(1,0,mm.Width-2,BenGamaUpperHeightMm);
                lower=Mm(1,BenGamaLowerYMm,mm.Width-2,BenGamaLowerHeightMm);
            }
            else { upper=Mm(1,LargeTwoRowUpperYMm,mm.Width-2,LargeTwoRowUpperHeightMm); lower=Mm(1,LargeTwoRowLowerYMm,mm.Width-2,LargeTwoRowLowerHeightMm); }
        }
        public static void Line(Graphics g,string text,string family,RectangleF area,float maximum)
        { Line(g,text,family,area,maximum,false,StringAlignment.Center); }
        public static void Line(Graphics g,string text,string family,RectangleF area,float maximum,bool strict)
        { Line(g,text,family,area,maximum,strict,StringAlignment.Center); }
        public static void Line(Graphics g,string text,string family,RectangleF area,float maximum,bool strict,StringAlignment align)
        { Line(g,text,family,area,maximum,strict,align,StringAlignment.Center); }
        public static void Line(Graphics g,string text,string family,RectangleF area,float maximum,bool strict,StringAlignment align,StringAlignment lineAlign)
        {
            if(String.IsNullOrWhiteSpace(text)) return;
            float size=strict?maximum:Fit(g,text,family,area,maximum);
            using(Font f=new Font(family,size*100f/72f,StyleFor(family),GraphicsUnit.Pixel))
            using(StringFormat format=new StringFormat(StringFormat.GenericTypographic)) {
                format.FormatFlags|=StringFormatFlags.NoWrap;
                format.Alignment=align; format.LineAlignment=lineAlign;
                g.DrawString(text,f,Brushes.Black,area,format);
            }
        }
        static RectangleF Nudge(RectangleF area,float mm) {
            if(mm<=0) return area;
            float dx=mm/0.254f;
            area.X+=dx; area.Width=Math.Max(1,area.Width-dx); return area;
        }
        public static void Draw(Graphics g,int kind,LabelData d)
        {
            LabelSpec s=Items[kind]; Size mm=Millimetres(kind); string name=NameForPrint(s,d.Person);
            if(s.Pending!="") { Line(g,"仕様確認中",P,Mm(1,1,mm.Width-2,mm.Height-2),16); return; }
            if(s.Layout=="Blank") return;
            if(s.Layout=="QR") { LabelRenderer.Draw(g,3,d); return; }
            if(s.Layout=="MixedName") { MixedLine(g,d,s.Family,Mm(1,1,78,48)); return; }
            if(s.Layout=="FixedSingle") { Line(g,s.Heading,s.Family,Mm(1,1,mm.Width-2,mm.Height-2),s.MaximumFont); return; }
            if(s.Layout=="Fixed") {
                string[] lines=s.Heading.Split('\n');
                // Excel「大フォークリフト」: two equal columns → same 25/25 bands as TwoRowBands.
                Line(g,lines[0],s.Family,Mm(1,LargeTwoRowUpperYMm,78,LargeTwoRowUpperHeightMm),30);
                Line(g,lines[1],s.Family,Mm(1,LargeTwoRowLowerYMm,78,LargeTwoRowLowerHeightMm),28); return;
            }
            float maximum=s.MaximumFont>0?s.MaximumFont:(s.Layout=="Surname" && !s.SurnameOnly?36:GenericSize(s.Small,name));
            if(s.SurnameSizing)maximum=!String.IsNullOrWhiteSpace(d.Role)?56:name.Length<=2?80:64;
            float heading=s.HeadingFont>0?s.HeadingFont:(s.Small?18:26);
            StringAlignment upperAlign=s.IndentUpper?StringAlignment.Near:StringAlignment.Center;
            RectangleF upper, lower; TwoRowBands(s,mm,out upper,out lower);
            if(s.Layout=="DeptOnly"){Line(g,d.Header.Replace('　',' '),s.Family,Mm(1,1,mm.Width-2,mm.Height-2),maximum);return;}
            if(s.Layout=="DeptRoleName") {
                Line(g,d.Header,s.Family,Mm(1,3,78,10),heading,false,upperAlign);
                Line(g,d.Role,s.Family,Nudge(Mm(1,15,78,10),s.MiddleNudge),24,false,s.MiddleNudge>0?StringAlignment.Near:StringAlignment.Center);
                Line(g,name,s.Family,Mm(1,27,78,22),maximum);return;
            }
            if(s.Layout=="DeptFurigana") {
                Line(g,d.Header,s.Family,Mm(1,2,78,10),heading,false,upperAlign);
                Line(g,d.Role,s.Family,Mm(1,14,78,10),28);
                Line(g,name,s.Family,Mm(1,25,78,24),maximum);return;
            }
            if(s.Layout=="StarRoman") {
                int stars;if(!Int32.TryParse(d.Role,out stars))stars=0;stars=Math.Max(0,Math.Min(99,stars));
                if(stars>0)Line(g,new string('☆',stars),s.Family,Mm(1,2,78,10),26);
                Line(g,name,s.Family,Mm(1,stars>0?13:3,78,stars>0?22:30),50);
                Line(g,d.Header,s.Family,Mm(1,36,78,12),26);return;
            }
            if(s.Layout=="OptionalHeading" && !String.IsNullOrWhiteSpace(d.Header)){Line(g,d.Header,s.Family,upper,heading,false,upperAlign);Line(g,name,s.Family,lower,maximum);return;}
            if(s.Layout=="SplitCode") {Line(g,name,s.Family,Mm(1,1,23,22),maximum);Line(g,name,s.Family,Mm(26,1,23,22),maximum);return;}
            if(s.Layout=="DeptName" || s.Layout=="DeptNumberName") {
                if(s.Layout=="DeptNumberName") {
                    float uy=s.Small?2:LargeTwoRowUpperYMm, uh=s.Small?7:LargeTwoRowUpperHeightMm;
                    Line(g,d.Header.Replace('　',' '),s.Family,Mm(1,uy,(mm.Width-3)/2f,uh),heading,false,upperAlign);
                    Line(g,d.Number,s.Family,Mm(mm.Width/2f+0.5f,uy,(mm.Width-3)/2f,uh),heading,false,upperAlign);
                } else Line(g,d.Header.Replace('　',' '),s.Family,upper,heading,false,upperAlign);
                Line(g,name,s.Family,lower,maximum,s.StrictFont); return;
            }
            if(s.Layout=="RoleName" && !String.IsNullOrWhiteSpace(d.Role)) { Line(g,d.Role,s.Family,upper,heading,false,upperAlign); Line(g,name,s.Family,lower,maximum); return; }
            if(s.Layout=="RepeatName" || s.Layout=="NameParts") {
                string first=name,second=name;
                if(s.Layout=="NameParts") { int space=name.IndexOf(' '); first=space<0?name:name.Substring(0,space); second=space<0?"":name.Substring(space+1).Trim(); }
                if(s.Small) {
                    float height=(mm.Height-4)/2f;
                    Line(g,first,s.Family,Mm(1,2,mm.Width-2,height),s.Layout=="RepeatName"?maximum:GenericSize(s.Small,first));
                    Line(g,second,s.Family,Mm(1,mm.Height/2f+1,mm.Width-2,height),s.Layout=="RepeatName"?maximum:GenericSize(s.Small,second));
                } else {
                    // Excel equal 25mm columns (e.g. 大コトメンフーズ実習生帽子).
                    Line(g,first,s.Family,Mm(1,LargeTwoRowUpperYMm,mm.Width-2,LargeTwoRowUpperHeightMm),s.Layout=="RepeatName"?maximum:GenericSize(s.Small,first));
                    Line(g,second,s.Family,Mm(1,LargeTwoRowLowerYMm,mm.Width-2,LargeTwoRowLowerHeightMm),s.Layout=="RepeatName"?maximum:GenericSize(s.Small,second));
                }
                return;
            }
            if(s.Layout=="InitialName" && !String.IsNullOrWhiteSpace(d.Role)) name+="("+d.Role.Trim()+")";
            if(s.Layout=="InitialMixed") {
                if(String.IsNullOrWhiteSpace(d.Role)) Line(g,name,s.Family,Mm(1,1,mm.Width-2,mm.Height-2),104);
                else MixedLine(g,new LabelData {Person=name+d.Role.Trim(),EmphasisStart=name.Length,EmphasisLength=d.Role.Trim().Length},s.Family,Mm(1,1,mm.Width-2,mm.Height-2),80,54);
                return;
            }
            if(s.Layout=="NumberOnly") name=d.Number;
            if(s.Layout=="SizeNumber") name=(d.ColorDepartment.StartsWith("その他")?d.Header:d.ColorDepartment)+" "+d.Number;
            int value;
            if(s.Layout=="PrefixName") name=(Int32.TryParse(d.Number,out value)?Circled(value):d.Number)+(s.Reading=="半角カタカナ"?" ":"")+name;
            if(s.Layout=="CohortName") name=(Int32.TryParse(d.Role,out value)?Circled(value):d.Role)+" "+name;
            if(s.Layout=="HeadingName" || s.Layout=="NumberName" || s.Layout=="CohortName" || s.Layout=="CompanyCodeName") {
                string top=s.Layout=="HeadingName"?s.Heading:s.NumberPrefix+d.Number;
                if(s.Layout=="CompanyCodeName") top=s.Heading+" "+(d.Number.Length==5?d.Number.Substring(0,1)+"-"+d.Number.Substring(1,2)+"-"+d.Number.Substring(3,2):d.Number);
                if(s.Customer==72) {
                    // 弁釜 Excel: 上段下揃え・下段上揃え（帯内の縦中央ではない）。
                    Line(g,top,s.HeaderFamily!=""?s.HeaderFamily:s.Family,upper,s.HeadingFont>0?s.HeadingFont:18,false,upperAlign,StringAlignment.Far);
                    Line(g,name,s.Family,lower,maximum,s.StrictFont,StringAlignment.Center,StringAlignment.Near);
                } else {
                    Line(g,top,s.HeaderFamily!=""?s.HeaderFamily:s.Family,upper,s.HeadingFont>0?s.HeadingFont:s.Layout=="HeadingName"?12:18,false,upperAlign);
                    Line(g,name,s.Family,lower,maximum,s.StrictFont);
                }
            } else if(s.Layout=="SplitNumber") {
                Line(g,d.Number,s.Family,Mm(1,1,23,22),36);
                Line(g,d.Number,s.Family,Mm(26,1,23,22),36);
            } else {
                Line(g,name,s.Family,Mm(1,1,mm.Width-2,mm.Height-2),maximum,s.StrictFont,s.IndentUpper && s.Layout=="Surname"?StringAlignment.Near:StringAlignment.Center);
            }
        }
        public static float GenericSize(bool small,string name) {
            int n=name.Replace(" ","").Replace("　","").Length;
            return small?(n<=4?26:n==5?24:20):(n<=4?48:n==5?38:30);
        }
    }
}
