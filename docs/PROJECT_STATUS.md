# Proje Durumu

Son güncelleme: 2026-10-28 (Aşama 6: üretim hazırlığı, Docker Compose ve CI doğrulaması; önceki: Aşama 5: arayüz cilası, üç dilli yerelleştirme, görsel deneyimi). Ayrıntı: [UI_UX_AUDIT.md](UI_UX_AUDIT.md) · [adr/0014-image-storage.md](adr/0014-image-storage.md). Ayrıntı: [MVP_GAP_ANALYSIS.md](MVP_GAP_ANALYSIS.md) · [DEVELOPMENT_PROGRESS.md](DEVELOPMENT_PROGRESS.md) · [marketplace-core.md](marketplace-core.md) (İngilizce teknik başvuru).

## 0. Aşama 6 – Üretim hazırlığı (2026-10-28)

**Docker Compose gerçekten çalıştırıldı** (Docker 29.9, Oracle AI Database 26ai Free, yeni adlandırılmış birimler; mevcut yerel Oracle XE'ye dokunulmadı): imajlar derlendi, Oracle sağlıklı, migrator 6 migrasyonu uyguladı, API/storefront sağlıklı, Playwright 16/16 geçti, konteynerler yeniden başlatıldı ve veriler ile yüklenen görsel korundu.

**Bulunan ve düzeltilen hatalar**
1. **Oracle kurulum betiği hiç çalışmıyordu:** güncel Oracle Free imajı hazır veritabanıyla geliyor, `scripts/setup` kancası yalnızca ilk oluşturmada çalışıyor. Betik `scripts/startup` altına alındı (idempotent), parolalar şifreli terminal yerine tanımlı değişkenlerle verilir (`create_schema_users_impl.sql`).
2. **CI'daki Oracle bootstrap adımı da bozuktu** (şifreli `ACCEPT` boru ile okunamıyor, ORA-01741); aynı yöntemle düzeltildi. Yerelde aynı Oracle imajında doğrulandı, uzak CI **çalıştırılmadı**.
3. **Derleme güvenlik açığı:** `System.Security.Cryptography.Pkcs 8.0.1` (yüksek+orta, ODP.NET'ten geçişli) Docker derlemesini düşürüyordu (NU1903, uyarılar hata) → 10.0.14'e sabitlendi.
4. **Next.js 16.4.0 kritik açıkları** (RCE/DoS) → 16.4.3. `npm audit --omit=dev`: 0 açık.
5. **API sağlık denetimi** `/bin/sh` altında çalışmıyordu (`/dev/tcp`) → `bash`.
6. **Görsel deposu** konteynerde yazılamıyordu → adlandırılmış birim + yazılabilir dizin.
7. **`AzerbaijaniCategoryNames` migrasyonu** yönetici düzenlemelerini ezebilirdi → yalnızca hâlâ özgün Türkçe tohum adını taşıyan satırları günceller (Oracle testiyle kanıtlandı).
8. **Üretimde güvensiz ayarlar** (`AllowAnyExternalImageHost`, `Media:Provider=Local`) artık başlangıçta hata verir; zayıf/eksik JWT anahtarı başlangıçta hata verir ve değeri yazmaz. Gerçek imajda doğrulandı.

**Testler (2026-10-28):** backend **283/283** (254 + 29 yeni), frontend 91, Playwright **16/16** (Compose yığınına karşı; 4 tam koşudan 2'sinde soğuk başlangıçta marketplace yolculuğu zaman aşımına uğradı, tek başına ve sonraki 3 tam koşuda geçti).

**Hâlâ yapılmadı:** uzak CI koşusu (depoda commit/uzak yok), temiz kurulumun Oracle XE 21c üzerinde doğrulanması, dev-only npm bağımlılık açıkları (vitest/tinypool; üretime girmez), parola sıfırlama / e-posta doğrulama, üretim görsel depolama, ödeme/kurye.

## 1. Genel durum

Azerbaycan pazarı için **üç paneli birbirine bağlı, çalışan bir MVP** var: müşteri vitrini, çiçekçi paneli ve yönetici paneli. Hepsi gerçek API ve Oracle ile konuşuyor; sahte veri yok. Birincil dil Azerbaycan Latın (`az`), para birimi AZN, saat dilimi Asia/Baku (tr ve en de korunuyor).

**Üretime hazır değil.** Ödeme sağlayıcısı ve kurye entegrasyonu bilerek kapsam dışı; ödemeyi yönetici elle kaydediyor. Docker Compose ve CI hiç çalıştırılmadı. Depoda hâlâ hiç commit yok.

## 1a. Aşama 5 özeti (2026-10-22)

**Görevde bulunan:** çalışan MVP ama görsel yükleme yok, boş allowlist her sunucuyu kabul ediyor, ham sunucu hata metinleri, `packages/ui` içinde İngilizce, tablolar telefonda taşıyor, otomatik erişilebilirlik denetimi yok, tarihler tarayıcı saat diliminde.

**Değişenler**
- **Görseller:** `IImageStorage` soyutlaması, `MED_IMAGES` üst veri tablosu (migrasyon `ProductAndShopImages`), `POST /my/shops/{id}/images`, anonim `GET /media/{id}`; içerik bayttan doğrulanır (JPEG/PNG/WebP), 5 MB sınırı, güvenli üretilmiş kimlik, mağaza sahipliği zorunlu, ikili veri Oracle dışında. Geliştirme için `Local` depolama; üretim bağdaştırıcısı **yok** (yükleme kapalı kalır). Boş allowlist artık dış URL'yi reddeder. Mağaza görseli eklendi.
- **Yerelleştirme:** az/tr/en tamamlandı; `az` çerezi hatası düzeltildi; ham sunucu metni yok; roller/izinler çevrildi; `packages/ui` dilsiz; katalog tamlık testleri.
- **Biçimlendirme:** varsayılanlar AZN / Asia/Baku; tarihler pazar saat diliminde; **gerçek Chrome/Edge'de `az` Intl verisinin eksik olduğu bulundu** ve uygulama kendi `az` biçimleyicisini kullanıyor (bkz. UI_UX_AUDIT §7).
- **Arayüz:** ana sayfa (hero, arama, kategoriler, yeni ürünler, çiçekçiler, nasıl çalışır), ürün/mağaza kartları (sabit oran, yer tutucu), hamburger menü, zengin alt bilgi, çiçekçi özeti ve arama/filtre, görsel yöneticisi, kabul/ret/teslim onay pencereleri, duyarlı `DataTable`, kritik durum vurgusu.
- **Testler:** testlerdeki TRY/İstanbul varsayımları AZN/Bakü oldu (çok para birimli testler korundu).

## 1b. Üretimde gerekli yapılandırma

| Ayar | Neden |
|---|---|
| `Media__Provider` + bir nesne depolama bağdaştırıcısı | `None` iken yükleme 503. `Local` üretimde **kullanılmamalı** |
| `Marketplace__ImageHostAllowlist` | Dış görsel URL'leri için zorunlu; boşsa hepsi reddedilir. `AllowAnyExternalImageHost` yalnızca geliştirme |
| `Marketplace__Currency=AZN`, `Marketplace__TimeZone=Asia/Baku` | Pazar varsayılanları |
| `NEXT_PUBLIC_DEFAULT_LOCALE`, `NEXT_PUBLIC_MARKET_TIME_ZONE` | Vitrin dili ve saat dilimi (API ile aynı) |
| `RateLimiting__UploadPermitsPerMinute` | Yükleme sınırı (varsayılan 30/dk/IP) |
| CDN / önbellek | Görseller değişmez önbellekle sunulur; boyutlandırma yok |

## 2. Önceki görev (MVP) başında durum

- Backend ve frontend temizdi (211 backend, 52 frontend testi geçiyordu; 3 migrasyon uygulanmış).
- **Yoktu:** yönetici arayüzü, Azerbaycan dili, çiçekçi başvurusunu reddetme, yönetici özeti ve kullanıcı listesi, demo verisi, canlı ana sayfa.

## 3. Bu görevde tamamlananlar

**Backend**
- `Rejected` mağaza durumu: yönetici gerekçeyle reddeder, sahibi düzeltip **yeniden başvurur**, onay atlanamaz. (`Shop.Reject/Resubmit`, `POST /admin/shops/{id}/reject`, `POST /my/shops/{id}/resubmit`.)
- `GET /admin/overview` (sayılar, tutarlar; yalnızca pazar para biriminde), `GET /admin/users` (hassas alan yok), `GET /admin/shops` artık sahip e-postası/ürün sayısı/arama ile, `GET /admin/shops/{id}`.
- Yeni izinler `admin.overview.read`, `users.read` (Administrator, Support).
- Üç migrasyon sonrası şema: `ShopRejectedStatus` (CHECK kısıtı, ham SQL), `AzerbaijaniCategoryNames` (yalnızca ad güncellemesi).
- `DbMigrator seed-demo` (yalnızca geliştirme).
- Bulunup düzeltilen hata: `Reject`/`Suspend` doğrulamadan önce durumu değiştiriyordu.

**Frontend**
- Yönetici paneli: özet, çiçekçi listesi/detayı (onayla/reddet/askıya al/yeniden etkinleştir, komisyon), siparişler (elle ödeme, iptal), kategoriler, kullanıcılar; erişilebilir `ConfirmDialog`.
- Çiçekçi paneli: reddedilme bildirimi + yeniden başvuru, mağaza bilgilerini düzenleme.
- Müşteri: canlı ana sayfa, ödeme durumu ve "elle bildirilir, kurye takibi değil" notları.
- Azerbaycanca (tam katalog), dil seçici, AZN.
- Kalıcı hatalar (401/403/404) artık yeniden denenmiyor.
- Bulunup düzeltilen hata: sunucu bileşenleri `"use client"` dosyasından nesne alıyordu, yönetici bile "Erişim reddedildi" görüyordu.

## 4. Çalışan akışlar (gerçek arayüz + Oracle ile doğrulandı)

Playwright, tüm adımları yalnızca arayüzden yürüten **tek yolculuk** çalıştırır: ziyaretçi sayfaları görür → çiçekçi kayıt olup başvurur (bekliyor) → bekleyen mağaza herkese açık değil → yönetici reddeder (gerekçe zorunlu) → çiçekçi gerekçeyi görür, düzeltir, yeniden başvurur → yönetici onaylar → çiçekçi ürün ekler (geçersiz fiyatta hata) ve düzenler → müşteri mağazaya girer, sepete ekler (sunucu toplamı), checkout yapar → sipariş geçmişinde görür ("ödeme alınmadı") → yönetici elle ödemeyi kaydeder → çiçekçi onaylar/hazırlar/teslim eder → müşteri durumu görür, komisyon görmez → başka müşteri siparişi göremez, mağaza yönetemez, yönetici sayfalarına erişemez → yönetici kategori ekler, kullanıcıyı arar, özeti görür.

## 5. Build ve test sonuçları (2026-10-22, hepsi bu görevde çalıştırıldı)

| Kontrol | Sonuç |
|---|---|
| `dotnet build` (uyarılar hata) | **Geçti**, 0 uyarı |
| `dotnet format --verify-no-changes` | **Geçti** |
| `dotnet test --solution Cicek.slnx` | **254 geçti**, 0 başarısız, 0 atlanan (başlangıçta 220) |
| Frontend `format:check`, `lint`, `typecheck` | **Geçti** |
| Frontend birim/bileşen testleri | **91 geçti** (storefront 69, api-client 10, ui 12; başlangıçta 69) |
| Frontend `npm run build` | **Geçti** |
| Playwright (gerçek API + Oracle) | **16 geçti**: 4 duman/dil, kimlik akışı, tam yolculuk, görsel, dil kalıcılığı, 4 görünüm × 23 sayfa taşma denetimi, axe (46 denetim), tablo/menü davranışı, gerçek Chrome `az` biçimi |
| Docker Compose / CI iş akışı | **Çalıştırılmadı** (Docker kapalıydı) |

Playwright'ı çalıştırmak için API'nin kimlik doğrulama hız sınırını gevşetmek gerekir (`RateLimiting__AuthPermitsPerMinute=1000`); aksi halde çok sayıda kayıt 429 verebilir.

## 6. Oracle ve migrasyon durumu

- Oracle 21c XE (`XEPDB1`) çalışıyor; uygulama ve sahip hesabıyla fiilen bağlanıldı.
- Uygulanmış 6 migrasyon, 0 bekleyen: `InitialIdentity`, `Marketplace`, `NullableOptionalText`, `ShopRejectedStatus`, `AzerbaijaniCategoryNames`, `ProductAndShopImages` (`SHP_SHOPS.IMAGE_URL` + `MED_IMAGES`; yeni tablo yetkileri `cicek_schema_access` ile otomatik eşitlendi).
- **Provider kusuru (ikinci kez):** `AddCheckConstraint` ayrı `ALTER TABLE` olarak çift tırnakla üretiliyor ve Oracle'da geçersiz. Migrasyon ham SQL ile yazıldı; bunun tekrarını engelleyen bir test eklendi.
- Geliştirme veritabanında önceki aşamalardan kalan TRY siparişler var; yönetici özeti yalnızca pazar para birimini (AZN) toplar.
- 19c/26ai ve Docker Oracle Free ile bağlantı **doğrulanmadı**.

## 7. Yetkilendirme nasıl çalışır

Tarayıcı belirteci hiç görmez: Next.js BFF, JWT erişim ve yenileme belirteçlerini HttpOnly çerezlerde tutar ve API'ye taşır. API her uç noktada kontrol eder: **yönetici** izinleri (`shops.manage`, `categories.manage`, `orders.read/manage`, `admin.overview.read`, `users.read`) rollerden türetilir; **çiçekçi** hakları mağaza üyeliğinden (Owner/Staff) gelir ve her `/my/shops/{id}/…` çağrısında kontrol edilir; **müşteri** yalnızca kendi sepetine/siparişlerine erişir (başkasınınki 404). Arayüzdeki gezinme ve "Erişim reddedildi" sayfası yalnızca kullanım kolaylığıdır; yetki API'dedir. Genel kayıt yalnızca `Customer` rolü verir; yönetici yalnızca `DbMigrator create-admin` ile oluşur.

## 8. Bilinen riskler

| Risk | Öneri |
|---|---|
| İş kararları onaysız (çoklu çiçekçi sepeti, sabit teslimat ücreti, komisyon tabanı, elle ödeme) | ADR-0012 varsayımlarını onaylayın |
| Depoda hiç commit yok | Hemen taban commit |
| `ImageHostAllowlist` boş (her HTTPS sunucusu) | Üretimde CDN sunucusunu yazın |
| Para birimi AZN'ye geçti ama **testler TRY'ye sabit**; AZN'e özel biçim testi yok | Aşama 4'te AZN senaryosu ekleyin |
| `az` tarih/para biçimi Playwright Chromium'unda doğrulanamadı (ICU verisi yok) | Gerçek Chrome/Edge'de gözle kontrol edin |
| Kategori adı migrasyonu, yöneticinin elle yeniden adlandırdığı tohum kategorileri eziyor | Üretimde bu migrasyondan önce adları kontrol edin |
| Docker/CI doğrulanmadı; Node 22.11 eski lint/test araçlarını zorunlu kılıyor | Compose ve CI'yi çalıştırın; Node ≥22.13 |
| Erişilebilirlik otomatik denetimi (axe) ve mobilde panel incelemesi yapılmadı | Aşama 4'te ekleyin |
| Yönetici paneli storefront uygulamasında (`/admin`); ayrı alan adı/başlık politikası yok | ADR-0013; ihtiyaç olursa ayrı uygulamaya ayırın |

## 9. Engeller

| Engel | Etki |
|---|---|
| Ödeme sağlayıcısı ve kurye henüz seçilmedi | Ödeme/teslimat entegrasyonu yapılamaz (kasıtlı kapsam dışı) |
| Docker servisi kapalı, uzak depo yok | Compose ve CI doğrulanamadı |

## 10. Eksik / kısmi

- **Eksik:** ödeme sağlayıcısı, iade yürütme, hakediş, kurye, bildirimler, personel daveti, görsel yükleme, teslimat bölgeleri/saatleri, varyantlar, şifre sıfırlama, e-posta doğrulama, promosyonlar, yorumlar, misafir alışverişi.
- **Kısmi:** ödeme (yalnızca elle kayıt), teslimat (satıcı elle bildirir).

## 11. Önerilen sonraki aşama

Ödeme entegrasyonu ve sipariş bildirimleri; ayrıntı ve kabul ölçütleri `DEVELOPMENT_PROGRESS.md` §6'da.

## 7. Kalan sorunlar (Aşama 5 sonrası)

- Üretim görsel depolama bağdaştırıcısı yok; yüklenen dosyalar silinmiyor (yetim temizliği yok); boyutlandırma/optimizasyon yok.
- Azerbaycanca metinler anadili konuşan biri tarafından gözden geçirilmedi.
- Kategori adları tek dilli veri.
- Ekran okuyucuyla elle deneme yapılmadı; axe yalnızca otomatik denetimdir.
- Docker Compose ve CI çalıştırılmadı; depoda hâlâ commit yok.
- Ödeme ve kurye kapsam dışı (önceki aşamalarla aynı).
