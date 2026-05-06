namespace RealEstateApi.Models;

/// <summary>
/// Marker interface for entities that should never be hard-deleted.
/// EF global query filters automatically exclude rows where IsDeleted = true,
/// and services flip the flag instead of calling DbSet.Remove().
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
