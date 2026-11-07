/**
 * UI strings. No user-facing text is hard-coded in components. Azerbaijani (Latin script) is the primary market language;
 * Turkish and English are maintained alongside it. A routing-aware i18n library can replace this module when locale URLs
 * are introduced (ADR-0011).
 */
import { adminEn, adminTr } from "./admin-messages";
import { azMessages } from "./az-messages";
import { marketEn, marketTr } from "./market-messages";

const tr = {
  brand: "Çiçek",
  market: marketTr,
  admin: adminTr,
  nav: {
    home: "Ana sayfa",
    signIn: "Giriş yap",
    register: "Üye ol",
    account: "Hesabım",
    mainLabel: "Ana menü",
    skipToContent: "İçeriğe geç",
  },
  home: {
    title: "Sevdiklerinize taze çiçekler",
    subtitle: "Yerel çiçekçilerden taze çiçekler ve hediyeler.",
    cta: "Ürünleri keşfedin",
    featuredShops: "Çiçekçiler",
    latestProducts: "Yeni ürünler",
    viewAll: "Tümünü gör",
    noShops: "Henüz yayında çiçekçi yok.",
    noProducts: "Henüz yayında ürün yok.",
    paymentNote: "Çevrimiçi ödeme yakında: şimdilik siparişlerin ödemesi yönetici tarafından elle onaylanır.",
    searchLabel: "Ne arıyorsunuz?",
    searchPlaceholder: "Örn. kırmızı gül, orkide, buket",
    searchButton: "Ara",
    categoriesTitle: "Kategoriler",
    howTitle: "Nasıl çalışır?",
    step1Title: "Seçin",
    step1Text: "Çiçekçilerin ürünlerine göz atın, beğendiğinizi sepete ekleyin.",
    step2Title: "Teslimat bilgisi girin",
    step2Text: "Alıcıyı, adresi ve teslimat tarihini yazın; isterseniz hediye notu ekleyin.",
    step3Title: "Çiçekçi hazırlasın",
    step3Text: "Ödeme onaylandıktan sonra çiçekçi siparişi hazırlar ve teslim eder.",
  },
  footer: {
    rights: "Tüm hakları saklıdır.",
    explore: "Keşfet",
    account: "Hesap",
    florists: "Çiçekçiler için",
    becomeFlorist: "Çiçekçi olarak başvurun",
  },
  auth: {
    loginTitle: "Giriş yap",
    registerTitle: "Üye ol",
    email: "E-posta",
    password: "Şifre",
    passwordHint: "En az 10 karakter. Kolay hatırlanan uzun bir cümle kullanabilirsiniz.",
    submitLogin: "Giriş yap",
    submitRegister: "Hesap oluştur",
    switchToRegister: "Hesabınız yok mu? Üye olun",
    switchToLogin: "Zaten üye misiniz? Giriş yapın",
    errors: {
      required: "Bu alan zorunludur.",
      invalid_email: "Geçerli bir e-posta adresi girin.",
      password_too_short: "Şifre en az 10 karakter olmalıdır.",
      too_long: "Bu alan çok uzun.",
      invalidCredentials: "E-posta veya şifre hatalı ya da hesap geçici olarak kilitlendi.",
      emailTaken: "Bu e-posta adresiyle kayıtlı bir hesap zaten var.",
      rateLimited: "Çok fazla deneme yaptınız. Lütfen biraz sonra tekrar deneyin.",
      unavailable: "Hizmete şu anda ulaşılamıyor. Lütfen daha sonra tekrar deneyin.",
      generic: "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.",
    },
  },
  account: {
    title: "Hesabım",
    loading: "Hesap bilgileri yükleniyor",
    email: "E-posta",
    roles: "Roller",
    permissions: "Ek yetkiler",
    noPermissionsTitle: "Ek yetki yok",
    noPermissionsDescription: "Müşteri hesapları için ek yönetim yetkisi gerekmez.",
    loadError: "Hesap bilgileri yüklenemedi.",
    retry: "Tekrar dene",
    reference: "Destek referansı",
    signOut: "Çıkış yap",
  },
  errors: {
    title: "Bir şeyler ters gitti",
    description: "Sayfa yüklenirken bir hata oluştu.",
    retry: "Tekrar dene",
    notFoundTitle: "Sayfa bulunamadı",
    notFoundDescription: "Aradığınız sayfa taşınmış veya kaldırılmış olabilir.",
    backHome: "Ana sayfaya dön",
  },
};

