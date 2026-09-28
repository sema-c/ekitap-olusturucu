using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

namespace EKitapAPI.Services
{
    public class PdfService
    {
        private const float MarginCm = 2f;

        private void BildiriIcerigiOlustur(IContainer container, BildiriIcerik bildiri)
        {
            container.Column(column =>
            {
                column.Spacing(8);

                column.Item().Text(bildiri.Baslik)
                    .FontSize(15).Bold();

                foreach (var paragraf in bildiri.Paragraflar)
                {
                    if (string.IsNullOrWhiteSpace(paragraf))
                        continue;

                    column.Item().Text(paragraf)
                        .FontSize(11);
                }
            });
        }

        private void IcindekilerOlustur(
            IContainer container,
            string kitapAdi,
            List<(string Baslik, int BaslangicSayfasi)> girdiler)
        {
            container.Column(column =>
            {
                column.Spacing(6);

                column.Item().AlignCenter().Text(kitapAdi)
                    .FontSize(18).Bold();

                column.Item().PaddingTop(12).PaddingBottom(8)
                    .Text("İÇİNDEKİLER").FontSize(14).Bold();

                foreach (var girdi in girdiler)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(girdi.Baslik).FontSize(11);
                        row.ConstantItem(40).AlignRight()
                            .Text(girdi.BaslangicSayfasi.ToString()).FontSize(11);
                    });
                }
            });
        }

        private int SayfaSayisiOlc(Action<IContainer> icerikOlusturucu)
        {
            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(MarginCm, Unit.Centimetre);
                    page.Content().Element(icerikOlusturucu);
                    page.Footer().AlignCenter().Text(t => t.CurrentPageNumber());
                });
            }).GeneratePdf();

            using var olculenPdf = PdfPigDocument.Open(pdfBytes);
            return olculenPdf.NumberOfPages;
        }

        public byte[] KitapPdfOlustur(List<BildiriIcerik> bildiriler, string kitapAdi)
        {
            var sayfaSayilari = bildiriler
                .Select(b => SayfaSayisiOlc(c => BildiriIcerigiOlustur(c, b)))
                .ToList();

            var taslakGirdiler = bildiriler.Select(b => (b.Baslik, 1)).ToList();
            int tocSayfaSayisi = SayfaSayisiOlc(c => IcindekilerOlustur(c, kitapAdi, taslakGirdiler));

            var gercekGirdiler = new List<(string Baslik, int BaslangicSayfasi)>();
            int suankiSayfa = tocSayfaSayisi + 1;
            for (int i = 0; i < bildiriler.Count; i++)
            {
                gercekGirdiler.Add((bildiriler[i].Baslik, suankiSayfa));
                suankiSayfa += sayfaSayilari[i];
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(MarginCm, Unit.Centimetre);

                    page.Content().Column(mainColumn =>
                    {
                        mainColumn.Item().Element(c => IcindekilerOlustur(c, kitapAdi, gercekGirdiler));

                        foreach (var bildiri in bildiriler)
                        {
                            mainColumn.Item().PageBreak();
                            mainColumn.Item().Element(c => BildiriIcerigiOlustur(c, bildiri));
                        }
                    });

                    page.Footer().AlignCenter().Text(t => t.CurrentPageNumber());
                });
            });

            return document.GeneratePdf();
        }

    }
}
