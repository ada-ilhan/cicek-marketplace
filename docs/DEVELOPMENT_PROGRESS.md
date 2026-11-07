# Geliştirme İlerlemesi

Kronolojik ve doğrulanabilir özet. Güncel durum: [PROJECT_STATUS.md](PROJECT_STATUS.md). Boşluk analizi: [MVP_GAP_ANALYSIS.md](MVP_GAP_ANALYSIS.md).

## 1. Kronoloji

| Tarih | Aşama | Sonuç |
|---|---|---|
| 2026-10-02 | 1 – Mimari | Mimari, alan modeli, durum makineleri, API planı, ADR'ler (kod yok) |
| 2026-10-13 | 2 – Oracle temeli | Çözüm yapısı, en az yetkili Oracle hesapları, migrator, PL/SQL paketi, kimlik doğrulama, API altyapısı, Next.js iskeleti, Compose, CI. 110 backend testi |
| 2026-10-13 | 3 – Çekirdek pazaryeri | Çiçekçiler, katalog, sepet, checkout, siparişler, komisyon; 208 backend + 52 frontend + 5 tarayıcı testi |
| 2026-10-19 | Kurtarma/denetim | Tam denetim; checkout'ta eski fiyatla satış penceresi kapatıldı; 211 backend testi |
| 2026-10-19 | **MVP tamamlama** | Yönetici paneli, `Rejected` durumu, Azerbaycanca/AZN, demo verisi, canlı ana sayfa; 220 backend + 69 frontend + 6 tarayıcı testi |

## 2. Bu görevde değişen dosyalar (önemliler)

**Backend**
| Dosya | Neden |
|---|---|
| `Domain/Shops/Shop.cs` | `Rejected` durumu, `Reject`, `Resubmit`; doğrulama durum değişikliğinden önce |
| `Application/Shops/ShopHandlers.cs` | `AdminShopDto` (sahip e-postası, ürün sayısı), arama, `GetShopForAdminHandler`, reddetme, `ResubmitShopHandler` |
| `Application/Admin/AdminHandlers.cs`, `Abstractions/Identity/IUserDirectory.cs` | Özet ve kullanıcı listesi (yalnızca pazar para biriminde toplam) |
| `Infrastructure/Identity/UserDirectory.cs` | Kimlik deposundan salt-okunur kullanıcı listesi (hassas alan yok) |
| `Application/Authorization/*` | `admin.overview.read`, `users.read` |
| `Api/Endpoints/AdminEndpoints.cs`, `ShopEndpoints.cs`, `Program.cs` | Yeni/değişen uç noktalar |
| `Infrastructure/Persistence/Migrations/*ShopRejectedStatus*`, `*AzerbaijaniCategoryNames*`, `CatalogConfigurations.cs` | Şema ve tohum veri |
| `DbMigrator/DemoDataSeeder.cs`, `MigratorCommands.cs`, `Program.cs` | `seed-demo` |
| `Api/appsettings.json`, `DbMigrator/appsettings.json` | AZN, Asia/Baku |
| `tests/…/AdminDashboardTests.cs`, `ShopTests.cs`, `MigrationSafetyTests.cs`, `ApiTestHost.cs` | Yeni testler; test sunucusu TRY'ye sabit |

**Frontend**
| Dosya | Neden |
|---|---|
| `features/admin/*`, `app/admin/**` | Yönetici paneli (özet, çiçekçiler, siparişler, kategoriler, kullanıcılar) |
| `packages/ui/src/ConfirmDialog.tsx` | Erişilebilir onay penceresi |
| `lib/i18n/{az-messages,market-messages-az,admin-messages}.ts`, `messages.ts` | Azerbaycanca + yönetici metinleri, varsayılan `az` |
| `components/{LocaleSwitcher,AdminNavLink}.tsx`, `app/layout.tsx`, `proxy.ts` | Dil seçici, role duyarlı gezinme, `/admin` koruması |
| `app/page.tsx` | Canlı ana sayfa |
| `features/seller/{ShopProfileEditor,profile,ShopDashboard}.tsx` | Reddedilme/yeniden başvuru, profil düzenleme |
| `features/orders/OrderComponents.tsx` | Ödeme/teslimat durumu |
| `lib/query-retry.ts`, `app/providers.tsx` | Kalıcı hatalar yeniden denenmez |
| `e2e/*`, `playwright.config.ts` | Tam yolculuk arayüzden; dil sabitlenmiş |

