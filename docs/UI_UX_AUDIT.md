# UI/UX Denetimi (Aşama 5)

Tarih: 2026-10-22. Yöntem: kod incelemesi (tüm rotalar), mevcut testlerin çalıştırılması, daha önce alınan ekran görüntüleri ve Playwright çalıştırmaları. **Başlangıç doğrulaması:** backend 220/220, frontend 69/69, Playwright 6/6, lint/tip/biçim temiz.

## 1. Rota rota denetim (görev başı durumu)

| Rota | Davranış doğrulandı mı | Bulunan sorunlar |
|---|---|---|
| `/` ana sayfa | Evet (Playwright, ekran görüntüsü) | Arama kutusu yok; kategoriler yok; hero'da görsel yok; alt bilgi yalnızca telif satırı; üst menü mobilde çok satıra sarılıyor |
| `/products`, `/products/[id]` | Evet | Kartta `alt=""` ve görsel yüklenemezse (`onError`) yer tutucu yok; kart görseli yok/kırık durumları ayrı ele alınmıyor |
| `/shops`, `/shops/[slug]` | Evet | Mağaza kartlarında görsel/yer tutucu yok; mağaza görseli desteği yok |
| `/cart`, `/checkout` | Evet | Sunucu alan hataları İngilizce ham metin olarak gösteriliyor |
| `/login`, `/register`, `/account` | Evet | Sunucu alan hataları ham; roller ham kimlik olarak gösteriliyor |
| `/account/orders`, `/account/orders/[n]` | Evet | Tablo mobilde yalnızca yatay kaydırma |
| `/seller` (başvuru) | Evet | Sunucu alan hataları ham |
| `/seller/shops/[id]` (özet) | Evet | "Özet" yok: bekleyen sipariş/ürün sayısı yok; mağaza görseli yok |
| `/seller/.../products` | Evet | Arama/filtre yok; tablo mobilde kaydırma |
| `/seller/.../products/new|[id]` | Evet | Görseller **yalnızca URL metin kutusu**: yükleme yok, önizleme yok, sıralama yok; sunucu hataları ham |
| `/seller/.../orders`, `/orders/[n]` | Evet | Kabul/hazırlık/teslim işlemleri **onaysız** tek tıkla; reddetmede onay penceresi yok |
| `/admin` (özet) | Evet | Kritik durumlar (ödeme bekleyen, iade) vurgulu değil |
| `/admin/shops`, `/admin/shops/[id]` | Evet | Tablo mobilde kaydırma |
| `/admin/orders`, `/admin/categories`, `/admin/users` | Evet | Tablolar mobilde kaydırma; roller ham |

## 2. Yerelleştirme boşlukları
- Üç katalog (az/tr/en) tür düzeyinde eşit yapıda, **ama** anahtar tamlığı/yer tutucu (`{count}`) tutarlılığı çalışma zamanında test edilmiyor.
- Sunucudan gelen doğrulama mesajları (İngilizce, FluentValidation) az/tr sayfalarında ham görünüyor (AuthForm, CheckoutForm, SellerHome, ShopProducts, ShopProfileEditor).
- Roller (`Customer`, `FloristOwner`…) çevrilmiyor.
- UI paketinin varsayılan etiketleri İngilizce (`"Loading"`, `"Try again"`, `"Reference"`); tüm çağrı yerleri etiket geçiyor, ama varsayılan kalması risk.
- Kategori adları veri tabanında (yönetici yönetir); tek dilli (Azerbaycanca). Üç dilli kategori adı bu aşamada **kapsam dışı** (belgelenecek).
- Tarih/para biçimi: `Intl` ile yerel biçim kullanılıyor. Node'da `az-Latn-AZ` doğru ("1.234,50 ₼"); Playwright'ın indirdiği Chromium'da `az` ICU verisi yok. Sistemde gerçek **Chrome ve Edge kurulu** → gerçek tarayıcıda doğrulanabilir.

