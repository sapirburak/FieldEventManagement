using FieldEventManagement.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FieldEventManagement.Core.Interfaces
{

    public interface IUserRepository
    {
        User? GetUserByCredentials(string username, string password);
    }
}
