using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace EKitapAPI.Services
{
    public class BildiriIcerik
    {
        public string Baslik { get; set; } = string.Empty;
        public List<string> Paragraflar { get; set; } = new();
    }

    public class WordService
    {
        // E-posta: standart e-posta formatı
        private static readonly Regex EmailRegex = new(
            @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
            RegexOptions.Compiled);

        // Telefon: +90 / 0 öneki opsiyonel, alan kodu parantez içinde olabilir,
        // ayraçlar boşluk/tire/nokta olabilir. ORCID (16 haneli, 4x4 grup) ile karışmaması
        // için başında/sonunda başka rakam olmamasını (?<!\d) / (?!\d) ile garanti ediyoruz.
        private static readonly Regex PhoneRegex = new(
            @"(?<!\d)(?:\+90[\s\-]?|0[\s\-]?)?\(?\s?0?\d{3}\)?[\s.\-]?\d{3}[\s.\-]?\d{2}[\s.\-]?\d{2}(?!\d)",
            RegexOptions.Compiled);

        // E-posta/telefon silindikten sonra geride kalan boş etiketleri
        // ("E-posta:", "Tel:" gibi) temizlemek için.
        private static readonly Regex EtiketRegex = new(
            @"(E-posta|Eposta|E-mail|Email|Mail|İletişim|İrtibat|Tel\.No|Telefon|GSM|Cep telefonu|Cep|Mobile|Tel)\s*:?\s*(?=$|\||/|-|;|,|\z)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public BildiriIcerik OkuVeTemizle(string dosyaYolu, string dosyaAdi)
        {
            using var wordDoc = WordprocessingDocument.Open(dosyaYolu, false);
            var mainPart = wordDoc.MainDocumentPart;
            var body = mainPart?.Document?.Body;

            if (body == null)
                throw new InvalidOperationException($"'{dosyaAdi}' dosyasının içeriği okunamadı.");

            var paragraphs = body.Elements<Paragraph>().ToList();

            // Stil ID -> gerçek stil adı eşlemesini çıkar (örn. "KonuBal" -> "Title")
            var stilAdlari = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var stylesPart = mainPart?.StyleDefinitionsPart;
            if (stylesPart?.Styles != null)
            {
                foreach (var style in stylesPart.Styles.Elements<Style>())
                {
                    var id = style.StyleId?.Value;
                    var name = style.StyleName?.Val?.Value;
                    if (id != null && name != null)
                        stilAdlari[id] = name;
                }
            }

            string baslik = BaslikCikar(paragraphs, stilAdlari, dosyaAdi);

            var temizParagraflar = new List<string>();
            foreach (var p in paragraphs)
            {
                var metin = p.InnerText;
                if (string.IsNullOrWhiteSpace(metin))
                    continue;

                temizParagraflar.Add(Temizle(metin));
            }

            return new BildiriIcerik
            {
                Baslik = baslik,
                Paragraflar = temizParagraflar
            };
        }

        private string BaslikCikar(
            List<Paragraph> paragraphs,
            Dictionary<string, string> stilAdlari,
            string dosyaAdi)
        {
            // 1) "Title" görünen adına sahip stildeki paragrafı ara
            foreach (var p in paragraphs)
            {
                var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                if (styleId != null &&
                    stilAdlari.TryGetValue(styleId, out var gercekAd) &&
                    gercekAd.Equals("Title", StringComparison.OrdinalIgnoreCase))
                {
                    var text = p.InnerText.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                        return Temizle(text);
                }
            }

            // 2) Bulunamazsa: ilk dolu paragrafı başlık say
            var ilkDoluParagraf = paragraphs
                .Select(p => p.InnerText.Trim())
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

            if (!string.IsNullOrWhiteSpace(ilkDoluParagraf))
                return Temizle(ilkDoluParagraf);

            // 3) Son çare: dosya adından türet
            return Path.GetFileNameWithoutExtension(dosyaAdi).Replace('_', ' ');
        }

        public static string Temizle(string metin)
        {
            var sonuc = EmailRegex.Replace(metin, "");
            sonuc = PhoneRegex.Replace(sonuc, "");
            sonuc = EtiketRegex.Replace(sonuc, "");

            // Ardışık ayraçları (| / - ; , boşluklarla karışık) tek karaktere indir
            sonuc = Regex.Replace(sonuc, @"[|/;,-]\s*(?:[|/;,-]\s*)+", m => m.Value.Trim()[0].ToString());

            sonuc = sonuc.Trim(' ', '\t', '|', '/', '-', ';', ',');
            sonuc = Regex.Replace(sonuc, @"[ \t]{2,}", " ");
            return sonuc.Trim();
        }
    }
}