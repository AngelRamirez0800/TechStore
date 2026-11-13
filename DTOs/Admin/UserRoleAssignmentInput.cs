using System.ComponentModel.DataAnnotations;

namespace TechStore.DTOs.Admin
{
    public sealed class UserRoleAssignmentInput
    {
        [Required(ErrorMessage = "El identificador del usuario es obligatorio.")]
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Roles asignados")]
        public List<string> SelectedRoles { get; set; } = new();

        public IReadOnlyList<string> AvailableRoles { get; set; } = [];
    }
}
