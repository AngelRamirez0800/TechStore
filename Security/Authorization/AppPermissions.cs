namespace TechStore.Security.Authorization
{
    /// <summary>
    /// Descriptor de un permiso granular usado en políticas de autorización y en la UI de administración.
    /// </summary>
    public sealed record PermissionDescriptor(string Key, string Group, string Label, string Description);

    /// <summary>
    /// Catálogo centralizado de permisos basados en Role Claims.
    /// </summary>
    public static class AppPermissions
    {
        public const string ClaimType = "Permission";

        public const string ProductsRead = "Products.Read";
        public const string ProductsCreate = "Products.Create";
        public const string ProductsEdit = "Products.Edit";
        public const string ProductsLifecycle = "Products.Lifecycle";

        public const string CategoriesRead = "Categories.Read";
        public const string CategoriesCreate = "Categories.Create";
        public const string CategoriesEdit = "Categories.Edit";
        public const string CategoriesLifecycle = "Categories.Lifecycle";

        public const string RolesManage = "Roles.Manage";
        public const string UsersManage = "Users.Manage";

        public static readonly IReadOnlyList<PermissionDescriptor> All =
        [
            new(ProductsRead, "Productos", "Ver productos", "Permite consultar el catálogo y el detalle de productos."),
            new(ProductsCreate, "Productos", "Crear productos", "Permite registrar nuevos productos en el catálogo."),
            new(ProductsEdit, "Productos", "Editar productos e imágenes", "Permite modificar productos existentes y gestionar sus imágenes."),
            new(ProductsLifecycle, "Productos", "Desactivar y reactivar productos", "Permite cambiar el estado activo o inactivo de los productos."),

            new(CategoriesRead, "Categorías", "Ver categorías", "Permite consultar el listado y el detalle de categorías."),
            new(CategoriesCreate, "Categorías", "Crear categorías", "Permite registrar nuevas categorías en el catálogo."),
            new(CategoriesEdit, "Categorías", "Editar categorías", "Permite modificar categorías y sus productos asociados."),
            new(CategoriesLifecycle, "Categorías", "Desactivar y reactivar categorías", "Permite cambiar el estado activo o inactivo de las categorías."),

            new(RolesManage, "Seguridad y Administración", "Administrar roles y permisos", "Permite crear, editar, eliminar roles y configurar sus permisos."),
            new(UsersManage, "Seguridad y Administración", "Administrar usuarios", "Permite asignar roles y bloquear o desbloquear cuentas de usuario.")
        ];

        public static readonly IReadOnlyList<string> AllKeys = All.Select(permission => permission.Key).ToArray();

        public static readonly IReadOnlyList<string> DefaultSellerPermissions =
        [
            ProductsRead,
            ProductsCreate,
            ProductsEdit,
            CategoriesRead
        ];

        public static readonly IReadOnlyList<string> DefaultClientPermissions =
        [
            ProductsRead,
            CategoriesRead
        ];

        public static PermissionDescriptor? Find(string? permissionKey) =>
            string.IsNullOrWhiteSpace(permissionKey)
                ? null
                : All.FirstOrDefault(permission =>
                    string.Equals(permission.Key, permissionKey.Trim(), StringComparison.OrdinalIgnoreCase));

        public static bool IsValid(string? permissionKey) => Find(permissionKey) is not null;
    }
}
