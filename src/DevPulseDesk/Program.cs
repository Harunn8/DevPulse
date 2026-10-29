using DevPulseDesk.Forms;
using DevPulseDesk.Services;

namespace DevPulseDesk
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new DevPulseAppContext(AppServices.Load()));
        }
    }

    /// <summary>
    /// Login → Ana ekran → (çıkış) → Login döngüsünü yöneten uygulama bağlamı.
    /// Aktif pencere <see cref="ApplicationContext.MainForm"/> olarak tutulur; o kapanınca uygulama sonlanır.
    /// </summary>
    internal sealed class DevPulseAppContext : ApplicationContext
    {
        private readonly AppServices _services;

        public DevPulseAppContext(AppServices services)
        {
            _services = services;
            ShowLogin();
        }

        private void ShowLogin()
        {
            var login = new LoginForm(_services);
            login.LoginSucceeded += (_, session) =>
            {
                var main = new MainForm(_services, session);
                main.LogoutRequested += (_, _) => ShowLogin();
                MainForm = main;
                main.Show();
                login.CloseAnimated();
            };
            MainForm = login;
            login.Show();
        }
    }
}
