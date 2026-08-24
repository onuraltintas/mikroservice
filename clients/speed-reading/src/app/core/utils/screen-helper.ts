/**
 * UI ve fiziksel ekran arasındaki ilişkiyi hesaplayan yardımcı sınıf.
 * Görsel genişleme egzersizlerinde derece -> piksel dönüşümü için kullanılır.
 */
export class ScreenHelper {
    /**
     * Cihaz tipine göre tahmini PPI (Pixels Per Inch) döndürür.
     * Tarayıcı fiziksel boyut vermediği için en yakın tahmini yapar.
     */
    static getEstimatedPPI(): number {
        const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
        const dpr = window.devicePixelRatio || 1;

        if (isMobile) {
            // Modern telefonlar genelde yüksek yoğunlukludur (300-500 PPI)
            // DPR 3+ ise muhtemelen 400+ PPI, 2 ise 300 PPI civarı
            return dpr >= 3 ? 450 : 326;
        }

        // Masaüstünde standart 96 PPI kabul edilir (DPR 1 için)
        // 4K monitörlerde Windows ölçeklendirmesi ile DPR artar.
        return 96 * dpr;
    }

    /**
     * Cihaz tipine göre tahmini göz-ekran mesafesini (cm) döndürür.
     */
    static getEstimatedViewingDistanceCm(): number {
        const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
        const isTablet = /(ipad|tablet|(android(?!.*mobile))|(windows(?!.*phone)(.*touch))|kindle|playbook|silk|(puffin(?!.*(IP|AP|WP))))/i.test(navigator.userAgent);

        if (isMobile) return 30; // Telefon 30cm
        if (isTablet) return 40; // Tablet 40cm
        return 60; // Masaüstü 60cm
    }

    /**
     * Verilen görsel açıyı (derece) piksel değerine dönüştürür.
     * Formül: s = 2 * d * tan(theta/2)
     * s: nesneler arası mesafe (cm)
     * d: göz mesafesi (cm)
     * theta: açı (radyan)
     */
    static degreesToPixels(degrees: number): number {
        const distanceCm = this.getEstimatedViewingDistanceCm();
        const ppi = this.getEstimatedPPI();
        const ppcm = ppi / 2.54; // Pixels per CM

        const angleRad = (degrees * Math.PI) / 180;
        const distanceBetweenItemsCm = 2 * distanceCm * Math.tan(angleRad / 2);

        return distanceBetweenItemsCm * ppcm;
    }
}
