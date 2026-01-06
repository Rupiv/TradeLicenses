using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gba.TradeLicense.Application.Models;

namespace Gba.TradeLicense.Application.Abstractions
{
    public interface IDashboardService
    {
        Task<IEnumerable<ApplicationStatusCount>> GetStatusCountAsync(
            int loginID,
            CancellationToken ct
        );
    }


}
