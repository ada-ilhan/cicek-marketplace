# MVP Boşluk Analizi

Tarih: 2026-10-19. Yöntem: depo incelendi, mevcut testler çalıştırıldı, sonra eksikler uygulanıp yeniden doğrulandı. Önceki raporlara güvenilmedi; **"Başlangıç" sütunu bu görev başında depoda ve çalıştırılan testlerde gerçekten bulunanı** gösterir.

Başlangıç doğrulaması: backend derleme 0 uyarı; 211/211 backend testi; frontend format/lint/tip/52 test geçti; 3 migrasyon uygulanmış. Yönetici arayüzü, Azerbaycan dili, çiçekçi **reddetme** durumu, yönetici özeti/kullanıcı listesi ve demo verisi **yoktu**.

Durum kodları: ✅ tam uygulandı ve doğrulandı · 🟡 uygulandı, kısmen doğrulandı · ◑ kısmi · ❌ eksik · ⛔ dış bağımlılık nedeniyle engelli.

## 1. Müşteri vitrini

| Gereksinim | Başlangıç | Kanıt | Eksik iş | Uygulanan çözüm | Doğrulama | Son durum |
|---|---|---|---|---|---|---|
| Ana sayfa | ◑ Statik başlık | `app/page.tsx` | Gerçek veri | Öne çıkan çiçekçiler + yeni ürünler (API'den), boş/hata durumları, ödeme notu | Playwright, ekran görüntüsü | ✅ |
| Çiçekçi listesi | ✅ | `/shops`, `GET /shops` | — | — | Playwright | ✅ |
| Çiçekçi detayı + ürünleri | ✅ | `/shops/[slug]` | — | — | Playwright | ✅ |
| Ürün listesi: arama/filtre/sıralama/sayfalama | ✅ | `/products` | — | — | API testi, Playwright | ✅ |
| Ürün detayı, stok, fiyat | ✅ | `/products/[id]` | Görsel yoksa yer tutucu | Mevcut "Şəkil yoxdur" yer tutucusu | Ekran görüntüsü | ✅ |
| Askıya alınmış/bekleyen/**reddedilmiş** çiçekçi herkese açık değil | ◑ (reddetme yoktu) | `ShopStatus.Rejected`, katalog sorguları `Active` ister | Reddedilmiş durumu | `Rejected` eklendi | API testi (404), alan testi | ✅ |
| Sepet (ekle/değiştir/sil, sunucu toplamı) | ✅ | `/cart` | — | — | API + bileşen testleri | ✅ |
| Checkout, çoklu çiçekçi bölünmesi açıkça bildirilir | ✅ | `/checkout`, `CartView` split notice | — | — | Playwright | ✅ |
| Fiyat/stok/toplam sunucuda belirlenir; eski fiyatla satış yok | ◑ (fiyat penceresi açıktı) | `ProductStock` | Fiyat+dükkân durumu denetimi | Önceki görevde kapatıldı | 3 Oracle testi | ✅ |
| Kayıt/giriş | ✅ | `/register`, `/login` | — | — | Playwright | ✅ |
| Sipariş geçmişi + detay + durum izleme | ✅ | `/account/orders` | Ödeme/teslimat durumu açık değil | Ödeme durumu + "elle bildirilir, kurye takibi değil" notu | Playwright | ✅ |
| Boş/hata/yükleme/yetkisiz durumlar | 🟡 | Bileşenler | Yetkisiz sayfa tutarlı değildi; 404 yeniden deneniyordu | `retryTransientOnly` (kalıcı hatalar hemen gösterilir); Erişim reddedildi sayfası | Birim + Playwright | ✅ |

## 2. Çiçekçi paneli

| Gereksinim | Başlangıç | Kanıt | Eksik iş | Uygulanan çözüm | Doğrulama | Son durum |
|---|---|---|---|---|---|---|
| Mağaza başvurusu | ✅ | `/seller` | — | — | Playwright | ✅ |
| Onay durumunu görme (bekliyor/reddedildi/askıda/pasif) | ◑ | `ShopDashboard` | Reddedilmiş durumu, gerekçe | Rejected bildirimi + gerekçe | Playwright, API testi | ✅ |
| Reddedilince düzeltip yeniden başvurma; onayı atlayamama | ❌ | — | Yeniden başvuru | `Shop.Resubmit`, `POST /my/shops/{id}/resubmit` (yalnızca sahip, yalnızca Rejected) | Alan + API testleri | ✅ |
| Mağaza bilgilerini düzenleme | ◑ API vardı, arayüz yoktu | `PUT /my/shops/{id}` | Form | `ShopProfileEditor` (sürüm denetimli) | Playwright | ✅ |
| Ürün listele/ekle/düzenle/fiyat-stok/aktif-pasif + doğrulama | ✅ | `/seller/shops/{id}/products` | — | — | API + Playwright | ✅ |
| Boş durum | ✅ | `EmptyState` | — | — | Bileşen | ✅ |
| Başka mağazanın verisine erişim sunucuda engelli | ✅ | `ShopAccess` | — | — | API testi (8 uç nokta 403) | ✅ |
| Siparişler: görüntüle, onayla, reddet(gerekçe), hazırlama, hazır, teslim | ✅ | `/seller/.../orders` | — | — | API + Playwright | ✅ |
| Geçersiz durum geçişi yok; reddedince stok iadesi | ✅ | `OrderStatusTransitions`, `OrderTransitionRunner` | — | — | Alan + API + Oracle testleri | ✅ |
| Manuel teslimat ilerlemesi ≠ kurye takibi | ◑ | — | Açık etiket | Müşteri ve satıcı görünümünde not | Playwright | ✅ |

## 3. Yönetici paneli

| Gereksinim | Başlangıç | Kanıt | Eksik iş | Uygulanan çözüm | Doğrulama | Son durum |
|---|---|---|---|---|---|---|
| Panel + gezinme | ❌ | — | Tümü | `/admin/*`, `AdminShell` (izne göre gezinme, Erişim reddedildi) | Birim + Playwright | ✅ |
| Özet (gerçek veri) | ❌ | — | Uç nokta + ekran | `GET /admin/overview` (izin `admin.overview.read`), `AdminOverviewView` | API testi (sayılar, 403/401), ekran görüntüsü | ✅ |
| Başvuruları listele/incele | ◑ yalnızca API | `GET /admin/shops` | Sahip e-postası, arama, detay | `AdminShopDto`, `GET /admin/shops/{id}`, liste+detay ekranı | API + Playwright | ✅ |
| Onayla/reddet | ◑ reddetme yok | — | Reddetme | `POST /admin/shops/{id}/reject` (gerekçe zorunlu) + onay penceresi | Alan, API, birim, Playwright | ✅ |
| Askıya al/yeniden etkinleştir, komisyon | ◑ yalnızca API | — | Ekran | Detay ekranında onay penceresiyle | Birim + API | ✅ |
| Kullanıcılar (hassas veri yok) | ❌ | — | Uç nokta + ekran | `GET /admin/users` (`users.read`): e-posta, roller, kilit; parola/belirteç/telefon yok | API testi (alan kontrolü) | ✅ |
| Kategori yönetimi | ◑ yalnızca API | `POST/PUT /admin/categories` | Ekran | `AdminCategories` (ekle/düzenle/aktif-pasif) | Playwright | ✅ |
| Sipariş listesi/detay/müdahale | ◑ yalnızca API | — | Ekran | `AdminOrders` (elle ödeme **açık uyarıyla**, iptal onay penceresiyle) | Playwright | ✅ |
| Önemli işlemlerde onay penceresi | ❌ | — | Bileşen | `ConfirmDialog` (erişilebilir, odak tuzağı, gerekçe) | Birim testleri | ✅ |
| Her yönetici işlemi sunucuda izinle korunur | ✅ | `RequirePermission` | — | — | API yetki matrisi | ✅ |
| Genel kayıtla yönetici yetkisi verilemez | ✅ | Kayıt yalnızca `Customer` | — | — | `RegisterCustomerHandler` | ✅ |
| Yönetici oluşturma (yerel) | ✅ | `DbMigrator create-admin` | Belge | README | — | ✅ |

## 4. Kimlik doğrulama ve rol tabanlı erişim

| Gereksinim | Başlangıç | Eksik | Çözüm / Kanıt | Durum |
|---|---|---|---|---|
| Tek kimlik sistemi (JWT + döner yenileme, BFF çerezleri) | ✅ | — | Aşama 2 | ✅ |
| Sunucuda yetkilendirme | ✅ | Yeni uç noktalar | `users.read`, `admin.overview.read` izinleri; testler 403/401 | ✅ |
| Yetkisiz kullanıcı yönlendirme/erişim reddi | ◑ | Yönetici sayfaları | `proxy.ts` + `AdminShell` | ✅ |
| Süresi dolmuş oturum | ✅ | — | Şeffaf yenileme; yenilenemezse temizlenir | ✅ |
| Önyüz korumasının yetki yerine geçmemesi | ✅ | — | Tüm veri API'den, API yetkilendirir | ✅ |
| **Bulunan hata:** sunucu bileşeni, `"use client"` dosyasından nesne içe aktarıyordu (yönetici bile reddediliyordu) | — | — | İzin sabitleri düz modüle taşındı; Playwright yakaladı | ✅ |

## 5. Yerelleştirme ve pazar (Azerbaycan)

| Gereksinim | Başlangıç | Çözüm | Doğrulama | Durum |
|---|---|---|---|---|
| Azerbaycan Latın (`az`) birincil dil | ❌ (tr/en) | `az` kataloğu (tüm ekranlar), varsayılan dil `az`, dil seçici, `html lang` | Birim (katalog bütünlüğü) + Playwright | ✅ |
| Metinler iş mantığına dağılmamış | ✅ | Tür güvenli katalog (eksik anahtar derleme hatası) | `tsc` | ✅ |
| Para birimi AZN, saat dilimi Asia/Baku | ❌ (TRY/İstanbul) | `Marketplace` ayarları; kategori adları Azerbaycanca (migrasyon) | Ekran görüntüsü; testler TRY'ye sabitlenmiş | 🟡 |
| Özet tutarları farklı para birimlerini toplamaz | ❌ | Yalnızca pazar para birimi | Kod incelemesi | 🟡 |

## 6. Ödeme ve teslimat kapsamı

| Gereksinim | Durum |
|---|---|
| Ödeme sağlayıcısı entegrasyonu yok; sahte başarı yok | ✅ Sipariş `Ödəniş gözlənilir` başlar; "ödeme alınmadı" açıkça gösterilir |
| Elle ödeme kaydı yalnızca açıkça etiketli | ✅ Yönetici ekranında uyarı; denetim kaydı |
| Kurye entegrasyonu yok; elle teslimat etiketli | ✅ |
| Genişletilebilir ödeme noktası | 🟡 Yalnızca `Order.MarkPaid` ve `PaymentReference` var; sağlayıcı soyutlaması Aşama 4 |

## 7. Test hesapları ve demo veri

| Gereksinim | Başlangıç | Çözüm | Durum |
|---|---|---|---|
| Geliştirme yöneticisi | ✅ | `DbMigrator create-admin` | ✅ |
| Örnek mağaza/ürün/kategori | ❌ | `DbMigrator seed-demo` (izin değişkeni + ortam değişkeninden parola, Production'da reddedilir, tekrarlanabilir) | ✅ (reddetme ve idempotensi denendi) |
| Parola sabit değil, üretime sızmaz | — | Parola yalnızca `CICEK_DEMO_PASSWORD` | ✅ |

## 8. Doğrulanamayanlar

| Konu | Neden |
|---|---|
| Docker Compose ile tam yığın | Docker servisi kapalıydı |
| GitHub Actions iş akışı | Uzak depo yok |
| Gerçek Chrome'da `az` tarih/para biçimi | Playwright'ın Chromium'unda `az-Latn-AZ` ICU verisi yok (tarih/para İngilizce görünüyor); sunucu tarafı doğru ("45,00 ₼"). Gerçek Chrome/Edge'de kontrol edilmeli |
| Mobil/tablet düzeni | Yalnızca anonim ana sayfa 390 px genişlikte gözle incelendi (taşma yok). Giriş yapmış menü, formlar, tablolar (yatay kaydırmalı) ve yönetici panelinin mobil görünümü incelenmedi |
| Erişilebilirlik | Rol/etiket testleri var; ekran okuyucu veya otomatik denetim (axe) çalıştırılmadı |