## 3. Duyarlılık sorunları
- Üst menü hamburger değil; giriş yapmış kullanıcıda 7+ öğe birkaç satıra sarılıyor.
- 5 tablo yalnızca `overflow-x-auto` (kaydırılabilir ama eylemler ekran dışına çıkabilir).
- Yalnızca anonim ana sayfa 390 px'te gözle incelenmişti; müşteri/çiçekçi/yönetici sayfaları incelenmedi.

## 4. Görsel (resim) boşlukları
- Ürün görseli yalnızca **dış HTTPS URL**; dosya yükleme yok; mağaza görseli yok.
- `Marketplace:ImageHostAllowlist` **boşsa her HTTPS sunucusu kabul** ediliyor (üretimde güvensiz varsayılan).
- Kırık/eksik görselde yer tutucu yok (yalnızca URL hiç yoksa).
- Güvenlik gereksinimleri (içerik doğrulama, güvenli depolama kimliği, yetki, boyut sınırı, depolama soyutlaması) uygulanmadı.

## 5. Erişilebilirlik
- Var: etiketli alanlar, `aria-invalid`/`describedby`, erişilebilir onay penceresi, "içeriğe geç", durum rozetlerinde metin.
- Eksik/denetlenmemiş: otomatik denetim (axe) yok; kontrast ölçülmedi; mobil menü klavye davranışı yok.

## 6. Öncelikli uygulama planı
1. **Görsel yönetimi (güvenlik):** depolama soyutlaması + yerel geliştirme deposu; yükleme uç noktası (imza/boyut/izin doğrulaması); ürün ve mağaza görseli; boş allowlist artık dış URL'yi **reddeder** (yalnızca geliştirmede açık anahtarla izin).
2. **Yerelleştirme:** sunucu hata metinlerini ham göstermeyi bırak; roller çevrilsin; anahtar/yer tutucu tamlık testleri; UI varsayılanlarını kaldır.
3. **Pazar yapılandırması testleri:** varsayılan AZN/Asia/Baku; testlerdeki TRY varsayımlarını güncelle; biçimleri gerçek Chrome'da doğrula.
4. **Vitrin:** ana sayfa (arama, kategoriler, hero, alt bilgi), mağaza/ürün kartları, güvenli görsel bileşeni, hamburger menü.
5. **Çiçekçi paneli:** özet kartları, ürün arama/filtre, görsel yönetimi, sipariş eylemlerinde onay.
6. **Yönetici paneli:** duyarlı tablo bileşeni, kritik durum vurgusu.
7. **Duyarlılık ve erişilebilirlik:** 390/430/768/1280 px'te üç rol; axe denetimi; düzeltmeler.
8. **Belgeler ve son doğrulama.**

---

## 7. Sonuçlar (Aşama 5 uygulaması sonrası, 2026-10-22)

Bu bölüm yukarıdaki bulguların kapanış durumunu gösterir. Yalnızca gerçekten çalıştırılanlar "doğrulandı" olarak işaretlidir.

