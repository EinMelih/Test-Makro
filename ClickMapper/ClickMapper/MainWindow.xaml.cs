using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ClickMapper.Controls;
using ClickMapper.Native;

namespace ClickMapper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _isEditMode = true;
        private KeyboardHook _keyboardHook;
        private Dictionary<Key, ClickPuck> _keyToPuckMap = new Dictionary<Key, ClickPuck>();
        private ClickPuck _puckAwaitingKeyAssign = null;
        private int _puckCounter = 0;

        #region Win32 API for Multi-Monitor

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            InitializeKeyboardHook();
            UpdatePuckCount();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Spanne Fenster über ALLE Monitore (virtueller Desktop)
            int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        #region Keyboard Hook

        private void InitializeKeyboardHook()
        {
            _keyboardHook = new KeyboardHook();
            _keyboardHook.KeyPressed += OnGlobalKeyPressed;
        }

        private void OnGlobalKeyPressed(object sender, KeyboardHookEventArgs e)
        {
            // Im Edit-Modus: Prüfen ob wir auf Tastenzuweisung warten
            if (_isEditMode && _puckAwaitingKeyAssign != null)
            {
                Dispatcher.Invoke(() => AssignKeyToPuck(e.Key));
                e.Handled = true;
                return;
            }

            // Im Play-Modus: Klick simulieren
            if (!_isEditMode && _keyToPuckMap.ContainsKey(e.Key))
            {
                Dispatcher.Invoke(() =>
                {
                    ClickPuck puck = _keyToPuckMap[e.Key];
                    Point screenPos = GetPuckCenterScreenPosition(puck);
                    MouseSimulator.Click((int)screenPos.X, (int)screenPos.Y);
                });
                e.Handled = true;
            }
        }

        private Point GetPuckCenterScreenPosition(ClickPuck puck)
        {
            // Position des Pucks im Canvas
            double left = Canvas.GetLeft(puck);
            double top = Canvas.GetTop(puck);

            // Mitte des Pucks berechnen
            double centerX = left + puck.ActualWidth / 2;
            double centerY = top + puck.ActualHeight / 2;

            // In Bildschirmkoordinaten umwandeln
            Point screenPoint = PuckCanvas.PointToScreen(new Point(centerX, centerY));
            return screenPoint;
        }

        #endregion

        #region Mode Switching

        private void ModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = !_isEditMode;

            if (_isEditMode)
            {
                // Edit Mode
                ModeButton.Content = "▶ PLAY";
                ModeButton.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x4C, 0xAF, 0x50));
                ModeButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x8E, 0x3C));
                ModeLabel.Text = "EDIT";
                ModeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));

                AddPuckButton.Visibility = Visibility.Visible;
                SetPucksEditMode(true);
                _keyboardHook.Stop();
            }
            else
            {
                // Play Mode
                ModeButton.Content = "✏ EDIT";
                ModeButton.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0x98, 0x00));
                ModeButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x7C, 0x00));
                ModeLabel.Text = "PLAY";
                ModeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));

                AddPuckButton.Visibility = Visibility.Collapsed;
                SetPucksEditMode(false);
                _keyboardHook.Start();
            }
        }

        private void SetPucksEditMode(bool editMode)
        {
            foreach (UIElement child in PuckCanvas.Children)
            {
                if (child is ClickPuck puck)
                {
                    puck.SetEditMode(editMode);
                }
            }

            // Im Play-Mode: Gesamtes Canvas und Pucks komplett durchklickbar
            if (!editMode)
            {
                // Verstecke Pucks komplett im Play-Mode damit Klicks durchgehen
                PuckCanvas.Visibility = Visibility.Hidden;
                PuckCanvas.IsHitTestVisible = false;
            }
            else
            {
                // Edit Mode: Alles sichtbar und interaktiv
                PuckCanvas.Visibility = Visibility.Visible;
                PuckCanvas.IsHitTestVisible = true;
            }
        }

        #endregion

        #region Puck Management

        private void AddPuckButton_Click(object sender, RoutedEventArgs e)
        {
            AddNewPuck();
        }

        private void AddNewPuck()
        {
            _puckCounter++;
            ClickPuck puck = new ClickPuck();
            puck.PuckId = _puckCounter;

            // Standard-Position: Mitte des Bildschirms, versetzt
            double offsetX = (_puckCounter - 1) % 5 * 70;
            double offsetY = (_puckCounter - 1) / 5 * 70;
            Canvas.SetLeft(puck, 100 + offsetX);
            Canvas.SetTop(puck, 100 + offsetY);

            // Events registrieren
            puck.RequestKeyAssignment += Puck_RequestKeyAssignment;
            puck.RequestDelete += Puck_RequestDelete;

            PuckCanvas.Children.Add(puck);
            UpdatePuckCount();
        }

        private void Puck_RequestKeyAssignment(object sender, EventArgs e)
        {
            if (sender is ClickPuck puck)
            {
                _puckAwaitingKeyAssign = puck;
                KeyAssignDialog.Visibility = Visibility.Visible;
            }
        }

        private void Puck_RequestDelete(object sender, EventArgs e)
        {
            if (sender is ClickPuck puck)
            {
                // Aus dem Mapping entfernen
                if (puck.AssignedKey.HasValue && _keyToPuckMap.ContainsKey(puck.AssignedKey.Value))
                {
                    _keyToPuckMap.Remove(puck.AssignedKey.Value);
                }

                // Aus dem Canvas entfernen
                PuckCanvas.Children.Remove(puck);
                UpdatePuckCount();
            }
        }

        private void AssignKeyToPuck(Key key)
        {
            if (_puckAwaitingKeyAssign == null) return;

            // Alte Zuweisung entfernen falls vorhanden
            if (_puckAwaitingKeyAssign.AssignedKey.HasValue)
            {
                _keyToPuckMap.Remove(_puckAwaitingKeyAssign.AssignedKey.Value);
            }

            // Prüfen ob Taste bereits belegt
            if (_keyToPuckMap.ContainsKey(key))
            {
                // Alte Belegung vom anderen Puck entfernen
                ClickPuck oldPuck = _keyToPuckMap[key];
                oldPuck.AssignedKey = null;
                oldPuck.UpdateDisplay();
            }

            // Neue Zuweisung
            _puckAwaitingKeyAssign.AssignedKey = key;
            _puckAwaitingKeyAssign.UpdateDisplay();
            _keyToPuckMap[key] = _puckAwaitingKeyAssign;

            // Dialog schließen
            _puckAwaitingKeyAssign = null;
            KeyAssignDialog.Visibility = Visibility.Collapsed;
        }

        private void CancelKeyAssign_Click(object sender, RoutedEventArgs e)
        {
            _puckAwaitingKeyAssign = null;
            KeyAssignDialog.Visibility = Visibility.Collapsed;
        }

        private void UpdatePuckCount()
        {
            PuckCountLabel.Text = PuckCanvas.Children.Count.ToString();
        }

        #endregion

        #region Window Events

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            CleanupAndClose();
        }

        protected override void OnClosed(EventArgs e)
        {
            CleanupAndClose();
            base.OnClosed(e);
        }

        private void CleanupAndClose()
        {
            if (_keyboardHook != null)
            {
                _keyboardHook.Stop();
                _keyboardHook.Dispose();
                _keyboardHook = null;
            }
            Application.Current.Shutdown();
        }

        // Tastatureingaben für Key-Assignment abfangen wenn Dialog offen
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (_puckAwaitingKeyAssign != null)
            {
                AssignKeyToPuck(e.Key);
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }

        #endregion

        #region Save/Load Configuration

        private class PuckConfig
        {
            public int Id { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public string Key { get; set; }
        }

        private class ConfigFile
        {
            public List<PuckConfig> Pucks { get; set; }
        }

        private string GetConfigPath()
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string exeDir = Path.GetDirectoryName(exePath);
            return Path.Combine(exeDir, "clickmapper_config.json");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = new ConfigFile { Pucks = new List<PuckConfig>() };

                foreach (UIElement child in PuckCanvas.Children)
                {
                    if (child is ClickPuck puck)
                    {
                        config.Pucks.Add(new PuckConfig
                        {
                            Id = puck.PuckId,
                            X = Canvas.GetLeft(puck),
                            Y = Canvas.GetTop(puck),
                            Key = puck.AssignedKey?.ToString()
                        });
                    }
                }

                // Einfache JSON-Serialisierung ohne externe Bibliotheken
                string json = SerializeConfig(config);
                File.WriteAllText(GetConfigPath(), json, Encoding.UTF8);

                MessageBox.Show($"Konfiguration gespeichert!\n{config.Pucks.Count} Pucks", 
                    "Gespeichert", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", 
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string configPath = GetConfigPath();
                if (!File.Exists(configPath))
                {
                    MessageBox.Show("Keine gespeicherte Konfiguration gefunden.", 
                        "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string json = File.ReadAllText(configPath, Encoding.UTF8);
                ConfigFile config = DeserializeConfig(json);

                // Alle bestehenden Pucks entfernen
                PuckCanvas.Children.Clear();
                _keyToPuckMap.Clear();
                _puckCounter = 0;

                // Pucks aus Konfiguration laden
                foreach (var puckConfig in config.Pucks)
                {
                    ClickPuck puck = new ClickPuck();
                    puck.PuckId = puckConfig.Id;
                    if (_puckCounter < puckConfig.Id) _puckCounter = puckConfig.Id;

                    Canvas.SetLeft(puck, puckConfig.X);
                    Canvas.SetTop(puck, puckConfig.Y);

                    if (!string.IsNullOrEmpty(puckConfig.Key))
                    {
                        if (Enum.TryParse<Key>(puckConfig.Key, out Key key))
                        {
                            puck.AssignedKey = key;
                            _keyToPuckMap[key] = puck;
                        }
                    }

                    puck.UpdateDisplay();
                    puck.RequestKeyAssignment += Puck_RequestKeyAssignment;
                    puck.RequestDelete += Puck_RequestDelete;

                    PuckCanvas.Children.Add(puck);
                }

                UpdatePuckCount();
                MessageBox.Show($"Konfiguration geladen!\n{config.Pucks.Count} Pucks", 
                    "Geladen", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", 
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Einfache JSON-Serialisierung (ohne externe Bibliotheken)
        private string SerializeConfig(ConfigFile config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"Pucks\": [");

            for (int i = 0; i < config.Pucks.Count; i++)
            {
                var p = config.Pucks[i];
                string keyValue = p.Key != null ? $"\"{p.Key}\"" : "null";
                sb.Append($"    {{ \"Id\": {p.Id}, \"X\": {p.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"Y\": {p.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"Key\": {keyValue} }}");
                if (i < config.Pucks.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // Einfache JSON-Deserialisierung
        private ConfigFile DeserializeConfig(string json)
        {
            var config = new ConfigFile { Pucks = new List<PuckConfig>() };

            // Finde alle Puck-Objekte
            int pucksStart = json.IndexOf("[");
            int pucksEnd = json.LastIndexOf("]");
            if (pucksStart < 0 || pucksEnd < 0) return config;

            string pucksSection = json.Substring(pucksStart + 1, pucksEnd - pucksStart - 1);

            // Einfacher Parser für Puck-Objekte
            int braceDepth = 0;
            int objStart = -1;

            for (int i = 0; i < pucksSection.Length; i++)
            {
                if (pucksSection[i] == '{')
                {
                    if (braceDepth == 0) objStart = i;
                    braceDepth++;
                }
                else if (pucksSection[i] == '}')
                {
                    braceDepth--;
                    if (braceDepth == 0 && objStart >= 0)
                    {
                        string objStr = pucksSection.Substring(objStart, i - objStart + 1);
                        var puck = ParsePuckObject(objStr);
                        if (puck != null) config.Pucks.Add(puck);
                        objStart = -1;
                    }
                }
            }

            return config;
        }

        private PuckConfig ParsePuckObject(string objStr)
        {
            try
            {
                var puck = new PuckConfig();

                // Id
                int idIdx = objStr.IndexOf("\"Id\"");
                if (idIdx >= 0)
                {
                    int colonIdx = objStr.IndexOf(":", idIdx);
                    int endIdx = objStr.IndexOfAny(new[] { ',', '}' }, colonIdx);
                    string val = objStr.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim();
                    puck.Id = int.Parse(val);
                }

                // X
                int xIdx = objStr.IndexOf("\"X\"");
                if (xIdx >= 0)
                {
                    int colonIdx = objStr.IndexOf(":", xIdx);
                    int endIdx = objStr.IndexOfAny(new[] { ',', '}' }, colonIdx);
                    string val = objStr.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim();
                    puck.X = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                }

                // Y
                int yIdx = objStr.IndexOf("\"Y\"");
                if (yIdx >= 0)
                {
                    int colonIdx = objStr.IndexOf(":", yIdx);
                    int endIdx = objStr.IndexOfAny(new[] { ',', '}' }, colonIdx);
                    string val = objStr.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim();
                    puck.Y = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                }

                // Key
                int keyIdx = objStr.IndexOf("\"Key\"");
                if (keyIdx >= 0)
                {
                    int colonIdx = objStr.IndexOf(":", keyIdx);
                    int endIdx = objStr.IndexOfAny(new[] { ',', '}' }, colonIdx);
                    string val = objStr.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim();
                    if (val != "null" && val.Length > 2)
                    {
                        puck.Key = val.Trim('"');
                    }
                }

                return puck;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
