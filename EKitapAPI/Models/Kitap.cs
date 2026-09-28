using System.ComponentModel.DataAnnotations;

namespace EKitapAPI.Models
{
    public enum KitapDurum
    {
        Pending,
        Processing,
        Completed,
        Failed
    }

    public class Kitap
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Ad { get; set; } = string.Empty;

        public KitapDurum Durum { get; set; } = KitapDurum.Pending;

        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;

        public string? PdfDosyaYolu { get; set; }

        public string? HataMesaji { get; set; }

        public List<Bildiri> Bildiriler { get; set; } = new();
    }
}