## 3. Önemli düzeltmeler

- `Reject`/`Suspend` geçersiz gerekçede durumu yarım güncelliyordu → doğrula, sonra değiştir (test eklendi).
- Migrasyonda `AddCheckConstraint` çift tırnakla geçersiz SQL üretiyordu → ham SQL + engelleyici test.
- Sunucu bileşenlerinde `"use client"` dosyasından nesne içe aktarma → yönetici reddediliyordu → sabitler düz modüle.
- 404/403 yanıtları 7 sn yeniden deneniyordu → yalnızca geçici hatalar yeniden denenir.
- Yönetici özeti farklı para birimlerini topluyordu → yalnızca pazar para birimi.

## 4. Yapılan kontroller

Bkz. `PROJECT_STATUS.md` §5. Hepsi bu görevde çalıştırıldı; Docker/CI hariç hepsi geçti.

## 5. Bekleyen işler

1. Taban commit; Docker Compose ve CI'nin çalıştırılması.
2. AZN senaryosuna özel testler; gerçek Chrome'da `az` biçim kontrolü; axe ve mobil panel incelemesi.
3. İş kararları (ADR-0012) ve ödeme/kurye seçimi.

## 6. Önerilen sonraki aşama: Aşama 4 – Ödeme ve bildirimler

**Neden:** MVP akışı eksiksiz; tek büyük boşluk, ödemenin elle yapılması. Platform gelir toplayamıyor ve iade borçları kapanmıyor.

