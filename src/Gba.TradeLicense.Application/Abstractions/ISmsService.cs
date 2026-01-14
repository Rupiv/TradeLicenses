using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Application.Abstractions
{
    public interface ISmsService
    {
        Task<string> SendAsync(
            string templateKey,
            string mobileNo,
            params string[] variables
        );
    }

}
