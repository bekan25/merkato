import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'mk-admin-empty-state',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="admin-empty" role="status">
      <span class="admin-empty__icon" aria-hidden="true">{{icon}}</span>
      <strong>{{title}}</strong>
      <span class="admin-empty__message">{{message}}</span>
      <a *ngIf="actionLabel && actionHref" class="admin-empty__action" [href]="actionHref">{{actionLabel}}</a>
    </section>
  `,
  styles: [`
    :host{display:block;min-width:0}.admin-empty{display:grid;justify-items:center;gap:8px;padding:30px 16px;text-align:center;color:#b0bfd4;font-size:13px;line-height:1.5}
    .admin-empty__icon{display:grid;place-items:center;width:40px;height:40px;border-radius:12px;background:#1c2d49;color:#9ecfff;font-size:18px}
    strong{font-size:14px;color:#f1f5f9}.admin-empty__message{max-width:360px}.admin-empty__action{margin-top:4px;color:#9ed1ff;font-weight:700}
  `]
})
export class AdminEmptyStateComponent {
  @Input() icon = '•';
  @Input() title = 'Nothing here yet';
  @Input() message = '';
  @Input() actionLabel = '';
  @Input() actionHref = '';
}

@Component({
  selector: 'mk-admin-error-state',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="admin-error" role="alert">
      <span class="admin-error__icon" aria-hidden="true">⚠</span>
      <div class="admin-error__copy"><strong>{{title}}</strong><p *ngIf="message">{{message}}</p><small *ngIf="detail">{{detail}}</small></div>
      <button type="button" class="admin-error__retry" (click)="retry.emit()" [disabled]="busy">{{busy ? busyLabel : retryLabel}}</button>
    </section>
  `,
  styles: [`
    :host{display:block}.admin-error{display:flex;align-items:flex-start;gap:11px;padding:13px;margin-bottom:14px;border:1px solid #8a3b47;border-radius:11px;background:#351e2a;color:#ffd5d8}
    .admin-error__icon{flex:0 0 auto;font-size:20px}.admin-error__copy{min-width:0;flex:1}.admin-error strong{display:block;font-size:14px}.admin-error p{margin:4px 0 0;color:#f2bfc4;font-size:12px;line-height:1.5}.admin-error small{display:block;margin-top:5px;color:#f2bfc4;font-size:12px}
    .admin-error__retry{flex:0 0 auto;padding:8px 11px;border:1px solid #b85c67;border-radius:8px;background:#512832;color:#fff;font-weight:750;cursor:pointer}.admin-error__retry:focus-visible{outline:3px solid #72baff;outline-offset:3px}.admin-error__retry:disabled{opacity:.55;cursor:not-allowed}
    @media(max-width:420px){.admin-error{flex-wrap:wrap}.admin-error__retry{margin-left:30px}}
  `]
})
export class AdminErrorStateComponent {
  @Input() title = 'Something went wrong';
  @Input() message = '';
  @Input() detail = '';
  @Input() retryLabel = 'Retry';
  @Input() busyLabel = 'Retrying…';
  @Input() busy = false;
  @Output() retry = new EventEmitter<void>();
}

@Component({
  selector: 'mk-admin-skeleton',
  standalone: true,
  template: `<span class="admin-skeleton" role="status" aria-label="Loading"></span>`,
  styles: [`
    :host{display:block;min-width:0}.admin-skeleton{display:block;width:100%;height:12px;border-radius:7px;background:linear-gradient(90deg,#1a2b45 25%,#2a3e5e 38%,#1a2b45 62%);background-size:400% 100%;animation:admin-shimmer 1.35s ease infinite}
    @keyframes admin-shimmer{0%{background-position:100% 0}100%{background-position:0 0}}
    @media(prefers-reduced-motion:reduce){.admin-skeleton{animation:none}}
  `]
})
export class AdminSkeletonComponent {}

@Component({
  selector: 'mk-admin-stat-badge',
  standalone: true,
  template: `<span class="admin-stat-badge" [class.pending]="tone==='pending'" [class.open]="tone==='open'" [class.zero]="zero">{{label}}</span>`,
  styles: [`
    :host{display:inline-flex}.admin-stat-badge{padding:6px 9px;border-radius:8px;border:1px solid #3a4e6d;background:#1c2d49;color:#e0eaf8;font-size:12px;font-weight:800}
    .pending{background:#493719;border-color:#765a29;color:#ffe0a1}.open{background:#51252c;border-color:#873b46;color:#ffc1c6}.zero{background:#263247;border-color:#43516a;color:#cbd5e1}
  `]
})
export class AdminStatBadgeComponent {
  @Input() label = '';
  @Input() tone: 'neutral' | 'pending' | 'open' = 'neutral';
  @Input() zero = false;
}

@Component({
  selector: 'mk-admin-quick-action',
  standalone: true,
  template: `<a class="admin-quick-action" [href]="href" [attr.aria-label]="label"><span aria-hidden="true">{{icon}}</span><strong>{{label}}</strong></a>`,
  styles: [`
    :host{display:block;min-width:0}.admin-quick-action{display:flex;align-items:center;gap:10px;min-height:76px;padding:14px;border:1px solid #405574;border-radius:12px;background:#14213d;color:#f1f5f9;text-decoration:none;font-size:14px}
    .admin-quick-action span{display:grid;place-items:center;width:34px;height:34px;border-radius:10px;background:#1b4269;color:#9ed1ff;font-size:18px}.admin-quick-action:hover{background:#1b3456;border-color:#69aaff}.admin-quick-action:focus-visible{outline:3px solid #72baff;outline-offset:3px}
  `]
})
export class AdminQuickActionComponent {
  @Input() href = '#';
  @Input() icon = '•';
  @Input() label = '';
}
