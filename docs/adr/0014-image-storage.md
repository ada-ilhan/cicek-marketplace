# ADR-0014: Görsel depolama ve doğrulama

Durum: kabul edildi (2026-10-22)

## Bağlam
Ürün görselleri yalnızca dış HTTPS adresiydi; boş `ImageHostAllowlist` her sunucuyu kabul ediyordu. Çiçekçilerin kendi görsellerini yükleyebilmesi gerekiyor, ancak ikili veri Oracle'a girmemeli ve güvenli olmalı.

## Karar
- **Depolama soyutlaması:** `IImageStorage` (Application). Oracle yalnızca üst veriyi tutar (`MED_IMAGES`: kimlik, mağaza, depolama anahtarı, içerik türü, boyut, yükleyen, zaman).
- **Sağlayıcılar:** `None` (varsayılan, yükleme 503 verir) ve `Local` (**yalnızca geliştirme/test**, API ana makinesinin diski). Üretim için nesne depolama bağdaştırıcısı (S3 uyumlu, Azure Blob vb.) aynı arayüzü uygular; henüz yazılmadı ve satıcı/kimlik bilgisi koda gömülmedi.
- **İçerik doğrulama:** tür dosyanın ilk baytlarından (JPEG/PNG/WebP) belirlenir; dosya adı ve istemci MIME türü yok sayılır. SVG ve GIF reddedilir (betik/animasyon riski). Boyut `Media:MaxBytes` (5 MB) ile akış sırasında sınırlanır; istek gövdesi 8 MB ile kesilir.
- **Güvenli kimlik:** depolama anahtarı `{guid}.{jpg|png|webp}`; kullanıcı girdisi yola girmez, yerel sağlayıcı anahtarı yeniden doğrular ve klasör dışına çıkışı reddeder.
- **Sahiplik:** yükleme `ManageCatalog` üyeliği ister; ürün/mağaza kaydederken `/media/{guid}` başvurusu **aynı mağazaya** ait olmalı (aksi 400 `media.invalid_reference`). Mağaza başvurusu sırasında henüz mağaza olmadığından platform görseli başvurusu reddedilir.
- **Dış URL:** yalnızca HTTPS, kullanıcı bilgisi yok, allowlist zorunlu; boş liste reddeder. `Marketplace:AllowAnyExternalImageHost=true` yalnızca geliştirme/test için.
- **Sunum:** `GET /api/v1/media/{id}` anonim, `Cache-Control: public, max-age=31536000, immutable`, `X-Content-Type-Options: nosniff`, ETag; vitrin `/media/{id}` rotası akışı aynı kökenden iletir ve `CSP: sandbox` ekler.
- **Kötüye kullanım sınırları:** yükleme hız sınırı (`RateLimiting:UploadPermitsPerMinute`, 30), mağaza başına en fazla 300 görsel.

## Sonuçlar
- Yüklenen dosyalar silinmez (yetim temizliği ve kullanılmayan görsel silme sonraki iş).
- Üretimde bir nesne depolama bağdaştırıcısı ve CDN gerekir; yoksa yükleme kapalıdır (fail-safe).
- Yeniden boyutlandırma/optimizasyon yoktur.
