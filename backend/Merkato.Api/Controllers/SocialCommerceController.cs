using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Merkato.Api.Controllers;
[ApiController]
[Route("api/v1/social")]
public sealed class SocialCommerceController(MerkatoDbContext db):ControllerBase
{
 long? UserId()=>long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:null;
 [Authorize(Roles="BUYER,MERCHANT")]
 [HttpPost("posts")]
 public async Task<IActionResult> Create(CreateSocialPost input,CancellationToken ct)
 {
  var creator=UserId();if(creator is null)return Unauthorized();
  if(input.ProductId<1||string.IsNullOrWhiteSpace(input.Caption)||input.Caption.Trim().Length>1200||!Uri.TryCreate(input.MediaUrl,UriKind.Absolute,out var uri)||uri.Scheme!=Uri.UriSchemeHttps||input.MediaUrl.Length>2048||input.MediaType is not ("VIDEO" or "IMAGE"))return BadRequest("Provide a product, caption, HTTPS media URL and VIDEO or IMAGE type.");
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
  if(input.Decision is not ("APPROVE" or "REJECT")||input.Note?.Length>1000)return BadRequest("Decision must be APPROVE or REJECT; note max 1000 characters.");
  var post=await db.SocialCommercePosts.FirstOrDefaultAsync(x=>x.Id==id,ct);if(post is null)return NotFound();if(post.Status!="PENDING")return Conflict("Only pending posts can be moderated.");
  post.Status=input.Decision=="APPROVE"?"APPROVED":"REJECTED";post.ModerationNote=input.Note?.Trim();post.UpdatedAt=DateTime.UtcNow;post.PublishedAt=post.Status=="APPROVED"?DateTime.UtcNow:null;await db.SaveChangesAsync(ct);return Ok(new{post.Id,post.Status,post.PublishedAt});
 }
 [AllowAnonymous]
 [HttpPost("posts/{id:long}/click")]
 public async Task<IActionResult> Click(long id,SocialClickInput input,CancellationToken ct)
 {
  if(string.IsNullOrWhiteSpace(input.VisitorKey)||input.VisitorKey.Length>200)return BadRequest("Visitor key required.");
  var post=await db.SocialCommercePosts.FirstOrDefaultAsync(x=>x.Id==id&&x.Status=="APPROVED",ct);if(post is null)return NotFound();
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.VisitorKey)));
  if(!await db.SocialCommerceClicks.AnyAsync(x=>x.PostId==id&&x.VisitorHash==hash,ct)){db.SocialCommerceClicks.Add(new SocialCommerceClick{PostId=id,ViewerId=UserId(),VisitorHash=hash});post.ClickCount++;await db.SaveChangesAsync(ct);}
  return Ok(new{tracked=true,productId=post.ProductId});
 }
}
public sealed record CreateSocialPost(long ProductId,string Caption,string MediaUrl,string MediaType="VIDEO");
public sealed record ModerateSocialPost(string Decision,string? Note);
public sealed record SocialClickInput(string VisitorKey);