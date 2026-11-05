using System.Security.Claims;
using System.Data;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Merkato.Api.Controllers;
[ApiController]
[Route("api/v1/sourcing")]
public sealed class SourcingController(MerkatoDbContext db) : ControllerBase
{
 [Authorize(Roles="BUYER")]
 [HttpPost("requests")]
 public async Task<IActionResult> Create(CreateSourcingRequest input,CancellationToken ct)
 {
  var buyer=UserId(); if(buyer is null)return Unauthorized();
  if(string.IsNullOrWhiteSpace(input.Title)||input.Title.Trim().Length is <3 or >160||string.IsNullOrWhiteSpace(input.Description)||input.Description.Length>2000||string.IsNullOrWhiteSpace(input.Category)||input.Category.Length>100||string.IsNullOrWhiteSpace(input.City)||input.City.Length>100||input.Quantity is <1 or >100000||input.TargetUnitPriceEtb is <=0 or >1000000000m||input.ValidDays is <1 or >30)return BadRequest("Invalid sourcing request fields.");
  var now=DateTime.UtcNow; var row=new SourcingRequest{RequestNumber=$"SRC-{Guid.NewGuid():N}"[..15].ToUpperInvariant(),BuyerId=buyer.Value,Title=input.Title.Trim(),Description=input.Description.Trim(),Category=input.Category.Trim(),City=input.City.Trim(),Quantity=input.Quantity,TargetUnitPriceEtb=input.TargetUnitPriceEtb,ExpiresAt=now.AddDays(input.ValidDays),CreatedAt=now,UpdatedAt=now};
  db.SourcingRequests.Add(row); await db.SaveChangesAsync(ct); return Ok(new{row.Id,row.RequestNumber,row.Status,row.ExpiresAt});
 }
 [Authorize(Roles="BUYER")]
 [HttpGet("requests/mine")]
 public async Task<IActionResult> Mine(CancellationToken ct){var id=UserId();if(id is null)return Unauthorized();return Ok(await db.SourcingRequests.AsNoTracking().Where(x=>x.BuyerId==id).OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Id,x.RequestNumber,x.Title,x.Description,x.Category,x.City,x.Quantity,x.TargetUnitPriceEtb,Status=x.ExpiresAt<=DateTime.UtcNow&&x.Status=="OPEN"?"EXPIRED":x.Status,x.ExpiresAt,OfferCount=db.SourcingOffers.Count(o=>o.SourcingRequestId==x.Id)}).ToListAsync(ct));}
 [Authorize(Roles="MERCHANT,ADMIN")]
 [HttpGet("requests/open")]
 public async Task<IActionResult> Open([FromQuery]string? category,[FromQuery]string? city,CancellationToken ct){var q=db.SourcingRequests.AsNoTracking().Where(x=>x.Status=="OPEN"&&x.ExpiresAt>DateTime.UtcNow);if(!string.IsNullOrWhiteSpace(category))q=q.Where(x=>x.Category==category.Trim());if(!string.IsNullOrWhiteSpace(city))q=q.Where(x=>x.City==city.Trim());return Ok(await q.OrderByDescending(x=>x.CreatedAt).Take(100).Select(x=>new{x.Id,x.RequestNumber,x.Title,x.Description,x.Category,x.City,x.Quantity,x.TargetUnitPriceEtb,x.ExpiresAt}).ToListAsync(ct));}
 [Authorize(Roles="MERCHANT,ADMIN")]
 [HttpPost("requests/{id:long}/offers")]
 public async Task<IActionResult> Offer(long id,SubmitSourcingOffer input,CancellationToken ct){var seller=UserId();if(seller is null)return Unauthorized();if(input.UnitPriceEtb<=0||input.AvailableQuantity<1||input.AvailableQuantity>100000||input.LeadTimeDays is <0 or >365||input.Note?.Length>1000)return BadRequest("Invalid offer.");var request=await db.SourcingRequests.FirstOrDefaultAsync(x=>x.Id==id,ct);if(request is null)return NotFound();if(request.BuyerId==seller)return Forbid();if(request.Status!="OPEN"||request.ExpiresAt<=DateTime.UtcNow)return Conflict("Request is closed.");if(!User.IsInRole("ADMIN")&&!await db.Users.AnyAsync(x=>x.Id==seller&&x.Role==Merkato.Domain.Enums.UserRole.MERCHANT,ct))return Forbid();var row=await db.SourcingOffers.FirstOrDefaultAsync(x=>x.SourcingRequestId==id&&x.SellerId==seller,ct);if(row?.Status=="ACCEPTED")return Conflict("Accepted offer cannot be edited.");var isNew=row is null;if(row is null){row=new SourcingOffer{SourcingRequestId=id,SellerId=seller.Value};db.SourcingOffers.Add(row);}row.UnitPriceEtb=input.UnitPriceEtb;row.AvailableQuantity=input.AvailableQuantity;row.Note=input.Note?.Trim();row.LeadTimeDays=input.LeadTimeDays;row.Status="SUBMITTED";row.UpdatedAt=DateTime.UtcNow;if(isNew)db.UserNotifications.Add(new UserNotification{UserId=request.BuyerId,Type="SOURCING_NEW_OFFER",Title="A merchant sent an offer",Message=$"A merchant submitted an offer for {request.RequestNumber}. Compare offers in One Request.",ActionUrl="/sourcing"});await db.SaveChangesAsync(ct);return Ok(new{row.Id,row.Status,row.UnitPriceEtb});}
 [Authorize(Roles="BUYER")]
 [HttpGet("requests/{id:long}/offers")]
 public async Task<IActionResult> Offers(long id,CancellationToken ct){var buyer=UserId();if(buyer is null)return Unauthorized();if(!await db.SourcingRequests.AnyAsync(x=>x.Id==id&&x.BuyerId==buyer,ct))return NotFound();return Ok(await db.SourcingOffers.AsNoTracking().Where(x=>x.SourcingRequestId==id).OrderBy(x=>x.UnitPriceEtb).Select(x=>new{x.Id,x.SellerId,x.UnitPriceEtb,x.AvailableQuantity,x.Note,x.LeadTimeDays,x.Status,x.CreatedAt}).ToListAsync(ct));}
 [Authorize(Roles="BUYER")]
 [HttpPost("requests/{id:long}/offers/{offerId:long}/accept")]
 public async Task<IActionResult> Accept(long id,long offerId,CancellationToken ct){var buyer=UserId();if(buyer is null)return Unauthorized();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);var req=await db.SourcingRequests.FirstOrDefaultAsync(x=>x.Id==id&&x.BuyerId==buyer,ct);if(req is null)return NotFound();if(req.Status!="OPEN"||req.ExpiresAt<=DateTime.UtcNow)return Conflict("Request is closed.");var offer=await db.SourcingOffers.FirstOrDefaultAsync(x=>x.Id==offerId&&x.SourcingRequestId==id&&x.Status=="SUBMITTED",ct);if(offer is null)return NotFound();if(offer.AvailableQuantity<req.Quantity)return Conflict("Insufficient quantity.");req.Status="OFFER_ACCEPTED";req.UpdatedAt=DateTime.UtcNow;offer.Status="ACCEPTED";offer.UpdatedAt=DateTime.UtcNow;var others=await db.SourcingOffers.Where(x=>x.SourcingRequestId==id&&x.Id!=offerId&&x.Status=="SUBMITTED").ToListAsync(ct);foreach(var other in others){other.Status="NOT_SELECTED";other.UpdatedAt=DateTime.UtcNow;}db.UserNotifications.Add(new UserNotification{UserId=offer.SellerId,Type="SOURCING_OFFER_ACCEPTED",Title="Your MERKATO offer was selected",Message=$"Your offer for {req.RequestNumber} was selected. Payment has not been taken; use standard checkout.",ActionUrl="/commerce"});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Ok(new{req.Id,req.Status,acceptedOfferId=offer.Id,offer.UnitPriceEtb,paymentStatus="NOT_PAID"});}
 [Authorize(Roles="BUYER")]
 [HttpPost("requests/{id:long}/close")]
 public async Task<IActionResult> Close(long id,CancellationToken ct){var buyer=UserId();if(buyer is null)return Unauthorized();var row=await db.SourcingRequests.FirstOrDefaultAsync(x=>x.Id==id&&x.BuyerId==buyer,ct);if(row is null)return NotFound();if(row.Status!="OPEN")return Conflict();row.Status="CANCELLED";row.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);return Ok(new{row.Id,row.Status});}
 long? UserId()=>long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:null;
}
public sealed class CreateSourcingRequest{public string Title{get;set;}="";public string Description{get;set;}="";public string Category{get;set;}="";public string City{get;set;}="Addis Ababa";public int Quantity{get;set;}=1;public decimal? TargetUnitPriceEtb{get;set;}public int ValidDays{get;set;}=7;}
public sealed class SubmitSourcingOffer{public decimal UnitPriceEtb{get;set;}public int AvailableQuantity{get;set;}public string? Note{get;set;}public int? LeadTimeDays{get;set;}}