using System;

namespace Gba.TradeLicense.Domain.Entities
{
    /* =========================================================
       1️⃣ INITIATE PAYMENT DTO
       Used for /api/payment/initiate
    ========================================================= */
    public class InitiatePaymentDto
    {
        public long LicenceApplicationId { get; set; }
        public int CorporationId { get; set; }
        public decimal Amount { get; set; }

        public string ApplicantName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    /* =========================================================
       2️⃣ VERIFY PAYMENT DTO (READ ONLY)
       Used for /api/payment/verify
       ❗ DOES NOT update DB
    ========================================================= */
    public class VerifyPaymentDto
    {
        public string Txnid { get; set; }
        public int CorporationId { get; set; }

        public decimal Amount { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    /* =========================================================
       3️⃣ REFUND PAYMENT DTO
       Used for /api/payment/refund
       ❗ Easebuzz REQUIRES original amount
    ========================================================= */
    public class RefundPaymentDto
    {
        public string Txnid { get; set; }
        public int CorporationId { get; set; }

        public decimal RefundAmount { get; set; }     // Amount to refund
        public decimal OriginalAmount { get; set; }   // 🔴 REQUIRED by Easebuzz

        public string Email { get; set; }
        public string Phone { get; set; }
    }
}
