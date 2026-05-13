using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.DTOs;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public interface IUserService
{
    Task<UserListResponse> GetAllAsync(UserQueryParams q);
    Task<AdminUserDto?> GetByIdAsync(int id);
    Task<AdminUserDto> CreateAsync(CreateUserRequest req);
    Task<AdminUserDto?> UpdateAsync(int id, UpdateUserRequest req);
    Task<AdminUserDto?> ToggleStatusAsync(int id);
    Task<bool> DeleteAsync(int id);
}

public class UserService(AppDbContext db) : IUserService
{
    public async Task<UserListResponse> GetAllAsync(UserQueryParams q)
    {
        var users = await db.Users
            .Select(u => new
            {
                User = u,
                PropertiesCount = db.Properties.Count(p =>
                    p.AgentId == u.Id || p.SubmittedByUserId == u.Id),
                InquiriesCount = db.Inquiries.Count(i => i.UserId == u.Id),
            })
            .ToListAsync();

        // Filter in-memory after loading (small admin dataset; cleaner search logic).
        IEnumerable<AdminUserDto> filtered = users.Select(x => new AdminUserDto(
            x.User.Id, x.User.FirstName, x.User.LastName, x.User.Email,
            x.User.Phone ?? "", x.User.City, x.User.Role.ToString(),
            x.User.IsActive, x.User.CreatedAt, x.User.LastActiveAt,
            x.PropertiesCount, x.InquiriesCount));

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(u =>
                u.FirstName.ToLowerInvariant().Contains(term) ||
                u.LastName.ToLowerInvariant().Contains(term) ||
                u.Email.ToLowerInvariant().Contains(term) ||
                u.Phone.Contains(term) ||
                (u.City?.ToLowerInvariant().Contains(term) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(q.Role) && q.Role != "all")
        {
            filtered = filtered.Where(u => u.Role.Equals(q.Role, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(q.Status) && q.Status != "all")
        {
            var wantActive = q.Status.Equals("active", StringComparison.OrdinalIgnoreCase);
            filtered = filtered.Where(u => u.IsActive == wantActive);
        }

        var list = filtered
            .OrderByDescending(u => u.CreatedAt)
            .ToList();

        var counts = new UserCountsDto(
            All: users.Count,
            Employee: users.Count(x => x.User.Role == UserRole.Employee),
            Seller: users.Count(x => x.User.Role == UserRole.Seller),
            Agent: users.Count(x => x.User.Role == UserRole.Agent),
            Admin: users.Count(x => x.User.Role == UserRole.Admin),
            Buyer: users.Count(x => x.User.Role == UserRole.Buyer),
            Active: users.Count(x => x.User.IsActive),
            Inactive: users.Count(x => !x.User.IsActive)
        );

        return new UserListResponse(list, list.Count, counts);
    }

    public async Task<AdminUserDto?> GetByIdAsync(int id)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return null;

        var propsCount = await db.Properties.CountAsync(p =>
            p.AgentId == u.Id || p.SubmittedByUserId == u.Id);
        var inquiriesCount = await db.Inquiries.CountAsync(i => i.UserId == u.Id);

        return new AdminUserDto(u.Id, u.FirstName, u.LastName, u.Email,
            u.Phone ?? "", u.City, u.Role.ToString(), u.IsActive,
            u.CreatedAt, u.LastActiveAt, propsCount, inquiriesCount);
    }

    public async Task<AdminUserDto> CreateAsync(CreateUserRequest req)
    {
        if (!Enum.TryParse<UserRole>(req.Role, ignoreCase: true, out var role))
            throw new ArgumentException($"Invalid role: {req.Role}");

        // Email uniqueness applies only to active, non-deleted users — so an
        // address belonging to a deactivated / soft-deleted account is free
        // to reuse for a fresh registration. (The global query filter on
        // ISoftDeletable already hides IsDeleted rows; we add IsActive on top.)
        var emailTaken = await db.Users.AnyAsync(x =>
            x.Email == req.Email && x.IsActive);
        if (emailTaken)
            throw new ArgumentException("Email is already in use by an active account.");

        var u = new User
        {
            FirstName = req.FirstName,
            LastName = req.LastName,
            Email = req.Email,
            Phone = req.Phone,
            City = req.City,
            Role = role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password ?? "ChangeMe123!"),
            IsActive = true,
        };

        db.Users.Add(u);
        await db.SaveChangesAsync();

        return new AdminUserDto(u.Id, u.FirstName, u.LastName, u.Email,
            u.Phone ?? "", u.City, u.Role.ToString(), u.IsActive,
            u.CreatedAt, u.LastActiveAt, 0, 0);
    }

    public async Task<AdminUserDto?> UpdateAsync(int id, UpdateUserRequest req)
    {
        var u = await db.Users.FindAsync(id);
        if (u is null) return null;

        if (!Enum.TryParse<UserRole>(req.Role, ignoreCase: true, out var role))
            throw new ArgumentException($"Invalid role: {req.Role}");

        // If the email is being changed, make sure no other active user owns it.
        if (!string.Equals(u.Email, req.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailTakenByOther = await db.Users.AnyAsync(x =>
                x.Id != id && x.Email == req.Email && x.IsActive);
            if (emailTakenByOther)
                throw new ArgumentException("Email is already in use by another active account.");
        }

        u.FirstName = req.FirstName;
        u.LastName = req.LastName;
        u.Email = req.Email;
        u.Phone = req.Phone;
        u.City = req.City;
        u.Role = role;
        u.IsActive = req.IsActive;
        u.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<AdminUserDto?> ToggleStatusAsync(int id)
    {
        var u = await db.Users.FindAsync(id);
        if (u is null) return null;
        u.IsActive = !u.IsActive;
        u.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var u = await db.Users.FindAsync(id);
        if (u is null) return false;
        if (u.Role == UserRole.Admin) return false; // never delete admin accounts via this endpoint
        db.Users.Remove(u);
        await db.SaveChangesAsync();
        return true;
    }
}
