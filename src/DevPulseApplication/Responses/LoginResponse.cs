namespace DevPulseApplication.Responses
{
    public class LoginResponse
    {
        public string Token { get; set; }
        public Guid Id { get; set; }
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Organization { get; set; }
        public int ExpiredTime { get; set; } // token geçerlilik süresi (saniye)
        public string LanguageKey { get; set; }
        public List<string> Permissions { get; set; } = new();
    }
}
