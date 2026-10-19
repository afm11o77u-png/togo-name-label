using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.Xml;
namespace UnifiedLabels {
 public sealed class PrintOffset { public decimal X,Y; public PrintOffset() {} public PrintOffset(decimal x,decimal y) {X=x;Y=y;} }
 public sealed class UserSettings {
  // 印字位置調整 UI（OffsetDialog）と同じ許容範囲。範囲外は読込み失敗として退避する。
  const decimal OffsetMin=-10m, OffsetMax=10m;
  readonly string path;
  public readonly List<string> Favorites=new List<string>(), Recent=new List<string>();
  readonly Dictionary<string,PrintOffset> offsets=new Dictionary<string,PrintOffset>();
  /// <summary>既存設定の内容不正で退避し、空の初期設定で続行したとき true。ファイル未作成時は false。</summary>
  public bool RecoveredFromCorrupt { get; private set; }
  /// <summary>退避した settings.broken-*.xml のフルパス。通常起動では null。</summary>
  public string BrokenBackupPath { get; private set; }
  public UserSettings(string file) {
   path=file;
   if(!File.Exists(path)) return; // 未作成は従来どおり静かに初期設定
   try { Load(); }
   catch {
    string backup=QuarantineBroken();
    Favorites.Clear(); Recent.Clear(); offsets.Clear();
    BrokenBackupPath=backup;
    RecoveredFromCorrupt=true;
   }
  }
  void Load() {
   XmlDocument d=new XmlDocument();
   d.Load(path);
   foreach(XmlNode n in d.SelectNodes("/settings/favorite")) Favorites.Add(n.InnerText);
   foreach(XmlNode n in d.SelectNodes("/settings/recent")) Recent.Add(n.InnerText);
   foreach(XmlElement n in d.SelectNodes("/settings/offset")) {
    decimal x=Decimal.Parse(n.GetAttribute("x"),CultureInfo.InvariantCulture);
    decimal y=Decimal.Parse(n.GetAttribute("y"),CultureInfo.InvariantCulture);
    if(x<OffsetMin||x>OffsetMax||y<OffsetMin||y>OffsetMax)
     throw new FormatException("offset out of range");
    offsets[n.GetAttribute("key")]=new PrintOffset(x,y);
   }
  }
  /// <summary>壊れた settings.xml を同フォルダへ日時付きで退避（既存退避は上書きしない）。</summary>
  string QuarantineBroken() {
   string dir=Path.GetDirectoryName(Path.GetFullPath(path));
   if(string.IsNullOrEmpty(dir)) dir=".";
   Directory.CreateDirectory(dir);
   string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss");
   string dest=Path.Combine(dir,"settings.broken-"+stamp+".xml");
   int n=0;
   while(File.Exists(dest)) {
    n++;
    dest=Path.Combine(dir,"settings.broken-"+stamp+"-"+n+".xml");
   }
   File.Move(path,dest);
   return dest;
  }
  public PrintOffset Get(string key) {PrintOffset p;return offsets.TryGetValue(key,out p)?new PrintOffset(p.X,p.Y):new PrintOffset();}
  public void Set(string key,PrintOffset p) {PrintOffset old=Get(key);bool had=offsets.ContainsKey(key); offsets[key]=new PrintOffset(p.X,p.Y);try {Save();} catch {if(had) offsets[key]=old;else offsets.Remove(key);throw;} }
  public void Visit(string name) {Recent.Remove(name);Recent.Insert(0,name);if(Recent.Count>10) Recent.RemoveRange(10,Recent.Count-10);Save();}
  public void Toggle(string name) {var old=new List<string>(Favorites);if(!Favorites.Remove(name))Favorites.Add(name);try{Save();}catch{Favorites.Clear();Favorites.AddRange(old);throw;}}
  public void RenameCustomer(string previous,string current){bool changed=false;foreach(List<string> items in new[]{Favorites,Recent}){int i=items.IndexOf(previous);if(i<0)continue;items[i]=current;changed=true;}if(changed)Save();}
  public void Save() {Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));XmlDocument d=new XmlDocument();XmlElement root=d.CreateElement("settings");d.AppendChild(root);foreach(string s in Favorites) {XmlElement n=d.CreateElement("favorite");n.InnerText=s;root.AppendChild(n);}foreach(string s in Recent) {XmlElement n=d.CreateElement("recent");n.InnerText=s;root.AppendChild(n);}foreach(var pair in offsets) {XmlElement n=d.CreateElement("offset");n.SetAttribute("key",pair.Key);n.SetAttribute("x",pair.Value.X.ToString(CultureInfo.InvariantCulture));n.SetAttribute("y",pair.Value.Y.ToString(CultureInfo.InvariantCulture));root.AppendChild(n);}string tmp=path+".tmp";d.Save(tmp);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}
  static string Compact(string value){return value.Normalize(NormalizationForm.FormKC).Replace(" ","").Replace("\u3000","");}
  /// <summary>顧客名（および読み）に query が含まれるか。空 query は全件一致。全半角・かな・英字大小を区別しない。</summary>
  public static bool Matches(string name,string query) {
   if(string.IsNullOrEmpty(query)) return true;
   CompareInfo comparer=CultureInfo.GetCultureInfo("ja-JP").CompareInfo;
   const CompareOptions opts=CompareOptions.IgnoreCase|CompareOptions.IgnoreWidth|CompareOptions.IgnoreKanaType;
   string needle=Compact(query);
   if(needle.Length==0) return true;
   if(comparer.IndexOf(Compact(name),needle,opts)>=0) return true;
   return comparer.IndexOf(Compact(LabelCatalog.CustomerReading(name)),needle,opts)>=0;
  }
 }
 public static partial class LabelCatalog {
  static readonly Dictionary<string,string> CustomerReadings=new Dictionary<string,string> {
{"よつ葉乳業㈱十勝工場","よつばにゅうぎょうとかちこうじょう"},
{"だるま食品","だるましょくひん"},
{"Umios Hokkaido 札幌工場","うみおすほっかいどうさっぽろこうじょう"},
{"DHC夕張工場","でぃーえいちしーゆうばりこうじょう"},
{"アンデルセン","あんでるせん"},
{"コープフーズ旭川","こーぷふーずあさひかわ"},
{"コープフーズ釧路","こーぷふーずくしろ"},
{"コープフーズ札幌","こーぷふーずさっぽろ"},
{"スリーコード","すりーこーど"},
{"ダイイチ惣菜センター","だいいちそうざいせんたー"},
{"トップラン","とっぷらん"},
{"コープフーズ函館 惣菜","こーぷふーずはこだてそうざい"},
{"ドリームファクトリー惣菜","どりーむふぁくとりーそうざい"},
{"Umios Hokkaido 十勝工場","うみおすほっかいどうとかちこうじょう"},
{"Umios Hokkaido 名寄工場","うみおすほっかいどうなよろこうじょう"},
{"ふか河","ふかがわ"},
{"ホッカン","ほっかん"},
{"ほんま","ほんま"},
{"ミルクの郷","みるくのさと"},
{"めむろフーズ","めむろふーず"},
{"コープフーズ帯広","こーぷふーずおびひろ"},
{"コープフーズ苫小牧","こーぷふーずとまこまい"},
{"コープフーズ / 石狩食品工場","こーぷふーずいしかりしょくひんこうじょう"},
{"あすかHOUSE東苗穂","あすかはうすひがしなえぼ"},
{"シティデリカセンター","してぃでりかせんたー"},
{"アオキ","あおき"},
{"㈱アグリテック真狩","あぐりてっくまっかり"},
{"エスプリ","えすぷり"},
{"キユーピータマゴ","きゆーぴーたまご"},
{"コトメンフーズ","ことめんふーず"},
{"サッポロライス","さっぽろらいす"},
{"タケダ海産","たけだかいさん"},
{"ニチレイ食品","にちれいしょくひん"},
{"ヒナタフーズ","ひなたふーず"},
{"フジネット","ふじねっと"},
{"ホクレン食肉加工工場","ほくれんしょくにくかこうこうじょう"},
{"北海道畜産公社","ほっかいどうちくさんこうしゃ"},
{"ホワイトファーム","ほわいとふぁーむ"},
{"マザアス","まざあす"},
{"マルゼン食品","まるぜんしょくひん"},
{"ミートピア","みーとぴあ"},
{"モリタン岩見沢","もりたんいわみざわ"},
{"モリタン紋別","もりたんもんべつ"},
{"ユーハイム","ゆーはいむ"},
{"よつ葉物流㈱","よつばぶつりゅう"},
{"ラッキー生鮮センター","らっきーせいせんせんたー"},
{"ラッキーデリカセンター","らっきーでりかせんたー"},
{"ワタリ","わたり"},
{"わらく堂","わらくどう"},
{"函館みかど","はこだてみかど"},
{"王子サーモン","おうじさーもん"},
{"丸藤水産","まるふじすいさん"},
{"釧路丸水","くしろまるすい"},
{"合食","ごうしょく"},
{"札幌禎心会病院","さっぽろていしんかいびょういん"},
{"札幌日清","さっぽろにっしん"},
{"三ッ輪物流㈱","みつわぶつりゅう"},
{"三栄食品","さんえいしょくひん"},
{"山川牧場","やまかわぼくじょう"},
{"山本忠信商店","やまもとただのぶしょうてん"},
{"首都圏日立物流サービス","しゅとけんひたちぶつりゅうさーびす"},
{"十勝製餡","とかちせいあん"},
{"出塚水産","でづかすいさん"},
{"小川商店","おがわしょうてん"},
{"小林製麺","こばやしせいめん"},
{"新札幌乳業","しんさっぽろにゅうぎょう"},
{"新谷","しんや"},
{"深川未来ファーム","ふかがわみらいふぁーむ"},
{"西華","せいか"},
{"石狩市公務サービス","いしかりしこうむさーびす"},
{"日綜中垣病院","にっそうなかがきびょういん"},
{"日通帯広運輸","にっつうおびひろうんゆ"},
{"日通稚内","にっつうわっかない"},
{"函館酪農公社","はこだてらくのうこうしゃ"},
{"布目","ぬのめ"},
{"弁釜本社/札幌/八軒/旭川/函館/ローズD/","べんかま"},
{"よつ葉東京工場","よつばとうきょうこうじょう"},
{"よつ葉根釧/宗谷工場","よつばこんせんそうやこうじょう"},
{"よつ葉北見工場","よつばきたみこうじょう"},
{"十勝大福本舗㈱","とかちだいふくほんぽ"},
{"日鉄FT","にってつえふてぃー"},
{"北のアトリエ","きたのあとりえ"},
{"札幌/白石/岩見沢/宮の森明日佳","さっぽろしろいしいわみざわみやのもりあすか"},
{"柳月","りゅうげつ"},
{"鱗幸食品","りんこうしょくひん"},
{"西山製麺/西山食品","にしやませいめんにしやましょくひん"},
{"有楽製菓","ゆうらくせいか"},
{"マルキチ","まるきち"},
{"BN企画","びーえぬきかく"},
{"パン工房ASUKA","ぱんこうぼうあすか"},
{"あすかベーカリー","あすかべーかりー"},
{"北海道サービス","ほっかいどうさーびす"},
{"あすかHOUSE","あすかはうす"},
{"北海道保証牛乳","ほっかいどうほしょうぎゅうにゅう"},
{"らくらダイニング","らくらだいにんぐ"},
{"日総道立緑ヶ丘病院","にっそうどうりつみどりがおかびょういん"},
{"北海道サンプラス","ほっかいどうさんぷらす"},
{"丸善 納谷商店","まるぜんなやしょうてん"},
{"札幌ヤマグチ","さっぽろやまぐち"},
{"文明堂","ぶんめいどう"},
{"トテッポ工房","とてっぽこうぼう"},
{"日総中垣病院","にっそうなかがきびょういん"},
{"日総アメニティ本別","にっそうあめにてぃほんべつ"},
{"自然農園","しぜんのうえん"},
{"モリタン栗沢","もりたんくりさわ"},
{"堀川","ほりかわ"},
{"北海道醤油","ほっかいどうしょうゆ"},
{"北海道フード","ほっかいどうふーど"},
{"倉島乳業","くらしまにゅうぎょう"},
{"北海道酪農公社","ほっかいどうらくのうこうしゃ"},
{"DADACA千歳工場/夕張工場","だだかちとせこうじょうゆうばりこうじょう"},
{"タンゼンテクニカルプロダクト","たんぜんてくにかるぷろだくと"},
{"ディカス","でぃかす"},
{"アドワン","あどわん"},
{"マルヒロ太田食品","まるひろおおたしょくひん"},
{"桜テラス平岡","さくらてらすひらおか"},
{"ホクショク","ほくしょく"},
{"川島食品","かわしましょくひん"},
{"GOOD NEWS","ぐっどにゅーす"},
{"とかちアークキッチン","とかちあーくきっちん"},
{"Kコンフェクト","けーこんふぇくと"},
{"千秋庵/千秋庵東雁来工場","せんしゅうあん"},
{"北海道種鶏農場","ほっかいどうしゅけいのうじょう"},
{"ケラピリカ","けらぴりか"},
{"藤森商会","ふじもりしょうかい"},
{"北の食品","きたのしょくひん"},
{"フレッシュフーズ","ふれっしゅふーず"},
{"エフケー","えふけー"},
{"ホクレン鶏卵流通センター札幌","ほくれんけいらんりゅうつうせんたーさっぽろ"},
{"ホクレン鶏卵流通センター旭川","ほくれんけいらんりゅうつうせんたーあさひかわ"},
{"札幌健診センター","さっぽろけんしんせんたー"},
{"札幌中一","さっぽろなかいち"},
{"エア・ウォーター・アグリ 更別工場/札幌工場","えあうぉーたーあぐりせつべつこうじょうさっぽろこうじょう"},
{"十勝ミートセンター","とかちみーとせんたー"},
{"ベル・クール研究所","べるくーるけんきゅうじょ"}
  };
  public static string CustomerReading(string name){string reading;return CustomerReadings.TryGetValue(name,out reading)?reading:name;}
  public static string[] SortedCustomers(){var names=(string[])Customers.Clone();var comparer=CultureInfo.GetCultureInfo("ja-JP").CompareInfo;Array.Sort(names,delegate(string a,string b){int n=comparer.Compare(CustomerReading(a),CustomerReading(b),CompareOptions.IgnoreWidth|CompareOptions.IgnoreKanaType);return n!=0?n:comparer.Compare(a,b);});return names;}
 }
 public sealed class OffsetDialog:Form {
  readonly NumericUpDown x=new NumericUpDown(),y=new NumericUpDown();readonly PictureBox picture=new PictureBox();readonly Func<PrintOffset,Bitmap> render;public PrintOffset Value {get{return new PrintOffset(x.Value,y.Value);}}
  public OffsetDialog(string title,PrintOffset current,Func<PrintOffset,Bitmap> draw,Action<PrintOffset> test,Action<PrintOffset> save) {render=draw;Text="印字位置調整";Font=new Font("Yu Gothic UI",10);ClientSize=new Size(740,530);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
   Controls.Add(new Label {Text=title,Left=20,Top=12,Width=700,Height=48});picture.SetBounds(20,65,700,300);picture.SizeMode=PictureBoxSizeMode.Zoom;Controls.Add(picture);
   Controls.Add(new Label {Text="プレビューに対して ＋は右／下、−は左／上。0.1 mm単位で調整します。",Left=20,Top=373,Width=710,Height=25});
   Controls.Add(new Label {Text="左右（mm）",Left=20,Top=410,Width=100});Controls.Add(new Label {Text="上下（mm）",Left=280,Top=410,Width=100});x.SetBounds(125,405,120,30);y.SetBounds(385,405,120,30);foreach(NumericUpDown n in new[]{x,y}){n.DecimalPlaces=1;n.Increment=.1m;n.Minimum=-10;n.Maximum=10;Controls.Add(n);}x.Value=current.X;y.Value=current.Y;x.ValueChanged+=delegate{RefreshImage();};y.ValueChanged+=delegate{RefreshImage();};
   Button reset=new Button {Text="0に戻す",Left=550,Top=402,Width=150,Height=35};Button proof=new Button {Text="テスト印刷（1枚）",Left=20,Top=470,Width=220,Height=36};Button ok=new Button {Text="保存して閉じる",Left=370,Top=470,Width=180,Height=36};Button cancel=new Button {Text="キャンセル",Left=570,Top=470,Width=150,Height=36,DialogResult=DialogResult.Cancel};Controls.AddRange(new Control[]{reset,proof,ok,cancel});CancelButton=cancel;
   reset.Click+=delegate{x.Value=y.Value=0;};proof.Click+=delegate{proof.Enabled=false;try{test(Value);}catch(Exception ex){MessageBox.Show(this,ex.Message,"印刷できませんでした");}finally{proof.Enabled=true;}};ok.Click+=delegate{try{save(Value);DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,ex.Message,"保存できませんでした");}};FormClosed+=delegate{if(picture.Image!=null)picture.Image.Dispose();};RefreshImage();
  }
  void RefreshImage(){Image old=picture.Image;picture.Image=render(Value);if(old!=null)old.Dispose();}
 }
}
