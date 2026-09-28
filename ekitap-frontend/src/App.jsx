import { useState, useEffect } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';
import './App.css';

pdfjs.GlobalWorkerOptions.workerSrc = `//unpkg.com/pdfjs-dist@${pdfjs.version}/build/pdf.worker.min.mjs`;

const API_URL = 'http://localhost:5232/api/Kitap';
const GEREKLI_DOSYA_SAYISI = 10;

const ASAMA = {
  FORM: 'form',
  YUKLENIYOR: 'yukleniyor',
  TAMAMLANDI: 'tamamlandi',
  HATA: 'hata',
};

const YUKLEME_ASAMALARI = [
  'Bildiriler okunuyor',
  'İletişim bilgileri temizleniyor',
  'Sayfalar hesaplanıyor',
  'PDF birleştiriliyor',
];

function App() {
  const [kitapAdi, setKitapAdi] = useState('');
  const [dosyalar, setDosyalar] = useState([]);
  const [asama, setAsama] = useState(ASAMA.FORM);
  const [hataMesaji, setHataMesaji] = useState('');
  const [kitapId, setKitapId] = useState(null);
  const [sayfaSayisi, setSayfaSayisi] = useState(null);
  const [aktifAsama, setAktifAsama] = useState(0);

  useEffect(() => {
    if (asama !== ASAMA.YUKLENIYOR) return;

    setAktifAsama(0);
    const interval = setInterval(() => {
      setAktifAsama((prev) =>
        prev < YUKLEME_ASAMALARI.length - 1 ? prev + 1 : prev
      );
    }, 1300);

    return () => clearInterval(interval);
  }, [asama]);

  function handleDosyaSecimi(e) {
    const secilenler = Array.from(e.target.files);
    setDosyalar(secilenler);
  }

  async function handleKitabiOlustur() {
    if (!kitapAdi.trim()) {
      setHataMesaji('Lütfen bir kitap adı girin.');
      setAsama(ASAMA.HATA);
      return;
    }

    if (dosyalar.length !== GEREKLI_DOSYA_SAYISI) {
      setHataMesaji(
        'Tam olarak ' + GEREKLI_DOSYA_SAYISI + ' adet .docx dosyası seçmelisiniz. Şu an seçili: ' + dosyalar.length
      );
      setAsama(ASAMA.HATA);
      return;
    }

    setAsama(ASAMA.YUKLENIYOR);
    setHataMesaji('');

    try {
      const formData = new FormData();
      formData.append('ad', kitapAdi);
      dosyalar.forEach(function (dosya) {
        formData.append('dosyalar', dosya);
      });

      const olusturResponse = await fetch(API_URL, {
        method: 'POST',
        body: formData,
      });

      if (!olusturResponse.ok) {
        const hataMetni = await olusturResponse.text();
        throw new Error(hataMetni || 'Kitap oluşturulamadı.');
      }

      const kitap = await olusturResponse.json();
      setKitapId(kitap.id);

      const uretResponse = await fetch(API_URL + '/' + kitap.id + '/olustur', {
        method: 'POST',
      });

      if (!uretResponse.ok) {
        let mesaj = 'PDF üretimi sırasında bir hata oluştu.';
        try {
          const hataJson = await uretResponse.json();
          if (hataJson && hataJson.hata) {
            mesaj = hataJson.hata;
          }
        } catch (parseErr) {
        }
        throw new Error(mesaj);
      }

      setAsama(ASAMA.TAMAMLANDI);
    } catch (err) {
      setHataMesaji(err.message || 'Beklenmeyen bir hata oluştu.');
      setAsama(ASAMA.HATA);
    }
  }

  function sifirla() {
    setKitapAdi('');
    setDosyalar([]);
    setAsama(ASAMA.FORM);
    setHataMesaji('');
    setKitapId(null);
    setSayfaSayisi(null);
  }

  const indirmeLinki = kitapId ? API_URL + '/' + kitapId + '/indir' : '';

  return (
    <div className="sayfa">
      <header className="baslik-alani">
        <svg className="kitap-ikon" viewBox="0 0 48 48" fill="none" xmlns="http://www.w3.org/2000/svg">
          <path d="M24 10C20 7 12 6 6 8V36C12 34 20 35 24 38C28 35 36 34 42 36V8C36 6 28 7 24 10Z" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" />
          <path d="M24 10V38" stroke="currentColor" strokeWidth="2" />
        </svg>
        <h1>E-Kitap Oluşturucu</h1>
        <p className="alt-baslik">Bildirilerinizi tek bir PDF kitapta birleştirin</p>
      </header>

      <main className="kart">
        {asama === ASAMA.FORM && (
          <div className="form-alani">
            <label className="alan">
              <span className="alan-etiketi">Kitap adı</span>
              <input
                type="text"
                value={kitapAdi}
                onChange={function (e) { setKitapAdi(e.target.value); }}
                placeholder="Örnek: 2026 Konferans Bildirileri"
              />
            </label>

            <div className="alan">
              <span className="alan-etiketi">
                Bildiri dosyaları
                <span className="sayac">{dosyalar.length}/{GEREKLI_DOSYA_SAYISI}</span>
              </span>

              <div className="dosya-secici">
                <input
                  type="file"
                  id="dosya-input"
                  accept=".docx"
                  multiple
                  onChange={handleDosyaSecimi}
                  className="gizli-input"
                />
                <label htmlFor="dosya-input" className="dosya-buton">
                  Dosya seç
                </label>
                <span className="dosya-ipucu">Tam olarak {GEREKLI_DOSYA_SAYISI} adet .docx dosyası</span>
              </div>
            </div>

            {dosyalar.length > 0 && (
              <ol className="dosya-listesi">
                {dosyalar.map(function (dosya, i) {
                  return <li key={i}>{dosya.name}</li>;
                })}
              </ol>
            )}

            <button className="ana-buton" onClick={handleKitabiOlustur}>
              Kitabı oluştur
            </button>
          </div>
        )}

        {asama === ASAMA.YUKLENIYOR && (
          <div className="yukleme-alani">
            <div className="sayfa-cevirme">
              <span></span><span></span><span></span>
            </div>
            <ul className="asama-listesi">
              {YUKLEME_ASAMALARI.map(function (metin, i) {
                let durum = 'bekliyor';
                if (i < aktifAsama) durum = 'tamamlandi';
                if (i === aktifAsama) durum = 'aktif';
                return (
                  <li key={i} className={'asama ' + durum}>
                    <span className="asama-isareti"></span>
                    {metin}
                  </li>
                );
              })}
            </ul>
          </div>
        )}

        {asama === ASAMA.HATA && (
          <div className="hata-alani">
            <h2>Bir sorun oluştu</h2>
            <p>{hataMesaji}</p>
            <button className="ikincil-buton" onClick={sifirla}>Tekrar dene</button>
          </div>
        )}

        {asama === ASAMA.TAMAMLANDI && (
          <div className="sonuc-alani">
            <div className="sonuc-basligi">
              <h2>Kitabınız hazır</h2>
              {sayfaSayisi && <span className="sayfa-etiketi">{sayfaSayisi} sayfa</span>}
            </div>

            <a href={indirmeLinki} className="ana-buton indir-buton" download>
              PDF'i indir
            </a>

            <div className="pdf-cerceve">
              <Document
                file={indirmeLinki}
                onLoadSuccess={function (info) { setSayfaSayisi(info.numPages); }}
                loading={<p className="pdf-durum-metni">PDF yükleniyor...</p>}
                error={<p className="pdf-durum-metni">PDF görüntülenemedi, indirme linkini kullanabilirsiniz.</p>}
              >
                {Array.from({ length: sayfaSayisi || 0 }).map(function (_, i) {
                  return <Page key={i} pageNumber={i + 1} width={560} />;
                })}
              </Document>
            </div>

            <button className="ikincil-buton" onClick={sifirla}>Yeni kitap oluştur</button>
          </div>
        )}
      </main>
    </div>
  );
}

export default App;