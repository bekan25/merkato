using System.Security.Claims;
using Merkato.Api.Controllers;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Merkato.Tests;
public sealed class SocialCommerceWorkflowTests
{
 private static SocialCommerceController Controller(MerkatoDbContext db,long user,string role){var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,user.ToString()),new Claim(ClaimTypes.Role,role)},"test");return new SocialCommerceController(db){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(identity)}}};}
 [Fact]
 public async Task CreatorPostRequiresModerationBeforePublicFeed()
 {
  var options=new DbContextOptionsBuilder<MerkatoDbContext>().UseInMemoryDatabase(nameof(CreatorPostRequiresModerationBeforePublicFeed)).Options;
  await using var db=new MerkatoDbContext(options);
  db.Users.Add(new User{Id=5,PhoneNumber="creator-5",FullName="Creator",Role=UserRole.BUYER});
  db.Products.Add(new Product{Id=20,SellerId=6,Name="Phone",Category="Electronics",PriceEtb=10000,IsActive=true});
  await db.SaveChangesAsync();
  var creator=Controller(db,5,"BUYER");
  var created=Assert.IsType<CreatedAtActionResult>(await creator.Create(new CreateSocialPost(20,"My product story","https://media.example.test/phone.mp4","VIDEO"),CancellationToken.None));
  Assert.Equal("PENDING",created.Value!.GetType().GetProperty("Status")!.GetValue(created.Value));
  var feed=Assert.IsType<OkObjectResult>(await creator.Feed(1,20,CancellationToken.None));
  var items=feed.Value!.GetType().GetProperty("items")!.GetValue(feed.Value);
  Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(items));
 }
 [Fact]
 public async Task OnlyAdminCanPublishPendingCreatorStory()
 {
  var options=new DbContextOptionsBuilder<MerkatoDbContext>().UseInMemoryDatabase(nameof(OnlyAdminCanPublishPendingCreatorStory)).Options;
  await using var db=new MerkatoDbContext(options);
  db.Users.Add(new User{Id=5,PhoneNumber="creator-5",FullName="Creator",Role=UserRole.BUYER});
  db.Products.Add(new Product{Id=20,SellerId=6,Name="Phone",Category="Electronics",PriceEtb=10000,IsActive=true});
  db.SocialCommercePosts.Add(new SocialCommercePost{Id=3,CreatorId=5,ProductId=20,Caption="Story",MediaUrl="https://media.example.test/a.mp4"});
  await db.SaveChangesAsync();
  var nonAdmin=Controller(db,5,"BUYER");
  Assert.IsType<ForbidResult>(await nonAdmin.Moderate(3,new ModerateSocialPost("APPROVE",null),CancellationToken.None));
  var admin=Controller(db,1,"ADMIN");
  Assert.IsType<OkObjectResult>(await admin.Moderate(3,new ModerateSocialPost("APPROVE","Reviewed"),CancellationToken.None));
  Assert.Equal("APPROVED",(await db.SocialCommercePosts.SingleAsync()).Status);
 }
}