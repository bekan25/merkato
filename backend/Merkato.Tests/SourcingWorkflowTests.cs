using System.Security.Claims;
using Merkato.Api.Controllers;
using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Merkato.Tests;
public sealed class SourcingWorkflowTests
{
 private sealed class AllowRisk : IRiskAssessmentService
 {
  public Task<RiskDecision> EvaluateOrderRiskAsync(long userId, decimal orderAmountEtb, CancellationToken ct = default)
      => Task.FromResult(RiskDecision.ALLOW);
 }
 private static SourcingController Controller(MerkatoDbContext db,long user,string role){var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,user.ToString()),new Claim(ClaimTypes.Role,role)},"test");return new SourcingController(db,new AllowRisk()){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(identity)}}};}
 [Fact]
 public async Task BuyerCanPublishAndMultipleMerchantsCanOffer()
 {
  var options=new DbContextOptionsBuilder<MerkatoDbContext>().UseInMemoryDatabase(nameof(BuyerCanPublishAndMultipleMerchantsCanOffer)).Options;
  await using var db=new MerkatoDbContext(options);
  db.Users.AddRange(new User{Id=10,PhoneNumber="merchant-10",FullName="Seller 10",Role=UserRole.MERCHANT},new User{Id=11,PhoneNumber="merchant-11",FullName="Seller 11",Role=UserRole.MERCHANT});
  await db.SaveChangesAsync();
  var buyer=Controller(db,1,"BUYER");
  var created=Assert.IsType<OkObjectResult>(await buyer.Create(new CreateSourcingRequest{Title="Office laptop",Description="Core i5 laptop, 16GB RAM",Category="Electronics",City="Addis Ababa",Quantity=5,ValidDays=7},CancellationToken.None));
  var id=(long)created.Value!.GetType().GetProperty("Id")!.GetValue(created.Value)!;
  var sellerOne=Controller(db,10,"MERCHANT");
  var sellerTwo=Controller(db,11,"MERCHANT");
  Assert.IsType<OkObjectResult>(await sellerOne.Offer(id,new SubmitSourcingOffer{UnitPriceEtb=45000,AvailableQuantity=8,LeadTimeDays=2},CancellationToken.None));
  Assert.IsType<OkObjectResult>(await sellerTwo.Offer(id,new SubmitSourcingOffer{UnitPriceEtb=43000,AvailableQuantity=6,LeadTimeDays=4},CancellationToken.None));
  var offers=Assert.IsType<OkObjectResult>(await buyer.Offers(id,CancellationToken.None));
  var rows=Assert.IsAssignableFrom<System.Collections.IEnumerable>(offers.Value);
  Assert.NotNull(rows);
  Assert.Equal(2,await db.SourcingOffers.CountAsync(x=>x.SourcingRequestId==id));
 }
 [Fact]
 public async Task BuyerCannotReadAnotherBuyersRequestOffers()
 {
  var options=new DbContextOptionsBuilder<MerkatoDbContext>().UseInMemoryDatabase(nameof(BuyerCannotReadAnotherBuyersRequestOffers)).Options;
  await using var db=new MerkatoDbContext(options);
  db.SourcingRequests.Add(new SourcingRequest{Id=1,BuyerId=99,RequestNumber="SRC-TEST",Title="Phone",Description="Phone",Category="Electronics",City="Addis",Quantity=1,ExpiresAt=DateTime.UtcNow.AddDays(2)});
  await db.SaveChangesAsync();
  var otherBuyer=Controller(db,100,"BUYER");
  Assert.IsType<NotFoundResult>(await otherBuyer.Offers(1,CancellationToken.None));
 }
}