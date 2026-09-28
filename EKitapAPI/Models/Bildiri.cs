using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EKitapAPI.Models
{
    public class Bildiri
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int KitapId { get; set; }

        [ForeignKey(nameof(KitapId))]
        public Kitap? Kitap { get; set; }

        [Required]
        [MaxLength(255)]
        public string DosyaAdi { get; set; } = string.Empty;

        [Required]
        public int Sira { get; set; }

        [Required]
        public string OrijinalDosyaYolu { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Baslik { get; set; }
    }
}