using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EKitapAPI.Data;
using EKitapAPI.Models;
using EKitapAPI.Services;

namespace EKitapAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KitapController : ControllerBase
    {
        private const int GerekliDosyaSayisi = 10;

        private readonly AppDbContext _db;
        private readonly WordService _wordService;
        private readonly PdfService _pdfService;
        private readonly IWebHostEnvironment _env;

        public KitapController(
            AppDbContext db,
            WordService wordService,
            PdfService pdfService,
            IWebHostEnvironment env)
        {
            _db = db;
            _wordService = wordService;
            _pdfService = pdfService;
            _env = env;
        }

        private string WebRoot =>
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        // 1) Yeni kitap oluştur: ad + tam olarak 10 adet .docx
        [HttpPost]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> KitapOlustur(
            [FromForm] string ad,
            [FromForm] List<IFormFile> dosyalar)
        {
            if (string.IsNullOrWhiteSpace(ad))
                return BadRequest("Kitap adı zorunludur.");

            if (dosyalar == null || dosyalar.Count != GerekliDosyaSayisi)
                return BadRequest(
                    $"Tam olarak {GerekliDosyaSayisi} adet .docx dosyası yüklemelisiniz. " +
                    $"Gönderilen: {dosyalar?.Count ?? 0}");

            foreach (var dosya in dosyalar)
            {
                if (!dosya.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                    return BadRequest($"'{dosya.FileName}' bir .docx dosyası değil.");
            }

            var kitap = new Kitap { Ad = ad, Durum = KitapDurum.Pending };
            _db.Kitaplar.Add(kitap);
            await _db.SaveChangesAsync(); // Id üretilsin diye önce kaydediyoruz

            var kitapKlasoru = Path.Combine(WebRoot, "uploads", kitap.Id.ToString());
            Directory.CreateDirectory(kitapKlasoru);

            for (int i = 0; i < dosyalar.Count; i++)
            {
                var dosya = dosyalar[i];
                var hedefYol = Path.Combine(kitapKlasoru, dosya.FileName);

                using (var stream = new FileStream(hedefYol, FileMode.Create))
                {
                    await dosya.CopyToAsync(stream);
                }

                _db.Bildiriler.Add(new Bildiri
                {
                    KitapId = kitap.Id,
                    DosyaAdi = dosya.FileName,
                    Sira = i + 1,
                    OrijinalDosyaYolu = hedefYol
                });
            }

            await _db.SaveChangesAsync();

            return Ok(new { kitap.Id, kitap.Ad, Durum = kitap.Durum.ToString() });
        }

        // 2) "Kitabı Oluştur" işlemini tetikler
        [HttpPost("{id}/olustur")]
        public async Task<IActionResult> KitabiUret(int id)
        {
            var kitap = await _db.Kitaplar
                .Include(k => k.Bildiriler)
                .FirstOrDefaultAsync(k => k.Id == id);

            if (kitap == null)
                return NotFound("Kitap bulunamadı.");

            kitap.Durum = KitapDurum.Processing;
            kitap.HataMesaji = null;
            await _db.SaveChangesAsync();

            try
            {
                var siraliBildiriler = kitap.Bildiriler.OrderBy(b => b.Sira).ToList();

                var icerikler = new List<BildiriIcerik>();
                foreach (var bildiri in siraliBildiriler)
                {
                    var icerik = _wordService.OkuVeTemizle(bildiri.OrijinalDosyaYolu, bildiri.DosyaAdi);
                    bildiri.Baslik = icerik.Baslik;
                    icerikler.Add(icerik);
                }

                var pdfBytes = _pdfService.KitapPdfOlustur(icerikler, kitap.Ad);

                var outputKlasoru = Path.Combine(WebRoot, "output");
                Directory.CreateDirectory(outputKlasoru);
                var pdfYolu = Path.Combine(outputKlasoru, $"{kitap.Id}.pdf");
                await System.IO.File.WriteAllBytesAsync(pdfYolu, pdfBytes);

                kitap.PdfDosyaYolu = pdfYolu;
                kitap.Durum = KitapDurum.Completed;
                await _db.SaveChangesAsync();

                return Ok(new { kitap.Id, Durum = kitap.Durum.ToString() });
            }
            catch (Exception ex)
            {
                kitap.Durum = KitapDurum.Failed;
                kitap.HataMesaji = ex.Message;
                await _db.SaveChangesAsync();

                return StatusCode(500, new { Durum = "Failed", Hata = ex.Message });
            }
        }

        // 3) Durum sorgula
        [HttpGet("{id}")]
        public async Task<IActionResult> DurumSorgula(int id)
        {
            var kitap = await _db.Kitaplar
                .Include(k => k.Bildiriler)
                .FirstOrDefaultAsync(k => k.Id == id);

            if (kitap == null)
                return NotFound("Kitap bulunamadı.");

            return Ok(new
            {
                kitap.Id,
                kitap.Ad,
                Durum = kitap.Durum.ToString(),
                kitap.HataMesaji,
                kitap.OlusturmaTarihi,
                Bildiriler = kitap.Bildiriler
                    .OrderBy(b => b.Sira)
                    .Select(b => new { b.Sira, b.DosyaAdi, b.Baslik })
            });
        }

        // 4) PDF indir
        [HttpGet("{id}/indir")]
        public async Task<IActionResult> PdfIndir(int id)
        {
            var kitap = await _db.Kitaplar.FindAsync(id);

            if (kitap == null)
                return NotFound("Kitap bulunamadı.");

            if (kitap.Durum != KitapDurum.Completed || string.IsNullOrEmpty(kitap.PdfDosyaYolu))
                return BadRequest("Kitap henüz hazır değil.");

            if (!System.IO.File.Exists(kitap.PdfDosyaYolu))
                return NotFound("PDF dosyası bulunamadı.");

            var bytes = await System.IO.File.ReadAllBytesAsync(kitap.PdfDosyaYolu);
            return File(bytes, "application/pdf", $"{kitap.Ad}.pdf");
        }

        // 5) Tüm kitapları listele (frontend'de "geçmiş kitaplar" gösterebilmek için faydalı)
        [HttpGet]
        public async Task<IActionResult> TumKitaplar()
        {
            var kitaplar = await _db.Kitaplar
                .OrderByDescending(k => k.OlusturmaTarihi)
                .Select(k => new { k.Id, k.Ad, Durum = k.Durum.ToString(), k.OlusturmaTarihi })
                .ToListAsync();

            return Ok(kitaplar);
        }
    }
}