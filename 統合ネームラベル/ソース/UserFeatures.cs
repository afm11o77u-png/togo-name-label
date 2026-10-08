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
  readonly string path;
  public readonly List<string> Favorites=new List<string>(), Recent=new List<string>();
  readonly Dictionary<string,PrintOffset> offsets=new Dictionary<string,PrintOffset>();
  public UserSettings(string file) {path=file; if(!File.Exists(path)) return; XmlDocument d=new XmlDocument(); d.Load(path); foreach(XmlNode n in d.SelectNodes("/settings/favorite")) Favorites.Add(n.InnerText); foreach(XmlNode n in d.SelectNodes("/settings/recent")) Recent.Add(n.InnerText); foreach(XmlElement n in d.SelectNodes("/settings/offset")) offsets[n.GetAttribute("key")]=new PrintOffset(Decimal.Parse(n.GetAttribute("x"),CultureInfo.InvariantCulture),Decimal.Parse(n.GetAttribute("y"),CultureInfo.InvariantCulture)); }
  public PrintOffset Get(string key) {PrintOffset p;return offsets.TryGetValue(key,out p)?new PrintOffset(p.X,p.Y):new PrintOffset();}
  public void Set(string key,PrintOffset p) {PrintOffset old=Get(key);bool had=offsets.ContainsKey(key); offsets[key]=new PrintOffset(p.X,p.Y);try {Save();} catch {if(had) offsets[key]=old;else offsets.Remove(key);throw;} }
  public void Visit(string name) {Recent.Remove(name);Recent.Insert(0,name);if(Recent.Count>10) Recent.RemoveRange(10,Recent.Count-10);Save();}
  public void Toggle(string name) {var old=new List<string>(Favorites);if(!Favorites.Remove(name))Favorites.Add(name);try{Save();}catch{Favorites.Clear();Favorites.AddRange(old);throw;}}
  public void RenameCustomer(string previous,string current){bool changed=false;foreach(List<string> items in new[]{Favorites,Recent}){int i=items.IndexOf(previous);if(i<0)continue;items[i]=current;changed=true;}if(changed)Save();}
  public void Save() {Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));XmlDocument d=new XmlDocument();XmlElement root=d.CreateElement("settings");d.AppendChild(root);foreach(string s in Favorites) {XmlElement n=d.CreateElement("favorite");n.InnerText=s;root.AppendChild(n);}foreach(string s in Recent) {XmlElement n=d.CreateElement("recent");n.InnerText=s;root.AppendChild(n);}foreach(var pair in offsets) {XmlElement n=d.CreateElement("offset");n.SetAttribute("key",pair.Key);n.SetAttribute("x",pair.Value.X.ToString(CultureInfo.InvariantCulture));n.SetAttribute("y",pair.Value.Y.ToString(CultureInfo.InvariantCulture));root.AppendChild(n);}string tmp=path+".tmp";d.Save(tmp);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}
  public static bool Matches(string name,string query) {string a=name.Normalize(NormalizationForm.FormKC).Replace(" ","").ToUpperInvariant();string b=query.Normalize(NormalizationForm.FormKC).Replace(" ","").ToUpperInvariant();return a.Contains(b);}
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
{"札幌中一","さっぽろなかいち"}
  };
  public static string CustomerReading(string name){string reading;return CustomerReadings.TryGetValue(name,out reading)?reading:name;}
  public static string[] SortedCustomers(){var names=(string[])Customers.Clone();var comparer=CultureInfo.GetCultureInfo("ja-JP").CompareInfo;Array.Sort(names,delegate(string a,string b){int n=comparer.Compare(CustomerReading(a),CustomerReading(b),CompareOptions.IgnoreWidth|CompareOptions.IgnoreKanaType);return n!=0?n:comparer.Compare(a,b);});return names;}
 }
 public sealed class CustomerPicker:Form {
  readonly UserSettings settings;readonly TextBox query=new TextBox();readonly ComboBox filter=new ComboBox();readonly ListBox list=new ListBox();readonly Label count=new Label();readonly List<int> ids=new List<int>();public int SelectedCustomer=-1;
  public CustomerPicker(UserSettings s) {settings=s;Text="顧客を探す";Font=new Font("Yu Gothic UI",11);ClientSize=new Size(670,540);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
   AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
   Controls.Add(new Label {Text="顧客名の一部を入力してください",Left=20,Top=15,Width=600,Height=25});
   Panel searchFrame=new Panel {Left=20,Top=45,Width=420,Height=44,BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle};
   query.BorderStyle=BorderStyle.None;query.SetBounds(10,9,398,26);query.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;searchFrame.Controls.Add(query);Controls.Add(searchFrame);
   filter.SetBounds(455,53,195,32);filter.DropDownStyle=ComboBoxStyle.DropDownList;filter.Items.AddRange(new object[]{"すべて","お気に入り","最近選んだ顧客"});Controls.Add(filter);
   count.SetBounds(20,99,630,25);Controls.Add(count);list.SetBounds(20,130,630,335);list.IntegralHeight=false;list.DrawMode=DrawMode.OwnerDrawFixed;list.ItemHeight=28;
   list.DrawItem+=delegate(object sender,DrawItemEventArgs e){e.DrawBackground();if(e.Index>=0){Rectangle bounds=e.Bounds;bounds.Inflate(-8,0);TextRenderer.DrawText(e.Graphics,list.Items[e.Index].ToString(),e.Font,bounds,e.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}e.DrawFocusRectangle();};Controls.Add(list);
   Button star=new Button {Text="☆ お気に入り切替",Left=20,Top=485,Width=210,Height=35};Button ok=new Button {Text="この顧客を選ぶ",Left=300,Top=485,Width=190,Height=35};Button cancel=new Button {Text="閉じる",Left=510,Top=485,Width=140,Height=35,DialogResult=DialogResult.Cancel};Controls.AddRange(new Control[]{star,ok,cancel});CancelButton=cancel;AcceptButton=ok;
   query.TextChanged+=delegate{RefreshList();};filter.SelectedIndexChanged+=delegate{RefreshList();};ok.Click+=delegate{Choose();};list.DoubleClick+=delegate{Choose();};star.Click+=delegate{if(list.SelectedIndex<0)return;try {int selected=ids[list.SelectedIndex];settings.Toggle(LabelCatalog.Customers[selected]);RefreshList();int restored=ids.IndexOf(selected);if(restored>=0)list.SelectedIndex=restored;}catch(Exception ex){MessageBox.Show(this,ex.Message,"保存できませんでした");}};filter.SelectedIndex=0;
  }
  public void VerifyFilters(){query.Text="存在しない顧客000";if(ids.Count!=0)throw new Exception("空の検索結果の検証失敗");query.Text="ＵＭＩＯＳ";if(ids.Count<1)throw new Exception("顧客名検索の検証失敗");query.Text="";filter.SelectedIndex=1;foreach(int id in ids)if(!settings.Favorites.Contains(LabelCatalog.Customers[id]))throw new Exception("お気に入り絞込失敗");filter.SelectedIndex=2;foreach(int id in ids)if(!settings.Recent.Contains(LabelCatalog.Customers[id]))throw new Exception("履歴絞込失敗");filter.SelectedIndex=0;}
  void Choose(){if(list.SelectedIndex<0)return;SelectedCustomer=ids[list.SelectedIndex];DialogResult=DialogResult.OK;}
  void RefreshList(){ids.Clear();list.Items.Clear();IEnumerable<string> names=filter.SelectedIndex==2?(IEnumerable<string>)settings.Recent:LabelCatalog.SortedCustomers();foreach(string n in names){int id=Array.IndexOf(LabelCatalog.Customers,n);if(id<0 || !UserSettings.Matches(n,query.Text) || (filter.SelectedIndex==1&&!settings.Favorites.Contains(n)))continue;ids.Add(id);list.Items.Add((settings.Favorites.Contains(n)?"★  ":"     ")+n+"   （"+LabelCatalog.ForCustomer(id).Length+"形式）");}count.Text=ids.Count==0?"該当する顧客がありません。検索文字や表示範囲を変更してください。":ids.Count+"件の顧客";if(ids.Count>0)list.SelectedIndex=0;}
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
