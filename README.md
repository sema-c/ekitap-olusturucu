# E-Kitap Oluşturucu

Konferans bildirilerinden tek bir PDF e-kitap üreten uygulama.

## İçindekiler

- [Kurulum](#kurulum)
- [MSSQL Bağlantısı](#mssql-bağlantısı)
- [Dosya Saklama Yaklaşımı](#dosya-saklama-yaklaşımı)
- [Kullanılan Kütüphaneler](#kullanılan-kütüphaneler)
- [İşleme Akışı](#i̇şleme-akışı)
- [İletişim Bilgisi Temizliği](#i̇letişim-bilgisi-temizliği)
- [Masaüstü ve Mobil Tasarım Kararları](#masaüstü-ve-mobil-tasarım-kararları)
- [Bilinen Eksikler](#bilinen-eksikler)
- [Yapay Zeka Kullanımı](#yapay-zeka-kullanımı)

---

## Kurulum

### Gereksinimler
- .NET SDK 8.0 veya üzeri
- Node.js 18+ ve npm
- MSSQL (SQL Server Express, Developer veya Docker container)

### Backend

```bash
cd EKitapAPI
dotnet restore
dotnet ef database update
dotnet run
```

Backend `http://localhost:5232` adresinde ayağa kalkar. Swagger arayüzü: `http://localhost:5232/swagger`

### Frontend

```bash
cd ekitap-frontend
npm install
npm run dev
```

Frontend `http://localhost:5173` adresinde ayağa kalkar.

**Not:** Backend ve frontend'in aynı anda, iki ayrı terminalde çalışıyor olması gerekir.

---

## MSSQL Bağlantısı

Bağlantı bilgisi `EKitapAPI/appsettings.json` içinde tanımlıdır:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=EKitapDB;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

- **Windows Authentication** (`Trusted_Connection=True`) kullanılmıştır, kullanıcı adı/şifre gerekmez.
- Farklı bir SQL Server instance adı kullanıyorsanız (`SQLEXPRESS` yerine), `Server=` kısmını kendi instance adınıza göre güncelleyin.
- Veritabanı ve tablolar migration ile otomatik kurulur: `dotnet ef database update`

### Veri Modeli

İki tablo, 1-N ilişkili:

**Kitaplar**
| Kolon | Tip | Açıklama |
|---|---|---|
| Id | int (PK) | |
| Ad | nvarchar(200) | Kullanıcının girdiği kitap adı |
| Durum | int (enum) | Pending / Processing / Completed / Failed |
| OlusturmaTarihi | datetime2 | |
| PdfDosyaYolu | nvarchar | Üretilen PDF'in disk yolu |
| HataMesaji | nvarchar | Üretim başarısız olursa hata detayı |

**Bildiriler**
| Kolon | Tip | Açıklama |
|---|---|---|
| Id | int (PK) | |
| KitapId | int (FK → Kitaplar, ON DELETE CASCADE) | |
| DosyaAdi | nvarchar(255) | Orijinal .docx dosya adı |
| Sira | int | Kitaptaki sıra (yükleme sırası) |
| OrijinalDosyaYolu | nvarchar | Yüklenen .docx'in disk yolu |
| Baslik | nvarchar(500) | Word'den çıkarılan bildiri başlığı |

---

## Dosya Saklama Yaklaşımı

Dosyalar `wwwroot` altında saklanır:
- Yüklenen orijinal `.docx` dosyaları: `wwwroot/uploads/{kitapId}/`
- Üretilen PDF: `wwwroot/output/{kitapId}.pdf`

Bu yaklaşım kolaylık amacıyla seçilmiştir (case dokümanında belirtildiği gibi). Orijinal Word dosyaları değişmeden saklanır, temizleme işlemi yalnızca üretilen PDF üzerinde etkilidir.

---

## Kullanılan Kütüphaneler

**Backend:**
- `DocumentFormat.OpenXml` — Word dosyalarından metin ve stil bilgisi okuma
- `QuestPDF` — PDF üretimi (Community lisansı)
- `PdfPig` — Ara ölçüm aşamasında üretilen geçici PDF'lerin sayfa sayısını okumak için
- `Microsoft.EntityFrameworkCore.SqlServer` — MSSQL erişimi ve migration

**Frontend:**
- `react-pdf` üretilen PDF'in tarayıcıda önizlenmesi
- Vite — geliştirme ortamı

---

## İşleme Akışı

1. Kullanıcı kitap adı girer, tam olarak 10 adet `.docx` seçer.
2. `POST /api/Kitap` — dosyalar `wwwroot/uploads/{kitapId}/` altına kaydedilir, veritabanında `Kitaplar` ve `Bildiriler` kayıtları oluşturulur.
3. `POST /api/Kitap/{id}/olustur` tetiklenir, Durum `Processing` olur:
   - Her `.docx` sırayla okunur (`WordService`): başlık, Word'ün "Title" stiline sahip paragraftan çıkarılır (bulunamazsa ilk dolu paragraf, o da yoksa dosya adı kullanılır).
   - Her paragraftaki e-posta ve telefon numaraları regex ile temizlenir.
   - Her bildirinin PDF'te kaç sayfa tutacağı, tek başına geçici olarak render edilip `PdfPig` ile sayılarak ölçülür.
   - Bu ölçümlerden yola çıkılarak her bildirinin İçindekiler'deki gerçek başlangıç sayfası hesaplanır.
   - Tüm bildiriler, doğru sayfa numaralarıyla İçindekiler + sırasıyla birleştirilerek tek PDF olarak üretilir (`QuestPDF`), `wwwroot/output/{kitapId}.pdf` altına yazılır.
   - Durum `Completed` olur, `PdfDosyaYolu` kaydedilir. Herhangi bir aşamada hata olursa Durum `Failed` olur, `HataMesaji` doldurulur.
4. `GET /api/Kitap/{id}` — durum ve bildiri listesi sorgulanır.
5. `GET /api/Kitap/{id}/indir` — üretilen PDF indirilir/önizlenir.

Bildiriler **yükleme sırasına göre** birleştirilir.

---

## İletişim Bilgisi Temizliği

E-posta ve telefon numaraları, sunucu tarafında (`WordService.Temizle`), her paragraf için regex ile temizlenir. Orijinal `.docx` dosyaları değişmeden kalır; temizleme yalnızca PDF'e yazılacak metin üzerinde uygulanır.

**E-posta regex:** standart e-posta formatını yakalar (`kullanici@alan.uzanti`).

**Telefon regex:** Türkiye telefon formatlarının farklı yazım biçimlerini kapsar — `+90` veya `0` öneki, alan kodu parantez içinde olabilir, ayraç olarak boşluk/tire/nokta kullanılabilir. ORCID gibi 16 haneli, 4'erli gruplu numaralarla (`0000-0001-1000-0001`) karışmaması için sınır kontrolü (`(?<!\d)...(?!\d)`) uygulanmıştır.

Test edilen örnek formatlar:
| Orijinal | Sonuç |
|---|---|
| `Tel: 0500 000 00 01` | temizlendi |
| `Telefon: +90 (500) 000 00 02` | temizlendi |
| `GSM 0 (500) 000 00 12` | temizlendi |
| `0500-000-00-03` | temizlendi |
| `Cep: +90 500 000 00 04` | temizlendi |
| `(0500) 000 00 05` | temizlendi |
| `Mobile +90-500-000-00-06` | temizlendi |
| `Tel.No: 0 500 000 00 07` | temizlendi |
| `İrtibat: 0500.000.00.08` | temizlendi |
| `Cep telefonu: +90 500 000 00 10` | temizlendi |
| `ORCID: 0000-0001-1000-0001` | **bozulmadan korundu** |

E-posta/telefon silindikten sonra geride kalan boş etiketler (örn. `"E-posta: | Tel: |"`) da ayrıca temizlenir; satırda gerçek bilgi kalmıyorsa satırın tamamı PDF'te gösterilmez.

İki yazarlı bildirilerde tüm paragraflar taranır, tek bir satıra bağımlı kalınmaz.

---

## Masaüstü ve Mobil Tasarım Kararları

- Tek sütunlu, ortalanmış, maksimum 640px genişliğinde bir düzen kullanıldı; bu sayede ek bir medya sorgusu yazmadan büyük ekranlarda da mobilde de doğal olarak okunabilir kalıyor.
- 480px altı ekranlar için kart iç boşlukları ve dosya seçme alanının yönü ayrıca ayarlandı.
- Dosya listesi, kitaptaki gerçek bölüm sırasını yansıttığı için numaralandırılmış liste (`<ol>`) olarak gösterildi.
- Yükleme durumunda gerçek zamanlı sunucu ilerlemesi yerine, işlemin bilinen aşamalarını (okuma → temizleme → sayfa hesaplama → birleştirme) sırayla gösteren görsel bir aşama göstergesi kullanıldı.
- Klavye ile gezinme için görünür focus stilleri (`:focus-visible`) eklendi.

---

## Bilinen Eksikler

Yok — case'de zorunlu tutulan tüm maddeler tamamlanmış ve gerçek test verisiyle doğrulanmıştır.

---

## Yapay Zeka Kullanımı

Geliştirme sürecinde Anthropic'in Claude asistanından şu konularda destek alınmıştır:

- **Kullanıcı deneyimi:** Kitap PDF'i oluşturulurken kullanıcının beklerken göreceği adım adım ilerleyen ilerleme çubukları (progress bar) fikri Claude'dan alınmıştır.
- **Arayüz tasarımı:** CSS ile renk paleti ve genel görsel tasarım konusunda Claude'dan öneriler alınmıştır.
- **Hata çözümü:** PDF üretiminde karşılaşılan Türkçe karakter sorunu Claude ile birlikte analiz edilerek çözülmüştür.

Claude'un önerileri geliştirici tarafından değerlendirilmiş ve projeye uyarlanmıştır. Karşılaşılan hatalar birlikte analiz edilip düzeltilmiştir.