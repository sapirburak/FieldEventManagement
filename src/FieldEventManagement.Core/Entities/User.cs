using System;
using System.Collections.Generic;
using System.Text;

namespace FieldEventManagement.Core.Entities
{
    public class User
    {
        public Guid Id { get; private set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty; // Never store a plain-text password!
        public string Role { get; set; } = string.Empty; // "Dispatcher" or "Technician"
    }
}
