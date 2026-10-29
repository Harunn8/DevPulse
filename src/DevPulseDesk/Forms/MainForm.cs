using DevPulseApplication.HttpClients.Services;
using DevPulseApplication.Models;
using DevPulseApplication.Responses;
using DevPulseDesk.Services;
using DevPulseDesk.UI;
using DevPulseDesk.UI.Animation;
using DevPulseDesk.UI.Controls;

namespace DevPulseDesk.Forms
{
    /// <summary>
    /// Test merkezi: istatistik kartları, canlı test akışı, günlük ve cihaz ızgarası.
    /// </summary>
    internal sealed class MainForm : BorderlessForm
    {
        private readonly Session _session;
        private readonly DeviceHttpClientService? _deviceService;
        private readonly DeviceTestRunner _runner;

        private readonly TitleBar _titleBar;
        private readonly SideNav _nav;
        private readonly GreetingBlock _greeting;
        private readonly ModernTextBox _search;
        private readonly GlowButton _refreshButton;
        private readonly GlowButton _startButton;
        private readonly StatCard _statTotal;
        private readonly StatCard _statSuccess;
        private readonly StatCard _statFail;
        private readonly StatCard _statLatency;
        private readonly PipelineView _pipeline;
        private readonly LogConsole _log;
        private readonly SectionHeader _section;
        private readonly DeviceGrid _grid;
        private readonly Toast _toast;

        private List<DeviceTestItem> _items = new();
        private CancellationTokenSource? _cts;
        private int _runId;
        private bool _loading;

        public MainForm(AppServices services, Session session)
        {
            _session = session;
            _deviceService = session.IsDemo ? null : services.CreateDeviceService(session);
            _runner = new DeviceTestRunner
            {
                Simulate = session.IsDemo,
                Parallelism = services.Parallelism,
                MinStageDurationMs = services.MinStageDurationMs
            };

            Text = "DevPulse – Test Merkezi";
            Resizable = true;
            KeyPreview = true;
            BackColor = Theme.Chrome;
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1600, 1000);
            ClientSize = new Size(Math.Min(Dpi.S(1400), (int)(area.Width * 0.94)), Math.Min(Dpi.S(900), (int)(area.Height * 0.94)));
            MinimumSize = Dpi.S(1120, 720);

            _titleBar = new TitleBar
            {
                Dock = DockStyle.Top,
                Height = Dpi.S(46),
                Badge = session.IsDemo ? "DEMO MODU • simülasyon" : session.BaseUrl,
                IsDemo = session.IsDemo
            };
            _nav = new SideNav { Dock = DockStyle.Left, Width = Dpi.S(250), UserName = session.UserName, IsDemo = session.IsDemo };

            // Başlık satırı
            _greeting = new GreetingBlock { UserName = session.UserName, Info = "Cihazlar yükleniyor…" };
            _search = new ModernTextBox { Label = "Cihaz ara (ad, IP, marka)", Glyph = Glyph.Search, BackColor = Theme.Background, Size = Dpi.S(270, 54) };
            _refreshButton = new GlowButton { Text = "Yenile", Glyph = Glyph.Refresh, Ghost = true, Size = Dpi.S(124, 62) };
            _startButton = new GlowButton { Text = "Testi Başlat", Glyph = Glyph.Play, Size = Dpi.S(184, 62) };
            var header = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Theme.Background, Margin = Padding.Empty };
            header.Controls.AddRange([_greeting, _search, _refreshButton, _startButton]);
            header.Layout += (_, _) => LayoutHeader(header);

            // İstatistik kartları
            _statTotal = new StatCard { Title = "Toplam Cihaz", Glyph = Glyph.Devices, Accent = Theme.Accent, Dock = DockStyle.Fill, Margin = Padding.Empty };
            _statSuccess = new StatCard { Title = "Başarılı", Glyph = Glyph.Check, Accent = Theme.Success, Dock = DockStyle.Fill, Margin = Padding.Empty };
            _statFail = new StatCard { Title = "Başarısız", Glyph = Glyph.Cancel, Accent = Theme.Danger, Dock = DockStyle.Fill, Margin = Padding.Empty };
            _statLatency = new StatCard { Title = "Ort. Yanıt Süresi", Glyph = Glyph.Speed, Accent = Theme.Accent2, Suffix = " ms", Placeholder = "—", Dock = DockStyle.Fill, Margin = Padding.Empty };
            var stats = CreateColumns([25, 25, 25, 25], _statTotal, _statSuccess, _statFail, _statLatency);

