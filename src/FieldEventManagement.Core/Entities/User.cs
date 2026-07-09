using System;
using System.Collections.Generic;
using System.Text;

namespace FieldEventManagement.Core.Entities
{
    public class User
    {
        public Guid Id { get; private set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty; // לעולם לא סיסמה בטקסט חופשי!
        public string Role { get; set; } = string.Empty; // "Scheduler" או "Technician"
    }
}
