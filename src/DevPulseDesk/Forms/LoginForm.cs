using DevPulseApplication.Models;
using DevPulseDesk.Services;
using DevPulseDesk.UI;
using DevPulseDesk.UI.Controls;

namespace DevPulseDesk.Forms
{
    /// <summary>
    /// Giriş ekranı: solda canlı marka paneli, sağda kademeli olarak beliren giriş kartı.
    /// Başarılı girişte "Hoş geldin, kullanıcı" karşılama animasyonu oynatılır.
    /// </summary>
    internal sealed class LoginForm : BorderlessForm
    {
        private const int CardWidth = 392;
        private const int CardHeight = 580;

        private readonly AppServices _services;
        private readonly UserPreferences _preferences;
        private readonly BrandPanel _brand;
        private readonly BufferedPanel _right;
        private readonly BufferedPanel _card;
        private readonly WindowButtons _windowButtons;
        private readonly LoginHeader _header;
        private readonly ModernTextBox _server;
        private readonly ModernTextBox _userName;
        private readonly ModernTextBox _password;
        private readonly ToggleSwitch _remember;
        private readonly GlowButton _loginButton;
        private readonly InfoBanner _banner;
        private readonly GlowButton _demoButton;
        private bool _busy;

        public LoginForm(AppServices services)
        {
            _services = services;
            _preferences = UserPreferences.Load();

            Text = "DevPulse – Giriş";
            ClientSize = Dpi.S(1080, 660);
            BackColor = Theme.Chrome;

            _brand = new BrandPanel { Dock = DockStyle.Left, Width = Dpi.S(520) };
            _right = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Theme.Chrome };
            _card = new BufferedPanel { Size = Dpi.S(CardWidth, CardHeight), BackColor = Theme.Chrome };
            _windowButtons = new WindowButtons { ShowMaximize = false, BackColor = Theme.Chrome, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _windowButtons.Size = new Size(_windowButtons.PreferredWidth, Dpi.S(38));

            _header = new LoginHeader { Bounds = Rect(6, 0, 380, 126) };
            _server = new ModernTextBox { Label = "Sunucu adresi", Glyph = Glyph.Globe, Bounds = Rect(6, 140, 380, 58), TabIndex = 0 };
            _userName = new ModernTextBox { Label = "Kullanıcı adı", Glyph = Glyph.User, Bounds = Rect(6, 210, 380, 58), TabIndex = 1 };
            _password = new ModernTextBox { Label = "Şifre", Glyph = Glyph.Lock, IsPassword = true, Bounds = Rect(6, 280, 380, 58), TabIndex = 2 };
            _remember = new ToggleSwitch { Text = "Beni hatırla", Bounds = Rect(6, 352, 220, 28), TabIndex = 3 };
            _loginButton = new GlowButton { Text = "Giriş Yap", Glyph = "", Bounds = Rect(1, 392, 390, 62), TabIndex = 4 }; // Forward
            _banner = new InfoBanner { Bounds = Rect(6, 462, 380, 42) };
            _demoButton = new GlowButton { Text = "Demo modunda keşfet", Glyph = Glyph.Flash, Ghost = true, Bounds = Rect(1, 512, 390, 56), TabIndex = 5 };

            _card.Controls.AddRange([_header, _server, _userName, _password, _remember, _loginButton, _banner, _demoButton]);
            _right.Controls.Add(_card);
            _right.Controls.Add(_windowButtons);
            _right.Resize += (_, _) => LayoutRight();
            _right.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left) Native.DragWindow(this);
            };
            Controls.Add(_right);
            Controls.Add(_brand);

            _server.Text = _preferences.ServerUrl ?? services.DefaultServerUrl;
            if (_preferences.RememberMe)
            {
                _userName.Text = _preferences.UserName ?? "";
                _remember.SetCheckedInstant(true);
            }

            _server.Submit += (_, _) => _userName.FocusInput();
            _userName.Submit += (_, _) => _password.FocusInput();
            _password.Submit += (_, _) => _ = LoginAsync();
            _loginButton.Click += (_, _) => _ = LoginAsync();
            _demoButton.Click += (_, _) => _ = StartDemoAsync();

