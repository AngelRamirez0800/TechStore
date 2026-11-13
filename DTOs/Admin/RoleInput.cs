using System.ComponentModel.DataAnnotations;

namespace TechStore.DTOs.Admin
{
    public sealed class RoleInput
    {
        public string? Id { get; set; }

        [Required(ErrorMessage = "El nombre del rol es obligatorio.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "El nombre del rol debe tener entre {2} y {1} caracteres.")]
        [Display(Name = "Nombre del rol")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Permisos asignados")]
        public List<string> SelectedPermissions { get; set; } = new();

        public bool IsBuiltIn { get; set; }
    }
}
