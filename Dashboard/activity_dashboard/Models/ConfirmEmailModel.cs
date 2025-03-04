using System.ComponentModel.DataAnnotations;

namespace activity_dashboard.Models
{
    public class ConfirmEmailModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Confirmation Code")]
        public string Code { get; set; }
    }
}