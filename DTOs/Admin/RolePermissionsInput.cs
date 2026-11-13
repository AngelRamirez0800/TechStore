using System.ComponentModel.DataAnnotations;

namespace TechStore.DTOs.Admin
{
    public sealed class RolePermissionsInput
    {
        [Required(ErrorMessage = "El identificador del rol es obligatorio.")]
        public string RoleId { get; set; } = string.Empty;

        public string RoleName { get; set; } = string.Empty;

        [Display(Name = "Permisos asignados")]
        public List<string> SelectedPermissions { get; set; } = new();
    }
}
