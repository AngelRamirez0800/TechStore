using System.ComponentModel.DataAnnotations;

namespace TechStore.DTOs
{
    public sealed class CategoryInput
    {
        [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
        [Display(Name = "Nombre")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción de la categoría es obligatoria.")]
        [StringLength(500, ErrorMessage = "La descripción no puede superar los {1} caracteres.")]
        [Display(Name = "Descripción")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;
    }
}
