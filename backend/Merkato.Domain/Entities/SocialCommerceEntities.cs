namespace Merkato.Domain.Entities;
public sealed class SocialCommercePost
{
 public long Id {get;set;}
 public long CreatorId {get;set;}
 public long ProductId {get;set;}
 public string Caption {get;set;}=string.Empty;
 public string MediaUrl {get;set;}=string.Empty;
 public string MediaType {get;set;}="VIDEO";
 public string Status {get;set;}="PENDING";
 public string? ModerationNote {get;set;}
 public int ViewCount {get;set;}
 public int ClickCount {get;set;}
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
 public DateTime UpdatedAt {get;set;}=DateTime.UtcNow;
 public DateTime? PublishedAt {get;set;}
}
public sealed class SocialCommerceClick
{
 public long Id {get;set;}
 public long PostId {get;set;}
 public long? ViewerId {get;set;}
 public string VisitorHash {get;set;}=string.Empty;
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}