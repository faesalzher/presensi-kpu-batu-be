namespace presensi_kpu_batu_be.Modules.UserModule
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string Username
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                return user?.FindFirst("email")?.Value
                    ?? user?.FindFirst("full_name")?.Value
                    ?? user?.Identity?.Name
                    ?? "anonymous";
            }
        }
    }

}
