import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'mk-button',
  standalone: true,
  template: `<button class="mk-button" [ngClass]="'mk-button--' + variant" [type]="type" [disabled]="disabled" [attr.aria-label]="ariaLabel || null"><ng-content /></button>`,
  imports: [CommonModule],
  styles: [':host{display:inline-block;max-width:100%}']
})
export class MkButtonComponent {
  @Input() variant: 'primary' | 'secondary' | 'ghost' | 'danger' = 'primary';
  @Input() type: 'button' | 'submit' | 'reset' = 'button';
  @Input() disabled = false;
  @Input() ariaLabel = '';
}

@Component({
  selector: 'mk-card',
  standalone: true,
  template: `<section class="mk-card"><ng-content /></section>`,
  styles: [':host{display:block;min-width:0}']
})
export class MkCardComponent {}

@Component({
  selector: 'mk-badge',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="mk-badge" [ngClass]="'mk-badge--' + variant" [attr.aria-label]="ariaLabel || null"><ng-content /></span>`,
  styles: [':host{display:inline-flex;max-width:100%;vertical-align:middle}']
})
export class MkBadgeComponent {
  @Input() variant: 'neutral' | 'gold' | 'success' | 'warning' | 'danger' = 'neutral';
  @Input() ariaLabel = '';
}

@Component({
  selector: 'mk-input',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="mk-field">
      <label *ngIf="label" [for]="id">{{ label }}<span *ngIf="required" aria-hidden="true"> *</span></label>
      <input [id]="id" [name]="name || null" [type]="type" [placeholder]="placeholder"
        [attr.autocomplete]="autocomplete" [required]="required" [disabled]="disabled"
        [value]="value" [attr.aria-invalid]="error ? 'true' : null"
        [attr.aria-describedby]="error ? id + '-error' : null" (input)="onInput($event)">
      <p *ngIf="error" class="mk-field__error" [id]="id + '-error'">{{ error }}</p>
    </div>`,
  styles: [':host{display:block;min-width:0}']
})
export class MkInputComponent {
  @Input() id = 'mk-input';
  @Input() name = '';
  @Input() label = '';
  @Input() type: 'text' | 'email' | 'tel' | 'password' | 'search' = 'text';
  @Input() placeholder = '';
  @Input() autocomplete = 'off';
  @Input() value = '';
  @Input() required = false;
  @Input() disabled = false;
  @Input() error = '';
  @Output() valueChange = new EventEmitter<string>();

  onInput(event: Event): void {
    this.valueChange.emit((event.target as HTMLInputElement).value);
  }
}

@Component({
  selector: 'mk-skeleton',
  standalone: true,
  template: `<span class="mk-skeleton" aria-hidden="true" [style.width]="width" [style.height]="height"></span>`,
  styles: [`
    :host{display:block;max-width:100%;min-width:0}
    .mk-skeleton{display:block;max-width:100%;border-radius:8px;background:linear-gradient(105deg,#edf1eb 20%,#faf8f1 38%,#e4eee5 56%);background-size:220% 100%;animation:mk-skeleton-shimmer 1.45s ease-in-out infinite}
    @keyframes mk-skeleton-shimmer{to{background-position-x:-220%}}
    @media(prefers-reduced-motion:reduce){.mk-skeleton{animation:none;background:#e9eee8}}
  `]
})
export class MkSkeletonComponent {
  @Input() width = '100%';
  @Input() height = '1rem';
  @Input() label = 'Loading';
}
