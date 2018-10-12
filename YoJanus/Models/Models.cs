using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace YoJanus.Web.Models
{
    public class PromoCodes {
        public Guid ID { get; set; }
        [Remote("IsCodeUnique", "Promo")]
        public string Code { get; set; }
        public string GroupName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class PromoCodeUsers {
        public Guid ID { get; set; }
        public Guid UserID { get; set; }
        [Remote("ValidatePromoCode", "Promo")]
        public string Code { get; set; }
        public string FirstName { get; set; }
        public string LastName {get; set;}
        [Remote("ValidateEmail", "Promo")]
        public string Email {get; set;}
        public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;
    }

    public class Promocode_User_View {
        public Guid ID { get; set; }
        public string Code { get; set; }
        public string FirstName { get; set; }
        public string LastName {get; set;}
        public string GroupName { get; set; }
        public string Email {get; set;}
    }

    public class PromoCode_User_Rel {
        public Guid ID { get; set; }
        public Guid UserKey { get; set; }
        public Guid CodeKey { get; set; }
    }
}