export type Messages = typeof tr;

const en: Messages = {
  brand: "Çiçek",
  market: marketEn,
  admin: adminEn,
  nav: {
    home: "Home",
    signIn: "Sign in",
    register: "Register",
    account: "My account",
    mainLabel: "Main menu",
    skipToContent: "Skip to content",
  },
  home: {
    title: "Fresh flowers for the people you love",
    subtitle: "Fresh flowers and gifts from local florists.",
    cta: "Browse products",
    featuredShops: "Florists",
    latestProducts: "New products",
    viewAll: "View all",
    noShops: "No florists are live yet.",
    noProducts: "No products are live yet.",
    paymentNote:
      "Online payment is coming soon: for now, order payments are confirmed manually by an administrator.",
    searchLabel: "What are you looking for?",
    searchPlaceholder: "E.g. red roses, orchid, bouquet",
    searchButton: "Search",
    categoriesTitle: "Categories",
    howTitle: "How it works",
    step1Title: "Choose",
    step1Text: "Browse the florists' products and add what you like to your cart.",
    step2Title: "Enter delivery details",
    step2Text: "Enter the recipient, address and delivery date; add a gift note if you like.",
    step3Title: "The florist prepares it",
    step3Text: "Once payment is confirmed, the florist prepares and delivers the order.",
  },
  footer: {
    rights: "All rights reserved.",
    explore: "Explore",
    account: "Account",
    florists: "For florists",
    becomeFlorist: "Apply to sell as a florist",
  },
  auth: {
    loginTitle: "Sign in",
    registerTitle: "Create an account",
    email: "Email",
    password: "Password",
    passwordHint: "At least 10 characters. A long, memorable sentence works well.",
    submitLogin: "Sign in",
    submitRegister: "Create account",
    switchToRegister: "No account yet? Register",
    switchToLogin: "Already registered? Sign in",
    errors: {
      required: "This field is required.",
      invalid_email: "Enter a valid email address.",
      password_too_short: "Password must be at least 10 characters.",
      too_long: "This value is too long.",
      invalidCredentials: "The email or password is incorrect, or the account is temporarily locked.",
      emailTaken: "An account with this email address already exists.",
      rateLimited: "Too many attempts. Please wait a moment and try again.",
      unavailable: "The service is unavailable right now. Please try again later.",
      generic: "Something went wrong. Please try again.",
    },
  },
  account: {
    title: "My account",
    loading: "Loading account details",
    email: "Email",
    roles: "Roles",
    permissions: "Additional permissions",
    noPermissionsTitle: "No additional permissions",
    noPermissionsDescription: "Customer accounts do not need administrative permissions.",
    loadError: "Could not load your account.",
    retry: "Try again",
    reference: "Support reference",
    signOut: "Sign out",
  },
  errors: {
    title: "Something went wrong",
    description: "An error occurred while loading this page.",
    retry: "Try again",
    notFoundTitle: "Page not found",
    notFoundDescription: "The page may have been moved or removed.",
    backHome: "Back to home",
  },
};

export const locales = ["az", "tr", "en"] as const;
export type Locale = (typeof locales)[number];

const catalog: Record<Locale, Messages> = { az: azMessages, tr, en };

/** Native names for the language switcher. */
export const localeLabels: Record<Locale, string> = { az: "Azərbaycanca", tr: "Türkçe", en: "English" };

export function isLocale(value: string | undefined | null): value is Locale {
  return value === "az" || value === "tr" || value === "en";
}

export function defaultLocale(): Locale {
  const configured = process.env.NEXT_PUBLIC_DEFAULT_LOCALE;
  return isLocale(configured) ? configured : "az";
}

export function getMessages(locale: Locale): Messages {
  return catalog[locale];
}
