using FieldEventManagement.Core.Entities;
using FieldEventManagement.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace FieldEventManagement.Infrastructure.Persistence
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public User? GetUserByCredentials(string username, string password)
        {
            // Query the Users table in SQL Server
            // In Production: make sure you compare a Hash and not a plain-text password!
            return _context.Users
                .FirstOrDefault(u => u.Username == username && u.PasswordHash == password);
        }
    }
}
