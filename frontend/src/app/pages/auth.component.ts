import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
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
          <label>PIN<input name="pin" [(ngModel)]="pin" required type="password" minlength="4" maxlength="8" autocomplete="current-password" placeholder="4–8 digit PIN"></label>
          <label *ngIf="isRegister">I want to join as<select name="role" [(ngModel)]="role"><option value="BUYER">Buyer — discover and shop</option><option value="MERCHANT">Merchant — list my products</option></select></label>
          <p class="form-error" *ngIf="error">{{error}}</p><button class="btn btn-primary auth-submit" [disabled]="loading">{{loading ? 'Please wait...' : (isRegister ? 'Create account' : 'Sign in securely')}} <span>↗</span></button>
        </form>
        <div class="auth-note"><span>⌑</span><p>Your PIN is protected with server-side password hashing. Never share it with anyone.</p></div><p class="auth-register-note">{{isRegister ? 'Already have an account?' : 'New to MERKATO?'}} <button class="link-button" type="button" (click)="toggleMode()">{{isRegister ? 'Sign in' : 'Create a buyer or merchant account'}}</button></p>
      </div></div>
    </section>
  `
})
export class AuthComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  isRegister = false;
  fullName = '';
  phoneNumber = '';
  pin = '';
  role: 'BUYER' | 'MERCHANT' = 'BUYER';
  loading = false;
  error = '';

  toggleMode(): void { this.isRegister = !this.isRegister; this.error = ''; }

  submit(): void {
    this.error = '';
    if (!this.phoneNumber.trim() || !this.pin.trim()) { this.error = 'Enter your phone number and PIN.'; return; }
    if (this.isRegister && !this.fullName.trim()) { this.error = 'Enter your full name.'; return; }
    if (this.pin.length < 4 || this.pin.length > 8 || !/^\d+$/.test(this.pin)) { this.error = 'PIN must contain 4 to 8 digits.'; return; }
    this.loading = true;
    const request = this.isRegister
      ? this.auth.register(this.fullName.trim(), this.phoneNumber.trim(), this.pin, this.role)
      : this.auth.login(this.phoneNumber.trim(), this.pin);
    request.subscribe({
      next: () => { this.loading = false; this.router.navigateByUrl(this.auth.dashboardPath()); },
      error: (err) => {
        this.loading = false;
        if (err?.status === 0) this.error = 'Cannot connect to MERKATO API. Check that the backend is running.';
        else if (err?.status === 409) this.error = 'This phone number already has an account. Please sign in.';
        else this.error = this.isRegister ? (err?.error?.message || 'Account could not be created. Check your details.') : 'Sign-in failed. Check your phone number and PIN.';
      }
    });
  }
}
