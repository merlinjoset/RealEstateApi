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
    Task<List<PropertyDto>> GetPendingAsync();
    Task<bool> ToggleFavoriteAsync(int propertyId, int userId);
}

public class PropertyService(AppDbContext db) : IPropertyService
{
    private static PropertyDto ToDto(Property p) => new(
        p.Id, p.Title, p.Description, p.TotalPrice, p.PricePerCent,
        p.Address, p.City, p.District, p.State, p.PinCode,
        p.AreaInCents, p.AreaInSqFt, p.Bedrooms, p.Bathrooms,
        p.PropertyType.ToString(), p.Status.ToString(),
        p.Images, p.Features, p.NearbyLandmarks,
        p.LegalStatus, p.RoadAccess, p.IsFeatured, p.IsVerified,
        p.ApprovalStatus.ToString(),
        p.Latitude, p.Longitude,
        p.AgentId,
        p.SubmittedByUser != null ? $"{p.SubmittedByUser.FirstName} {p.SubmittedByUser.LastName}" : null,
        p.SubmittedByUser?.Phone,
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
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            SubmittedByUserId = submittedByUserId,
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
            .Where(p => p.ApprovalStatus == ApprovalStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync())
        .Select(ToDto).ToList();

    public async Task<PropertyDto?> ApproveOrRejectAsync(int id, ApprovalRequest req)
    {
        var prop = await db.Properties.FindAsync(id);
        if (prop is null) return null;

        prop.ApprovalStatus = req.Action.ToLower() == "approve"
            ? ApprovalStatus.Approved
            : ApprovalStatus.Rejected;
        prop.IsApproved = prop.ApprovalStatus == ApprovalStatus.Approved;
        prop.RejectionReason = req.Reason;
        prop.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
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
}
