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


---

# Aşama 7.1–7.9 – Git taban commit'i, GitHub CI ve Oracle-lite uyumluluğu (2026-11-07 → 2026-11-24)

Bu bölüm, yukarıdaki "Aşama 7 – Yürütme kaydı"nın devamıdır; oradaki durum etiketleri o günkü durumu gösterir ve değiştirilmemiştir. Burada yalnızca commit geçmişi ve GitHub Actions kayıtlarıyla doğrulanabilen bilgiler vardır; doğrulanamayanlar **UNVERIFIED** ile işaretlidir. CI koşu tarihleri UTC'dir; commit tarihleri Git'in yerel saatidir (bu yüzden birkaç commit bir gün sonra görünür).

Depo: `https://github.com/ada-ilhan/cicek-marketplace` (özel). Dal: `main`.

## 7.1 Git taban commit'i ve GitHub deposu — COMPLETED
- 369 dosyalık taban commit hazırlandı (Görev 6'daki 370 sayısı, `.docx` dosyasının hariç tutulmasıyla 369'a indi). Depo kimliği yalnızca bu depoya özgü: kullanıcı adı `İlhan`, e-posta GitHub noreply adresi; global ayar değişmedi.
- `README.md` düzeltmeleri: gözlenen çıkış kodu 137, yapılandırılan 2 dakikalık `stop_grace_period`, temiz çıkış (0) **doğrulanmadı** ifadesi; CI özeti dört işi içerir.
- Commit `28a8030` — *Initial baseline: Çiçek multi-vendor flower marketplace* (2026-11-07). `azeri çiçekçi promptları.docx` dahil edilmedi, taşınmadı, silinmedi.
- `.git/index.lock` (0 bayt, ~64 saat eski) Git süreci olmadığı doğrulanarak kullanıcı onayıyla silindi; yalnızca o dosya.
- Düzeltme: Görev 6'da "CRLF'li dosya yok" denmişti; sonradan 14 çalışma ağacı dosyasının CRLF olduğu görüldü. `.gitattributes` (`* text=auto eol=lf`) bunları dizinde LF'ye normalleştirir, sorun yoktur.

