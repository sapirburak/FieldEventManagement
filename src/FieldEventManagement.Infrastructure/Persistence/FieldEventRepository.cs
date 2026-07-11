using FieldEventManagement.Application.Interfaces;
using FieldEventManagement.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FieldEventManagement.Infrastructure.Persistence;
/// <summary>
/// Concrete implementation of IFieldEventRepository.
/// Responsible for all CRUD operations against the DbContext.
/// This class isolates the Application layer from the technical details of Entity Framework Core.
/// </summary>
public class FieldEventRepository : IFieldEventRepository
{
    private readonly ApplicationDbContext _context;

    public FieldEventRepository(ApplicationDbContext context) => _context = context;
  
    /// <summary>
    /// Checks whether an event exists in the database by unique identifier.
    /// Critical for ensuring Idempotency when receiving messages from the Agent.
    /// </summary>
    public async Task<bool> ExistsAsync(Guid id) =>
        await _context.FieldEvents.AnyAsync(e => e.Id == id);

    /// <summary>
    /// Fetches an event including its status history (Include) required by the State Machine.
    /// Include is necessary here because History is a private collection that is not loaded automatically.
    /// </summary>
    public async Task<FieldEvent?> GetByIdAsync(Guid id) =>
        await _context.FieldEvents
            .Include("_history") // Internal field name – required because the collection is private
            .FirstOrDefaultAsync(e => e.Id == id);

    public async Task AddAsync(FieldEvent fieldEvent) =>
        await _context.FieldEvents.AddAsync(fieldEvent);

    public async Task SaveChangesAsync() =>
        await _context.SaveChangesAsync();
}