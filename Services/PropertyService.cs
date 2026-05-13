using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.DTOs;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public interface IPropertyService
{
    Task<PaginatedResponse<PropertyDto>> GetAllAsync(PropertyQueryParams q);
    Task<PropertyDto?> GetByIdAsync(int id);
    Task<List<PropertyDto>> GetFeaturedAsync();
    Task<List<PropertyDto>> GetRelatedAsync(int id);
    Task<PropertyDto> CreateAsync(CreatePropertyRequest req, int? submittedByUserId, bool autoApprove);
    Task<PropertyDto?> UpdateAsync(int id, UpdatePropertyRequest req);
    Task<bool> DeleteAsync(int id);
    Task<PropertyDto?> ApproveOrRejectAsync(int id, ApprovalRequest req);
    Task<PropertyDto?> AssignToVerifyAsync(int id, int userId);
    Task<List<PropertyDto>> GetPendingAsync();
    Task<bool> ToggleFavoriteAsync(int propertyId, int userId);
    Task<List<PropertyDto>> GetFavoritesAsync(int userId);
    Task<List<PropertyDto>> GetMineAsync(int userId);
    Task<PropertyDto?> UpdateAsOwnerAsync(int id, int userId, UpdatePropertyRequest req);
}

public class PropertyService(
    AppDbContext db,
    INotificationService notifications,
    IEmailService email,
    ISmsTemplateService templates) : IPropertyService
{
    private static PropertyDto ToDto(Property p) => new(
        p.Id, p.Title, p.Description, p.TotalPrice, p.PricePerCent,
        p.Address, p.City, p.District, p.State, p.PinCode,
        p.AreaInCents, p.AreaInSqFt, p.Bedrooms, p.Bathrooms,
        p.PropertyType.ToString(), p.Status.ToString(),
        p.Images, p.Features, p.NearbyLandmarks,
        p.LegalStatus, p.RoadAccess, p.IsFeatured, p.IsVerified,
        p.ApprovalStatus.ToString(),
        p.MarketingPlan.ToString(),
        p.Latitude, p.Longitude,
        p.AgentId,
        // Prefer the registered user's name; fall back to anonymous submitter name
        p.SubmittedByUser != null
            ? $"{p.SubmittedByUser.FirstName} {p.SubmittedByUser.LastName}"
            : p.SubmitterName,
        p.SubmittedByUser?.Phone ?? p.SubmitterPhone,
        p.AssignedToVerifyUserId,
        p.AssignedToVerifyUser != null
            ? $"{p.AssignedToVerifyUser.FirstName} {p.AssignedToVerifyUser.LastName}"
            : null,
        p.AssignedToVerifyAt,
        p.CreatedAt, p.UpdatedAt
    );

    public async Task<PaginatedResponse<PropertyDto>> GetAllAsync(PropertyQueryParams q)
    {
        var query = db.Properties
            .Include(p => p.SubmittedByUser)
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.ToLower();
            query = query.Where(p => p.Title.ToLower().Contains(s) ||
                                     p.City.ToLower().Contains(s) ||
                                     p.Address.ToLower().Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(q.City))
            query = query.Where(p => p.City.ToLower() == q.City.ToLower());
        if (!string.IsNullOrWhiteSpace(q.PropertyType) &&
            Enum.TryParse<PropertyType>(q.PropertyType, true, out var pt))
            query = query.Where(p => p.PropertyType == pt);
        if (!string.IsNullOrWhiteSpace(q.Status) &&
            Enum.TryParse<ListingStatus>(q.Status, true, out var ls))
            query = query.Where(p => p.Status == ls);
        if (q.MinPrice.HasValue) query = query.Where(p => p.TotalPrice >= q.MinPrice.Value);
        if (q.MaxPrice.HasValue) query = query.Where(p => p.TotalPrice <= q.MaxPrice.Value);
        if (q.MinAreaCents.HasValue) query = query.Where(p => p.AreaInCents >= q.MinAreaCents.Value);
        if (q.MaxAreaCents.HasValue) query = query.Where(p => p.AreaInCents <= q.MaxAreaCents.Value);
        if (q.RoadAccess.HasValue) query = query.Where(p => p.RoadAccess == q.RoadAccess.Value);
        if (!string.IsNullOrWhiteSpace(q.MarketingPlan) &&
            Enum.TryParse<MarketingPlan>(q.MarketingPlan, true, out var mpFilter))
            query = query.Where(p => p.MarketingPlan == mpFilter);

        query = q.SortBy switch
        {
            "price_asc" => query.OrderBy(p => p.TotalPrice),
            "price_desc" => query.OrderByDescending(p => p.TotalPrice),
            "area_asc" => query.OrderBy(p => p.AreaInCents),
            "area_desc" => query.OrderByDescending(p => p.AreaInCents),
            "oldest" => query.OrderBy(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt),
        };

        var total = await query.CountAsync();
        var data = await query
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return new PaginatedResponse<PropertyDto>(
            data.Select(ToDto).ToList(),
            total, q.Page, q.PageSize,
            (int)Math.Ceiling(total / (double)q.PageSize)
        );
    }

    public async Task<PropertyDto?> GetByIdAsync(int id)
    {
        var p = await db.Properties.Include(p => p.SubmittedByUser).FirstOrDefaultAsync(p => p.Id == id);
        return p is null ? null : ToDto(p);
    }

    public async Task<List<PropertyDto>> GetFeaturedAsync() =>
        (await db.Properties
            .Where(p => p.IsFeatured && p.ApprovalStatus == ApprovalStatus.Approved)
            .OrderByDescending(p => p.CreatedAt)
            .Take(8)
            .ToListAsync())
        .Select(ToDto).ToList();

    public async Task<List<PropertyDto>> GetRelatedAsync(int id)
    {
        var prop = await db.Properties.FindAsync(id);
        if (prop is null) return [];
        return (await db.Properties
            .Where(p => p.Id != id && p.City == prop.City && p.ApprovalStatus == ApprovalStatus.Approved)
            .Take(4).ToListAsync())
            .Select(ToDto).ToList();
    }

    public async Task<PropertyDto> CreateAsync(CreatePropertyRequest req, int? submittedByUserId, bool autoApprove)
    {
        if (!Enum.TryParse<PropertyType>(req.PropertyType, true, out var pt))
            pt = PropertyType.OpenLand;
        if (!Enum.TryParse<ListingStatus>(req.Status ?? "ForSale", true, out var ls))
            ls = ListingStatus.ForSale;
        if (!Enum.TryParse<MarketingPlan>(req.MarketingPlan ?? "Free", true, out var mp))
            mp = MarketingPlan.Free;

        var prop = new Property
        {
            Title = req.Title,
            Description = req.Description,
            TotalPrice = req.TotalPrice,
            PricePerCent = req.PricePerCent,
            Address = req.Address,
            City = req.City,
            District = req.District ?? "Kanyakumari",
            State = req.State ?? "Tamil Nadu",
            PinCode = req.PinCode ?? string.Empty,
            AreaInCents = req.AreaInCents,
            AreaInSqFt = req.AreaInSqFt,
            Bedrooms = req.Bedrooms,
            Bathrooms = req.Bathrooms,
            PropertyType = pt,
            Status = ls,
            Images = req.Images ?? [],
            Features = req.Features ?? [],
            NearbyLandmarks = req.NearbyLandmarks ?? [],
            LegalStatus = req.LegalStatus,
            RoadAccess = req.RoadAccess,
            IsFeatured = req.IsFeatured,
            MarketingPlan = mp,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            SubmittedByUserId = submittedByUserId,
            SubmitterName = req.SubmitterName,
            SubmitterPhone = req.SubmitterPhone,
            SubmitterEmail = req.SubmitterEmail,
            ApprovalStatus = autoApprove ? ApprovalStatus.Approved : ApprovalStatus.Pending,
            IsApproved = autoApprove,
        };

        db.Properties.Add(prop);
        await db.SaveChangesAsync();
        return ToDto(prop);
    }

    public async Task<PropertyDto?> UpdateAsync(int id, UpdatePropertyRequest req)
    {
        var prop = await db.Properties.FindAsync(id);
        if (prop is null) return null;

        if (req.Title is not null) prop.Title = req.Title;
        if (req.Description is not null) prop.Description = req.Description;
        if (req.TotalPrice.HasValue) prop.TotalPrice = req.TotalPrice.Value;
        if (req.PricePerCent.HasValue) prop.PricePerCent = req.PricePerCent;
        if (req.Address is not null) prop.Address = req.Address;
        if (req.City is not null) prop.City = req.City;
        if (req.AreaInCents.HasValue) prop.AreaInCents = req.AreaInCents.Value;
        if (req.AreaInSqFt.HasValue) prop.AreaInSqFt = req.AreaInSqFt;
        if (req.Bedrooms.HasValue) prop.Bedrooms = req.Bedrooms;
        if (req.Bathrooms.HasValue) prop.Bathrooms = req.Bathrooms;
        if (req.Images is not null) prop.Images = req.Images;
        if (req.Features is not null) prop.Features = req.Features;
        if (req.NearbyLandmarks is not null) prop.NearbyLandmarks = req.NearbyLandmarks;
        if (req.LegalStatus is not null) prop.LegalStatus = req.LegalStatus;
        if (req.RoadAccess.HasValue) prop.RoadAccess = req.RoadAccess.Value;
        if (req.IsFeatured.HasValue) prop.IsFeatured = req.IsFeatured.Value;
        if (req.IsVerified.HasValue) prop.IsVerified = req.IsVerified.Value;
        if (req.MarketingPlan is not null &&
            Enum.TryParse<MarketingPlan>(req.MarketingPlan, true, out var mpUpd))
            prop.MarketingPlan = mpUpd;
        if (req.Latitude.HasValue) prop.Latitude = req.Latitude;
        if (req.Longitude.HasValue) prop.Longitude = req.Longitude;
        if (req.PropertyType is not null && Enum.TryParse<PropertyType>(req.PropertyType, true, out var pt))
            prop.PropertyType = pt;
        if (req.Status is not null && Enum.TryParse<ListingStatus>(req.Status, true, out var ls))
            prop.Status = ls;

        prop.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToDto(prop);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var prop = await db.Properties.FindAsync(id);
        if (prop is null) return false;
        db.Properties.Remove(prop);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<PropertyDto>> GetPendingAsync() =>
        (await db.Properties
            .Include(p => p.SubmittedByUser)
            .Include(p => p.AssignedToVerifyUser)
            .Where(p => p.ApprovalStatus == ApprovalStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync())
        .Select(ToDto).ToList();

    /// <summary>
    /// Admin hands a pending property off to an Employee/Agent/Admin to do
    /// the verification work (site visit, document check, photos). The
    /// assignee gets pinged via WhatsApp/SMS using the property.assignedForVerification template.
    /// </summary>
    public async Task<PropertyDto?> AssignToVerifyAsync(int id, int userId)
    {
        var prop = await db.Properties
            .Include(p => p.SubmittedByUser)
            .Include(p => p.AssignedToVerifyUser)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (prop is null) return null;

        var assignee = await db.Users.FindAsync(userId);
        if (assignee is null || !assignee.IsActive
            || (assignee.Role != UserRole.Employee
                && assignee.Role != UserRole.Agent
                && assignee.Role != UserRole.Admin))
        {
            throw new ArgumentException("Verification can only be assigned to active Employees, Agents, or Admins.");
        }

        prop.AssignedToVerifyUserId = userId;
        prop.AssignedToVerifyUser = assignee;
        prop.AssignedToVerifyAt = DateTime.UtcNow;
        prop.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Best-effort notification to the assignee — uses the new
        // property.assignedForVerification SMS template (seeded with id 11).
        if (!string.IsNullOrWhiteSpace(assignee.Phone))
        {
            var submitter = prop.SubmittedByUser != null
                ? $"{prop.SubmittedByUser.FirstName} {prop.SubmittedByUser.LastName}"
                : prop.SubmitterName ?? "the seller";
            var submitterPhone = prop.SubmittedByUser?.Phone ?? prop.SubmitterPhone ?? "";

            var body = await templates.RenderAsync("property.assignedForVerification", new Dictionary<string, string?>
            {
                ["assigneeFirstName"] = assignee.FirstName,
                ["title"]             = prop.Title,
                ["city"]              = prop.City,
                ["submitter"]         = submitter,
                ["submitterPhone"]    = submitterPhone,
                ["propertyId"]        = prop.Id.ToString(),
            });
            if (!string.IsNullOrWhiteSpace(body))
                await notifications.SendAsync(assignee.Phone!, body, preferWhatsApp: true);
        }

        return ToDto(prop);
    }

    public async Task<PropertyDto?> ApproveOrRejectAsync(int id, ApprovalRequest req)
    {
        var prop = await db.Properties
            .Include(p => p.SubmittedByUser)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (prop is null) return null;

        var isApprove = req.Action.ToLower() == "approve";
        prop.ApprovalStatus = isApprove ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        prop.IsApproved = isApprove;
        prop.RejectionReason = isApprove ? null : req.Reason;
        prop.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // ── Notify the seller ────────────────────────────────────────────
        // Prefer the registered user's contact, fall back to anonymous
        // submitter contact captured at submission time.
        var sellerName = prop.SubmittedByUser is not null
            ? prop.SubmittedByUser.FirstName
            : prop.SubmitterName ?? "there";
        var sellerPhone = prop.SubmittedByUser?.Phone ?? prop.SubmitterPhone;
        var sellerEmail = prop.SubmittedByUser?.Email ?? prop.SubmitterEmail;

        if (!string.IsNullOrWhiteSpace(sellerPhone))
        {
            var key = isApprove ? "property.approved" : "property.rejected";
            var vars = new Dictionary<string, string?>
            {
                ["name"] = sellerName,
                ["title"] = prop.Title,
                ["reasonSuffix"] = string.IsNullOrWhiteSpace(req.Reason) ? "" : $" Reason: {req.Reason}.",
            };
            var body = await templates.RenderAsync(key, vars);
            if (!string.IsNullOrWhiteSpace(body))
                await notifications.SendAsync(sellerPhone, body, preferWhatsApp: true);
        }

        // Email follow-up (optional — only if we have an address)
        if (!string.IsNullOrWhiteSpace(sellerEmail))
        {
            var subject = isApprove
                ? $"Your property '{prop.Title}' is now live!"
                : $"Update on your property submission '{prop.Title}'";
            var body = isApprove
                ? $"<p>Hi {sellerName},</p>" +
                  $"<p>Great news! Your listing <strong>{prop.Title}</strong> has been " +
                  $"approved and is <strong>now live</strong> on Jose For Land.</p>" +
                  $"<p>Buyers can view it at " +
                  $"<a href=\"https://joseforland.com/properties/{prop.Id}\">joseforland.com/properties/{prop.Id}</a>.</p>" +
                  "<p>We'll notify you whenever an inquiry comes in.</p>" +
                  "<p>— The Jose For Land team</p>"
                : $"<p>Hi {sellerName},</p>" +
                  $"<p>We've reviewed your submission <strong>{prop.Title}</strong>, but unfortunately we " +
                  "could not approve it at this time.</p>" +
                  (string.IsNullOrWhiteSpace(req.Reason) ? "" : $"<p><strong>Reason:</strong> {req.Reason}</p>") +
                  "<p>Please call us at <strong>+91 99944 88490</strong> if you'd like to discuss " +
                  "or resubmit with updated details.</p>" +
                  "<p>— The Jose For Land team</p>";
            await email.SendAsync(sellerEmail, subject, body);
        }

        return ToDto(prop);
    }

    public async Task<bool> ToggleFavoriteAsync(int propertyId, int userId)
    {
        var existing = await db.SavedProperties
            .FirstOrDefaultAsync(sp => sp.PropertyId == propertyId && sp.UserId == userId);

        if (existing is not null)
        {
            db.SavedProperties.Remove(existing);
            await db.SaveChangesAsync();
            return false;
        }

        db.SavedProperties.Add(new SavedProperty { PropertyId = propertyId, UserId = userId });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<PropertyDto>> GetFavoritesAsync(int userId) =>
        (await db.SavedProperties
            .Where(sp => sp.UserId == userId)
            .Include(sp => sp.Property)
                .ThenInclude(p => p!.SubmittedByUser)
            .OrderByDescending(sp => sp.SavedAt)
            .Select(sp => sp.Property!)
            .ToListAsync())
        .Select(ToDto).ToList();

    /// <summary>
    /// Properties submitted by (or assigned as agent to) the given user —
    /// includes pending/rejected ones so the user can review their queue.
    /// </summary>
    public async Task<List<PropertyDto>> GetMineAsync(int userId) =>
        (await db.Properties
            .Include(p => p.SubmittedByUser)
            .Where(p => p.SubmittedByUserId == userId || p.AgentId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync())
        .Select(ToDto).ToList();

    /// <summary>
    /// Update a property only if the user owns it (submitter or assigned agent).
    /// Returns null when the property doesn't exist OR the user isn't the owner.
    /// </summary>
    public async Task<PropertyDto?> UpdateAsOwnerAsync(int id, int userId, UpdatePropertyRequest req)
    {
        var prop = await db.Properties.FindAsync(id);
        if (prop is null) return null;
        if (prop.SubmittedByUserId != userId && prop.AgentId != userId) return null;
        return await UpdateAsync(id, req);
    }
}
