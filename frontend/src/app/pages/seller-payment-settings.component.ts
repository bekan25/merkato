import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, SellerPaymentAccount, SellerChapaOnboarding } from '../core/api.service';

@Component({
  standalone: true,
  selector: 'app-seller-payment-settings',
  imports: [CommonModule, FormsModule],
  template: `
    <main class="page">
      <header class="hero">
        <span class="eyebrow">MERCHANT SETTINGS</span>
        <h1>Payment accounts</h1>
        <p>Manage the bank or mobile-money accounts where buyers transfer payments for your orders.</p>
      </header>

      <section class="notice">
        <strong>Important</strong>
        <p>Enter an account that belongs to your business or you. Buyers will see these details for the matching payment method. MERKATO does not receive or hold seller-direct transfers. Never enter your PIN, password, or one-time code.</p>
      </section>

      <section class="panel chapa-panel">
        <div class="chapa-heading">
          <img src="/assets/payment-methods/chapa.svg" alt="Chapa" width="112" height="45">
          <div><h2>Chapa seller settlement</h2><p class="muted">Connect an approved Chapa subaccount to receive seller proceeds through hosted checkout.</p></div>
        </div>
        <div class="chapa-status"><span class="status-dot" [class.approved]="chapa?.hasApprovedSubaccount"></span><strong>{{chapaStatusLabel()}}</strong></div>
        <p class="muted" *ngIf="chapa?.hasApprovedSubaccount">Your Chapa subaccount is approved. Checkout activation is still controlled by MERKATO after provider verification and sandbox testing.</p>
        <p class="muted" *ngIf="!chapa?.hasApprovedSubaccount">Chapa checkout is not available yet. MERKATO is waiting for marketplace split-payment approval and seller subaccount onboarding. Your existing direct-to-seller accounts remain active.</p>
        <small class="chapa-meta" *ngIf="chapa?.accountNumberLast4">Verified payout account ending •••• {{chapa?.accountNumberLast4}}</small>
      </section>
      <section class="panel">
        <h2>{{editingId ? 'Update payment account' : 'Add payment account'}}</h2>
        <form (ngSubmit)="save()">
          <label>Payment method
            <select name="rail" [(ngModel)]="paymentRail" required>
              <option [ngValue]="0">Telebirr</option>
              <option [ngValue]="1">CBE Birr</option>
              <option [ngValue]="2">Awash Birr</option>
              <option [ngValue]="3">Bank of Abyssinia</option>
            </select>
          </label>
          <label>Account holder name
            <input name="holder" [(ngModel)]="accountHolderName" maxlength="120" required placeholder="Name registered on the account">
          </label>
          <label>Account number or mobile-money phone
            <input name="identifier" [(ngModel)]="accountIdentifier" maxlength="120" required placeholder="Enter your account number or phone">
          </label>
          <label>Bank name <span>(required for bank accounts)</span>
            <input name="bank" [(ngModel)]="bankName" maxlength="120" placeholder="Bank name">
          </label>
          <label>Buyer instructions <span>(optional)</span>
            <textarea name="instructions" [(ngModel)]="instructions" maxlength="500" rows="3" placeholder="Optional transfer instructions. Do not include secrets."></textarea>
          </label>
          <label class="toggle"><input type="checkbox" name="active" [(ngModel)]="isActive"> Available to buyers</label>
          <button type="submit" [disabled]="saving">{{saving ? 'Saving…' : 'Save payment account'}}</button>
        </form>
        <p class="error" *ngIf="error">{{error}}</p>
        <p class="success" *ngIf="success">{{success}}</p>
      </section>

      <section class="panel">
        <div class="list-head"><h2>Your saved accounts</h2><button class="secondary" type="button" (click)="load()">Refresh</button></div>
        <p class="muted" *ngIf="loading">Loading accounts…</p>
        <p class="muted" *ngIf="!loading && accounts.length === 0">No payment accounts configured yet.</p>
        <article class="account" *ngFor="let account of accounts">
          <div>
            <strong>{{railName(account.paymentRail)}}</strong>
            <p>{{account.accountHolderName}} · {{account.accountIdentifier}}</p>
            <small>{{account.bankName || 'Mobile money'}} · {{account.isActive ? 'Visible to buyers' : 'Inactive'}}</small>
          </div>
          <div class="actions">
            <button class="secondary" type="button" (click)="edit(account)">Edit</button>
            <button class="danger" type="button" (click)="deactivate(account)" [disabled]="!account.isActive">Deactivate</button>
          </div>
        </article>
      </section>
    </main>
  `,
  styles: [`
    .chapa-heading{display:flex;align-items:center;gap:16px}.chapa-heading h2{margin:0 0 5px}.chapa-heading p{margin:0}.chapa-status{display:flex;align-items:center;gap:9px;margin:16px 0 8px;color:#72551d;font-size:12px}.status-dot{width:9px;height:9px;border-radius:50%;background:#c49a36}.status-dot.approved{background:#25834c}.chapa-meta{display:block;color:#63786a;font-size:11px;margin-top:8px}.chapa-panel{border-color:#e7def4;background:linear-gradient(135deg,#fff 0%,#fbf8ff 100%)}
    :host{display:block;min-height:80vh;background:#f4f7f3;color:#20382a;font-family:Inter,Arial,sans-serif}
    .page{max-width:900px;margin:auto;padding:30px 16px 70px}.hero{padding:12px 2px 20px}
    .eyebrow{color:#a27e36;font-size:10px;font-weight:900;letter-spacing:1.5px}
    h1{margin:9px 0;color:#173d2d;font-size:clamp(28px,5vw,40px);letter-spacing:-1px}
    h2{margin:0 0 17px;color:#214832;font-size:17px}.hero p,.muted{color:#748278;font-size:13px;line-height:1.7}
    .panel,.notice{margin:16px 0;padding:clamp(18px,3vw,26px);border:1px solid #e1e9df;border-radius:17px;background:#fff;box-shadow:0 12px 36px #173d2d08}
    .notice{border-color:#eee3c9;background:#fffaf0;color:#78653b;font-size:12px;line-height:1.7}.notice p{margin:6px 0 0}
    form{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}
    label{display:grid;gap:7px;color:#405b47;font-size:12px;font-weight:800}
    label span{display:inline;color:#8b968d;font-weight:500;font-size:10px}
    input,select,textarea{width:100%;min-height:43px;padding:10px 11px;border:1px solid #dce6db;border-radius:10px;background:#fbfcfa;color:#263e2e;font:13px Arial}
    textarea{resize:vertical}.toggle{display:flex;align-items:center;gap:9px}.toggle input{width:17px;min-height:17px}
    button{min-height:43px;padding:0 16px;border:0;border-radius:10px;background:#1e563a;color:#fff;font-weight:800;cursor:pointer}
    button:disabled{opacity:.5;cursor:not-allowed}.secondary{background:#edf3ec;color:#31533b}.danger{background:#fff0ed;color:#a64d42}
    .error,.success{margin:12px 0 0;font-size:12px}.error{color:#ad4d42}.success{color:#267442}
    .list-head,.account,.actions{display:flex;align-items:center;justify-content:space-between;gap:12px}
    .list-head{margin-bottom:10px}.list-head h2{margin:0}.account{padding:14px 0;border-top:1px solid #edf1eb}
    .account strong{color:#2b4e35;font-size:13px}.account p{margin:5px 0;color:#607264;font-size:12px}.account small{color:#8a968c;font-size:10px}
    .actions button{min-height:35px;padding:0 11px;font-size:11px}
    @media(max-width:620px){form{grid-template-columns:1fr}.account{align-items:flex-start;flex-direction:column}.actions{width:100%}.actions button{flex:1}}
  `]
})
export class SellerPaymentSettingsComponent implements OnInit {
  private api = inject(ApiService);
  accounts: SellerPaymentAccount[] = [];
  chapa: SellerChapaOnboarding | null = null;
  loading = true;
  saving = false;
  error = '';
  success = '';
  editingId: number | null = null;
  paymentRail = 0;
  accountHolderName = '';
  accountIdentifier = '';
  bankName = '';
  instructions = '';
  isActive = true;

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.api.getMyChapaOnboarding().subscribe({ next: row => this.chapa = row, error: () => this.chapa = null });
    this.api.getSellerPaymentAccounts().subscribe({
      next: rows => { this.accounts = rows ?? []; this.loading = false; },
      error: e => { this.error = e?.error?.error || 'Could not load seller payment accounts.'; this.loading = false; }
    });
  }

  chapaStatusLabel(): string {
    if (this.chapa?.hasApprovedSubaccount) return 'Seller subaccount approved';
    const status = this.chapa?.onboardingStatus || 'NOT_STARTED';
    return ({ NOT_STARTED: 'Not connected', PENDING: 'Approval pending', REJECTED: 'Needs review' } as Record<string, string>)[status] || status;
  }

  railName(rail: string): string {
    return ({ TELEBIRR: 'Telebirr', CBE_BIRR: 'CBE Birr', AWASH_BIRR: 'Awash Birr', BANK_OF_ABYSSINIA: 'Bank of Abyssinia' } as Record<string,string>)[rail] || rail;
  }

  edit(account: SellerPaymentAccount): void {
    this.editingId = account.id;
    this.paymentRail = ({ TELEBIRR: 0, CBE_BIRR: 1, AWASH_BIRR: 2, BANK_OF_ABYSSINIA: 3 } as Record<string,number>)[account.paymentRail] ?? 0;
    this.accountHolderName = account.accountHolderName;
    this.accountIdentifier = account.accountIdentifier;
    this.bankName = account.bankName || '';
    this.instructions = account.instructions || '';
    this.isActive = account.isActive;
    this.error = '';
    this.success = '';
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  save(): void {
    this.saving = true; this.error = ''; this.success = '';
    this.api.saveSellerPaymentAccount({
      paymentRail: this.paymentRail,
      accountHolderName: this.accountHolderName.trim(),
      accountIdentifier: this.accountIdentifier.trim(),
      bankName: this.bankName.trim(),
      instructions: this.instructions.trim(),
      isActive: this.isActive
    }).subscribe({
      next: () => { this.success = 'Payment account saved.'; this.reset(); this.load(); this.saving = false; },
      error: e => { this.error = e?.error?.error || 'Could not save payment account.'; this.saving = false; }
    });
  }

  deactivate(account: SellerPaymentAccount): void {
    if (!confirm('Deactivate this payment account? Buyers will no longer see it.')) return;
    this.api.deactivateSellerPaymentAccount(account.id).subscribe({
      next: () => { this.success = 'Payment account deactivated.'; this.load(); },
      error: e => { this.error = e?.error?.error || 'Could not deactivate payment account.'; }
    });
  }

  private reset(): void {
    this.editingId = null; this.paymentRail = 0; this.accountHolderName = '';
    this.accountIdentifier = ''; this.bankName = ''; this.instructions = ''; this.isActive = true;
  }
}
