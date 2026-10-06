using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FRCM
{
 public interface IAuthService
    {
        Task<string?> GetTokenAsync(string username, string password);
    }
}
