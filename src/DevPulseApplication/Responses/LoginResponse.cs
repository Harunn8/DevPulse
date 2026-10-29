namespace DevPulseApplication.Responses
{
    public class LoginResponse
    {
        public string Token { get; set; }
        public Guid Id { get; set; }
        public string UserName { get; set; }
    }
}