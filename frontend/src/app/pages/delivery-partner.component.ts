import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, DeliveryPartnerProfile } from '../core/api.service';

@Component({
 standalone:true, selector:'app-delivery-partner', imports:[CommonModule,FormsModule],
 template:`
 <main class="page"><header><small>MERKATO DELIVERY NETWORK</small><h1>Individual delivery partner</h1><p>Apply to deliver orders in the service areas you support.</p></header>
 <p class="notice" *ngIf="message">{{message}}</p>
 <section class="panel" *ngIf="!profile"><h2>Partner application</h2>
 <label>Service areas (comma separated)<input [(ngModel)]="areas" placeholder="Bole, Gerji, Kazanchis"></label>
 <label>Transport mode<select [(ngModel)]="transportMode"><option>MOTORCYCLE</option><option>BICYCLE</option><option>CAR</option><option>WALKING</option></select></label>
 <label>Vehicle description<input [(ngModel)]="vehicle" maxlength="200"></label>
 <label>Capacity in kg<input type="number" min="0" max="10000" [(ngModel)]="capacity"></label>
 <button [disabled]="busy || !areas.trim()" (click)="apply()">Submit application</button></section>
 <section class="panel" *ngIf="profile"><h2>Your application · <b>{{profile.approvalStatus}}</b></h2><p>{{(profile.serviceAreas||[]).join(', ')}} · {{profile.transportMode}}</p><p *ngIf="profile.reviewNote">Review note: {{profile.reviewNote}}</p><button *ngIf="profile.approvalStatus==='REJECTED'||profile.approvalStatus==='SUSPENDED'" (click)="profile=null">Apply again</button></section>
 <section class="panel"><div class="head"><h2>Delivery offers</h2><button class="light" (click)="loadOffers()">Refresh</button></div>
 <article *ngFor="let a of offers" class="offer"><div><b>{{a.displayName}}</b><p>Delivery #{{a.deliveryId}} · {{a.agreedFeeEtb|number:'1.2-2'}} ETB</p><small>{{a.status}} · {{a.assignedAt|date:'medium'}}</small></div><div *ngIf="a.status==='OFFERED'"><button (click)="respond(a.id,true)">Accept</button> <button class="danger" (click)="respond(a.id,false)">Decline</button></div><div *ngIf="a.status==='ACCEPTED'" class="progress"><button *ngIf="a.deliveryStatus==='ASSIGNED_COURIER'" (click)="updateStatus(a.id,'PICKED_UP')">Picked up</button><button *ngIf="a.deliveryStatus==='PICKED_UP'" (click)="updateStatus(a.id,'IN_TRANSIT')">In transit</button><button *ngIf="a.deliveryStatus==='PICKED_UP'||a.deliveryStatus==='IN_TRANSIT'" (click)="updateStatus(a.id,'OUT_FOR_DELIVERY')">Out for delivery</button><button *ngIf="a.deliveryStatus==='FAILED_DELIVERY'" (click)="updateStatus(a.id,'RETURNED_TO_MERCHANT')">Return to seller</button><ng-container *ngIf="a.deliveryStatus==='OUT_FOR_DELIVERY'"><input [(ngModel)]="otpCodes[a.id]" inputmode="numeric" maxlength="8" placeholder="Buyer OTP"><button (click)="complete(a.id)">Complete with OTP</button></ng-container><small>Progress: {{a.deliveryStatus}}</small></div></article>
 <p *ngIf="!offers.length">No delivery offers yet.</p></section></main>`,
 styles:[`.page{max-width:850px;margin:36px auto;padding:0 18px 48px;color:#173d2d}.page header small{color:#16834f;font-weight:800;letter-spacing:1.4px}.page h1{font-size:36px;margin:8px 0}.page header p{color:#68766d}.panel{padding:22px;margin:18px 0;background:#fff;border:1px solid #e4ebe5;border-radius:16px}.panel h2{margin:0 0 15px}.panel label{display:grid;gap:7px;margin:13px 0;font-weight:700;font-size:13px}.panel input,.panel select{padding:12px;border:1px solid #dce5dd;border-radius:9px;background:#fbfcfb}.panel button{padding:10px 14px;border:0;border-radius:9px;background:#176b43;color:#fff;font-weight:800}.panel button:disabled{opacity:.5}.progress{display:flex;flex-wrap:wrap;gap:7px;margin-top:10px}.progress input{min-width:100px;padding:9px;border:1px solid #dce5dd;border-radius:8px}.panel .light{background:#edf5ef;color:#176b43}.panel .danger{background:#fff0ed;color:#a33b2d}.notice{padding:12px;background:#edf8ef;border-radius:9px}.head,.offer{display:flex;justify-content:space-between;align-items:center;gap:14px}.offer{padding:14px 0;border-top:1px solid #edf0ed}.offer p{margin:5px 0;color:#52645a}.offer small{color:#7b887f}@media(max-width:600px){.offer,.head{align-items:flex-start;flex-direction:column}}`]
})
export class DeliveryPartnerComponent implements OnInit{
 private api=inject(ApiService); profile:DeliveryPartnerProfile|null=null;
 offers:Array<{id:number;deliveryId:number;status:string;deliveryStatus:string;agreedFeeEtb:number;assignedAt:string;displayName:string}>=[];
 areas='';transportMode='MOTORCYCLE';vehicle='';capacity:number|null=null;busy=false;message='';otpCodes:Record<number,string>={};
 ngOnInit():void{this.api.getMyDeliveryPartnerApplication().subscribe({next:x=>this.profile=x,error:()=>this.profile=null});this.loadOffers();}
 apply():void{this.busy=true;this.api.applyAsDeliveryPartner({serviceAreas:this.areas.split(',').map(x=>x.trim()).filter(Boolean),transportMode:this.transportMode,vehicleDescription:this.vehicle||null,capacityKg:this.capacity}).subscribe({next:r=>{this.message=r.message;this.api.getMyDeliveryPartnerApplication().subscribe({next:x=>this.profile=x});this.busy=false;},error:e=>{this.message=e?.error?.message||'Application failed.';this.busy=false;}});}
 loadOffers():void{this.api.getMyDeliveryAssignments().subscribe({next:x=>this.offers=x,error:()=>this.offers=[]});}
 updateStatus(id:number,status:string):void{this.api.updateAssignedDeliveryStatus(id,status).subscribe({next:()=>{this.message='Delivery status updated.';this.loadOffers();},error:e=>this.message=e?.error?.message||'Status update failed.'});}
 complete(id:number):void{const code=this.otpCodes[id]||'';this.api.verifyAssignedDeliveryOtp(id,code).subscribe({next:()=>{this.message='Delivery completed after OTP verification.';this.loadOffers();},error:e=>this.message=e?.error?.message||'OTP verification failed.'});}
 respond(id:number,accept:boolean):void{(accept?this.api.acceptDeliveryAssignment(id):this.api.rejectDeliveryAssignment(id)).subscribe({next:()=>{this.message=accept?'Offer accepted.':'Offer declined.';this.loadOffers();},error:e=>this.message=e?.error?.message||'Could not update offer.'});}
}