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
  template: `<span class="mk-skeleton" role="status" aria-live="polite" [attr.aria-label]="label" [style.width]="width" [style.height]="height"></span>`,
  styles: [':host{display:block;max-width:100%}']
})
export class MkSkeletonComponent {
  @Input() width = '100%';
  @Input() height = '1rem';
  @Input() label = 'Loading';
}
