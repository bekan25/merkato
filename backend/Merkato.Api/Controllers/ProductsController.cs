using System.Security.Claims;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly MerkatoDbContext _db;
    private readonly IWebHostEnvironment _environment;

    private const long MaxImageSize = 5 * 1024 * 1024;

    private static readonly string[] AllowedExtensions =
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private static readonly string[] AllowedContentTypes =
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public ProductsController(
        MerkatoDbContext db,
        IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    // ==========================================
    // GET: api/products
    // ==========================================
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetProducts(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null)
    {
        var query = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(p =>
                p.Name.Contains(search) ||
                p.Category.Contains(search) ||
                p.Location.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(category) &&
            !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.Category == category);
        }

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProductResponse(
                p.Id,
                p.SellerId,
                p.Name,
                p.PriceEtb,
                p.Location,
                p.Category,
                p.Emoji,
                p.ImageUrl,
                p.IsActive,
                p.CreatedAt))
            .ToListAsync();

        return Ok(products);
    }

    // ==========================================
    // GET: api/products/{id}
    // ==========================================
    [AllowAnonymous]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ProductResponse>> GetProduct(long id)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id && p.IsActive)
            .Select(p => new ProductResponse(
                p.Id,
                p.SellerId,
                p.Name,
                p.PriceEtb,
                p.Location,
                p.Category,
                p.Emoji,
                p.ImageUrl,
                p.IsActive,
                p.CreatedAt))
            .FirstOrDefaultAsync();

        if (product == null)
            return NotFound("Product not found.");

        return Ok(product);
    }

    // ==========================================
    // POST: api/products
    // ==========================================
    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> CreateProduct(
        [FromBody] CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Product name is required.");

        if (request.PriceEtb <= 0)
            return BadRequest("Price must be greater than zero.");

        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(claim, out var currentUserId))
            return Unauthorized();

        var sellerId = currentUserId;

        if (User.IsInRole("ADMIN") && request.SellerId > 0)
            sellerId = request.SellerId;

        var sellerExists = await _db.Users
            .AnyAsync(u => u.Id == sellerId && u.Role == Merkato.Domain.Enums.UserRole.MERCHANT);

        if (!sellerExists)
            return BadRequest("Seller does not exist.");

        var product = new Product
        {
            SellerId = sellerId,
            Name = request.Name.Trim(),
            PriceEtb = request.PriceEtb,
            Location = string.IsNullOrWhiteSpace(request.Location)
                ? "Addis Ababa"
                : request.Location.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category)
                ? "Other"
                : request.Category.Trim(),
            Emoji = string.IsNullOrWhiteSpace(request.Emoji)
                ? "??"
                : request.Emoji.Trim(),
            ImageUrl = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return Ok(new ProductResponse(
            product.Id,
            product.SellerId,
            product.Name,
            product.PriceEtb,
            product.Location,
            product.Category,
            product.Emoji,
            product.ImageUrl,
            product.IsActive,
            product.CreatedAt));
    }

    // ==========================================
    // PUT: api/products/{id}
    // ==========================================
    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ProductResponse>> UpdateProduct(
        long id,
        [FromBody] UpdateProductRequest request)
    {
        var product = await _db.Products.FindAsync(id);

        if (product == null)
            return NotFound("Product not found.");

        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(claim, out var userId))
            return Unauthorized();

        if (!User.IsInRole("ADMIN") && product.SellerId != userId)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Product name is required.");

        if (request.PriceEtb <= 0)
            return BadRequest("Price must be greater than zero.");

        product.Name = request.Name.Trim();
        product.PriceEtb = request.PriceEtb;
        product.Location = string.IsNullOrWhiteSpace(request.Location)
            ? "Addis Ababa"
            : request.Location.Trim();
        product.Category = string.IsNullOrWhiteSpace(request.Category)
            ? "Other"
            : request.Category.Trim();
        product.Emoji = string.IsNullOrWhiteSpace(request.Emoji)
            ? "??"
            : request.Emoji.Trim();
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ProductResponse(
            product.Id,
            product.SellerId,
            product.Name,
            product.PriceEtb,
            product.Location,
            product.Category,
            product.Emoji,
            product.ImageUrl,
            product.IsActive,
            product.CreatedAt));
    }

    // ==========================================
    // POST: api/products/{id}/image
    // ==========================================
    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpPost("{id:long}/image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ProductResponse>> UploadImage(
        long id,
        IFormFile image)
    {
        var product = await _db.Products.FindAsync(id);

        if (product == null)
            return NotFound("Product not found.");

        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(claim, out var userId))
            return Unauthorized();

        // Only owner or ADMIN can upload/change image
        if (!User.IsInRole("ADMIN") && product.SellerId != userId)
            return Forbid();

        if (image == null || image.Length == 0)
            return BadRequest("Image is required.");

        if (image.Length > MaxImageSize)
            return BadRequest("Image size must not exceed 5 MB.");

        if (!AllowedContentTypes.Contains(
                image.ContentType,
                StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(
                "Only JPEG, PNG, and WebP images are allowed.");
        }

        var extension = Path.GetExtension(image.FileName)
            .ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(
                "Only .jpg, .jpeg, .png, and .webp files are allowed.");
        }

        var webRoot = _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot");
        }

        var uploadDirectory = Path.Combine(
            webRoot,
            "images",
            "products");

        Directory.CreateDirectory(uploadDirectory);

        // Delete previous image if it belongs to our local image folder
        if (!string.IsNullOrWhiteSpace(product.ImageUrl))
        {
            var oldFileName = Path.GetFileName(product.ImageUrl);

            if (!string.IsNullOrWhiteSpace(oldFileName))
            {
                var oldFilePath = Path.Combine(
                    uploadDirectory,
                    oldFileName);

                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
            }
        }

        // Generate unique filename
        var fileName =
            $"{Guid.NewGuid():N}{extension}";

        var filePath = Path.Combine(
            uploadDirectory,
            fileName);

        await using (var stream = new FileStream(
            filePath,
            FileMode.CreateNew))
        {
            await image.CopyToAsync(stream);
        }

        // Relative URL stored in database
        product.ImageUrl =
            $"/images/products/{fileName}";

        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ProductResponse(
            product.Id,
            product.SellerId,
            product.Name,
            product.PriceEtb,
            product.Location,
            product.Category,
            product.Emoji,
            product.ImageUrl,
            product.IsActive,
            product.CreatedAt));
    }

    // ==========================================
    // DELETE: api/products/{id}/image
    // ==========================================
    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpDelete("{id:long}/image")]
    public async Task<IActionResult> DeleteImage(long id)
    {
        var product = await _db.Products.FindAsync(id);

        if (product == null)
            return NotFound("Product not found.");

        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(claim, out var userId))
            return Unauthorized();

        if (!User.IsInRole("ADMIN") && product.SellerId != userId)
            return Forbid();

        if (!string.IsNullOrWhiteSpace(product.ImageUrl))
        {
            var webRoot = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot");
            }

            var uploadDirectory = Path.Combine(
                webRoot,
                "images",
                "products");

            var fileName = Path.GetFileName(product.ImageUrl);

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var filePath = Path.Combine(
                    uploadDirectory,
                    fileName);

                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            product.ImageUrl = null;
            product.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    // ==========================================
    // DELETE: api/products/{id}
    // ==========================================
    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteProduct(long id)
    {
        var product = await _db.Products.FindAsync(id);

        if (product == null)
            return NotFound("Product not found.");

        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(claim, out var userId))
            return Unauthorized();

        if (!User.IsInRole("ADMIN") && product.SellerId != userId)
            return Forbid();

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return NoContent();
    }
}
