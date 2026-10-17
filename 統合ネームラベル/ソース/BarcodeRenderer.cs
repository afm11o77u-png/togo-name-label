using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace UnifiedLabels
{
    /// <summary>
    /// QR / Code39 の生成と配置をアプリ内で集約する。Program から ZXing を直接呼ばない。
    /// 座標・サイズは従来の DotNetBarcode.WriteBar 呼び出しに合わせている。
    /// </summary>
    public static class BarcodeRenderer
    {
        /// <summary>だるま食品 Code39 で許可する文字（現行 Validate と同じ）。</summary>
        public const string Code39AllowedChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%";

        public static bool IsCode39ContentAllowed(string value)
        {
            if (String.IsNullOrEmpty(value)) return false;
            foreach (char c in value)
                if (Code39AllowedChars.IndexOf(c) < 0) return false;
            return true;
        }

        /// <summary>レガシー形式 kind 0〜3 のバーコードを、従来どおりの位置・大きさで描画する。</summary>
        /// <param name="umiosQrLeft">kind=3（Umios 大ネームQR）の描画 X。未指定時は 15（従来）。社員番号中心合わせ時は呼び出し側で算出。</param>
        public static void DrawForLabel(Graphics g, int kind, string number, float umiosQrLeft = 15f)
        {
            if (String.IsNullOrWhiteSpace(number)) return;
            if (kind == 2)
            {
                if (!IsCode39ContentAllowed(number)) return;
                using (Bitmap bmp = CreateCode39(number, 260, 60))
                    g.DrawImage(bmp, 15, 100, 260, 60);
                return;
            }
            if (kind == 3)
            {
                using (Bitmap bmp = CreateQr(number, 60, 60))
                    g.DrawImage(bmp, umiosQrLeft, 100, 60, 60);
                return;
            }
            if (kind == 0 || kind == 1)
            {
                using (Bitmap bmp = CreateQr(number, 200, 200))
                {
                    GraphicsState state = g.Save();
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(bmp, kind == 0 ? new Rectangle(156, 50, 30, 30) : new Rectangle(255, 115, 45, 45));
                    g.Restore(state);
                }
            }
        }

        /// <summary>
        /// QR を生成。Margin=0 は旧 DotNetBarcode.QRQuitZone=0 に合わせる（余白は読取りに影響するため変更しない）。
        /// </summary>
        public static Bitmap CreateQr(string content, int width, int height)
        {
            if (content == null) throw new ArgumentNullException("content");
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 0,
                    CharacterSet = "ISO-8859-1",
                    ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M
                }
            };
            return writer.Write(content);
        }

        public static Bitmap CreateCode39(string content, int width, int height)
        {
            if (content == null) throw new ArgumentNullException("content");
            if (!IsCode39ContentAllowed(content))
                throw new ArgumentException("Code39 に使えない文字が含まれています。", "content");
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.CODE_39,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 0,
                    PureBarcode = true
                }
            };
            return writer.Write(content);
        }

        /// <summary>
        /// 検証用デコード。生成画像は Margin=0 のため、読取り時だけ白余白を付けてソフトウェア検証する（生成側の余白は変えない）。
        /// </summary>
        public static string Decode(Bitmap image, params BarcodeFormat[] formats)
        {
            var formatList = formats != null && formats.Length > 0 ? new List<BarcodeFormat>(formats) : null;
            var reader = new BarcodeReader
            {
                AutoRotate = false,
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    PureBarcode = true,
                    PossibleFormats = formatList
                }
            };
            Result result = reader.Decode(image);
            if (result != null) return result.Text;
            using (Bitmap padded = PadWhite(image, 24))
            {
                result = reader.Decode(padded);
                return result == null ? null : result.Text;
            }
        }

        static Bitmap PadWhite(Bitmap source, int pad)
        {
            var canvas = new Bitmap(source.Width + pad * 2, source.Height + pad * 2);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.White);
                g.DrawImageUnscaled(source, pad, pad);
            }
            return canvas;
        }

        /// <summary>
        /// 代表値でサイズとデコード内容を検証する（画像ファイルは出さない）。
        /// 成功時は folder に barcode-verification.txt を書く。
        /// </summary>
        public static void VerifyRepresentativeSamples(string folder)
        {
            Directory.CreateDirectory(folder);
            var log = new StringBuilder();
            void Check(bool ok, string message)
            {
                if (!ok) throw new Exception(message);
                log.AppendLine("OK: " + message);
            }

            // よつ葉小/大で使う QR（200→縮小描画）と Umios 直描画 60x60
            string[] qrSamples = { "Y12345", "Y1234", "123456" };
            foreach (string sample in qrSamples)
            {
                using (Bitmap large = CreateQr(sample, 200, 200))
                {
                    Check(large.Width == 200 && large.Height == 200, "QR 200x200 size for " + sample);
                    Check(Decode(large, BarcodeFormat.QR_CODE) == sample, "QR 200x200 decode " + sample);
                }
                using (Bitmap small = CreateQr(sample, 60, 60))
                {
                    Check(small.Width == 60 && small.Height == 60, "QR 60x60 size for " + sample);
                    Check(Decode(small, BarcodeFormat.QR_CODE) == sample, "QR 60x60 decode " + sample);
                }
            }

            // だるま Code39（許可文字セットの代表）
            string[] code39Samples = { "00123", "ABC-12", "A B$%/+" };
            foreach (string sample in code39Samples)
            {
                Check(IsCode39ContentAllowed(sample), "Code39 charset allows " + sample);
                using (Bitmap bar = CreateCode39(sample, 260, 60))
                {
                    Check(bar.Width == 260 && bar.Height == 60, "Code39 260x60 size for " + sample);
                    string decoded = Decode(bar, BarcodeFormat.CODE_39);
                    Check(decoded == sample, "Code39 decode expected '" + sample + "' got '" + decoded + "'");
                }
            }

            Check(!IsCode39ContentAllowed("abc"), "Code39 rejects lowercase");
            Check(!IsCode39ContentAllowed("A*B"), "Code39 rejects asterisk");

            log.AppendLine("PASS: ZXing QR/Code39 representative size and content checks.");
            log.AppendLine("NOTE: Real printer output and physical scanner reads still require manual confirmation.");
            File.WriteAllText(Path.Combine(folder, "barcode-verification.txt"), log.ToString(), Encoding.UTF8);
        }
    }
}