            // Test akışı + canlı günlük
            _pipeline = new PipelineView { Dock = DockStyle.Fill, Margin = Dpi.S(6, 8, 8, 6) };
            _log = new LogConsole { Dock = DockStyle.Fill, Margin = Dpi.S(8, 8, 6, 6) };
            var middle = CreateColumns([62, 38], _pipeline, _log);

            _section = new SectionHeader { Dock = DockStyle.Fill, Margin = Dpi.S(6, 6, 6, 0) };
            _grid = new DeviceGrid { Dock = DockStyle.Fill, Margin = Dpi.S(6, 4, 0, 0) };

            var layout = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Theme.Background, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(84)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(142)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(286)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(44)));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(stats, 0, 1);
            layout.Controls.Add(middle, 0, 2);
            layout.Controls.Add(_section, 0, 3);
            layout.Controls.Add(_grid, 0, 4);

            var content = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = Dpi.S(22, 18, 20, 14) };
            content.Controls.Add(layout);

            _toast = new Toast();

            // Dock sırası: son eklenen önce yerleşir (başlık → menü → içerik).
            Controls.Add(content);
            Controls.Add(_nav);
            Controls.Add(_titleBar);
            Controls.Add(_toast);
            _toast.BringToFront();

            _nav.FilterChanged += (_, filter) =>
            {
                _grid.Filter = filter;
                UpdateSectionInfo();
            };
            _nav.LogoutRequested += (_, _) => Logout();
            _search.TextChanged += (_, _) =>
            {
                _grid.SearchText = _search.Text;
                UpdateSectionInfo();
            };
            _refreshButton.Click += (_, _) => _ = LoadDevicesAsync();
            _startButton.Click += (_, _) => _ = ToggleTestAsync();

            _pipeline.Reset(0);
            UpdateStats();
        }

        public event EventHandler? LogoutRequested;

        private static BufferedTable CreateColumns(float[] percents, params Control[] controls)
        {
            var table = new BufferedTable
            {
                Dock = DockStyle.Fill,
                ColumnCount = percents.Length,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            foreach (var p in percents) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, p));
            for (var i = 0; i < controls.Length; i++) table.Controls.Add(controls[i], i, 0);
            return table;
        }

        private void LayoutHeader(Control header)
        {
            var h = header.ClientSize.Height;
            var right = header.ClientSize.Width;
            _startButton.Location = new Point(right - _startButton.Width, (h - _startButton.Height) / 2);
            _refreshButton.Location = new Point(_startButton.Left - _refreshButton.Width, (h - _refreshButton.Height) / 2);
            _search.Location = new Point(_refreshButton.Left - _search.Width - Dpi.S(6), (h - _search.Height) / 2);
            _greeting.SetBounds(Dpi.S(6), Dpi.S(4), Math.Max(10, _search.Left - Dpi.S(20)), h - Dpi.S(4));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _greeting.Appear();
            StatCard[] cards = [_statTotal, _statSuccess, _statFail, _statLatency];
            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                Animator.Animate(1, Easing.Linear, _ => { }, card.Appear, delayMs: 120 + i * 90);
            }

            _log.Add(LogLevel.System, $"Oturum açıldı: {_session.UserName}{(_session.IsDemo ? " (demo modu – sonuçlar simüle edilir)" : $" @ {_session.BaseUrl}")}");
            _ = LoadDevicesAsync();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.F5)
            {
                _ = ToggleTestAsync();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                _search.FocusInput();
                e.Handled = true;
            }
        }

        private async Task LoadDevicesAsync()
        {
            if (_loading || _cts != null) return;
            _loading = true;
            _refreshButton.IsLoading = true;
            _grid.IsLoading = true;
            _log.Add(LogLevel.Info, "Cihaz listesi sunucudan alınıyor…");

            try
            {
                List<PagDeviceModel>? devices = _session.IsDemo
                    ? await DemoData.GetDevicesAsync()
                    : await _deviceService!.GetPagDevices().WaitAsync(TimeSpan.FromSeconds(30));

                if (devices == null)
                {
                    _log.Add(LogLevel.Error, "Cihaz listesi alınamadı (sunucu başarılı yanıt dönmedi).");
                    _toast.Notify(LogLevel.Error, "Cihazlar alınamadı", "Sunucu cihaz listesini döndürmedi. Oturum süresi dolmuş olabilir.");
                    devices = new List<PagDeviceModel>();
                }
                else
                {
                    _log.Add(LogLevel.Success, $"{devices.Count} cihaz yüklendi.");
                    _toast.Notify(LogLevel.Success, "Cihazlar hazır", $"{devices.Count} cihaz yüklendi. Başlatmak için F5'e basabilirsiniz.");
                }

                _items = devices.Where(d => !d.InMaintenance).Select(d => new DeviceTestItem(d)).ToList();
                var maintenance = devices.Count - _items.Count;
                if (maintenance > 0) _log.Add(LogLevel.Warning, $"{maintenance} cihaz bakım modunda olduğu için listeye alınmadı.");
            }
            catch (Exception ex)
            {
                _log.Add(LogLevel.Error, $"Cihaz listesi alınamadı: {ex.Message}");
                _toast.Notify(LogLevel.Error, "Bağlantı hatası", "Cihaz listesi alınamadı. Sunucu bağlantısını kontrol edin.");
                _items = new List<DeviceTestItem>();
            }
            finally
            {
                _loading = false;
                _refreshButton.IsLoading = false;
                _grid.IsLoading = false;
            }

            _grid.SetItems(_items);
            _pipeline.Reset(_items.Count);
            UpdateStats();
        }

        private async Task ToggleTestAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _log.Add(LogLevel.Warning, "Durdurma isteği gönderildi, aktif bağlantılar kapatılıyor…");
                return;
            }
            if (_loading) return;
            if (_items.Count == 0)
            {
                _toast.Notify(LogLevel.Warning, "Test edilecek cihaz yok", "Önce cihaz listesini yenileyin.");
                return;
            }

            var runId = ++_runId;
            using var cts = new CancellationTokenSource();
            _cts = cts;

            foreach (var item in _items) item.Reset();
            _grid.NotifyAllChanged();
            _pipeline.Begin(_items.Count);
            _nav.IsBusy = true;
            _nav.PulseCaption = "Test sürüyor…";
            SetRunningUi(true);
            UpdateStats();
            _log.Add(LogLevel.System, $"Test oturumu #{runId} başladı • {_items.Count} cihaz • {_runner.Parallelism} paralel kanal");

            var progress = new Progress<TestEvent>(e =>
            {
                if (runId == _runId && !IsDisposed) OnTestEvent(e);
            });

            var cancelled = false;
            try
            {
                await _runner.RunAsync(_items, progress, cts.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                _log.Add(LogLevel.Error, $"Test motoru beklenmeyen bir hatayla durdu: {ex.Message}");
                cancelled = true;
            }
            finally
            {
                _cts = null;
            }

            if (IsDisposed) return;
            foreach (var item in _items.Where(i => !i.IsDone)) item.Cancel();
            _grid.NotifyAllChanged();
            _pipeline.Update(_items, null, LogLevel.Info);
            _pipeline.Complete(cancelled);
            SetRunningUi(false);
            UpdateStats();

            var success = _items.Count(i => i.State == TestState.Success);
            var fail = _items.Count(i => i.State == TestState.Fail);
            _nav.IsBusy = false;
            _nav.PulseCaption = $"Son test {DateTime.Now:HH:mm} • {success}/{_items.Count} başarılı";

            if (cancelled)
            {
                _log.Add(LogLevel.Warning, $"Test oturumu #{runId} durduruldu • {success + fail}/{_items.Count} cihaz tamamlandı");
                _toast.Notify(LogLevel.Warning, "Test durduruldu", $"{success + fail} cihazın testi tamamlandı, kalanlar iptal edildi.");
            }
            else
            {
                var results = _items.Select(i => i.Result).OfType<TestResultResponse>().ToList();
                _log.Add(fail == 0 ? LogLevel.Success : LogLevel.Warning,
                    $"Test oturumu #{runId} tamamlandı • {results.Count(r => r.Status == TestResult.Success)} başarılı, {results.Count(r => r.Status == TestResult.Fail)} başarısız");
                _toast.Notify(fail == 0 ? LogLevel.Success : LogLevel.Warning,
                    fail == 0 ? "Tüm cihazlar sağlıklı" : "Test tamamlandı",
                    fail == 0 ? $"{success} cihazın tamamı testi başarıyla geçti." : $"{success} başarılı, {fail} başarısız cihaz. Detaylar için filtreyi kullanın.");
            }
        }

        private void SetRunningUi(bool running)
        {
            _startButton.Text = running ? "Testi Durdur" : "Testi Başlat";
            _startButton.Glyph = running ? Glyph.Stop : Glyph.Play;
            if (running) _startButton.SetColors(Theme.Danger, Theme.Accent3);
            else _startButton.SetColors(Theme.Accent, Theme.Accent2);
            _refreshButton.Enabled = !running;
        }

        private void OnTestEvent(TestEvent e)
        {
            e.Item.Apply(e);
            _grid.NotifyChanged(e.Item);

            if (e.Kind != TestEventKind.Started) _log.Add(e.State == StageState.Active ? LogLevel.Info : e.Level, e.State == StageState.Active ? $"{e.Item.Name} • {e.Message}" : e.Message);

            var detail = e.Kind == TestEventKind.Started ? null : e.State == StageState.Active ? $"{e.Item.Name} • {e.Message}" : e.Message;
            _pipeline.Update(_items, detail, e.Level);
            UpdateStats();
        }

        private void UpdateStats()
        {
            var total = _items.Count;
            var success = _items.Count(i => i.State == TestState.Success);
            var fail = _items.Count(i => i.State == TestState.Fail);
            var running = _items.Count(i => i.State == TestState.Running);
            var pending = _items.Count(i => i.State is TestState.Pending or TestState.Cancelled);
            var done = success + fail;

            _statTotal.SetValue(total, total == 0 ? 0 : 1, total == 0 ? "Cihaz bulunamadı" : $"{running} test ediliyor • {pending} bekliyor");
            _statSuccess.SetValue(success, done == 0 ? 0 : success / (float)done, done == 0 ? "Henüz sonuç yok" : $"%{success * 100 / done} başarı oranı");
            _statFail.SetValue(fail, done == 0 ? 0 : fail / (float)done, done == 0 ? "Henüz sonuç yok" : fail == 0 ? "Sorunlu cihaz yok" : $"{fail} cihaz müdahale bekliyor");

            var latencies = _items.Select(i => i.LatencyMs).OfType<long>().ToList();
            if (latencies.Count == 0)
            {
                _statLatency.Placeholder = "—";
                _statLatency.SetValue(0, 0, "Henüz ölçüm yok");
            }
            else
            {
                var average = (float)latencies.Average();
                _statLatency.Placeholder = null;
                _statLatency.SetValue(average, Math.Min(1f, average / 200f), $"En hızlı {latencies.Min()} ms • en yavaş {latencies.Max()} ms");
            }

            _nav.UpdateCounts(total, running, success, fail, pending);
            _greeting.Info = total == 0 ? "İzlenen cihaz yok" : $"{total} cihaz izleniyor";
            _greeting.Invalidate();
            UpdateSectionInfo();
        }

        private void UpdateSectionInfo()
        {
            var filter = _grid.Filter switch
            {
                DeviceFilter.Running => "test ediliyor",
                DeviceFilter.Success => "başarılı",
                DeviceFilter.Fail => "başarısız",
                DeviceFilter.Pending => "bekleyen",
                _ => null
            };
            var parts = new List<string> { $"{_grid.VisibleCount} / {_items.Count} gösteriliyor" };
            if (filter != null) parts.Add($"filtre: {filter}");
            if (!string.IsNullOrWhiteSpace(_search.Text)) parts.Add($"arama: \"{_search.Text.Trim()}\"");
            _section.Info = "•  " + string.Join("  •  ", parts);
            _section.Invalidate();
        }

        private void Logout()
        {
            _cts?.Cancel();
            LogoutRequested?.Invoke(this, EventArgs.Empty);
            CloseAnimated();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            base.OnFormClosing(e);
        }
    }
}
