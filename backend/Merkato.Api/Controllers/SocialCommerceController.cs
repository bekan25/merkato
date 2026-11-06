using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Merkato.Api.Controllers;
[ApiController]
[Route("api/v1/social")]
public sealed class SocialCommerceController(
 MerkatoDbContext db,
 IConfiguration configuration,
 IHttpClientFactory httpClientFactory,
 ILogger<SocialCommerceController> logger):ControllerBase
{
 const long MaxImageBytes=10L*1024*1024;
 const long MaxVideoBytes=100L*1024*1024;

 [Authorize(Roles="BUYER,MERCHANT")]
 [HttpPost("posts/upload")]
 [Consumes("multipart/form-data")]
 [RequestSizeLimit(110L*1024*1024)]
 [RequestFormLimits(MultipartBodyLengthLimit=110L*1024*1024)]
 public async Task<IActionResult> UploadAndCreate([FromForm] UploadSocialPost input,CancellationToken ct)
 {
  var creator=UserId();if(creator is null)return Unauthorized();
  if(input.ProductId<1||string.IsNullOrWhiteSpace(input.Caption)||input.Caption.Trim().Length>1200)
   return BadRequest("A valid product and caption (max 1200 characters) are required.");
  if(input.Media is null||input.Media.Length==0)return BadRequest("Choose an image or video.");
  var isImage=string.Equals(input.MediaType,"IMAGE",StringComparison.OrdinalIgnoreCase);
  var isVideo=string.Equals(input.MediaType,"VIDEO",StringComparison.OrdinalIgnoreCase);
  if(!isImage&&!isVideo)return BadRequest("Media type must be IMAGE or VIDEO.");
  var maxBytes=isImage?MaxImageBytes:MaxVideoBytes;
  if(input.Media.Length>maxBytes)return BadRequest(isImage?"Images must not exceed 10 MB.":"Videos must not exceed 100 MB.");
  var extension=Path.GetExtension(input.Media.FileName).ToLowerInvariant();
  var allowedExtensions=isImage?new[]{".jpg",".jpeg",".png",".webp"}:new[]{".mp4",".webm",".mov"};
  var allowedTypes=isImage?new[]{"image/jpeg","image/png","image/webp"}:new[]{"video/mp4","video/webm","video/quicktime"};
  if(!allowedExtensions.Contains(extension)||!allowedTypes.Contains(input.Media.ContentType,StringComparer.OrdinalIgnoreCase))
   return BadRequest("File extension and content type do not match an allowed media format.");
  var product=await db.Products.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==input.ProductId&&x.IsActive,ct);
  if(product is null)return NotFound("Active product not found.");
  var cloudName=configuration["Cloudinary:CloudName"];
  var apiKey=configuration["Cloudinary:ApiKey"];
  var apiSecret=configuration["Cloudinary:ApiSecret"];
  if(string.IsNullOrWhiteSpace(cloudName)||string.IsNullOrWhiteSpace(apiKey)||string.IsNullOrWhiteSpace(apiSecret))
   return StatusCode(StatusCodes.Status503ServiceUnavailable,"Media storage is not configured. Set Cloudinary__CloudName, Cloudinary__ApiKey and Cloudinary__ApiSecret.");
  await using var mediaStream=input.Media.OpenReadStream();
  var header=new byte[12];
  var read=await mediaStream.ReadAsync(header.AsMemory(0,header.Length),ct);
  if(!IsValidMediaSignature(header,read,isImage,extension))
   return BadRequest("The uploaded file content does not match its declared media type.");
  mediaStream.Position=0;
  var resourceType=isImage?"image":"video";
  var publicId=$"merkato/social/{creator.Value}/{Guid.NewGuid():N}";
  var timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
  var signatureBase=$"public_id={publicId}&timestamp={timestamp}{apiSecret}";
  var signature=Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(signatureBase))).ToLowerInvariant();
  using var form=new MultipartFormDataContent();
  using var fileContent=new StreamContent(mediaStream);
  fileContent.Headers.ContentType=new MediaTypeHeaderValue(input.Media.ContentType);
  form.Add(fileContent,"file",Path.GetFileName(input.Media.FileName));
  form.Add(new StringContent(apiKey),"api_key");
  form.Add(new StringContent(timestamp),"timestamp");
  form.Add(new StringContent(publicId),"public_id");
  form.Add(new StringContent(signature),"signature");
  using var client=httpClientFactory.CreateClient();
  using var response=await client.PostAsync($"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(cloudName)}/{resourceType}/upload",form,ct);
  if(!response.IsSuccessStatusCode)
  {
   logger.LogWarning("Cloudinary social media upload failed with status {StatusCode}.",(int)response.StatusCode);
   return StatusCode(StatusCodes.Status502BadGateway,"Media storage provider could not accept the upload.");
  }
  await using var responseStream=await response.Content.ReadAsStreamAsync(ct);
  using var payload=await JsonDocument.ParseAsync(responseStream,cancellationToken:ct);
  if(!payload.RootElement.TryGetProperty("secure_url",out var secureUrl)||string.IsNullOrWhiteSpace(secureUrl.GetString()))
  {
   logger.LogWarning("Cloudinary social upload response did not contain secure_url.");
   return StatusCode(StatusCodes.Status502BadGateway,"Media storage returned an invalid response.");
  }
  var mediaUrl=secureUrl.GetString()!;
  if(!Uri.TryCreate(mediaUrl,UriKind.Absolute,out var mediaUri)||mediaUri.Scheme!=Uri.UriSchemeHttps||!mediaUri.Host.EndsWith("cloudinary.com",StringComparison.OrdinalIgnoreCase))
   return StatusCode(StatusCodes.Status502BadGateway,"Media storage returned an unsafe media URL.");
  var post=new SocialCommercePost{CreatorId=creator.Value,ProductId=product.Id,Caption=input.Caption.Trim(),MediaUrl=mediaUrl,MediaType=isImage?"IMAGE":"VIDEO",Status="PENDING"};
  db.SocialCommercePosts.Add(post);
  await db.SaveChangesAsync(ct);
  return CreatedAtAction(nameof(MyPosts),new{},new{post.Id,post.Status,post.MediaUrl,post.MediaType,message="Media uploaded to Cloudinary and submitted for moderation."});
 }

 static bool IsValidMediaSignature(byte[] b,int length,bool image,string extension)
 {
  if(image)
  {
   if(extension is ".jpg" or ".jpeg")return length>=3&&b[0]==0xFF&&b[1]==0xD8&&b[2]==0xFF;
   if(extension==".png")return length>=8&&b.Take(8).SequenceEqual(new byte[]{0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A});
   if(extension==".webp")return length>=12&&Encoding.ASCII.GetString(b,0,4)=="RIFF"&&Encoding.ASCII.GetString(b,8,4)=="WEBP";
   return false;
  }
  if(extension==".webm")return length>=4&&b[0]==0x1A&&b[1]==0x45&&b[2]==0xDF&&b[3]==0xA3;
  if(extension is ".mp4" or ".mov")return length>=8&&Encoding.ASCII.GetString(b,4,4)=="ftyp";
  return false;
 }
 long? UserId()=>long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:null;
 [Authorize(Roles="BUYER,MERCHANT")]
 [HttpPost("posts")]
 public async Task<IActionResult> Create(CreateSocialPost input,CancellationToken ct)
 {
  var creator=UserId();if(creator is null)return Unauthorized();
  if(input.ProductId<1||string.IsNullOrWhiteSpace(input.Caption)||input.Caption.Trim().Length>1200||!Uri.TryCreate(input.MediaUrl,UriKind.Absolute,out var uri)||uri.Scheme!=Uri.UriSchemeHttps||!uri.Host.Equals("res.cloudinary.com",StringComparison.OrdinalIgnoreCase)||input.MediaUrl.Length>2048||input.MediaType is not ("VIDEO" or "IMAGE"))return BadRequest("Provide a product, caption, HTTPS media URL and VIDEO or IMAGE type.");
  var product=await db.Products.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==input.ProductId&&x.IsActive,ct);if(product is null)return NotFound("Active product not found.");
  var post=new SocialCommercePost{CreatorId=creator.Value,ProductId=product.Id,Caption=input.Caption.Trim(),MediaUrl=input.MediaUrl.Trim(),MediaType=input.MediaType,Status="PENDING"};
  db.SocialCommercePosts.Add(post);await db.SaveChangesAsync(ct);
  return CreatedAtAction(nameof(MyPosts),new{},new{post.Id,post.Status,message="Submitted for moderation. Not visible in public feed until approved."});
 }
 [AllowAnonymous]
 [HttpGet("feed")]
 public async Task<IActionResult> Feed([FromQuery]int page=1,[FromQuery]int pageSize=20,CancellationToken ct=default)
 {
  page=Math.Clamp(page,1,10000);pageSize=Math.Clamp(pageSize,1,40);
  var rows=await (from p in db.SocialCommercePosts.AsNoTracking()
   join product in db.Products.AsNoTracking() on p.ProductId equals product.Id
   join creator in db.Users.AsNoTracking() on p.CreatorId equals creator.Id
   where p.Status=="APPROVED"&&product.IsActive
   orderby p.PublishedAt descending
   select new{p.Id,p.Caption,p.MediaUrl,p.MediaType,p.ViewCount,p.ClickCount,p.PublishedAt,productId=product.Id,productName=product.Name,product.PriceEtb,product.Location,creatorName=creator.FullName})
   .Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct);
  return Ok(new{page,pageSize,items=rows});
 }
 [Authorize(Roles="BUYER,MERCHANT")]
 [HttpGet("commissions/mine")]
 public async Task<IActionResult> MyCommissions(CancellationToken ct)
 {
  var id=UserId();if(id is null)return Unauthorized();
  var query=db.CreatorCommissions.AsNoTracking().Where(x=>x.CreatorId==id.Value);
  var total=await query.SumAsync(x=>(decimal?)x.CommissionEtb,ct)??0m;
  var items=await query.OrderByDescending(x=>x.CreatedAt)
   .Select(x=>new{x.Id,x.OrderId,x.PostId,x.EligibleAmountEtb,x.RatePercent,x.CommissionEtb,x.Status,x.CreatedAt,x.ReleasedAt})
   .ToListAsync(ct);
  return Ok(new{totalEtb=total,items});
 }
 [Authorize(Roles="ADMIN")]
 [HttpGet("commissions/admin")]
 public async Task<IActionResult> AdminCommissions([FromQuery]int page=1,[FromQuery]int pageSize=50,CancellationToken ct=default)
 {
  page=Math.Clamp(page,1,10000);pageSize=Math.Clamp(pageSize,1,100);
  var query=db.CreatorCommissions.AsNoTracking();
  var totals=await query.GroupBy(_=>1).Select(g=>new{
   commissionCount=g.Count(),
   heldEtb=g.Where(x=>x.Status=="HELD").Sum(x=>(decimal?)x.CommissionEtb)??0m,
   availableEtb=g.Where(x=>x.Status=="AVAILABLE").Sum(x=>(decimal?)x.CommissionEtb)??0m,
   reversedEtb=g.Where(x=>x.Status=="REVERSED").Sum(x=>(decimal?)x.CommissionEtb)??0m
  }).FirstOrDefaultAsync(ct);
  var items=await (from commission in query
   join creator in db.Users.AsNoTracking() on commission.CreatorId equals creator.Id
   join order in db.Orders.AsNoTracking() on commission.OrderId equals order.Id
   orderby commission.CreatedAt descending
   select new{
    commission.Id,commission.CreatorId,creatorName=creator.FullName,
    commission.OrderId,order.OrderNumber,commission.PostId,
    commission.EligibleAmountEtb,commission.RatePercent,commission.CommissionEtb,
    commission.Status,commission.CreatedAt,commission.ReleasedAt
   }).Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct);
  return Ok(new{page,pageSize,totals=totals??new{commissionCount=0,heldEtb=0m,availableEtb=0m,reversedEtb=0m},items});
 }
 [Authorize(Roles="ADMIN")]
 [HttpPost("commissions/{commissionId:long}/payout")]
 public async Task<IActionResult> MarkCreatorCommissionPaid(long commissionId,CreatorCommissionPayout input,CancellationToken ct)
 {
  var reference=input.PayoutReference?.Trim();
  if(string.IsNullOrWhiteSpace(reference)||reference.Length>120)
   return BadRequest("A payout reference (1-120 characters) is required.");
  var paidAt=DateTime.UtcNow;
  // Atomic compare-and-set: two administrators cannot settle the same creator commission.
  var updated=await db.CreatorCommissions
   .Where(x=>x.Id==commissionId&&x.Status=="AVAILABLE")
   .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"PAID").SetProperty(x=>x.PayoutReference,reference).SetProperty(x=>x.PaidAt,paidAt),ct);
  if(updated==0)
  {
   var current=await db.CreatorCommissions.AsNoTracking().Where(x=>x.Id==commissionId)
    .Select(x=>new{x.Id,x.Status}).FirstOrDefaultAsync(ct);
   if(current is null)return NotFound();
   return Conflict(new{error="Only AVAILABLE creator commissions can be marked as paid.",currentStatus=current.Status});
  }
  return Ok(new{commissionId,status="PAID",payoutReference=reference,paidAt,recordedAs="ADMIN_RECONCILIATION_ONLY",note="This records an administrator-confirmed external payout; it does not initiate a bank or mobile-money transfer."});
 }

 [Authorize]
 [HttpGet("posts/mine")]
 public async Task<IActionResult> MyPosts(CancellationToken ct)
 {
  var id=UserId();if(id is null)return Unauthorized();
  return Ok(await db.SocialCommercePosts.AsNoTracking().Where(x=>x.CreatorId==id).OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Id,x.ProductId,x.Caption,x.MediaUrl,x.MediaType,x.Status,x.ModerationNote,x.ViewCount,x.ClickCount,x.CreatedAt,x.PublishedAt}).ToListAsync(ct));
 }
 [Authorize(Roles="ADMIN")]
 [HttpGet("moderation")]
 public async Task<IActionResult> Moderation(CancellationToken ct)=>Ok(await db.SocialCommercePosts.AsNoTracking().Where(x=>x.Status=="PENDING").OrderBy(x=>x.CreatedAt).Take(100).Select(x=>new{x.Id,x.CreatorId,x.ProductId,x.Caption,x.MediaUrl,x.MediaType,x.CreatedAt}).ToListAsync(ct));
 [Authorize(Roles="ADMIN")]
 [HttpPost("posts/{id:long}/moderate")]
 public async Task<IActionResult> Moderate(long id,ModerateSocialPost input,CancellationToken ct)
 {
  if(!User.IsInRole("ADMIN"))return Forbid();
  if(input.Decision is not ("APPROVE" or "REJECT")||input.Note?.Length>1000)return BadRequest("Decision must be APPROVE or REJECT; note max 1000 characters.");
  var post=await db.SocialCommercePosts.FirstOrDefaultAsync(x=>x.Id==id,ct);if(post is null)return NotFound();if(post.Status!="PENDING")return Conflict("Only pending posts can be moderated.");
  post.Status=input.Decision=="APPROVE"?"APPROVED":"REJECTED";post.ModerationNote=input.Note?.Trim();post.UpdatedAt=DateTime.UtcNow;post.PublishedAt=post.Status=="APPROVED"?DateTime.UtcNow:null;await db.SaveChangesAsync(ct);return Ok(new{post.Id,post.Status,post.PublishedAt});
 }
 [AllowAnonymous]
 [HttpPost("posts/{id:long}/click")]
 public async Task<IActionResult> Click(long id,SocialClickInput input,CancellationToken ct)
 {
  if(string.IsNullOrWhiteSpace(input.VisitorKey)||input.VisitorKey.Length>200)return BadRequest("Visitor key required.");
  var post=await db.SocialCommercePosts.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id&&x.Status=="APPROVED",ct);if(post is null)return NotFound();
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.VisitorKey)));
  await using var tx=await db.Database.BeginTransactionAsync(ct);
  db.SocialCommerceClicks.Add(new SocialCommerceClick{PostId=id,ViewerId=UserId(),VisitorHash=hash});
  try
  {
   await db.SaveChangesAsync(ct);
  }
  catch(DbUpdateException ex) when(ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
  {
   await tx.RollbackAsync(ct);
   db.Entry(db.ChangeTracker.Entries<SocialCommerceClick>().First(x=>x.Entity.PostId==id&&x.Entity.VisitorHash==hash).Entity).State=EntityState.Detached;
   return Ok(new{tracked=false,productId=post.ProductId,message="This visitor click was already counted."});
  }
  await db.SocialCommercePosts.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ClickCount,x=>x.ClickCount+1),ct);
  await tx.CommitAsync(ct);
  return Ok(new{tracked=true,productId=post.ProductId});
 }
}
public sealed record CreateSocialPost(long ProductId,string Caption,string MediaUrl,string MediaType="VIDEO");
public sealed class UploadSocialPost { public long ProductId {get;set;} public string Caption {get;set;}=string.Empty; public string MediaType {get;set;}="VIDEO"; public IFormFile? Media {get;set;} }
public sealed record ModerateSocialPost(string Decision,string? Note);
public sealed record SocialClickInput(string VisitorKey);
public sealed record CreatorCommissionPayout(string PayoutReference);
