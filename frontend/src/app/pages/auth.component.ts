import { Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  standalone: true,
  selector: 'app-auth',
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="auth-page">
      <div class="auth-aside"><a routerLink="/" class="brand auth-brand"><span class="brand-mark">M</span><span>merkato<small>ETHIOPIAN MARKETPLACE</small></span></a><div class="auth-message"><span class="eyebrow">YOUR MARKET. YOUR PEOPLE.</span><h1>Local commerce,<br><em>without limits.</em></h1><p>One account connects you to a growing community of buyers and merchants across Ethiopia.</p><div class="auth-benefit"><span>✓</span> Discover local products</div><div class="auth-benefit"><span>✓</span> Manage your marketplace activity</div><div class="auth-benefit"><span>✓</span> Built around secure transactions</div></div><span class="auth-footnote">MERKATO · MADE FOR ETHIOPIA</span></div>
      <div class="auth-form-wrap"><div class="auth-form"><a routerLink="/" class="back-link">← Back to marketplace</a><span class="eyebrow">{{isRegister ? 'JOIN THE MARKETPLACE' : 'WELCOME BACK'}}</span><h2>{{isRegister ? 'Create your account' : 'Sign in to MERKATO'}}</h2><p class="muted">{{isRegister ? 'Choose how you want to participate in MERKATO.' : 'Use your registered phone number and PIN to continue.'}}</p>
        <form (ngSubmit)="submit()">
          <label *ngIf="isRegister">Full name<input name="fullName" [(ngModel)]="fullName" required autocomplete="name" placeholder="Your full name"></label>
          <label>Phone number<input name="phone" [(ngModel)]="phoneNumber" required autocomplete="tel" placeholder="09XXXXXXXX" inputmode="tel"></label>
          <label>PIN<input name="pin" [(ngModel)]="pin" required type="password" [minlength]="isRegister ? 6 : 4" maxlength="8" autocomplete="current-password" inputmode="numeric" [placeholder]="isRegister ? '6–8 digit PIN' : 'Your PIN'"></label>
          <label *ngIf="isRegister">I want to join as<select name="role" [(ngModel)]="role"><option value="BUYER">Buyer — discover and shop</option><option value="MERCHANT">Merchant — list my products</option></select></label>
          <p class="form-error" *ngIf="error">{{error}}</p><button class="btn btn-primary auth-submit" [disabled]="loading">{{loading ? 'Please wait...' : (isRegister ? 'Create account' : 'Sign in securely')}} <span>↗</span></button>
        </form>
        <div class="auth-note"><span>⌑</span><p>Your PIN is protected with server-side password hashing. Never share it with anyone.</p></div><p class="auth-register-note">{{isRegister ? 'Already have an account?' : 'New to MERKATO?'}} <button class="link-button" type="button" (click)="toggleMode()">{{isRegister ? 'Sign in' : 'Create a buyer or merchant account'}}</button></p>
      </div></div>
    </section>
  `,
  styles: [`
    .auth-page{min-height:calc(100vh - 110px);background:radial-gradient(circle at 12% 12%,rgba(215,231,211,.72),transparent 34%),linear-gradient(135deg,#f5f8f2,#edf3ed);padding:clamp(22px,5vw,64px);gap:clamp(24px,5vw,72px)}
    .auth-aside{position:relative;overflow:hidden;border:1px solid rgba(255,255,255,.35);border-radius:26px;padding:clamp(28px,4vw,54px);background:linear-gradient(145deg,rgba(13,61,45,.97),rgba(31,91,65,.94));box-shadow:0 26px 75px rgba(14,54,37,.2)}
    .auth-aside:after{content:"";position:absolute;width:360px;height:360px;border:1px solid rgba(255,255,255,.13);border-radius:50%;right:-130px;top:-120px;box-shadow:0 0 0 34px rgba(255,255,255,.035),0 0 0 72px rgba(255,255,255,.025)}
    .auth-brand,.auth-message,.auth-footnote{position:relative;z-index:1}
    .auth-brand{color:#fff}.auth-brand small{color:#c3d6c8}.auth-brand .brand-mark{background:linear-gradient(145deg,#e2bd7c,#c18e49);color:#173b2c}
    .auth-message{max-width:540px;margin:clamp(54px,10vh,110px) 0 55px}
    .auth-message .eyebrow{color:#c8d8ca}.auth-message h1{color:#fff;font-size:clamp(42px,5vw,68px);letter-spacing:-3px}.auth-message h1 em{color:#e3bd7c}
    .auth-message>p{color:#d4e1d6;line-height:1.8;max-width:430px}
    .auth-benefit{color:#edf5ee;margin-top:17px;font-size:13px}.auth-benefit span{display:inline-grid;place-items:center;width:23px;height:23px;margin-right:10px;border-radius:50%;background:rgba(255,255,255,.13);color:#e4c184}
    .auth-footnote{color:#c4d7c8;font-size:9px;letter-spacing:1.8px}
    .auth-form-wrap{display:grid;place-items:center}
    .auth-form{width:min(100%,490px);padding:clamp(24px,4vw,42px);border:1px solid rgba(255,255,255,.9);border-radius:24px;background:rgba(255,255,255,.78);box-shadow:0 24px 75px rgba(24,57,37,.1);backdrop-filter:blur(22px) saturate(145%);-webkit-backdrop-filter:blur(22px) saturate(145%)}
    .auth-form h2{font-size:clamp(25px,3vw,32px);letter-spacing:-1px;color:#173d2d;margin:12px 0 8px}
    .auth-form>.muted{line-height:1.7;margin-bottom:26px}
    .auth-form form{display:grid;gap:17px}
    .auth-form label{display:grid;gap:8px;color:#405548;font-size:12px;font-weight:700}
    .auth-form input,.auth-form select{width:100%;min-height:48px;padding:0 14px;border:1px solid #dfe8df;border-radius:11px;background:rgba(250,252,249,.9);color:#1c3528;font-size:14px;outline:none}
    .auth-form input:focus,.auth-form select:focus{border-color:#91b197;box-shadow:0 0 0 4px rgba(145,177,151,.16)}
    .auth-submit{width:100%;min-height:49px;margin-top:4px;border-radius:12px}
    .auth-note{display:flex;gap:11px;align-items:flex-start;margin-top:20px;padding:13px;border:1px solid #e6ede5;border-radius:12px;background:rgba(241,247,240,.8)}
    .auth-note>span{color:#a77c42;font-size:20px}.auth-note p{margin:0;color:#6c7b70;font-size:11px;line-height:1.6}
    .auth-register-note{margin-top:23px;text-align:center;color:#718077;font-size:12px;line-height:1.8}
    .auth-register-note .link-button{color:#1c6045}
    .auth-form .back-link{display:inline-block;margin-bottom:24px;color:#64766a;font-size:12px}
    @media(max-width:760px){.auth-page{padding:16px;gap:16px}.auth-aside{padding:24px;border-radius:19px}.auth-message{margin:35px 0 24px}.auth-message h1{font-size:42px}.auth-footnote{display:none}.auth-form{border-radius:19px}}
    @media(prefers-reduced-motion:reduce){.auth-form input,.auth-form select{transition:none!important}}
  `]
})
export class AuthComponent {
  private auth = inject(AuthService);
  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  isRegister = false;
  referralCode = '';
  fullName = '';
  phoneNumber = '';
  pin = '';
  role: 'BUYER' | 'MERCHANT' = 'BUYER';
  loading = false;
  error = '';

  constructor() { this.isRegister = this.route.snapshot.queryParamMap.get('mode') === 'register'; this.referralCode = this.route.snapshot.queryParamMap.get('ref') || ''; }

  toggleMode(): void { this.isRegister = !this.isRegister; this.error = ''; }

  submit(): void {
    this.error = '';
    if (!this.phoneNumber.trim() || !this.pin.trim()) { this.error = 'Enter your phone number and PIN.'; return; }
    if (this.isRegister && !this.fullName.trim()) { this.error = 'Enter your full name.'; return; }
    const minimumPinLength = this.isRegister ? 6 : 4;
    if (this.pin.length < minimumPinLength || this.pin.length > 8 || !/^\d+$/.test(this.pin)) {
      this.error = this.isRegister ? 'PIN must contain 6 to 8 digits.' : 'PIN must contain 4 to 8 digits.';
      return;
    }
    this.loading = true;
    const request = this.isRegister
      ? this.auth.register(this.fullName.trim(), this.phoneNumber.trim(), this.pin, this.role, this.referralCode || undefined)
      : this.auth.login(this.phoneNumber.trim(), this.pin);
    request.subscribe({
      next: () => {
        this.loading = false;
        const referralCode = localStorage.getItem('merkato_referral_code');
        const visitorKey = localStorage.getItem('merkato_visitor_key');
        if (!this.referralCode && referralCode && visitorKey) {
          this.http.post(`/api/commerce/referrals/${encodeURIComponent(referralCode)}/claim`, { visitorKey }).subscribe({
            next: () => localStorage.removeItem('merkato_referral_code'),
            error: () => { /* Existing accounts or untracked visits can claim later in Commerce Hub. */ }
          });
        }
        this.router.navigateByUrl(this.auth.dashboardPath());
      },
      error: (err) => {
        this.loading = false;
        if (err?.status === 0) this.error = 'Cannot connect to MERKATO API. Check that the backend is running.';
        else if (err?.status === 409) this.error = 'This phone number already has an account. Please sign in.';
        else this.error = this.isRegister ? (err?.error?.message || 'Account could not be created. Check your details.') : 'Sign-in failed. Check your phone number and PIN.';
      }
    });
  }
}
