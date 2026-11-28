"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { ArrowLeft, Bell, MapPin, Package, Plus, RefreshCw, ShieldCheck, Trash2, UserRound } from "lucide-react";

const API = (process.env.NEXT_PUBLIC_API_BASE_URL || "/api").replace(/\/$/, "");
type Profile = { id:number; fullName:string; phoneNumber:string; email?:string|null; role:string; trustScore:number; isFaydaVerified:boolean };
type Address = { id:number; label:string; recipientName:string; phoneNumber:string; city:string; subCity:string; woreda?:string|null; streetAddress?:string|null; deliveryInstructions?:string|null; isDefault:boolean };
type Order = { id:number; orderNumber:string; productTitle:string; totalAmountEtb:number; escrowState:string; deliveryStatus:string; createdAt:string };
type Saved = { id:number; productId:number; productName:string; priceEtb:number; location:string; category:string };
type Notice = { id:number; title:string; message:string; isRead:boolean; createdAt:string };
type Section = "profile"|"addresses"|"orders"|"wishlist"|"notifications";
const money=(v:number)=>new Intl.NumberFormat("en-ET",{style:"currency",currency:"ETB",maximumFractionDigits:0}).format(v||0);
export default function AccountPage(){
 const [section,setSection]=useState<Section>("profile");
 const [token,setToken]=useState("");
 const [profile,setProfile]=useState<Profile|null>(null);
 const [addresses,setAddresses]=useState<Address[]>([]);
 const [orders,setOrders]=useState<Order[]>([]);
 const [wishlist,setWishlist]=useState<Saved[]>([]);
 const [notifications,setNotifications]=useState<Notice[]>([]);
 const [error,setError]=useState("");
 const [message,setMessage]=useState("");
 const [busy,setBusy]=useState(false);
 const [showAddress,setShowAddress]=useState(false);
 const [draft,setDraft]=useState({label:"Home",recipientName:"",phoneNumber:"",city:"Addis Ababa",subCity:"",woreda:"",streetAddress:"",deliveryInstructions:"",isDefault:false});
 const request=useCallback(async(path:string,init:RequestInit={})=>{
  const response=await fetch(API+path,{...init,headers:{Accept:"application/json",...(init.body instanceof FormData?{}:{"Content-Type":"application/json"}),Authorization:"Bearer "+token,...init.headers}});
  if(!response.ok){const body=await response.json().catch(()=>({}));throw new Error(body.message||"Request failed ("+response.status+")");}
  if(response.status===204)return null;
  return response.json();
 },[token]);
 const load=useCallback(async()=>{
  if(!token)return;setBusy(true);setError("");
  try{
   const [p,a]=await Promise.all([request("/account/profile"),request("/account/addresses")]);
   setProfile(p as Profile);setAddresses((a||[]) as Address[]);
   if(section==="orders"){setOrders((await request("/orders/mine")||[]) as Order[]);}
   if(section==="wishlist"){setWishlist((await request("/engagement/wishlist")||[]) as Saved[]);}
   if(section==="notifications"){const n=await request("/engagement/notifications");setNotifications((n?.notifications||[]) as Notice[]);}
  }catch(e){setError(e instanceof Error?e.message:"Could not load account data.");}
  finally{setBusy(false);}
 },[token,request,section]);
 useEffect(()=>{const t=sessionStorage.getItem("merkato_access_token")||"";setToken(t);if(!t)setError("Sign in to view your account.");},[]);
 useEffect(()=>{if(token)void load();},[token,load]);
 async function saveAddress(e:React.FormEvent){e.preventDefault();setBusy(true);setError("");try{await request("/account/addresses",{method:"POST",body:JSON.stringify(draft)});setMessage("Address saved.");setShowAddress(false);setDraft({label:"Home",recipientName:"",phoneNumber:"",city:"Addis Ababa",subCity:"",woreda:"",streetAddress:"",deliveryInstructions:"",isDefault:false});await load();}catch(x){setError(x instanceof Error?x.message:"Address could not be saved.");}finally{setBusy(false);}}
 async function removeSaved(productId:number){try{await request("/engagement/wishlist/"+productId,{method:"DELETE"});setWishlist(v=>v.filter(x=>x.productId!==productId));setMessage("Removed from wishlist.");}catch(e){setError(e instanceof Error?e.message:"Could not remove item.");}}
 async function markRead(id:number){try{await request("/engagement/notifications/"+id+"/read",{method:"POST",body:"{}"});setNotifications(v=>v.map(n=>n.id===id?{...n,isRead:true}:n));}catch(e){setError(e instanceof Error?e.message:"Could not update notification.");}}
 const tabs:[Section,string,typeof UserRound][]=[["profile","Profile",UserRound],["addresses","Addresses",MapPin],["orders","Orders",Package],["wishlist","Wishlist",ShieldCheck],["notifications","Notifications",Bell]];
 return <main className="min-h-screen bg-[#f5f6ef] text-[#17382f]"><header className="bg-[#062f28] text-white"><div className="mx-auto flex max-w-7xl items-center justify-between px-5 py-5"><Link href="/" className="text-2xl font-black">MERKATO<span className="text-[#d5ad61]">.</span></Link><Link href="/" className="text-sm text-white/80">Marketplace</Link></div></header>
 <div className="mx-auto max-w-7xl px-5 py-9"><Link href="/" className="inline-flex items-center gap-2 text-sm text-emerald-900"><ArrowLeft size={16}/> Back to marketplace</Link>
 <div className="mt-6 grid gap-6 lg:grid-cols-[240px_1fr]"><aside className="h-fit rounded-3xl border border-emerald-950/10 bg-white p-4"><div className="mb-5 rounded-2xl bg-[#0b493b] p-5 text-white"><p className="text-xs tracking-widest text-[#e0bd76]">YOUR MERKATO</p><h1 className="mt-2 text-2xl font-black">My account</h1><p className="mt-2 text-xs text-white/70">Manage your marketplace activity.</p></div><nav className="grid gap-1">{tabs.map(([key,label,Icon])=><button key={key} onClick={()=>setSection(key)} className={`flex items-center gap-3 rounded-xl px-4 py-3 text-left text-sm font-semibold ${section===key?"bg-emerald-50 text-emerald-900":"text-slate-600 hover:bg-slate-50"}`}><Icon size={17}/>{label}</button>)}</nav><button onClick={()=>void load()} className="mt-4 flex w-full items-center justify-center gap-2 rounded-xl border border-slate-200 py-3 text-xs font-bold"><RefreshCw size={14}/> Refresh</button></aside>
 <section className="min-w-0 rounded-3xl border border-emerald-950/10 bg-white p-5 sm:p-8"><div className="mb-6 flex flex-wrap items-center justify-between gap-3"><div><p className="text-xs font-bold tracking-[.18em] text-emerald-800">ACCOUNT WORKSPACE</p><h2 className="mt-2 text-3xl font-black">{tabs.find(t=>t[0]===section)?.[1]}</h2></div>{busy&&<span className="text-xs text-slate-500">Loading…</span>}</div>
 {error&&<p role="alert" className="mb-4 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">{error}</p>}{message&&<p className="mb-4 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800">{message}</p>}
 {!token?<div className="rounded-2xl bg-slate-50 p-8 text-center"><p className="text-sm text-slate-600">Your session is not available in this browser.</p><Link className="mt-4 inline-block rounded-xl bg-[#073d32] px-6 py-3 text-sm font-bold text-white" href="/login">Sign in</Link></div>:null}
 {token&&section==="profile"&&<div className="grid gap-4 sm:grid-cols-2">{[["Full name",profile?.fullName],["Phone number",profile?.phoneNumber],["Email",profile?.email||"Not added"],["Role",profile?.role],["Trust score",String(profile?.trustScore??"—")],["Identity status",profile?.isFaydaVerified?"Verified":"Not verified"]].map(([k,v])=><div key={k} className="rounded-2xl bg-[#f7f8f3] p-5"><p className="text-xs font-bold uppercase tracking-wider text-slate-500">{k}</p><p className="mt-2 font-bold">{v||"—"}</p></div>)}</div>}
 {token&&section==="addresses"&&<><div className="mb-4 flex justify-end"><button onClick={()=>setShowAddress(v=>!v)} className="inline-flex items-center gap-2 rounded-xl bg-[#073d32] px-4 py-3 text-sm font-bold text-white"><Plus size={16}/> Add address</button></div>{showAddress&&<form onSubmit={saveAddress} className="mb-5 grid gap-3 rounded-2xl bg-[#f7f8f3] p-5 sm:grid-cols-2">{([["label","Label"],["recipientName","Recipient name"],["phoneNumber","Phone number"],["city","City"],["subCity","Sub-city"],["woreda","Woreda"],["streetAddress","Street address"],["deliveryInstructions","Delivery instructions"]] as const).map(([key,label])=><label key={key} className="grid gap-1 text-xs font-bold text-slate-600">{label}<input required={["label","recipientName","phoneNumber","city","subCity"].includes(key)} value={draft[key]} onChange={e=>setDraft(v=>({...v,[key]:e.target.value}))} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm font-normal"/></label>)}<label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={draft.isDefault} onChange={e=>setDraft(v=>({...v,isDefault:e.target.checked}))}/> Set as default</label><button disabled={busy} className="rounded-xl bg-emerald-900 px-4 py-3 text-sm font-bold text-white">Save address</button></form>}{addresses.length===0?<p className="rounded-2xl bg-slate-50 p-8 text-center text-sm text-slate-500">No saved addresses found.</p>:<div className="grid gap-3 md:grid-cols-2">{addresses.map(a=><article key={a.id} className="rounded-2xl border border-slate-200 p-5"><div className="flex justify-between gap-2"><strong>{a.label}</strong>{a.isDefault&&<span className="text-xs font-bold text-emerald-800">DEFAULT</span>}</div><p className="mt-2 text-sm">{a.recipientName}</p><p className="text-sm text-slate-500">{a.phoneNumber} · {a.subCity}, {a.city}</p><p className="mt-1 text-xs text-slate-400">{a.streetAddress} {a.woreda}</p></article>)}</div>}</>}
 {token&&section==="orders"&&(orders.length?<div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead className="text-xs text-slate-500"><tr><th className="p-3">Order</th><th className="p-3">Product</th><th className="p-3">Total</th><th className="p-3">Delivery</th><th className="p-3">Payment</th></tr></thead><tbody>{orders.map(o=><tr key={o.id} className="border-t border-slate-100"><td className="p-3 font-bold">{o.orderNumber}</td><td className="p-3">{o.productTitle}</td><td className="p-3">{money(o.totalAmountEtb)}</td><td className="p-3">{o.deliveryStatus}</td><td className="p-3">{o.escrowState}</td></tr>)}</tbody></table></div>:<p className="rounded-2xl bg-slate-50 p-8 text-center text-sm text-slate-500">No orders found.</p>)}
 {token&&section==="wishlist"&&(wishlist.length?<div className="grid gap-3 sm:grid-cols-2">{wishlist.map(x=><article key={x.productId} className="flex items-center justify-between gap-4 rounded-2xl border border-slate-200 p-4"><div><strong>{x.productName}</strong><p className="mt-1 text-sm text-emerald-900">{money(x.priceEtb)}</p><p className="text-xs text-slate-500">{x.category} · {x.location}</p></div><button aria-label="Remove saved product" onClick={()=>void removeSaved(x.productId)} className="rounded-lg p-2 text-red-700 hover:bg-red-50"><Trash2 size={17}/></button></article>)}</div>:<p className="rounded-2xl bg-slate-50 p-8 text-center text-sm text-slate-500">Your wishlist is empty.</p>)}
 {token&&section==="notifications"&&(notifications.length?<div className="grid gap-3">{notifications.map(n=><article key={n.id} className={`rounded-2xl border p-4 ${n.isRead?"border-slate-200":"border-emerald-200 bg-emerald-50/40"}`}><div className="flex items-start justify-between gap-3"><div><strong>{n.title}</strong><p className="mt-1 text-sm text-slate-600">{n.message}</p><p className="mt-2 text-xs text-slate-400">{new Date(n.createdAt).toLocaleString()}</p></div>{!n.isRead&&<button onClick={()=>void markRead(n.id)} className="shrink-0 text-xs font-bold text-emerald-800">Mark read</button>}</div></article>)}</div>:<p className="rounded-2xl bg-slate-50 p-8 text-center text-sm text-slate-500">No notifications found.</p>)}
 </section></div></div></main>;
}
