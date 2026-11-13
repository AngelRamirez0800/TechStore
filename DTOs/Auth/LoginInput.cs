using System.ComponentModel.DataAnnotations;

namespace TechStore.DTOs.Auth
{
    public sealed class LoginInput
    {
        [Required(ErrorMessage = "Ingresa tu usuario o correo electrónico.")]
        [StringLength(150, ErrorMessage = "El identificador no puede superar los {1} caracteres.")]
        [Display(Name = "Usuario o correo electrónico")]
        public string Identifier { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu contraseña.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Recordarme")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
