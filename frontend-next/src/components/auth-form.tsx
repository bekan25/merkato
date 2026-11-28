"use client";

import { FormEvent, useState } from "react";
import { ArrowLeft, ArrowRight, BadgeCheck, ShieldCheck } from "lucide-react";
import type { AuthUser, MerkatoRole } from "@/lib/types";

type AuthResponse = AuthUser & { token: string; role: string | number };
const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || "/api").replace(/\/$/, "");
const roles: MerkatoRole[] = ["BUYER", "MERCHANT", "COURIER", "INSPECTOR", "ARBITER", "ADMIN"];
function normalizeRole(role: string | number): MerkatoRole {
  if (typeof role === "number") return roles[role] || "BUYER";
  const normalized = role.toUpperCase() as MerkatoRole;
  return roles.includes(normalized) ? normalized : "BUYER";
}
function destination(role: MerkatoRole) {
  return role === "ADMIN" ? "/admin" : role === "MERCHANT" ? "/merchant" : role === "COURIER" ? "/delivery-partner" : "/dashboard";
}

export function AuthForm({ initialMode = "login" }: { initialMode?: "login" | "register" }) {
  const [mode, setMode] = useState<"login" | "register">(initialMode);
  const [adminMode, setAdminMode] = useState(false);
  const [fullName, setFullName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [pin, setPin] = useState("");
  const [role, setRole] = useState<"BUYER" | "MERCHANT">("BUYER");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError("");
    if (!adminMode && (!/^\\d{4,8}$/.test(pin) || (mode === "register" && pin.length < 6))) {
      setError(mode === "register" ? "PIN must contain 6 to 8 digits." : "PIN must contain 4 to 8 digits."); return;
    }
    setLoading(true);
    try {
      const endpoint = adminMode ? "/auth/admin-login" : mode === "register" ? "/auth/register" : "/auth/login";
      const body = adminMode ? { email: email.trim(), password } : mode === "register"
        ? { fullName: fullName.trim(), phoneNumber: phoneNumber.trim(), pin, role, faydaIdNumber: null, referralCode: null }
        : { phoneNumber: phoneNumber.trim(), pin };
      const response = await fetch(`${API_BASE}${endpoint}`, { method: "POST", headers: { "Content-Type": "application/json", Accept: "application/json" }, body: JSON.stringify(body) });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        if (response.status === 409) throw new Error("This phone number already has an account. Please sign in.");
        if (response.status === 423) throw new Error("Too many attempts. Please wait before trying again.");
        throw new Error(payload.message || (adminMode ? "Administrator sign-in failed." : mode === "register" ? "Account could not be created. Check your details." : "Sign-in failed. Check your phone number and PIN."));
      }
      const user = { ...payload, role: normalizeRole(payload.role) } as AuthResponse;
      if (!user.token || !user.userId) throw new Error("The server response did not contain a valid session.");
      sessionStorage.setItem("merkato_access_token", user.token);
      sessionStorage.setItem("merkato_user", JSON.stringify(user));
      window.location.assign(destination(user.role));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not connect to MERKATO. Please try again.");
    } finally { setLoading(false); }
  }

  return <main className="grid min-h-screen bg-[#f5f5ed] lg:grid-cols-2">
    <aside className="relative flex min-h-[300px] flex-col justify-between overflow-hidden bg-[radial-gradient(circle_at_80%_15%,rgba(213,173,97,.22),transparent_30%),linear-gradient(145deg,#052b25,#0b493b)] p-8 text-white sm:p-12 lg:min-h-screen lg:p-16">
      <a href="/" className="text-2xl font-black tracking-tight">MERKATO<span className="text-[#d5ad61]">.</span></a>
      <div className="relative z-10 my-12 max-w-xl"><p className="text-xs font-bold tracking-[.24em] text-[#e3c785]">YOUR MARKET. YOUR PEOPLE.</p><h1 className="mt-5 text-4xl font-black leading-tight tracking-tight sm:text-6xl">Local commerce,<br/><span className="text-[#e3bd7c]">without limits.</span></h1><p className="mt-6 max-w-md leading-8 text-white/75">One account connects you to a growing community of buyers and merchants across Ethiopia.</p><ul className="mt-8 space-y-4 text-sm text-white/85"><li className="flex items-center gap-3"><BadgeCheck className="text-[#e3bd7c]"/> Discover local products</li><li className="flex items-center gap-3"><BadgeCheck className="text-[#e3bd7c]"/> Manage your marketplace activity</li><li className="flex items-center gap-3"><BadgeCheck className="text-[#e3bd7c]"/> Built around secure transactions</li></ul></div>
      <p className="text-[10px] tracking-[.2em] text-white/55">MERKATO · MADE FOR ETHIOPIA</p>
    </aside>
    <section className="flex items-center justify-center p-5 sm:p-10"><div className="w-full max-w-md rounded-[2rem] border border-white bg-white p-7 shadow-[0_24px_80px_rgba(23,61,45,.1)] sm:p-10">
      <a href="/" className="mb-8 inline-flex items-center gap-2 text-xs font-semibold text-slate-500"><ArrowLeft size={15}/> Back to marketplace</a>
      <p className="text-xs font-bold tracking-[.2em] text-emerald-800">{adminMode ? "ADMINISTRATOR ACCESS" : mode === "register" ? "JOIN THE MARKETPLACE" : "WELCOME BACK"}</p>
      <h2 className="mt-3 text-3xl font-black tracking-tight text-[#173d2d]">{adminMode ? "Administrator sign in" : mode === "register" ? "Create your account" : "Sign in to MERKATO"}</h2>
      <p className="mt-3 text-sm leading-6 text-slate-500">{adminMode ? "Use your approved administrator email and password." : mode === "register" ? "Choose how you want to participate in MERKATO." : "Use your registered phone number and PIN to continue."}</p>
      <form onSubmit={submit} className="mt-8 grid gap-5">
        {adminMode ? <><label className="grid gap-2 text-xs font-bold text-slate-600">Administrator email<input required type="email" autoComplete="username" value={email} onChange={e=>setEmail(e.target.value)} placeholder="admin@example.com" className="h-12 rounded-xl border border-slate-200 bg-[#fbfcf9] px-4 text-sm outline-none focus:border-emerald-700"/></label><label className="grid gap-2 text-xs font-bold text-slate-600">Password<input required type="password" autoComplete="current-password" value={password} onChange={e=>setPassword(e.target.value)} className="h-12 rounded-xl border border-slate-200 bg-[#fbfcf9] px-4 text-sm outline-none focus:border-emerald-700"/></label></> : <>
          {mode === "register" && <label className="grid gap-2 text-xs font-bold text-slate-600">Full name<input required autoComplete="name" value={fullName} onChange={e=>setFullName(e.target.value)} placeholder="Your full name" className="h-12 rounded-xl border border-slate-200 bg-[#fbfcf9] px-4 text-sm outline-none focus:border-emerald-700"/></label>}
          <label className="grid gap-2 text-xs font-bold text-slate-600">Phone number<input required autoComplete="tel" inputMode="tel" value={phoneNumber} onChange={e=>setPhoneNumber(e.target.value)} placeholder="09XXXXXXXX" className="h-12 rounded-xl border border-slate-200 bg-[#fbfcf9] px-4 text-sm outline-none focus:border-emerald-700"/></label>
          <label className="grid gap-2 text-xs font-bold text-slate-600">PIN<input required type="password" inputMode="numeric" autoComplete={mode === "register" ? "new-password" : "current-password"} minLength={mode === "register" ? 6 : 4} maxLength={8} value={pin} onChange={e=>setPin(e.target.value.replace(/\\D/g,"").slice(0,8))} placeholder={mode === "register" ? "6–8 digit PIN" : "Your PIN"} className="h-12 rounded-xl border border-slate-200 bg-[#fbfcf9] px-4 text-sm outline-none focus:border-emerald-700"/></label>
          {mode === "register" && <label className="grid gap-2 text-xs font-bold text-slate-600">I want to join as<select value={role} onChange={e=>setRole(e.target.value as "BUYER"|"MERCHANT")} className="h-12 rounded-xl border border-slate-200 bg-[#fbfcf9] px-4 text-sm"><option value="BUYER">Buyer — discover and shop</option><option value="MERCHANT">Merchant — list my products</option></select></label>}
        </>}
        {error && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-xs leading-5 text-red-800">{error}</p>}
        <button disabled={loading} className="flex h-12 items-center justify-center gap-2 rounded-xl bg-[#073d32] px-5 text-sm font-bold text-white transition hover:bg-[#0b493b] disabled:cursor-wait disabled:opacity-60">{loading ? "Please wait…" : adminMode ? "Administrator sign in" : mode === "register" ? "Create account" : "Sign in securely"} <ArrowRight size={16}/></button>
      </form>
      <div className="mt-6 flex items-start gap-3 rounded-xl border border-emerald-100 bg-emerald-50/70 p-4"><ShieldCheck className="mt-0.5 shrink-0 text-emerald-800" size={18}/><p className="text-[11px] leading-5 text-slate-600">Your PIN is protected with server-side password hashing. Never share it with anyone.</p></div>
      <div className="mt-7 space-y-3 text-center text-xs text-slate-500">{!adminMode && <p>{mode === "register" ? "Already have an account?" : "New to MERKATO?"} <button type="button" onClick={()=>{setMode(mode==="register"?"login":"register");setError("");}} className="font-bold text-emerald-800">{mode === "register" ? "Sign in" : "Create a buyer or merchant account"}</button></p>}<p><button type="button" onClick={()=>{setAdminMode(!adminMode);setMode("login");setError("");}} className="font-semibold text-emerald-800">{adminMode ? "← Buyer / Merchant sign in" : "Administrator sign in"}</button></p></div>
    </div></section>
  </main>;
}
