import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, ShoppingAssistantResponse } from '../core/api.service';

@Component({
  selector: 'app-shopping-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="assistant-shell" aria-labelledby="assistant-title" [attr.aria-busy]="loading">
      <div class="assistant-heading">
        <div>
          <span class="assistant-kicker">MERKATO INTELLIGENCE</span>
          <h2 id="assistant-title">Find the right thing, faster.</h2>
          <p>Ask in English, Afaan Oromoo or Amharic. Recommendations use the live public catalogue.</p>
        </div>
        <span class="assistant-mark" aria-hidden="true">✦</span>
      </div>

      <div class="assistant-conversation" aria-live="polite" aria-relevant="additions text">
        <p class="assistant-loading" *ngIf="loading" role="status">Checking the live marketplace assistant…</p>
        <p class="assistant-intro" *ngIf="!answer && !loading">Try: “I need an affordable phone in Addis Ababa” or “Meeshaa manaaf gatii madaalawaa barbaada”.</p>
        <p class="assistant-answer" *ngIf="answer">{{ answer }}</p>
        <p class="assistant-error" *ngIf="error" role="alert">{{ error }}</p>
        <div class="assistant-products" *ngIf="result?.suggestedProducts?.length">
          <article *ngFor="let product of result?.suggestedProducts">
            <span>{{ product.emoji || '🛍️' }}</span>
            <div><strong>{{ product.name }}</strong><small>{{ product.category }} · {{ product.location }}</small></div>
            <b>{{ product.priceEtb | number:'1.2-2' }} ETB</b>
          </article>
        </div>
      </div>

      <form class="assistant-form" (ngSubmit)="send()">
        <input
          name="question"
          [(ngModel)]="question"
          maxlength="1000"
          placeholder="What are you looking for?"
          aria-label="Ask the MERKATO shopping assistant"
          aria-describedby="assistant-note"
          [disabled]="loading">
        <button type="submit" [disabled]="loading || !question.trim()" [attr.aria-label]="loading ? 'Assistant is responding' : 'Send question to shopping assistant'">
          {{ loading ? 'Thinking…' : 'Ask assistant ↗' }}
        </button>
      </form>
      <small id="assistant-note" class="assistant-note">AI suggestions can be imperfect. Confirm product details with the merchant before ordering.</small>
    </section>
  `,
  styles: [`
    :host{display:block;margin:0 auto 72px;max-width:1180px;padding:0 24px}
    .assistant-shell{padding:30px;border:1px solid #dfe8dc;border-radius:22px;background:linear-gradient(135deg,#f7faf5,#fff);box-shadow:0 18px 50px #173b2d0b}
    .assistant-heading{display:flex;justify-content:space-between;gap:20px;align-items:flex-start}
    .assistant-kicker{font-size:10px;font-weight:900;letter-spacing:1.8px;color:#a77c42}
    h2{margin:8px 0;color:#173b2d;font:800 clamp(22px,3vw,30px) Manrope,Arial,sans-serif;letter-spacing:-.8px}
    .assistant-heading p{margin:0;color:#718078;font-size:13px;line-height:1.6}
    .assistant-mark{display:grid;place-items:center;width:44px;height:44px;flex:none;border-radius:14px;background:#e7f0e5;color:#1e6848;font-size:24px}
    .assistant-conversation{min-height:74px;margin:22px 0 14px;padding:16px;border:1px solid #e4ebe1;border-radius:14px;background:#fff}
    .assistant-intro,.assistant-answer{margin:0;color:#526258;font-size:13px;line-height:1.7;white-space:pre-wrap}
    .assistant-error{margin:0;color:#a33434;font-size:13px;line-height:1.6}
    .assistant-loading{margin:0 0 10px;color:#42634d;font-size:12px;font-weight:750}
    .assistant-products{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:9px;margin-top:14px}
    .assistant-products article{display:flex;align-items:center;gap:10px;padding:11px;border-radius:11px;background:#f6f8f4}
    .assistant-products article>span{font-size:22px}
    .assistant-products article div{display:grid;min-width:0;flex:1}
    .assistant-products strong{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;color:#284635;font-size:11px}
    .assistant-products small{color:#87928a;font-size:10px}
    .assistant-products b{color:#1d5539;font-size:10px;white-space:nowrap}
    .assistant-form{display:flex;gap:10px}
    .assistant-form input{flex:1;min-width:0;padding:14px 16px;border:1px solid #dfe7dc;border-radius:12px;outline:none;font:inherit;font-size:13px}
    .assistant-form input:focus{border-color:#397b57;box-shadow:0 0 0 3px #397b5718}
    .assistant-form button{padding:0 18px;border:0;border-radius:12px;background:#174832;color:#fff;font-size:12px;font-weight:800;cursor:pointer}
    .assistant-form button:disabled{opacity:.55;cursor:not-allowed}
    .assistant-note{display:block;margin-top:11px;color:#929b94;font-size:10px}
    @media(max-width:600px){:host{padding:0 16px}.assistant-shell{padding:20px}.assistant-form{flex-direction:column}.assistant-form button{min-height:44px}}
  `]
})
export class ShoppingAssistantComponent {
  private readonly api = inject(ApiService);
  question = '';
  answer = '';
  error = '';
  loading = false;
  result: ShoppingAssistantResponse | null = null;

  send(): void {
    const message = this.question.trim();
    if (!message || this.loading) return;

    this.loading = true;
    this.answer = '';
    this.error = '';
    this.result = null;

    this.api.askShoppingAssistant(message).subscribe({
      next: response => {
        this.result = response;
        this.answer = response.message;
        this.loading = false;
      },
      error: httpError => {
        this.error = httpError?.status === 503
          ? 'The MERKATO AI assistant is temporarily unavailable (HTTP 503). You can still search the live marketplace catalogue or try again later.'
          : httpError?.error?.message
            || 'The shopping assistant is unavailable right now. Please try again.';
        this.loading = false;
      }
    });
  }
}