**Ön koşullar:** taban commit; Docker/CI doğrulaması; sağlayıcı seçimi (Azerbaycan'da iş anlaşmasıyla), bölünmüş ödeme/ön provizyon desteği, iade politikası, kurye firması.

**Kapsam:** ödeme sağlayıcısı soyutlaması + gerçek adaptör + geliştirme sahtesi (`Checkout` düzeyinde ödeme); imzalı webhook doğrulaması ve `InboxMessage` ile yineleme engelleme; outbox; zaman aşımına uğrayan ödemeler için mutabakat işi; `RefundDue` için iade yürütme; sipariş bildirimleri (az/tr/en şablonlar); yönetici panelinde ödeme/iade izleme.

**Kabul ölçütleri:** aynı webhook iki kez gelince tek etki; ödeme gelip rezervasyon süresi dolmuşsa otomatik iade/iptal; sağlayıcı zaman aşımında mutabakat durumu çözer; başarısız iade yutulmaz ve yönetici listesinde görünür; elle ödeme düğmesi yalnızca geliştirme/test yapılandırmasında kalır; tüm yeni Oracle testleri geçer; sır yok.

---

# Aşama 5 – Arayüz cilası, üç dilli yerelleştirme, görsel deneyimi (2026-10-22)

## Yapılanlar (sırayla)
1. **Denetim:** `UI_UX_AUDIT.md` kod değişmeden önce yazıldı (rota rota bulgular, 8 adımlı plan).
2. **Görsel altyapısı (backend):** `ImageReference`/`ImageSignature`/`MediaImage` (Domain); `IImageStorage`, `UploadShopImageHandler`, `GetMediaHandler`, `MediaReferenceValidator` (Application); `LocalFileImageStorage`/`UnconfiguredImageStorage`, `MED_IMAGES` yapılandırması, migrasyon (Infrastructure); `MediaEndpoints`, yükleme hız sınırı (API). Ürün kayıt listesi `q` ve `outOfStock` süzgeci kazandı.
3. **Pazar varsayılanları:** `MarketplaceOptions` AZN/Asia/Baku; test ana makinesi ve Oracle testleri AZN'e çevrildi.
4. **Frontend:** `SafeImage`, `ImageManager`, `ShopImageField`, `/media/[id]` rotası, `bffUpload`, `DataTable`, `SiteNav`, `SiteFooter`, `ShopCard`, yeni ana sayfa, çiçekçi özeti, onay pencereleri, `localizeFieldErrors`, `labels.ts`.
5. **Yerelleştirme:** `media`, `home`, `footer`, `seller` (özet/onay), `admin` (rol/izin) anahtarları üç dilde; `messages.test.ts` (anahtar eşitliği, yer tutucu uyumu, boş metin yok, İngilizce kopya tespiti, rol/izin/durum kapsamı).
6. **Gerçek tarayıcı doğrulaması:** Chrome/Edge'in eksik `az` Intl verisi bulundu; `format.ts` kendi `az` biçimleyicisini kazandı.
7. **Doğrulama:** Playwright (görsel, dil, taşma, axe, gerçek Chrome), tam backend/frontend kontrolleri.

## Önemli düzeltmeler
- `isLocale("az")` yanlış `false` dönüyordu (çerez yok sayılıyordu).
- Tarih/saat tarayıcı saat dilimiyle biçimleniyordu (sunucu/istemci farkı) → pazar saat dilimi.
- `az` biçimleri gerçek tarayıcıda yanlıştı (bkz. yukarı).
- Tabloların telefonda taşması → `DataTable`.
- Perl ile yapılan toplu değiştirme `Product.cs`'i bozmuştu; düzeltildi (özel karakterli değişiklikler için Edit kullanıldı).

## Dikkat
- Üretilen göç (`ProductAndShopImages`) önce yanlış kolon türüyle üretilmişti; hiç uygulanmadan silinip doğru türle (`VARCHAR2(1000)`) yeniden yazıldı. Uygulanmış hiçbir migrasyona dokunulmadı.

## Sonraki aşama: Aşama 4 (Ödeme ve bildirimler)
Plan değişmedi (bkz. önceki bölüm). Ek ön koşul: üretim görsel depolama bağdaştırıcısı ve CDN kararı.

---

# Aşama 6 – Üretim hazırlığı, Docker Compose ve CI doğrulaması (2026-10-28)

## Değişen dosyalar
- `backend/src/Cicek.Api/Configuration/ProductionSafetyValidators.cs`, `Program.cs`: üretimde geliştirme anahtarlarını reddeden seçenek doğrulayıcıları.
- `backend/src/Cicek.Infrastructure/Persistence/Migrations/20261019154100_AzerbaijaniCategoryNames.cs`: korumalı UPDATE.
- `backend/Directory.Packages.props`: `System.Security.Cryptography.Pkcs` 10.0.14 sabitlemesi (geçişli sabitleme açık).
- `deploy/docker-compose.yml`, `deploy/docker/api.Dockerfile`, `deploy/oracle/setup/01_create_cicek_users.sh`, `deploy/.env.example`: sağlık denetimleri, startup betiği, medya birimi, `AUTH_RATE_LIMIT`.
- `db/bootstrap/create_schema_users.sql` + yeni `create_schema_users_impl.sql`; `.github/workflows/ci.yml` bootstrap adımı.
- `frontend/apps/storefront/package.json`, `package-lock.json`: Next.js 16.4.3.
- Testler: `ProductionSafetyTests` (Api.Tests), `CategoryNameMigrationTests` (Oracle), `MigrationSafetyTests`.

## Yöntem ve güvenlik
- `deploy/.env` kullanıcı onayıyla rastgele yerel sırlarla oluşturuldu (git dışı, değerler hiçbir yerde yazdırılmadı).
- Yıkıcı komut kullanılmadı (`down -v` yok). Yalnızca bu oturumda oluşturduğum boş `CICEK_OWNER` kullanıcısı (0 nesne) ve geçici `CICEK_TMP_*` kullanıcıları, bootstrap düzeltmesini sınamak için Compose veritabanında silindi.
- Geliştirme veritabanına (XE) bu aşamada dokunulmadı; Playwright yalnızca Compose yığınına karşı koşuldu.

## Sonraki aşama
Üretim görsel depolama bağdaştırıcısı, hesap kurtarma/e-posta doğrulama ve sipariş teslimat durumu; ödeme yalnızca bunlardan sonra.


---

# Aşama 7 – Yürütme kaydı (2026-11-03'ten itibaren)

Durum etiketleri: COMPLETED / IN PROGRESS / FAILED / BLOCKED / NOT VERIFIED / NOT STARTED. Commit yok (taban commit onay bekliyor).

## Görev 1 — `cicek-e2e2` kalıntıları: **COMPLETED (inceleme); silme onay bekliyor**
- Kalıntılar: volume `cicek-e2e2_oracle-data` (1,99 GB, bağlı konteyner yok), `cicek-e2e2_media-data` (0 B), ağ `cicek-e2e2_default`, imajlar `cicek-e2e2-{api,migrator,storefront}` (366/326/338 MB; diğer projelerle paylaşılan imaj kimliği yok). Konteyner yok.
- Repo, betik veya Compose dosyalarında `e2e2` referansı yok. Gelecek testlere müdahale riski yok (ayrı proje adı).
- **Öneri (onay gerekir):** `docker volume rm cicek-e2e2_oracle-data cicek-e2e2_media-data`, `docker network rm cicek-e2e2_default`, `docker image rm cicek-e2e2-api cicek-e2e2-migrator cicek-e2e2-storefront`. Birimdeki veritabanı yarım kalmış kurulumdur, değerli veri içermez. Kazanç ≈ 2,7 GB. Hiçbir şey silinmedi.

## Görev 2 — Bağımlılık denetimi: **COMPLETED**
- .NET: `--vulnerable --include-transitive` tüm projelerde **0**; `--deprecated` 0. Microsoft paketleri 10.0.12 → 10.0.14 (yama), build 0 uyarı, format temiz, **283/283 test geçti**. `Oracle.EntityFrameworkCore` 10.23.26301 → 10.23.26400 **uygulanmadı** (sağlayıcı güncellemesi, ayrı doğrulama gerekir).
- npm üretim bağımlılıkları: **0 açık**. Önceki durum: 2 kritik (`vitest` doğrudan, `tinypool` geçişli; yalnızca geliştirme aracı) + 20 orta. Uygulanan: `vitest` 3.2.7 → 4.1.12, `vite` 6.4.4 → 6.4.5, `next`/`eslint-config-next` 16.4.3 → 16.4.4 (yama). Sonuç: **kritik 0, yüksek 0, 18 orta** (hepsi geliştirme aracı zinciri: eslint/minimatch DoS, openapi-typescript→redocly). npm'in önerdiği düzeltmeler anlamsız majör düşürmeler (`eslint@4`), uygulanmadı.
- Yeniden doğrulama: format, lint, typecheck exit 0; 69+10+12 test geçti (vitest 4 ile); `next build` geçti.
- Kalan: Next/React/Tailwind/TanStack/Playwright küçük yama sürümleri, `typescript` 7 ve `jsdom` 27 majör (uygulanmadı).

## Görev 3 — Playwright E2E kararlılığı: **COMPLETED (yeniden üretilebilir geçiş); kök neden KISMEN KANITLANDI**
- Bulgu 1 (doğrulandı): Çalışan `cicek-e2e` storefront imajı 28 Eki 13:40'ta, `KEEP_ALIVE_TIMEOUT` eklenmeden **önce** derlenmişti ve `globalSetup` yoktu; önceki başarısız koşular düzeltmeleri hiç içermiyordu.
- Yapılan: yalnızca `api` ve `storefront` imajları yeniden derlendi (`up -d --build --no-deps`, 52 sn; Oracle dokunulmadı, birimler korundu). Yeni imajda `KEEP_ALIVE_TIMEOUT` etkin, Next 16.4.4, .NET 10.0.14.
- Sonuçlar (hepsi `cicek-e2e` yığınına karşı, tek seferde bir koşu, `retries` CI dışında 0):
  | Koşu | Başlangıç koşulu | Sonuç | Süre |
  |---|---|---|---|
  | A | Yeni derlenmiş api+storefront (soğuk uygulama süreçleri) | **16/16** | 136 sn |
  | B | `api`+`storefront` yeniden başlatıldı (soğuk süreçler, sıcak Oracle) | **16/16** | 133 sn |
  | C | Oracle+api+storefront durdurulup `up --wait` ile yeniden başlatıldı (tam soğuk) | **16/16** | 136 sn |
  Her koşuda hazırlık kapısı: "Stack ready after 2.8s (0 failed probes)". Yeniden deneme, yeniden başlatma maskesi veya sabit uyku kullanılmadı.
- Kök neden: **KANITLANMADI.** Eski hatalar (`page.goto` 30 sn takılma, `ERR_CONNECTION_RESET`; bir POST'ta `ECONNRESET`) düzeltmesiz imajda ve aynı anda 3 Oracle + derleme + 5 Chromium yükü altında oluştu. Node keep-alive yarışı hipotezi, 4,5/5,05/5,2/7 sn boşta kalma sınamasında **tekrarlanamadı** (eski imajda da sıfır sıfırlama); `KEEP_ALIVE_TIMEOUT` yalnızca savunmacı sertleştirme olarak kaldı. En olası açıklama (hipotez): kaynak çekişmesi. Azaltıcı: küresel hazırlık kapısı (yayınlanan port üzerinden storefront → BFF → API → Oracle, 5 ardışık başarı).
- Açık risk: gerçek "ilk kez oluşturulan Oracle birimi" senaryosu bu görevde yeniden denenmedi (3. koşu mevcut birimle soğuk yeniden başlatma).

## Görev 4 — Oracle XE 21c yalıtılmış uyumluluk: **COMPLETED**
- Test hedefi: Oracle **Database 21c Express Edition 21.3.0.0.0** (`container-registry.oracle.com/database/express:21.3.0-xe`, 15,2 GB, yerelde mevcuttu), PDB `XEPDB1`. Yalıtılmış proje `cicek-xe` (ayrı konteyner/ağ/birim, portlar 1624/5182/3102, üretilmiş sırlar); geliştirme veritabanıyla ve diğer projelerle hiçbir bağlantısı yok. Bellek için önce yetkili `cicek-e2e` yığını durduruldu (birimler korundu); ana `cicek` yığınına dokunulmadı.
- Sonuçlar: DB ilk kez oluşturuldu, başlatma betiği (`scripts/startup`) kullanıcıları oluşturdu, Oracle sağlık denetimi (şema sahibi var) geçti; migrator **6/6 migrasyon** uyguladı; şema: 22 tablo, 57 indeks, 22 CHECK kısıtı, `CICEK_SCHEMA_ACCESS` paketi VALID, 10 kategori (Azerbaycanca adlar); API "6 migration(s) applied" ile bağlandı.
- Backend testleri **bu XE konteynerine karşı** (`CICEK_TEST_*` şeması, yalnızca konteynerde 22 tablo/6 migrasyon doğrulandı): **283/283**, 41 sn.
- Playwright (aynı XE yığını, taze veritabanı): **16/16**, 2,2 dk.
- Bulgu/düzeltme: Oracle konteynerleri `stop` sırasında 10 sn'de SIGKILL ile (çıkış 137) kapanıyordu → Compose'a `stop_grace_period: 2m` eklendi (çalışan konteynerlere ancak yeniden oluşturmada uygulanır). Yeni gizli bilgi/sürüm uyuşmazlığı yok; Free 26ai yerine XE kullanıldı, ikame yapılmadı.
- Not: Aynı yerel geliştirme veritabanı (host 1521) da XE 21c'dir: 6 uygulanmış, 0 bekleyen.

## Görev 5 — CI doğrulaması (yerel): **COMPLETED (yerel); uzak koşu NOT STARTED**
- `ci.yml` YAML geçerli; tetikleyiciler `push(main)`, `pull_request`, `workflow_dispatch`; `permissions: contents: read`; `concurrency` ile iptal; 4 iş (backend, frontend, docker-images, compose-e2e), hepsinde `timeout-minutes`. 23 `run` adımının tamamı `bash -n` ile sözdizimi geçti. Tüm `uses` satırları 40 karakterlik SHA'ya sabit ve uzak etiketlerle eşleşiyor (checkout v4.4.0, setup-dotnet v4.3.1, setup-node v4.4.0, upload-artifact v4.6.2); `dependabot.yml` haftalık güncelleme önerir.
- Başvurulan tüm dosyalar ve npm betikleri mevcut (`generate:api`, `test:e2e` dahil). Gizli bilgi: depo sırrı yok; parolalar çalıştırma başına üretilir ve maskelenir; `deploy/.env` gerekmez.
- Yerelde eşdeğeri çalıştırılan adımlar: compose geçerlilik denetimi (exit 0), NuGet açık denetimi (geçer), `npm audit --omit=dev --audit-level=high` (exit 0), format/lint/typecheck/test/build, imaj derlemeleri (önceki aşamada), bootstrap SQL gövdesi (önceki aşamada Oracle'da çalıştırıldı), `e2e-stack.sh` (üç kez Free, bir kez XE).
- **Yerelde doğrulanamayanlar:** `git diff --exit-code` tabanlı OpenAPI ve şema sapma denetimleri (commit gerekir), GitHub çalıştırıcılarında disk/süre, `23.26.3.0-lite` imajıyla sağlık denetimi, `actions/*` çalıştırmaları. Uzak koşu için gerekenler: GitHub'da depo oluşturmak, onaylı taban commit'i itmek, Actions'ı açmak.

## Görev 6 — Git taban commit hazırlığı: **COMPLETED (hazırlık); commit onay bekliyor**
- Commit yok, 370 izlenmeyen dosya, hiçbiri hazırlanmadı (staged değil). Gizli bilgi taraması: gerçek sır yok (yalnızca sahte test sabitleri ve kod içi `Password =` atamaları).
- Önerilen içerik: backend kaynak 146, backend test 34, backend yapılandırma 7 (`.editorconfig`, `Cicek.slnx`, `Directory.*.props`, `dotnet-tools.json`, `global.json`, `.dockerignore`), OpenAPI sözleşmesi 1 (`backend/openapi/cicek-api.json`; CI sapma denetimi için commit edilmeli), frontend kaynak 105, paketler 20, e2e testleri 5, frontend yapılandırma 15 (`package-lock.json` dahil), dokümantasyon 22, deploy 7, db bootstrap 2, CI 2, kök: `.gitattributes`, `.gitignore`, `README.md`.
- Asla commit edilmeyecekler (zaten `.gitignore` ile yok sayılıyor): `deploy/.env`, `frontend/apps/storefront/.env.local`, `backend/.local-media/`, `node_modules/`, `.next/`, `bin/`, `obj/`, `test-results/`, `playwright-report/`, `*.tsbuildinfo`, `next-env.d.ts`. Sınanan desenler (`deploy/.env.*`, `appsettings.*.local.json`, `.env.production`) yok sayılıyor.
- **Kararınız gereken:** `azeri çiçekçi promptları.docx` (27 KB, kök dizin, uygulama dosyası değil, AI istemleri) — öneri: taban commit'e **dahil etmeyin** (depo dışına taşıyın veya `.gitignore`'a ekleyin).
- `.gitattributes` (`* text=auto eol=lf`) CRLF/LF sapmasını önler; çalışma ağacında CRLF'li dosya bulunmadı.

## Görev 7 — Son doğrulama: **COMPLETED**
Kaynak/yapılandırma değişmediği sürece sonuçlar yeniden kullanıldı; bağımlılık güncellemesinden sonra yeniden koşulanlar işaretli.
| Kontrol | Sonuç | Zaman/not |
|---|---|---|
| `dotnet build` (uyarılar hata) | PASS, 0 uyarı | bağımlılık güncellemesinden sonra, 22 sn |
| `dotnet format --verify-no-changes` | PASS | aynı |
| `dotnet test` | **283/283** (host XE 21c `_TEST` şeması ve izole XE 21.3 konteyneri, iki ayrı koşu), 43 / 41 sn | |
| Frontend `format:check`, `lint`, `typecheck` | PASS (lint 212 sn) | vitest 4 / Next 16.4.4 sonrası |
| Frontend birim testleri | **91/91** (69+10+12) | aynı |
| `next build` | PASS | aynı |
| Playwright | **16/16 × 4** (Free üç koşu: A,B,C; XE bir koşu) | rebuild sonrası; yeniden deneme yok |
| Compose sağlık | `cicek` yığını healthy (api/web 200, anonim admin 401); `cicek-e2e` ve `cicek-xe` yığınları bellek için durdurulmuş (birimler duruyor) | |
| Migrasyon durumu | Geliştirme XE: 6 uygulanmış / 0 bekleyen; Free ve XE konteynerleri: 6/6 | |
| Bağımlılık taraması | .NET 0; npm üretim 0; geliştirme: 18 orta, 0 kritik/yüksek | |

## Açık konular / sonraki öneri
- Onay bekleyen: `cicek-e2e2` kalıntılarının silinmesi (~2,7 GB), `.docx` kararı, taban commit dosya listesi, uzak depo.
- E2E kök nedeni kanıtlanmadı (bkz. Görev 3); gerçek "ilk kez oluşturulan Oracle birimi" senaryosu yeniden denenmedi (XE koşusu taze birimdi ve geçti).
- `stop_grace_period` ana yığında ancak konteyner yeniden oluşturulunca etkinleşir (onay gerekir).
- Sonraki aşama önerisi: onaylı taban commit → uzak depo → CI'nin ilk gerçek koşusu.
