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
      <div class="auth-form-wrap"><div class="auth-form"><a routerLink="/" class="back-link">← Back to marketplace</a><span class="eyebrow">WELCOME BACK</span><h2>Sign in to MERKATO</h2><p class="muted">Use your registered phone number and PIN to continue.</p>
        <form (ngSubmit)="submit()"><label>Phone number<input name="phone" [(ngModel)]="phoneNumber" required autocomplete="tel" placeholder="09XXXXXXXX" inputmode="tel"></label><label>PIN<input name="pin" [(ngModel)]="pin" required type="password" minlength="4" maxlength="8" autocomplete="current-password" placeholder="Enter your PIN"></label><p class="form-error" *ngIf="error">{{error}}</p><button class="btn btn-primary auth-submit" [disabled]="loading">{{loading ? 'Signing in...' : 'Sign in securely'}} <span>↗</span></button></form>
        <div class="auth-note"><span>⌑</span><p>Your session is stored for this browser session. Never share your PIN with anyone.</p></div><p class="auth-register-note">New to MERKATO? <span>Merchant and buyer registration is being connected to the API.</span></p>
      </div></div>
    </section>
  `
})
export class AuthComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  phoneNumber = '';
  pin = '';
  loading = false;
  error = '';
  submit(): void {
    this.error = '';
    if (!this.phoneNumber.trim() || !this.pin.trim()) { this.error = 'Enter your phone number and PIN.'; return; }
    this.loading = true;
    this.auth.login(this.phoneNumber.trim(), this.pin.trim()).subscribe({
      next: () => { this.loading = false; this.router.navigateByUrl(this.auth.dashboardPath()); },
      error: (err) => { this.loading = false; this.error = err?.status === 0 ? 'Cannot connect to MERKATO API. Check that the backend is running.' : 'Sign-in failed. Check your phone number and PIN.'; }
    });
  }
}
