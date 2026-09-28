using Microsoft.AspNetCore.Mvc;
using EKitapAPI.Services;

namespace EKitapAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly WordService _wordService;
        private readonly PdfService _pdfService;

        public TestController(WordService wordService, PdfService pdfService)
        {
            _wordService = wordService;
            _pdfService = pdfService;
        }

        [HttpGet("word")]
        public IActionResult TestWord([FromQuery] string path)
        {
            if (!System.IO.File.Exists(path))
                return NotFound($"Dosya bulunamadı: {path}");

            try
            {
                var dosyaAdi = Path.GetFileName(path);
                var sonuc = _wordService.OkuVeTemizle(path, dosyaAdi);
                return Ok(sonuc);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata: {ex.Message}");
            }
        }

        [HttpGet("pdf")]
        public IActionResult TestPdf([FromQuery] string path)
        {
            if (!System.IO.File.Exists(path))
                return NotFound($"Dosya bulunamadı: {path}");

            try
            {
                var dosyaAdi = Path.GetFileName(path);
                var icerik = _wordService.OkuVeTemizle(path, dosyaAdi);
                var pdfBytes = _pdfService.TekBildiriTestPdfUret(icerik);
                return File(pdfBytes, "application/pdf", "test.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata: {ex.Message}");
            }
        }

        [HttpGet("kitap")]
        public IActionResult TestKitap([FromQuery] string klasor)
        {
            if (!Directory.Exists(klasor))
                return NotFound($"Klasör bulunamadı: {klasor}");

            try
            {
                var dosyalar = Directory.GetFiles(klasor, "*.docx")
                    .OrderBy(f => f)
                    .ToList();

                if (dosyalar.Count == 0)
                    return BadRequest("Klasörde .docx dosyası bulunamadı.");

                var bildirilerContent = dosyalar
                    .Select(f => _wordService.OkuVeTemizle(f, Path.GetFileName(f)))
                    .ToList();

                var pdfBytes = _pdfService.KitapPdfOlustur(bildirilerContent, "Test E-Kitap");

                return File(pdfBytes, "application/pdf", "kitap-test.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}