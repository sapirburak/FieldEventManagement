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
            // כאן אנחנו מחפשים בטבלת המשתמשים ב-SQL Server
            // ב-Production: תוודאי שאת משווה Hash ולא סיסמה בטקסט חופשי!
            return _context.Users
                .FirstOrDefault(u => u.Username == username && u.PasswordHash == password);
        }
    }
}
