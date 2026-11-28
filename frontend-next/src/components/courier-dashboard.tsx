"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";

const API = (process.env.NEXT_PUBLIC_API_BASE_URL || "/api").replace(/\/$/, "");
type Profile = { id: number; approvalStatus: string; serviceAreas: string[]; transportMode: string; vehicleDescription?: string | null; capacityKg?: number | null; reviewNote?: string | null };
type Offer = { id: number; deliveryId: number; status: string; deliveryStatus: string; agreedFeeEtb: number; assignedAt: string; displayName: string; code: string };
const money = (value: number) => new Intl.NumberFormat("en-ET", { style: "currency", currency: "ETB", maximumFractionDigits: 0 }).format(value || 0);

export default function CourierDashboard() {
  const [token, setToken] = useState("");
  const [role, setRole] = useState("");
  const [profile, setProfile] = useState<Profile | null>(null);
  const [profileMissing, setProfileMissing] = useState(false);
  const [offers, setOffers] = useState<Offer[]>([]);
  const [areas, setAreas] = useState("Addis Ababa");
  const [mode, setMode] = useState("MOTORCYCLE");
  const [vehicle, setVehicle] = useState("");
  const [capacity, setCapacity] = useState("50");
  const [otp, setOtp] = useState<Record<number, string>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  const request = useCallback(async (path: string, init: RequestInit = {}) => {
    const response = await fetch(API + path, { ...init, headers: { Accept: "application/json", Authorization: "Bearer " + token, ...(init.body ? { "Content-Type": "application/json" } : {}), ...init.headers } });
    const payload = response.status === 204 ? null : await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(payload?.message || payload?.error || "Request failed (" + response.status + ")");
    return payload;
  }, [token]);

  const load = useCallback(async () => {
    if (!token) return;
    setError("");
    try {
      const [p, a] = await Promise.all([
        request("/v1/delivery/partners/mine").catch((e: Error) => { if (e.message.includes("(404)")) { setProfileMissing(true); return null; } throw e; }),
        request("/v1/delivery/assignments/mine")
      ]);
      setProfile(p);
      setProfileMissing(!p);
      setOffers(a || []);
      if (p?.serviceAreas?.length) setAreas(p.serviceAreas.join(", "));
    } catch (e) { setError(e instanceof Error ? e.message : "Could not load courier workspace."); }
  }, [request, token]);

  useEffect(() => {
    const storedToken = sessionStorage.getItem("merkato_access_token") || "";
    let user: { role?: string } = {};
    try { user = JSON.parse(sessionStorage.getItem("merkato_user") || "{}"); } catch { /* invalid local session */ }
    setToken(storedToken);
    setRole(String(user.role || "").toUpperCase());
  }, []);
  useEffect(() => { if (token) void load(); }, [token, load]);

  async function act(path: string, body?: unknown) {
    setBusy(true); setError(""); setMessage("");
    try { await request(path, { method: "POST", ...(body === undefined ? { body: "{}" } : { body: JSON.stringify(body) }) }); setMessage("Update saved."); await load(); }
    catch (e) { setError(e instanceof Error ? e.message : "Could not save update."); }
    finally { setBusy(false); }
  }

  async function apply(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const serviceAreas = areas.split(",").map(x => x.trim()).filter(Boolean);
    if (!serviceAreas.length) { setError("Enter at least one service area."); return; }
    await act("/v1/delivery/partners/apply", { serviceAreas, transportMode: mode, vehicleDescription: vehicle || null, capacityKg: capacity ? Number(capacity) : null });
    setMessage("Application submitted. An administrator must approve it before you can receive delivery offers.");
  }

  if (!token) return <main className="grid min-h-screen place-items-center bg-stone-50 p-6"><section className="rounded-2xl bg-white p-8 text-center shadow-sm"><h1 className="text-2xl font-black text-emerald-950">Sign in required</h1><Link href="/login" className="mt-4 inline-block rounded-xl bg-emerald-900 px-5 py-3 font-bold text-white">Sign in</Link></section></main>;
  if (role !== "COURIER" && role !== "ADMIN") return <main className="p-10 text-center">Courier account required. <Link href="/">Return to MERKATO</Link></main>;

  return <main className="min-h-screen bg-[#f4f6f0] text-[#18392e]"><header className="bg-[#082f28] text-white"><div className="mx-auto flex max-w-6xl items-center justify-between px-5 py-5"><Link href="/" className="text-2xl font-black">MERKATO.</Link><button onClick={() => void load()} className="rounded-lg border border-white/20 px-4 py-2 text-sm">Refresh</button></div></header><div className="mx-auto max-w-6xl px-5 py-9"><p className="text-xs font-bold tracking-[.2em] text-emerald-800">DELIVERY NETWORK</p><h1 className="mt-2 text-4xl font-black">Courier workspace</h1><p className="mt-2 text-sm text-slate-500">Manage your application, delivery offers and verified handovers.</p>
    {error && <p role="alert" className="mt-5 rounded-xl bg-red-50 p-4 text-sm text-red-800">{error}</p>}{message && <p className="mt-5 rounded-xl bg-emerald-50 p-4 text-sm text-emerald-800">{message}</p>}
    {profileMissing || profile?.approvalStatus === "REJECTED" || profile?.approvalStatus === "SUSPENDED" ? <form onSubmit={apply} className="mt-7 grid gap-4 rounded-2xl border bg-white p-6 sm:grid-cols-2"><h2 className="text-xl font-black sm:col-span-2">Delivery partner application</h2><label className="grid gap-2 text-sm font-semibold sm:col-span-2">Service areas (comma separated)<input required value={areas} onChange={e=>setAreas(e.target.value)} className="rounded-lg border p-3 font-normal" placeholder="Bole, Gerji, Kazanchis"/></label><label className="grid gap-2 text-sm font-semibold">Transport mode<select value={mode} onChange={e=>setMode(e.target.value)} className="rounded-lg border p-3"><option>MOTORCYCLE</option><option>BICYCLE</option><option>CAR</option><option>WALKING</option></select></label><label className="grid gap-2 text-sm font-semibold">Vehicle description<input value={vehicle} maxLength={200} onChange={e=>setVehicle(e.target.value)} className="rounded-lg border p-3 font-normal"/></label><label className="grid gap-2 text-sm font-semibold">Capacity (kg)<input type="number" min="0" max="10000" value={capacity} onChange={e=>setCapacity(e.target.value)} className="rounded-lg border p-3 font-normal"/></label><button disabled={busy} className="self-end rounded-xl bg-emerald-900 px-5 py-3 font-bold text-white">{busy ? "Submitting…" : "Submit application"}</button></form> : profile && <section className="mt-7 rounded-2xl border bg-white p-6"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-xl font-black">Application status</h2><span className="rounded-full bg-amber-50 px-3 py-1 text-xs font-bold text-amber-800">{profile.approvalStatus}</span></div><p className="mt-3 text-sm text-slate-600">{profile.serviceAreas?.join(", ")} · {profile.transportMode} · {profile.vehicleDescription || "No vehicle details"}</p>{profile.reviewNote && <p className="mt-2 text-sm text-slate-500">Review note: {profile.reviewNote}</p>}{profile.approvalStatus === "PENDING" && <p className="mt-3 text-sm text-slate-500">Your application is waiting for administrator approval. Offers appear after approval.</p>}</section>}
    <section className="mt-7 rounded-2xl border bg-white p-6"><h2 className="text-xl font-black">Delivery offers & active assignments</h2>{offers.length === 0 ? <p className="py-8 text-center text-sm text-slate-500">No delivery offers are assigned to this account yet.</p> : <div className="mt-4 grid gap-3">{offers.map(o=><article key={o.id} className="rounded-xl border p-4"><div className="flex flex-wrap items-center justify-between gap-3"><div><strong>Delivery #{o.deliveryId}</strong><p className="mt-1 text-sm text-slate-500">{o.displayName} · {money(o.agreedFeeEtb)}</p></div><span className="text-xs font-bold">{o.status} · {o.deliveryStatus}</span></div>{o.status === "OFFERED" && <div className="mt-4 flex gap-2"><button disabled={busy} onClick={()=>void act("/v1/delivery/assignments/"+o.id+"/accept")} className="rounded-lg bg-emerald-900 px-4 py-2 text-sm font-bold text-white">Accept offer</button><button disabled={busy} onClick={()=>void act("/v1/delivery/assignments/"+o.id+"/reject")} className="rounded-lg border px-4 py-2 text-sm font-bold">Decline</button></div>}{o.status === "ACCEPTED" && <div className="mt-4 flex flex-wrap gap-2"><select id={"status-"+o.id} className="rounded-lg border p-2 text-sm"><option value="PICKED_UP">Picked up</option><option value="IN_TRANSIT">In transit</option><option value="OUT_FOR_DELIVERY">Out for delivery</option><option value="FAILED_DELIVERY">Failed delivery</option><option value="RETURNED_TO_MERCHANT">Returned to merchant</option></select><button disabled={busy} onClick={()=>{const el=document.getElementById("status-"+o.id) as HTMLSelectElement|null;if(el)void act("/v1/delivery/assignments/"+o.id+"/status",{status:el.value});}} className="rounded-lg bg-emerald-900 px-4 py-2 text-sm font-bold text-white">Update status</button>{o.deliveryStatus === "OUT_FOR_DELIVERY" && <><input aria-label="Buyer delivery OTP" inputMode="numeric" maxLength={8} value={otp[o.id]||""} onChange={e=>setOtp(v=>({...v,[o.id]:e.target.value.replace(/\D/g,"").slice(0,8)}))} placeholder="Buyer OTP" className="w-32 rounded-lg border p-2 text-sm"/><button disabled={busy || (otp[o.id]||"").length<4} onClick={()=>void act("/v1/delivery/assignments/"+o.id+"/verify-otp",{otpCode:otp[o.id]})} className="rounded-lg bg-amber-700 px-4 py-2 text-sm font-bold text-white">Complete with OTP</button></>}</div>}</article>)}</div>}</section>
  </div></main>;
}
