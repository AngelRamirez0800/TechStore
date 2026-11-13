using Microsoft.Extensions.Logging;

namespace TechStore.Logging
{
    /// <summary>
    /// Catálogo centralizado de identificadores de eventos estructurados para auditoría, seguridad y errores.
    /// </summary>
    public static class AppLogEvents
    {
        #region Auth

        public static readonly EventId LoginSucceeded = new(1001, nameof(LoginSucceeded));
        public static readonly EventId LoginFailed = new(1002, nameof(LoginFailed));
        public static readonly EventId UserLockedOut = new(1003, nameof(UserLockedOut));
        public static readonly EventId UserRegistered = new(1004, nameof(UserRegistered));
        public static readonly EventId Logout = new(1005, nameof(Logout));
        public static readonly EventId AccessDenied = new(1006, nameof(AccessDenied));

        #endregion

        #region Roles

        public static readonly EventId RoleCreated = new(2001, nameof(RoleCreated));
        public static readonly EventId RoleUpdated = new(2002, nameof(RoleUpdated));
        public static readonly EventId RolePermissionsUpdated = new(2003, nameof(RolePermissionsUpdated));
        public static readonly EventId RoleDeleted = new(2004, nameof(RoleDeleted));
        public static readonly EventId UserRolesUpdated = new(2005, nameof(UserRolesUpdated));
        public static readonly EventId UserLockoutToggled = new(2006, nameof(UserLockoutToggled));

        #endregion

        #region Catalog

        public static readonly EventId ProductCreated = new(3001, nameof(ProductCreated));
        public static readonly EventId ProductUpdated = new(3002, nameof(ProductUpdated));
        public static readonly EventId ProductDeactivated = new(3003, nameof(ProductDeactivated));
        public static readonly EventId ProductActivated = new(3004, nameof(ProductActivated));
        public static readonly EventId ProductDeleted = new(3005, nameof(ProductDeleted));

        public static readonly EventId CategoryCreated = new(3011, nameof(CategoryCreated));
        public static readonly EventId CategoryUpdated = new(3012, nameof(CategoryUpdated));
        public static readonly EventId CategoryDeactivated = new(3013, nameof(CategoryDeactivated));
        public static readonly EventId CategoryActivated = new(3014, nameof(CategoryActivated));
        public static readonly EventId CategoryDeleted = new(3015, nameof(CategoryDeleted));

        #endregion

        #region Audit

        public static readonly EventId ActionExecuting = new(4001, nameof(ActionExecuting));
        public static readonly EventId ActionExecuted = new(4002, nameof(ActionExecuted));

        #endregion

        #region Errors

        public static readonly EventId UnhandledException = new(5001, nameof(UnhandledException));
        public static readonly EventId HttpStatusCodeHandled = new(5002, nameof(HttpStatusCodeHandled));

        #endregion
    }
}