## 7.2–7.4 İlk push ve ilk CI koşusu — COMPLETED (CI: FAILED)
- `git push -u origin main` ile `28a8030` GitHub'a gönderildi.
- CI koşusu [`42384939400`](https://github.com/ada-ilhan/cicek-marketplace/actions/runs/42384939400) (2026-11-07): **docker-images başarılı; frontend, backend, compose-e2e başarısız.**

## 7.5–7.7 Tanılama ve başarısızlık incelemesi — COMPLETED (salt okunur)
`gh` ile (GitHub CLI, kullanıcı yetkili) koşu 42384939400 incelendi; kök nedenler:
| İş | Başarısız adım | Kök neden (doğrulandı) |
|---|---|---|
| frontend | `Generated API client is up to date` | `schema.d.ts` güncel değildi: backend sözleşmesindeki `outOfStock?: boolean` alanı üretilmiş istemcide eksikti |
| backend | `Bootstrap Oracle test schema` | `SP2-0310: unable to open file "/tmp/wrapper.sql"`: `docker cp` ile kopyalanan dosya root sahipli, mod 600 olduğu için `oracle` kullanıcısı okuyamıyordu |
| compose-e2e | `Start the stack and run the end-to-end suite` | Oracle-lite üzerinde uygulama tablespace'i yoktu (aşağıya bakın); bu işin ilk koşudaki ayrıntılı günlüğü bu kayıt için yeniden incelenmedi — **UNVERIFIED** |

Dependabot koşuları ayrı iş akışlarıdır (`dynamic` "Dependabot Updates") ve bu kayıtta yoktur.

## 7.8 İlk iki düzeltme — COMPLETED
- `npm run generate:api` ile `frontend/packages/api-client/src/schema.d.ts` yeniden üretildi (+1 satır: `outOfStock`).
- Bootstrap sarmalayıcı dosyası kaldırıldı; SQL*Plus betiği `docker exec -i … <<SQL` ile standart girdiden çalıştırılır (diske yazılan dosya yok).

## 7.9 Oracle-lite uyumluluğu — COMPLETED
Yerel kısıtlar: Windows ana makine ~16 GB RAM, sık sık 2 GB'ın altında boş bellek; Docker Desktop sanal makinesi. Bu yüzden `23.26.3.0-lite` imajı yerelde çalıştırılmadı (RAM yetersizdi) — tüm Oracle-lite doğrulaması GitHub çalıştırıcılarında yapıldı. Yerel RAM tanılaması (RAMMap, süreç grupları) yapıldı; hiçbir süreç sonlandırılmadı, bellek temizliği yapılmadı. Tarihleri — **UNVERIFIED**.

CI koşuları (push olayı) ve sonuçları:
| Commit | Koşu | Sonuç | Neden / değişiklik |
|---|---|---|---|
| `38651a5` fix(ci): repair bootstrap and Oracle tablespace compatibility | [`43610817971`](https://github.com/ada-ilhan/cicek-marketplace/actions/runs/43610817971) | FAILED | frontend `npm test`: varsayılan dil `tr` bulundu, `az` bekleniyordu (CI ortam değişkeni eksikti); backend: `ORA-20901 bootstrap.no_data_tablespace`: lite imajında `USERS` yok ve tek kalıcı tablespace `SYSTEM` (kullanıcılar için reddedildi). Bu commit: `schema.d.ts`, stdin tabanlı bootstrap ve `create_schema_users_impl.sql` içinde dinamik tablespace çözümü |
| `a0de574` ci: diagnose Oracle-lite and fix frontend locale | [`43613108415`](https://github.com/ada-ilhan/cicek-marketplace/actions/runs/43613108415) | FAILED | frontend düzeldi (`NEXT_PUBLIC_DEFAULT_LOCALE: az`); backend aynı ORA-20901; geçici salt-okunur Oracle tanılama adımı eklendi; `compose-e2e` bilerek atlandı (`if: ${{ false }}`) |
| `ce5e5ff` ci: provision application tablespace for Oracle-lite | [`43615718988`](https://github.com/ada-ilhan/cicek-marketplace/actions/runs/43615718988) | FAILED | tanılamanın gösterdiği: PDB `FREEPDB1`'de `USERS` yok, OMF yok; yeni `db/bootstrap/create_data_tablespace.sql` (seçmeli, tekrar çalıştırılabilir, `SYSAUX` dizininden dosya yolunu türetir, `DBMS_ASSERT` kullanır) ve `deploy/oracle/setup/01_create_cicek_users.sh` entegrasyonu. Bootstrap geçti; testler başarısız: dört güvenlik testi `ORA-01031` bekliyordu (Oracle 26ai `ORA-41900` döndürüyor) ve bir bağlantı testi (aşağıda) |
| `ccffb17` test: support Oracle 26ai privilege errors and diagnose connection failures | [`43618097168`](https://github.com/ada-ilhan/cicek-marketplace/actions/runs/43618097168) | FAILED (282/283) | dört append-only testi `ORA-01031` **veya** `ORA-41900` (yetki ve tablo adı mesajda) kabul eder; satırlar ve yetki politikası korundu. Kalan başarısız: `OracleDeploymentTests.Unreachable_database_is_classified_as_unavailable_without_details` |
| `28ea9f0` fix(db): classify ORA-50201 wrapping ORA-50232 as unavailable | [`43650136916`](https://github.com/ada-ilhan/cicek-marketplace/actions/runs/43650136916) | **SUCCESS** (compose-e2e atlandı) | Backend 299/299; OpenAPI adımı ilk kez çalıştı ve geçti |
| `364f5c5` ci: re-enable compose end-to-end verification | [`43650765165`](https://github.com/ada-ilhan/cicek-marketplace/actions/runs/43650765165) | **SUCCESS** (4/4 iş) | `compose-e2e` yeniden açıldı; Playwright 16/16 |
| `bfa5ff1` ci: remove temporary Oracle-lite tablespace diagnostic | [`45322310769`](https://github.com/ada-ilhan/cicek-marketplace/actions/runs/45322310769) | **SUCCESS** (4/4 iş) | Geçici tanılama adımı kaldırıldı (0 ekleme / 44 silme) |

### Bağlantı hatası sınıflandırması (kök neden — doğrulandı)
Yönetilen ODP.NET sürücüsü Linux'ta ulaşılamayan veritabanını `OracleException ORA-50201` ("Oracle Communication") olarak bildirir; iç zincirin en altı `ORA-50232` ("TCP transport address connect failure", 127.0.0.1:1599). `OracleDatabaseErrorClassifier` 50201'i bilmediği için sonuç `Unknown` (HTTP 500) oluyordu; beklenen `Unavailable` (503). Düzeltme: **dış kod 50201 ve iç zincirde mesajı `ORA-50232:` ile başlayan bir özel durum** varsa `Unavailable`; 50201 tek başına ya da başka iç nedenle `Unknown` kalır (parametre ayrıştırma hatası vb. kesin kesintiye çevrilmez). Yalnızca iç mesajın başındaki hata numarası okunur, metin değil. Eklenen: `ClassifyOracleError` (saf, test edilebilir uç), 16 yeni birim testi (`OracleErrorClassifierTests`: 7 → 23), `docs/oracle-database.md` içinde tablo satırı ve açıklama. Windows'ta aynı testin neden yerelde geçtiği kanıtlanmadı — **UNVERIFIED**.

### Kalıcı sonuçlar
- Yeni dosya: `db/bootstrap/create_data_tablespace.sql`; `create_schema_users_impl.sql` artık yalnızca gerektiğinde ve uygun bir kalıcı tablespace bulunursa kullanıcı oluşturur (yoksa `ORA-20901`); `SYSTEM`/`SYSAUX` kullanıcı verisi için reddedilir.
- `ci.yml`: `compose-e2e` etkin, geçici tanılama adımı yok, tüm eylemler SHA'ya sabit.
- `docs/oracle-database.md`: ORA-50201/ORA-50232 satırı.
- Güvenlik testi iddiası (`UPDATE`/`DELETE` yasak) zayıflatılmadı: yalnızca 26ai'nin farklı hata numarası ve ileti biçimi kabul edildi.

## Doğrulanmış taban çizgisi (2026-11-24)
- Dal `main`, commit `bfa5ff1af3e5f8979756cc965392cfde0f4972dc`, koşu [`45322310769`](https://github.com/ada-ilhan/cicek-marketplace/actions/runs/45322310769): `backend` 202 sn, `frontend` 99 sn, `docker-images` 92 sn, `compose-e2e` 506 sn — **hepsi başarılı, hiçbiri atlanmadı**.
- Backend: **299 geçti, 0 başarısız, 0 atlanan** (283 + 16 sınıflandırıcı testi); `Format` ve uyarılar hata olarak derleme geçti; savunmasız NuGet paketi yok.
- OpenAPI sapma denetimi geçti; frontend üretilmiş istemci denetimi geçti.
- Frontend: üç test paketinde **91 geçen test** gözlendi (69 + 10 + 12; başka paket olmadığı ayrıca doğrulanmadı); storefront duman testi **4 geçti**.
- Compose E2E: Oracle, API, storefront sağlıklı (`Stack ready after 2.9s (0 failed probes)`); Playwright **16 geçti**; günlükte yeniden deneme, kararsız (`flaky`) ya da atlanan test yok. Migrator ve yönetici hesabı adımları için ayrı satır kanıtı yok; adımın başarısı ve API sağlığı hata olmadığını gösterir.
- Oracle-lite uygulama tablespace'i (`USERS`) CI'da `create_data_tablespace.sql` ile oluşturulur.
- Doğrulanmayanlar: tam Oracle Free imajı (CI lite kullanır), üretim ortamı, yük/performans.

## Aşama 7.9 kapanışı
**Başarılanlar:** GitHub deposu ve taban commit'i; ilk CI'daki üç kök neden çözüldü (istemci sapması, bootstrap sarmalayıcı, Oracle-lite tablespace); Oracle 26ai yetki hatası uyumluluğu; bağlantı hatası sınıflandırması; CI'ın dört işi ilk kez birlikte yeşil.
**Doğrulanan:** yukarıdaki taban çizgisi — iki ardışık tam yeşil koşu (`43650765165`, `45322310769`).
**Doğrulanmayan:** üretim sertleştirmesi, tam Oracle imajı, uzun vadeli CI kararlılığı (yalnızca iki yeşil koşu), Playwright'ın CI'da `retries: 1` ile çalışması (bu koşularda yeniden deneme olmadı ama ayar duruyor), Azerbaycanca metinlerin anadil incelemesi.
**Neden devam edilebilir:** otomatik doğrulama zinciri (derleme, format, 299 backend testi, OpenAPI, istemci, imajlar, uçtan uca) artık güvenilir biçimde çalışıyor ve değişiklikleri yakalıyor; bilinen başarısızlık nedenleri kayıtlı.
**Neden CI yeşili üretime hazır demek değildir:** CI geçici sırlarla, tek Oracle-lite konteynerinde, tek makinede çalışır; barındırma, HTTPS/alan adı, sır yönetimi, yedekleme/geri yükleme, izleme, kalıcılık ve görsel depolama stratejisi hiç sınanmadı.

## Açık işler (hiçbiri yapılmadı)
1. **Dependabot:** şu an **18 açık PR** (önceki kayıtlardaki 14 güncelliğini yitirdi; GitHub'dan doğrulandı), çoğu majör sürüm (ör. .NET 11 imajları, Node 26, TypeScript 7, `vite` 8, Oracle sağlayıcıları, `actions/*`). Hiçbiri birleştirilmedi; her biri ayrı değerlendirilmeli (CI bunları ayrı koşturur).
2. **Playwright CI yeniden deneme politikası** (`retries: process.env.CI ? 1 : 0`) ayrıca değerlendirilecek.
3. **`.gitignore`:** `.docx` dışlama satırı hâlâ commit edilmedi (çalışma ağacında değişmiş duruyor).
4. Anadil Azerbaycanca dil incelemesi; lisans incelemesi.
5. Üretim barındırma ve dağıtım mimarisi; üretim sır yönetimi; HTTPS, alan adı, ters vekil; veritabanı ve medya kalıcılığı; yedek ve geri yükleme doğrulaması; izleme, günlükleme, operasyonel hazırlık; üretim görsel depolama stratejisi.
6. Gerekirse tam Oracle Free imajıyla uyumluluk doğrulaması.
7. Yerel Windows bellek kısıtları (Docker/Oracle yerelde ancak yeterli RAM varken).
8. Ödeme ve kurye entegrasyonu ertelenmiştir.

## Önerilen sonraki aşama: Aşama 8 – Üretim hazırlığı ve dağıtım hazırlığı
Ayrıntılı alt aşamalar ve mimari kararlar (barındırma, sır yönetimi, depolama vb.) ayrı bir değerlendirmede belirlenecek; bu kayıtta tasarlanmadı.



---

# Aşama 7.9 devamı – Bağımlılık modernizasyonu, güvenlik düzeltmeleri ve kararsız test (2026-11-24)

Bu bölüm yukarıdaki "Aşama 7.1–7.9" kaydının devamıdır; önceki içerik değiştirilmemiştir. Yalnızca GitHub kayıtlarıyla doğrulanan bilgiler vardır; doğrulanamayanlar **UNVERIFIED** ile işaretlidir. Koşu bağlantıları: `https://github.com/ada-ilhan/cicek-marketplace/actions/runs/<ID>`.

## R.2–R.3 Birleştirilen bağımlılık güncellemeleri
Her birleşme squash ile ve `--match-head-commit` korumasıyla yapıldı; her PR için CI'ın test ettiği birleşim (main + PR başı) doğrulandı; her birleşmeden sonra main CI'ı tam yeşil çıktı (backend 299/299, frontend 69+10+12, duman 4/4, Playwright 16/16).

| PR | Değişiklik | Main commit | PR koşusu | Main koşusu |
|---|---|---|---|---|
| #19 | JwtBearer 10.0.14 → 10.0.15 | `c99d3ef` | 45332792467 | 45334280146 |
| #21 | `next` 16.4.4 → 16.4.5 (güvenlik) | `81f068a` | 45334278825 | 45431985221 |
| #13 | `eslint-config-next` → 16.4.6 | `ca05219` | 45434172258 | 45435994420 |
| #22 | `typescript-eslint` 8.74.0 / `minimatch` 11.0.9 (güvenlik) | `7089543` | 45438062783 | 45439683764 |
| #5 | `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 | `1b66f57` | 45445974823 (bkz. kararsız test) | 45449041502 |
| #23 | Swashbuckle SwaggerUI 10.3.0 → 10.3.1 (elle, Dependabot #12 kapanmıştı) | `4285e1a` | 45450584794 | 45452068918 |
| #24 | 17 Microsoft paketi 10.0.14 → 10.0.15 | `f26056e` | 45453850875 | 45455410643 |
| #25 | `Oracle.EntityFrameworkCore` 10.23.26301 → 10.23.26400; `Oracle.ManagedDataAccess.Core` 23.26.301 → 23.26.401 | `33ee194` | 45472564067 | 45474045041 |
| #14 | `@playwright/test` 1.64.0 → 1.65.1 | `31eaa33` | 45475689990 | 45477143472 |
| #10 | `@axe-core/playwright` 4.13.0 → 4.14.0 | `8635737` | 45477591356 | 45482657582 |

- #20 (storefront `package.json`) #21 tarafından kapsandığı için Dependabot tarafından kendiliğinden kapandı; #12, #6, #9 Dependabot tarafından kapatıldı (#12 yerine #23, #6/#9 yerine #25).
- #24 kapsamı: EntityFrameworkCore (+Relational, +Design), System.Security.Cryptography.Pkcs, Extensions.Options (+ConfigurationExtensions, +DataAnnotations), Logging.Abstractions, Hosting (+Abstractions), Diagnostics.HealthChecks, Configuration.UserSecrets, Configuration.EnvironmentVariables, AspNetCore.Identity.EntityFrameworkCore, AspNetCore.OpenApi, Extensions.ApiDescription.Server, AspNetCore.Mvc.Testing. Hepsi resmî .NET 10.0.15 sürümünün (2026-11-10, güvenlik düzeltmeleri içerir) paket listesinde. `AspNetCore.OpenApi` 10.0.15, `Microsoft.OpenApi` alt sınırını 2.12.2'den 2.13.0'a çıkarır; OpenAPI sapma denetimi geçti.
- Dependabot'un rebase komutu birkaç kez yanıtsız kaldı (#5, #10). #5 için GitHub'ın zorlamasız "Update branch" işlemi kullanıldı.

## Güvenlik düzeltmesi
- Başlangıç: 15 açık Dependabot uyarısı, 8 farklı advisory (7 Next.js, 1 minimatch; hepsi orta). Sonuç: **15 fixed, 0 açık, 0 dismissed** (hiçbiri elle kapatılmadı). `npm audit` (üretim ve tümü) 0, NuGet savunmasız paket 0.
- Uygulama maruziyeti (kod incelemesi): Edge runtime yok → `GHSA-f3mh` uygulanmaz; `use cache`/Cache Components/`unstable_cache` kullanılmıyor → dört önbellek advisory'si uygulanmaz; `next/image` kullanılmıyor ama `/_next/image` uç noktası self-hosted varsayılanında açık → `GHSA-7qx7` **belirsiz**; BFF ve `media` route'ları yanıtı yeniden oluşturuyor → `GHSA-pc58` büyük olasılıkla uygulanmaz (kesin kanıt yok).
- Sıfır bilinen uyarı, uygulamanın güvenli olduğunu kanıtlamaz.

## Kararsız (flaky) testler ve CI politikası
1. **`stage5.spec.ts:296` (florist görsel yükleme)** — PR #5 koşusu 45445974823, 1. deneme: `getByText("Image 2")` `toHaveCount(0)` beklerken 5 sn boyunca 1 eleman buldu; retry geçti. Trace yüklenmemişti (iş başarılı olduğu için). **Kök neden (kod + hata imzasından, yüksek güven; eleman kimliği trace ile kanıtlanmadı):** `ImageManager` her görsel için ekran-okuyucu etiketi "Image description (optional) — Image N" üretir; yükleme bitince `getByText("Image 2")` (alt dize eşleşmesi) bu etiketi bulur. Eski olumsuz doğrulama yalnızca yükleme bitmeden önce değerlendirilirse geçebiliyordu (yarış). Düzeltme (PR #26, `226a18c`): önce bitmiş durum beklenir (`getByRole("img", {name: "Image 2"})`), sonra tam metin eşleşmesi (`exact: true`) ile "alt metin görünür metin değildir" doğrulanır; hiçbir doğrulama kaldırılmadı.
2. **CI kapısı:** `playwright.config.ts` içine `failOnFlakyTests: !!process.env.CI` eklendi (Playwright 1.64+ destekler). Retry ve hata izleri korunur, ancak yalnızca retry ile geçen test artık işi başarısız kılar.
3. **`marketplace.spec.ts:39` (uçtan uca yolculuk)** — #26 sonrası main koşusu 45464006518 yeni kapı nedeniyle **başarısız** oldu: 1. denemede 30 sn test zaman aşımı, retry geçti. İndirilen trace: 167 adım, hiçbiri takılmadı (en yavaş ~1,1 sn), test son adımlarda bütçeyi aştı; koşunun toplam E2E süresi 3,7 dk (diğer koşularda 2,6–3,3 dk) → yavaş çalıştırıcı **hipotezi** (adım başına ölçülmedi). Düzeltme (PR #27, `7edfd7f`): yalnızca bu teste `test.setTimeout(120_000)` (aynı dosyadaki diğer uzun yolculuklar zaten 120/240/300 sn kullanıyor); genel zaman aşımı, doğrulamalar ve kapsam değişmedi.
4. **Tekrarlı doğrulama (GitHub CI, birbirinden bağımsız tam koşular, retry yok):** #26 dalı: 45459260327 (PR), 45460875007, 45462377214 (workflow_dispatch) — hepsi temiz; #27 dalı: 45465922131 (PR), 45467369263, 45468698937 — hepsi temiz; ardından main 45470316011 ve sonraki tüm koşular temiz. Birinci hata (#26 sonrası main 45464006518) kayıtlıdır ve silinmemiştir. Üç yeşil koşu kalıcı kararlılığı kanıtlamaz.
5. Gözlem: stage5 satır 257/264/293 ve marketplace satır 186'daki `toHaveCount(0)` doğrulamaları da olumsuz doğrulamalardır; bunlar erken geçebilir (kararsızlık değil, zayıf doğrulama riski). Bu oturumda değiştirilmedi.

## Oracle sağlayıcı uyumluluğu (PR #25)
- Önce `OracleDatabaseErrorClassifier` kuralı korunur: yalnızca dış ORA-50201 + iç ORA-50232 → `Unavailable`; 50201 tek başına `Unknown`. Kural bu oturumda değişmedi.
- NuGet meta verisi: `Oracle.EntityFrameworkCore` 10.23.26400 → net10.0, `Microsoft.EntityFrameworkCore.Relational [10.0.0, 11.0.0)` (10.0.15 bu aralıkta), `Oracle.ManagedDataAccess.Core [23.26.400, 24.0.0)`. ODP.NET 23.26.401 → net6/net8/netstandard2.1; `System.Security.Cryptography.Pkcs` ve `System.Formats.Asn1` 10.0.13 (eski 6.x/8.x yerine), `System.Text.Json` 10.0.9. Depodaki `Pkcs` 10.0.15 sabitlemesi bununla uyumlu.
- Oracle'ın resmî notları sınırlı: EF Core 10 desteği 10.23.2600+'da başladı; 10.23.26400 notu "bağımlılık 23.26.400+ ve SaveChanges hızlandırması". 23.26.401 için ayrı resmî sürüm notu **bulunamadı (UNVERIFIED)**. Yerel kontrol olarak yalnızca NuGet'te var olduğu doğrulandı.
- **EF Core 11:** NuGet'te `Oracle.EntityFrameworkCore` 11.x sürümü **yoktur** (en yenisi 10.23.26400); .NET 11 / EF Core 11 geçişi için engel.
- CI kanıtı (Oracle Free **lite** 23.26.3.0): Oracle bootstrap, şema güvenliği (append-only; ORA-01031 veya ORA-41900), sınıflandırıcı ve `Unreachable_database_is_classified_as_unavailable_without_details` dâhil backend 299/299, atlanan 0. Gerçek özel durum zinciri bu koşuda ayrıca kaydedilmedi (test geçti → `Unavailable`; zincirin kendisi **UNVERIFIED**).

## Playwright ve Axe
- Playwright 1.65.1 (Chromium 159 paketli; CI `playwright install` ile sürüm eşleşir; Docker/CI'da Playwright imajı yok). Sürüm notu riskleri: `inert` bekleme, RFC 6265 çerez eşleşmesi, `test.slow()/skip()` imzası; suite etkilenmedi (16/16, retry yok).
- `@axe-core/playwright` 4.14.0 (axe-core 4.14, Node `>=22.12.0` ister); erişilebilirlik testi ve doğrulamaları değişmedi, geçti. Not: depo `engines` alanı hâlâ `>=22.11.0`; CI ve `node:22-alpine` bunu karşılar.
- Lockfile gözlemi: #14 sonrası storefront paket kaydında `^1.65.1` ile `package.json` `1.65.1` arasında önemsiz fark vardı; #10 bunu normalleştirdi.

## Kalan bağımlılık işleri
- Açık PR'lar: #1–#4 (GitHub Actions majörleri), #8 (TypeScript 7), #11 (vite 8), #15/#17/#18 (.NET 11 imajları), #16 (Node 26).
- Önkoşullar: TypeScript 7 için `typescript-eslint` 8.74.0 peer'ı `typescript <6.1.0` (uyumlu sürüm çıkana kadar bloklu); vite 8 için `@vitejs/plugin-react` 4.7.0 → 6.x (peer `vite ^8`) ve Node `^20.19 || >=22.12`; .NET 11 için Oracle EF Core 11 sağlayıcısı + Swashbuckle 11; Node 26 için `.nvmrc`, `engines` ve CI hizası.
- Yapılmadı: Aşama 8 (üretim hazırlığı), GitHub Actions modernizasyonu (R.6).

## Doğrulanmayanlar ve sınırlar
- Tüm Oracle doğrulaması Oracle **lite** konteynerinde, geçici sırlarla, tek GitHub çalıştırıcısında yapıldı; tam Oracle Free, üretim veritabanı, yük, yedekleme/geri yükleme, HTTPS/alan adı ve sır yönetimi sınanmadı. Yeşil CI üretime hazır olmak demek değildir.


---

# Aşama 7.9 devamı – R.6 Actions, Next 16.4.6, Node 26, Vite 8, SwaggerUI 11 (2026-11-25)

Önceki bölümler değiştirilmemiştir. Koşu bağlantıları: `https://github.com/ada-ilhan/cicek-marketplace/actions/runs/<ID>`. Doğrulanamayanlar **UNVERIFIED** ile işaretlidir.

## Birleştirilen değişiklikler

| PR | Değişiklik | Main commit | PR koşusu | Main koşusu |
|---|---|---|---|---|
| (doğrudan) | Docs R.2–R.5 + `.gitignore` | `f8c2c23`, `595bbdd` | n/a | 45502860889 |
| #28 | `next` 16.4.5 → 16.4.6 (yalnızca hata düzeltmesi) | `2500dc4` | 45612785591 | 45613959337 |
| #1 | `actions/checkout` v4.4.0 → v7.0.1 (`3d3c42e5…`) | `0102d78` | 45614202368 | 45615569341 |
| #2 | `actions/setup-dotnet` v4.3.1 → v6.1.0 (`0f4a354d…`) | `25ef7c5` | 45615746741 | 45617111510 |
| #3 | `actions/upload-artifact` v4.6.2 → v7.0.2 (`cf430e03…`) | `a8c6ce4` | 45617293260 | 45618594445 |
| #4 | `actions/setup-node` v4.4.0 → v7.1.0 (`949feb24…`) | `3bc615a` | 45620576332 | 45622023394 |
| #29 | Node.js 22 → **26 LTS** (Dockerfile, `.nvmrc`, `engines >=26.0.0 <27`, lockfile meta, README) | `bf88038` | 45624516950 | 45625833433 |
| #30 | `vite` 6.4.5 → 8.3.5 ve `@vitejs/plugin-react` 4.7.0 → 6.3.0 | `8bbbf08` | 45627385416 | 45629203784 |
| #31 | `Swashbuckle.AspNetCore.SwaggerUI` 10.3.1 → 11.0.1 | `06169b6` | 45631395355 | 45632874008 |

Her SHA, resmî `actions/*` deposundaki ilgili etiketin gösterdiği commit ile eşleştirildi; etiket yerine SHA sabitlemesi korundu. Her birleşme squash ve `--match-head-commit` korumalıdır. Her koşuda backend 299/299, frontend 69+10+12, duman 4/4, Playwright 16/16, flaky/retry 0, `npm audit` 0, NuGet savunmasız paket 0. Dependabot uyarıları: 0 açık.

## Notlar ve sınırlar
- Actions'ın kendi iç çalışma ortamı (hepsi `node24`) ile uygulamanın Node'u ayrıdır. Node 26 geçişinde CI `node v26.13.1` (setup-node, `.nvmrc` üzerinden) ve Docker `node:26-alpine` kullandı; Playwright E2E bu imaja karşı geçti. Node 26 için resmî bir "Next/Playwright/axe desteklenir" beyanı bulunmadı; uyumluluk yalnızca CI koşusuyla gösterildi (Next `engines >=20.9`, Playwright `>=20`, axe `>=22.12`). Node 26'da npm yeni bir "install-scripts" uyarısı veriyor (`esbuild`/`unrs-resolver` postinstall); bu bir hata değil ama ileride `allowScripts` düzenlemesi gerekebilir (UNVERIFIED).
- `engines` `>=26.0.0 <27` olarak daraltıldı (yalnızca Node 26 doğrulandı). Çalışan depolama (storefront) konteynerinin içinde `node --version` ayrıca gözlenmedi: CI'da yalnızca `node:26-alpine@sha256:6bd9ab43…` imajının çekildiği ve E2E'nin ona karşı geçtiği görüldü (iki aşama aynı etiketi kullanır). Bu oturumda yerel makine sahibi tarafından Node v26.13.1 / npm 11.20.0'a yükseltildi (önceden Node 22.11.0 / npm 10.9.0; bu ikisi artık geçerli değil); ben yerel yazılım kurmadım.
- Vite yalnızca Vitest motoru ve test JSX dönüşümü için kullanılır (Next üretim derleyicisi değildir). Vitest 4.1.12 değişmedi (peer `vite ^6 || ^7 || ^8`). Vite 8.3.8 yayında; PR #11 Dependabot tarafından 8.3.5 → 8.3.8 yamasına dönüştü, birleştirilmedi. Resmî Vite 6→7 ve 7→8 geçiş rehberleri #30 sırasında tam olarak gözden geçirilmedi; kanıt yapılandırma kapsamı (yalnızca varsayılan `react()` eklentisi, alias ve test seçenekleri) ve yeşil CI'dır.
- SwaggerUI yalnızca Development ortamında etkindir; OpenAPI belgesini Microsoft.AspNetCore.OpenApi üretir. Mevcut CI SwaggerUI'yi çalıştırmıyordu; bu yüzden geçici `diag/swaggerui-11.0.1` dalında (main'e birleştirilmedi) iki test koşturuldu: koşu 45631504164 **başarısız** (benim tanı varsayımım yanlıştı: uç nokta URL'i index.html içinde değil, yapılandırma betiğinde aranmalıydı), düzeltilmiş koşu 45631808105 başarılı (Development'ta UI, varlıklar ve `/openapi/v1.json`; Testing'de `/swagger` ve `/openapi` 404). Gerçek bir tarayıcıda UI etkileşimi denenmedi.
- upload-artifact v7.0.2: geçici `diag/upload-artifact-7.0.2` koşusu 45617244695 ilk denemede blob depolamadan "The server is busy" ile başarısız oldu (geçici altyapı sorunu olduğu **kanıtlanmadı**), ikinci denemede başarılı oldu; artifact indirilip içeriği doğrulandı. Gerçek Playwright rapor yükleme yolu bir test hatasıyla uçtan uca denenmedi.
- #1 Dependabot rebase ile, #2 ve #3 Dependabot rebase ile, #4 Dependabot yanıtlamadığı için GitHub'ın zorlamasız "Update branch" işlemiyle güncellendi.
- #16 (Node 26 imajı) #29 ile aynı Dockerfile değişikliğini içerdiği için Dependabot tarafından kendiliğinden kapandı; elle kapatılmadı.

## Kalan engeller
- TypeScript 7 (#8): `typescript-eslint` 8.74.0 (en yeni) `typescript <6.1.0`, `openapi-typescript` 7.13.0 (en yeni) `typescript ^5.x` ister. En yüksek karşılıklı uyumlu sürüm 5.9.3 (mevcut).
- .NET 11 (#15, #17, #18): Oracle EF Core 11 sağlayıcısı yok (en yeni 10.23.26400, net10.0); .NET 11 LTS değil. Ayrıntılar R.10 değerlendirmesinde.
- Tüm Oracle doğrulaması Oracle Free lite imajında ve tek GitHub çalıştırıcısında yapıldı; üretim hazırlığı veya tam Oracle doğrulaması iddia edilmez.
