using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    using System.ComponentModel.DataAnnotations;

    public class ApproverApplicationRequest
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Invalid LoginId")]
        public int LoginId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Invalid MohId")]
        public int? MohId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Invalid WardId")]
        public int? WardId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Invalid LicenceApplicationId")]
        public int? LicenceApplicationId { get; set; }

        [StringLength(50, ErrorMessage = "ApplicationNumber too long")]
        [RegularExpression(@"^[a-zA-Z0-9\-\/]*$",
            ErrorMessage = "ApplicationNumber contains invalid characters")]
        public string? ApplicationNumber { get; set; }

        [Range(1, 10000, ErrorMessage = "Invalid PageNumber")]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100")]
        public int PageSize { get; set; } = 10;
    }
}