            LayoutRight();
        }

        public event EventHandler<Session>? LoginSucceeded;

        private static Rectangle Rect(int x, int y, int w, int h) => new(Dpi.S(x), Dpi.S(y), Dpi.S(w), Dpi.S(h));

        private void LayoutRight()
        {
            _card.Location = new Point((_right.ClientSize.Width - _card.Width) / 2, Math.Max(Dpi.S(20), (_right.ClientSize.Height - _card.Height) / 2));
            _windowButtons.Location = new Point(_right.ClientSize.Width - _windowButtons.Width, 0);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Kart öğeleri sırayla aşağıdan yukarı kayarak gelir.
            Control[] sequence = [_header, _server, _userName, _password, _remember, _loginButton, _demoButton];
            for (var i = 0; i < sequence.Length; i++) Effects.SlideUp(sequence[i], 34, 120 + i * 70);

            var target = string.IsNullOrWhiteSpace(_server.Text) ? _server : string.IsNullOrWhiteSpace(_userName.Text) ? _userName : _password;
            BeginInvoke(() => target.FocusInput());
        }

        private void SetBusy(bool busy, GlowButton button)
        {
            _busy = busy;
            button.IsLoading = busy;
            (button == _loginButton ? _demoButton : _loginButton).Enabled = !busy;
        }

        private async Task LoginAsync()
        {
            if (_busy) return;
            _banner.HideMessage();

            var missing = new List<ModernTextBox>();
            if (string.IsNullOrWhiteSpace(_server.Text)) missing.Add(_server);
            if (string.IsNullOrWhiteSpace(_userName.Text)) missing.Add(_userName);
            if (string.IsNullOrEmpty(_password.Text)) missing.Add(_password);
            if (missing.Count > 0)
            {
                foreach (var field in missing)
                {
                    field.SetError(true);
                    Effects.Shake(field);
                }
                _banner.ShowMessage("Lütfen işaretli alanları doldurun.");
                missing[0].FocusInput();
                return;
            }

            SetBusy(true, _loginButton);
            var baseUrl = AppServices.NormalizeUrl(_server.Text);
            try
            {
                var service = _services.CreateLoginService(baseUrl);
                var response = await service.LoginAsync(new LoginModel { UserName = _userName.Text.Trim(), Password = _password.Text })
                    .WaitAsync(TimeSpan.FromSeconds(20));

                if (response == null || string.IsNullOrEmpty(response.Token))
                {
                    SetBusy(false, _loginButton);
                    _password.SetError(true);
                    _userName.SetError(true);
                    Effects.Shake(_card, 12);
                    _banner.ShowMessage("Kullanıcı adı veya şifre hatalı.");
                    _password.FocusInput();
                    _password.SelectAll();
                    return;
                }

                SavePreferences(baseUrl);
                var userName = string.IsNullOrWhiteSpace(response.UserName) ? _userName.Text.Trim() : response.UserName;
                PlayWelcome(new Session(userName, response.Id, response.Token, baseUrl, IsDemo: false));
            }
            catch (Exception ex)
            {
                SetBusy(false, _loginButton);
                Effects.Shake(_card, 12);
                if (ex is HttpRequestException or TimeoutException or TaskCanceledException or InvalidOperationException or UriFormatException)
                {
                    _server.SetError(true);
                    _banner.ShowMessage(ex is TimeoutException or TaskCanceledException
                        ? "Sunucu zamanında yanıt vermedi."
                        : "Sunucuya ulaşılamadı. Adresi kontrol edin.");
                }
                else
                {
                    _banner.ShowMessage("Sunucudan beklenmeyen bir yanıt alındı.");
                }
            }
        }

        private async Task StartDemoAsync()
        {
            if (_busy) return;
            _banner.HideMessage();
            SetBusy(true, _demoButton);
            await Task.Delay(900);
            var name = string.IsNullOrWhiteSpace(_userName.Text) ? "Misafir" : _userName.Text.Trim();
            PlayWelcome(new Session(name, Guid.Empty, "", "demo", IsDemo: true));
        }

        private void SavePreferences(string baseUrl)
        {
            _preferences.ServerUrl = baseUrl;
            _preferences.RememberMe = _remember.Checked;
            _preferences.UserName = _remember.Checked ? _userName.Text.Trim() : null;
            _preferences.Save();
        }

        private void PlayWelcome(Session session)
        {
            var snapshot = new Bitmap(ClientSize.Width, ClientSize.Height);
            DrawToBitmap(snapshot, new Rectangle(Point.Empty, ClientSize));

            var overlay = new WelcomeOverlay
            {
                Dock = DockStyle.Fill,
                UserName = session.UserName,
                Snapshot = snapshot,
                Subtitle = session.IsDemo ? "Demo çalışma alanı hazırlanıyor" : "Çalışma alanınız hazırlanıyor"
            };
            overlay.Finished += (_, _) => LoginSucceeded?.Invoke(this, session);
            overlay.Disposed += (_, _) => snapshot.Dispose();
            Controls.Add(overlay);
            overlay.BringToFront();
            overlay.Focus();
        }
    }
}
