import { Component } from '@angular/core';

@Component({
  selector: 'course-student-root',
  standalone: true,
  template: `
    <header><strong>eduivme</strong><span>Kurs öğrenci alanı</span></header>
    <main>
      <p class="eyebrow">KURS YÖNETİMİ</p>
      <h1>Öğrenme yolculuğunuz için<br>yeni bir çalışma alanı.</h1>
      <p class="intro">Ders programınız, ödevleriniz ve kurs bilgileriniz bu alanda bir araya gelecek.</p>
      <section aria-labelledby="status"><h2 id="status">Öğrenci alanı hazırlanıyor</h2>
        <p>Kayıt ve giriş henüz açılmadı. Kurum ve öğretmen davetleri tamamlandığında hesabınızla katılabileceksiniz.</p>
      </section>
      <div class="features">
        <article><span>01</span><h2>Ders programı</h2><p>Yaklaşan dersler ve program değişiklikleri.</p></article>
        <article><span>02</span><h2>Ödevler</h2><p>Öğretmeninizin verdiği çalışmalar ve geri bildirimler.</p></article>
        <article><span>03</span><h2>Kurs bilgilerim</h2><p>Kayıtlarınız, devam durumunuz ve ödeme bilgileriniz.</p></article>
      </div>
    </main>
    <footer>Eduİvme · Kurs yönetimi</footer>
  `
})
export class AppComponent {}
