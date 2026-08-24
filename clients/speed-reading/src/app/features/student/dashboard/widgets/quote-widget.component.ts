import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
    selector: 'app-quote-widget',
    standalone: true,
    imports: [CommonModule],
    templateUrl: './quote-widget.component.html',
    styleUrls: ['./quote-widget.component.scss']
})
export class QuoteWidgetComponent {
    currentQuote = {
        text: "Başarı, her gün tekrarlanan küçük çabaların toplamıdır.",
        author: "Robert Collier"
    };
}