| Bulgu | Durum | Nasıl doğrulandı |
|---|---|---|
| Görsel yükleme yok / yalnızca dış URL | **Çözüldü** | `MediaTests` (9 test), Playwright görsel testi (yükleme, önizleme, sıralama, kaldırma) |
| Boş `ImageHostAllowlist` her sunucuyu kabul ediyordu | **Çözüldü**: boş liste artık dış URL'yi reddeder; yalnızca `AllowAnyExternalImageHost=true` (geliştirme/test) izin verir | `ImageUrlPolicyTests`, `MediaTests` |
| Kırık/eksik görselde yer tutucu yok | **Çözüldü**: `SafeImage` (`onError` + hidrasyon öncesi hata tespiti) | Playwright: erişilemeyen sunucu ve görselsiz ürün "Görsel yok" yer tutucusu gösterir |
| Ham sunucu doğrulama metinleri arayüzde | **Çözüldü**: alan adları korunur, metin yerelleştirilir (`localizeFieldErrors`) | Kod incelemesi + tür denetimi |
| Roller/izinler ham kod olarak görünüyordu | **Çözüldü**: `roleNames`, `permissionNames` (3 dil) | Katalog testi |
| `packages/ui` içinde yerleşik İngilizce metin | **Çözüldü**: `Spinner` etiketi zorunlu; `ErrorState` etiketsizse düğme/referans göstermez | `components.test.tsx` |
| `az` çerezi geçerli sayılmıyordu (`isLocale`) | **Çözüldü** | `messages.test.ts` |
| Ana sayfa/kartlar/alt bilgi/hamburger menü | **Çözüldü** | Playwright (menü `aria-expanded`, Escape), görsel inceleme |
| Çiçekçi: özet, arama/filtre, onay pencereleri | **Çözüldü** (kabul/ret/teslim edildi onaylı) | Playwright tam yolculuk |
| Yönetici/çiçekçi/müşteri tabloları taşıyordu | **Çözüldü**: tek `DataTable` (telefonda etiketli kartlar) | Playwright 390/430/768/1280 px: yatay taşma yok (23 sayfa × 4 görünüm) |
| Erişilebilirlik otomatik denetimi yok | **Yapıldı**: axe (WCAG 2 A/AA, 2.1 A/AA), 23 sayfa × 2 görünüm = 46 denetim, ciddi/kritik ihlal yok | Playwright (`@axe-core/playwright`) |
| Tarih/saat tarayıcı saat dilimini kullanıyordu | **Çözüldü**: `Asia/Baku` (yapılandırılabilir) | `format.test.ts` |

### Gerçek tarayıcıda bulunan ve düzeltilen hata

Yüklü **Chrome 155 ve Edge**, `az` yerel ayarını "destekliyor" görünür ama veri eksiktir: tutar `AZN 1,234.50`, tarih `2026 M10 14` çıkar. Node'un tam ICU'su ise doğru üretir (`1.234,50 ₼`, `14 okt 2026`). Yani sunucu ve tarayıcı farklı metin üretiyor (hidrasyon uyuşmazlığı riski) ve kullanıcı yanlış biçim görüyordu. **Çözüm:** `lib/format.ts` Azerbaycan biçimlerini kendisi üretir (ayırıcılar için tam veriye sahip `tr-TR`, CLDR'ye uygun `₼` simgesi ve ay kısaltmaları). Gerçek Chrome'da istemci tarafı çizimle doğrulandı (`@real-browser` testi: sepet `1.234,50 ₼`, sipariş listesi `15 okt 2026, 00:30`). Playwright'ın paketli Chromium'unda `az` ICU verisi hiç yok; bu yüzden bu test `chrome` kanalıyla ayrı bir projede çalışır.

### Elle denetim notları (otomatik araçla kanıtlanamayanlar)

- Klavye: menü düğmesi, onay pencereleri (odak tuzağı, Escape), görsel yöneticisi düğmeleri klavyeyle kullanılabilir (kod ve davranış testi); tam ekran okuyucu denemesi **yapılmadı**.
- Kontrast: axe `color-contrast` kuralı geçti; degradeli hero arka planında axe "incomplete" verebilir, elle kontrol önerilir.
- Resim `alt`: kartlarda ürün adı bitişik olduğundan `alt=""` (süs); ürün detayında satıcının yazdığı açıklama/ürün adı kullanılır.

### Bilinen sınırlar

- Kategori adları veridir (tek dilli, Azerbaycanca); yönetici tek bir ad girer, çeviri alanı yok.
- Azerbaycanca metinler makine kontrolünden geçti (anahtar/yer tutucu tamlığı) ama **anadili konuşan biri tarafından gözden geçirilmedi** (ör. "inzibatçı" = yönetici terimi tartışılabilir).
- Görsel boyutlandırma/optimizasyon yok (tek orijinal dosya sunulur; CDN/optimizer sonraki aşama).
- Çiçekçi için "yolda/kuryede" durumu alan modelinde yok; eklenmedi.
