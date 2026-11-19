namespace SpeedReading.Domain.Sessions;

/// <summary>
/// Live test 19.11.2026: Kısa Gösterim level 5 showed random word pairs ("Futbol Top", "Yoklama Ay"). Two-word stimuli now come
/// from this list of meaningful, everyday Turkish phrases that suit every age. Every length of the level 5 ranges (6–16
/// characters, the space counted) has at least twelve phrases. The local preview in the client keeps an identical copy
/// (text-stream.engine.ts); a contract test compares them.
/// </summary>
public static class TachistoscopePhrases
{
    public const int MinimumLength = 6;
    public const int MaximumLength = 16;

    public static readonly IReadOnlyList<string> All =
    [
        // 6 karakter
        "ak süt", "bir ev", "bu kış", "bu yaz", "el işi", "ev içi",
        "ev işi", "iki el", "ilk ay", "son ay", "süt iç", "taş ev",
        "çok su",
        // 7 karakter
        "ana yol", "bir gün", "dağ evi", "el yıka", "iki göz", "ilk gün",
        "ilk kar", "iyi gün", "kapı aç", "kuş evi", "kış ayı", "son gün",
        "su yolu", "taş yol", "yaz ayı", "yol ver", "çok iyi",
        // 8 karakter
        "ay ışığı", "baba eli", "dağ başı", "ekmek al", "göz kırp", "ilk ders",
        "kar topu", "mavi gök", "sarı top", "soğuk su", "su içmek", "sıcak su",
        "tatlı su", "uzun yol", "yeni yıl",
        // 9 karakter
        "dar sokak", "ders zili", "güler yüz", "hızlı koş", "inek sütü", "kısa mola",
        "kış montu", "okul yolu", "son sayfa", "su şişesi", "sulu boya", "tren yolu",
        "yün kazak", "çam ağacı", "çarşı içi", "çay demle",
        // 10 karakter
        "arı kovanı", "at arabası", "atkı örmek", "buz pateni", "büyük ağaç", "dalga sesi",
        "deniz kumu", "doğum günü", "elma ağacı", "erken kalk", "eski şehir", "hafta sonu",
        "kar tanesi", "kitap rafı", "kum havuzu", "kum tepesi", "kuş yuvası", "küçük kedi",
        "kısa boylu", "kış gecesi", "kış tatili", "mavi deniz", "meyve suyu", "mutlu aile",
        "orman yolu", "oyun parkı", "park alanı", "pazar yeri", "pencere aç", "saç tarağı",
        "sessiz oku", "silgi tozu", "sıcak hava", "taze ekmek", "taze meyve", "temiz hava",
        "uzun tatil", "yavaş yürü", "yaz akşamı", "yaz tatili", "yeni hafta", "yeni kitap",
        // 11 karakter
        "anne kucağı", "basket topu", "beyaz bulut", "bilim fuarı", "boya kalemi", "büyük köprü",
        "defter kabı", "ders kitabı", "diş fırçası", "el sallamak", "futbol maçı", "gece yarısı",
        "gemi limanı", "geniş cadde", "gül bahçesi", "güneşli gün", "güzel bahçe", "iyi arkadaş",
        "kale kapısı", "kardan adam", "kedi maması", "kedi sevmek", "kitap fuarı", "koşu yarışı",
        "kuru yaprak", "meyve yemek", "müze gezisi", "nine masalı", "okul gezisi", "sabah sporu",
        "soru sormak", "spor salonu", "sıcak simit", "sıcak çorba", "top oynamak", "uçak bileti",
        "yatak odası", "yaya geçidi", "yağmur botu", "yeşil çimen", "yüksek bina", "çay bardağı",
        "ödev yapmak", "öğle yemeği",
        // 12 karakter
        "akşam yemeği", "bahar sabahı", "balık tutmak", "bulutlu hava", "cevap vermek", "dede bahçesi",
        "deniz feneri", "deniz kabuğu", "deniz kenarı", "dikkatli bak", "düzenli uyku", "erken uyumak",
        "fidan dikmek", "geri dönüşüm", "kalem kutusu", "kapıyı kapat", "kitap okumak", "koyun sürüsü",
        "kuş beslemek", "kırmızı elma", "kızak kaymak", "limon kabuğu", "nehir kıyısı", "okul bahçesi",
        "okul servisi", "okul çantası", "oturma odası", "renkli kalem", "resim yapmak", "serin rüzgâr",
        "tavuk kümesi", "trafik ışığı", "yeşil yaprak", "yüzme havuzu", "yüzme yarışı", "çöp toplamak",
        "ışığı söndür", "şapka takmak",
        // 13 karakter
        "banyo havlusu", "bayram sabahı", "göl manzarası", "güneş gözlüğü", "harita okumak", "hikâye yazmak",
        "konser salonu", "mektup yazmak", "mutfak masası", "otobüs durağı", "portakal suyu", "resim defteri",
        "sinema bileti", "spor ayakkabı", "tiyatro oyunu", "yıldızlı gece", "çiçek sulamak", "ödevini bitir",
        // 14 karakter
        "balık beslemek", "bilmece sormak", "bulmaca çözmek", "buğday tarlası", "derse hazırlan", "dişini fırçala",
        "dolunay gecesi", "dünya haritası", "eldiven giymek", "gökyüzü mavisi", "kardeş sevgisi", "komşu ziyareti",
        "köpek kulübesi", "masal anlatmak", "müzik dinlemek", "peynirli börek", "sağlıklı yaşam", "sınıf arkadaşı",
        "teneffüs arası", "yağmur damlası", "yemek pişirmek", "çalışma masası", "çikolatalı kek", "çiçek koklamak",
        "şarkı söylemek",
        // 15 karakter
        "akvaryum balığı", "bayram ziyareti", "bisiklet sürmek", "bisiklet yarışı", "domates salçası", "dondurma külahı",
        "ilkbahar çiçeği", "kahvaltı tabağı", "kütüphane kartı", "mahalle bakkalı", "matematik dersi", "matematik ödevi",
        "papatya tarlası", "piyano dersleri", "renkli balonlar", "resim öğretmeni", "saklambaç oyunu", "satranç tahtası",
        "sıcak gülümseme", "sınıf öğretmeni", "uçurtma uçurmak",
        // 16 karakter
        "basketbol potası", "beslenme çantası", "bilgisayar oyunu", "dengeli beslenme", "deniz yıldızları", "hayvanat bahçesi",
        "kahvaltı sofrası", "okul kütüphanesi", "sabah kahvaltısı", "sonbahar yaprağı", "yeşil kurbağalar", "çalışkan öğrenci",
        "çamaşır makinesi", "çilekli dondurma",
    ];

    /// <summary>The phrases of the length range in a session's own order, fixed by its stored seed.</summary>
    public static string[] Pool(int seed, int minLength, int maxLength)
    {
        var inRange = All.Where(phrase => phrase.Length >= minLength && phrase.Length <= maxLength).ToArray();
        if (inRange.Length == 0)
            inRange = [.. All];
        var random = new Random(seed);
        for (var index = inRange.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (inRange[index], inRange[swap]) = (inRange[swap], inRange[index]);
        }
        return inRange;
    }
}
