import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
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
        <p class="assistant-intro" *ngIf="!answer && !loading">Choose a suggestion or describe what you need.</p>
        <div class="assistant-suggestions" *ngIf="!answer && !loading" aria-label="Suggested questions">
          <button type="button" *ngFor="let suggestion of suggestions" (click)="useSuggestion(suggestion)">{{suggestion}}</button>
        </div>
        <p class="assistant-answer" *ngIf="answer">{{ answer }}</p>
        <p class="assistant-error" *ngIf="error" role="alert">{{ error }}</p>
        <div class="assistant-products" *ngIf="result?.suggestedProducts?.length">
          <article *ngFor="let product of result?.suggestedProducts">
            <span aria-hidden="true"></span>
            <div><strong>{{ product.name }}</strong><small>{{ product.category }} · {{ product.location }}</small></div>
            <b>{{ product.priceEtb | number:'1.2-2' }} <small>ETB</small></b>
          </article>
        </div>
      </div>

      <form class="assistant-form" (ngSubmit)="send()">
        <input
          name="question"
          [(ngModel)]="question"
          maxlength="1000"
          [placeholder]="questionPlaceholder"
          aria-label="Ask the MERKATO shopping assistant"
          aria-describedby="assistant-note assistant-disabled-reason"
          [disabled]="loading">
        <button type="submit" [disabled]="loading || !question.trim()" [attr.aria-label]="loading ? 'Assistant is responding' : 'Send question to shopping assistant'">
          {{ loading ? 'Thinking…' : 'Ask assistant ↗' }}
        </button>
      </form>
      <small id="assistant-disabled-reason" class="assistant-disabled-reason" *ngIf="!loading && !question.trim()">Enter a product, category or location to enable Ask.</small>
      <small id="assistant-note" class="assistant-note">AI suggestions can be imperfect. Confirm product details with the merchant before ordering.</small>
    </section>
  `,
  styles: [`
    :host{display:block;margin:0 auto 72px;max-width:1180px;padding:0 24px}
    .assistant-shell{padding:30px;border:1px solid #dfe8dc;border-radius:22px;background:#fff;box-shadow:0 18px 50px #173b2d0b}
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
    .assistant-products article{display:flex;align-items:center;gap:10px;padding:11px;border:1px solid #e5ebe3;border-radius:11px;background:#f8faf7}
    .assistant-products article>span{display:grid;place-items:center;flex:none;width:38px;height:38px;border-radius:10px;background:#eaf1e9}
    .assistant-products article>span:after{content:"";width:17px;height:17px;border:2px solid #174832;border-radius:4px}
    .assistant-products article div{display:grid;min-width:0;flex:1}
    .assistant-products strong{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;color:#284635;font-size:12px}
    .assistant-products small{color:#718078;font-size:11px}
    .assistant-products b{color:#1d5539;font-size:12px;white-space:nowrap}.assistant-products b small{font-size:10px}
    .assistant-form{display:flex;gap:10px}
    .assistant-form input{flex:1;min-width:0;min-height:48px;padding:14px 16px;border:1px solid #dfe7dc;border-radius:12px;outline:none;font:inherit;font-size:14px}
    .assistant-form input:focus{border-color:#397b57;box-shadow:0 0 0 3px #397b5718}
    .assistant-form button{min-width:130px;min-height:48px;padding:0 18px;border:0;border-radius:12px;background:#174832;color:#fff;font-size:13px;font-weight:800;cursor:pointer}
    .assistant-form button:disabled{background:#e8eee9;color:#59685f;opacity:1;cursor:not-allowed}
    .assistant-suggestions{display:flex;flex-wrap:wrap;gap:8px;margin:12px 0 0}
    .assistant-suggestions button{min-height:38px;padding:8px 12px;border:1px solid #dce8dd;border-radius:999px;background:#f6f9f5;color:#174832;font-size:12px;cursor:pointer}
    .assistant-suggestions button:hover{background:#eaf3ec;border-color:#a9cbb2}
    .assistant-disabled-reason,.assistant-note{display:block;margin-top:8px;color:#758279;font-size:12px;line-height:1.5}
    @media(max-width:600px){:host{padding:0 16px}.assistant-shell{padding:20px}.assistant-form{flex-direction:column}.assistant-form button{width:100%}}
    @media(prefers-reduced-motion:reduce){.assistant-suggestions button{transition:none}}
  `]
})
export class ShoppingAssistantComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  question = '';
  answer = '';
  error = '';
  loading = false;
  result: ShoppingAssistantResponse | null = null;
  language: 'en' | 'om' | 'am' = this.currentLanguage();
  private readonly languageListener = () => { this.language = this.currentLanguage(); };

  get suggestions(): string[] {
    if (this.language === 'om') return [
      'Bilbila gatii madaalawaa qabu barbaada',
      'Uffata dubartootaa Finfinnee keessatti barbaadi',
      'Meeshaalee manaaf barbaachisan naaf gorsi'
    ];
    if (this.language === 'am') return [
      'ተመጣጣኝ ዋጋ ያለው ስልክ እፈልጋለሁ',
      'በአዲስ አበባ የሴቶች ልብስ ፈልግ',
      'ለቤት የሚያስፈልጉ ዕቃዎችን ጠቁመኝ'
    ];
    return [
      'Find an affordable phone in Addis Ababa',
      'Show me women’s fashion',
      'Recommend useful home essentials'
    ];
  }

  get questionPlaceholder(): string {
    if (this.language === 'om') return 'Maal barbaadda? Afaan Oromoo, English ykn Amharic barreessi…';
    if (this.language === 'am') return 'ምን ይፈልጋሉ? በአማርኛ፣ በኦሮምኛ ወይም በእንግሊዝኛ ይጻፉ…';
    return 'What are you looking for?';
  }

  ngOnInit(): void { window.addEventListener('merkato-language-change', this.languageListener); }
  ngOnDestroy(): void { window.removeEventListener('merkato-language-change', this.languageListener); }

  private currentLanguage(): 'en' | 'om' | 'am' {
    try {
      const value = localStorage.getItem('merkato_language');
      return value === 'om' || value === 'am' ? value : 'en';
    } catch { return 'en'; }
  }

  useSuggestion(value: string): void {
    this.question = value;
    this.error = '';
  }

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
          : httpError?.error?.message || 'The shopping assistant is unavailable right now. Please try again.';
        this.loading = false;
      }
    });
  }
}
