using System.Linq.Expressions;

namespace WiseSub.Infrastructure.Repositories;

/// <summary>
/// Generic repository interface for data access operations
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Gets an entity by its ID
    /// </summary>
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all entities
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds entities matching the predicate
    /// </summary>
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a new entity
    /// </summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing entity
    /// </summary>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes an entity
    /// </summary>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if any entity matches the predicate
    /// </summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Counts entities matching the predicate
    /// </summary>
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
}
