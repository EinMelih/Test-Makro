using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        // Spawn Position
        private double _spawnX = 100;
        private double _spawnY = 100;
        private bool _isSettingSpawnPosition = false;

        // Control Panel dragging
        private bool _isDraggingPanel = false;
        private Point _panelDragStart;

        // Profile directory
        private string _profileDirectory;

        #region Win32 API for Multi-Monitor

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
            InitializeProfileDirectory();
            UpdatePuckCount();
            RefreshProfileList();
        }

        private void InitializeProfileDirectory()
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string exeDir = Path.GetDirectoryName(exePath);
            _profileDirectory = Path.Combine(exeDir, "profiles");
            
            if (!Directory.Exists(_profileDirectory))
            {
                Directory.CreateDirectory(_profileDirectory);
            }
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

            // Spawn Marker initial positionieren
            UpdateSpawnMarker();
        }

        #region Control Panel Dragging

        private void ControlPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Nur draggen wenn nicht auf einem Button geklickt
            if (e.Source is Button) return;

            _isDraggingPanel = true;
            _panelDragStart = e.GetPosition(MainGrid);
            ControlPanel.CaptureMouse();
            e.Handled = true;
        }

        private void ControlPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingPanel) return;

            Point currentPos = e.GetPosition(MainGrid);
            double deltaX = currentPos.X - _panelDragStart.X;
            double deltaY = currentPos.Y - _panelDragStart.Y;

            ControlPanelTransform.X += deltaX;
            ControlPanelTransform.Y += deltaY;

            _panelDragStart = currentPos;
            e.Handled = true;
        }

        private void ControlPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingPanel)
            {
                _isDraggingPanel = false;
                ControlPanel.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        #endregion

        #region Spawn Position

        private void SetSpawnButton_Click(object sender, RoutedEventArgs e)
        {
            _isSettingSpawnPosition = true;
            SpawnModeHint.Visibility = Visibility.Visible;
            
            // Temporär einen klickbaren Hintergrund setzen (fast unsichtbar aber klickbar)
            MainGrid.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            MainGrid.Cursor = Cursors.Cross;
            MainGrid.MouseLeftButtonDown += MainGrid_SetSpawnClick;
        }

        private void MainGrid_SetSpawnClick(object sender, MouseButtonEventArgs e)
        {
            if (!_isSettingSpawnPosition) return;
            
            // Ignoriere Klicks auf das Control Panel
            if (IsClickOnControlPanel(e)) return;

            Point pos = e.GetPosition(PuckCanvas);
            _spawnX = pos.X;
            _spawnY = pos.Y;

            UpdateSpawnMarker();
            EndSpawnPositionMode();
            e.Handled = true;
        }

        private bool IsClickOnControlPanel(MouseButtonEventArgs e)
        {
            Point clickPos = e.GetPosition(ControlPanel);
            return clickPos.X >= 0 && clickPos.Y >= 0 
                && clickPos.X <= ControlPanel.ActualWidth 
                && clickPos.Y <= ControlPanel.ActualHeight;
        }

        private void UpdateSpawnMarker()
        {
            Canvas.SetLeft(SpawnMarker, _spawnX - 15);
            Canvas.SetTop(SpawnMarker, _spawnY - 15);
            SpawnMarker.Visibility = Visibility.Visible;
        }

        private void EndSpawnPositionMode()
        {
            _isSettingSpawnPosition = false;
            SpawnModeHint.Visibility = Visibility.Collapsed;
            
            // Hintergrund wieder transparent machen
            MainGrid.Background = Brushes.Transparent;
            MainGrid.Cursor = Cursors.Arrow;
            MainGrid.MouseLeftButtonDown -= MainGrid_SetSpawnClick;
        }

        #endregion

        #region Keyboard Hook

        private void InitializeKeyboardHook()
        {
            _keyboardHook = new KeyboardHook();
            _keyboardHook.KeyPressed += OnGlobalKeyPressed;
        }

        private void OnGlobalKeyPressed(object sender, KeyboardHookEventArgs e)
        {
            // ESC zum Abbrechen der Spawn-Position-Einstellung
            if (_isSettingSpawnPosition && e.Key == Key.Escape)
            {
                Dispatcher.Invoke(() => EndSpawnPositionMode());
                e.Handled = true;
                return;
            }

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
            double left = Canvas.GetLeft(puck);
            double top = Canvas.GetTop(puck);
            double centerX = left + puck.ActualWidth / 2;
            double centerY = top + puck.ActualHeight / 2;
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
                SetSpawnButton.Visibility = Visibility.Visible;
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
                SetSpawnButton.Visibility = Visibility.Collapsed;
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

            if (!editMode)
            {
                PuckCanvas.Visibility = Visibility.Hidden;
                PuckCanvas.IsHitTestVisible = false;
                SpawnMarker.Visibility = Visibility.Collapsed;
            }
            else
            {
                PuckCanvas.Visibility = Visibility.Visible;
                PuckCanvas.IsHitTestVisible = true;
                SpawnMarker.Visibility = Visibility.Visible;
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

            // Neue Pucks an der Spawn-Position erstellen (mit leichtem Offset)
            int offset = (PuckCanvas.Children.Count % 10) * 35;
            Canvas.SetLeft(puck, _spawnX + (offset % 175));
            Canvas.SetTop(puck, _spawnY + (offset / 175) * 35);

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
                if (puck.AssignedKey.HasValue && _keyToPuckMap.ContainsKey(puck.AssignedKey.Value))
                {
                    _keyToPuckMap.Remove(puck.AssignedKey.Value);
                }
                PuckCanvas.Children.Remove(puck);
                UpdatePuckCount();
            }
        }

        private void AssignKeyToPuck(Key key)
        {
            if (_puckAwaitingKeyAssign == null) return;

            if (_puckAwaitingKeyAssign.AssignedKey.HasValue)
            {
                _keyToPuckMap.Remove(_puckAwaitingKeyAssign.AssignedKey.Value);
            }

            if (_keyToPuckMap.ContainsKey(key))
            {
                ClickPuck oldPuck = _keyToPuckMap[key];
                oldPuck.AssignedKey = null;
                oldPuck.UpdateDisplay();
            }

            _puckAwaitingKeyAssign.AssignedKey = key;
            _puckAwaitingKeyAssign.UpdateDisplay();
            _keyToPuckMap[key] = _puckAwaitingKeyAssign;

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

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // ESC zum Abbrechen der Spawn-Position-Einstellung
            if (_isSettingSpawnPosition && e.Key == Key.Escape)
            {
                EndSpawnPositionMode();
                e.Handled = true;
                return;
            }

            if (_puckAwaitingKeyAssign != null)
            {
                AssignKeyToPuck(e.Key);
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }

        #endregion

        #region Save/Load Configuration (Multiple Profiles)

        private class PuckConfig
        {
            public int Id { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public string Key { get; set; }
        }

        private class ConfigFile
        {
            public double SpawnX { get; set; }
            public double SpawnY { get; set; }
            public List<PuckConfig> Pucks { get; set; }
        }

        private string GetProfilePath(string profileName)
        {
            string safeName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_profileDirectory, safeName + ".json");
        }

        private void RefreshProfileList()
        {
            ProfileList.Items.Clear();
            
            if (Directory.Exists(_profileDirectory))
            {
                var files = Directory.GetFiles(_profileDirectory, "*.json");
                foreach (var file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    ProfileList.Items.Add(name);
                }
            }
        }

        private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileList.SelectedItem != null)
            {
                ProfileNameBox.Text = ProfileList.SelectedItem.ToString();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string profileName = ProfileNameBox.Text.Trim();
                if (string.IsNullOrEmpty(profileName))
                {
                    MessageBox.Show("Bitte einen Profilnamen eingeben.", "Hinweis", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var config = new ConfigFile 
                { 
                    SpawnX = _spawnX,
                    SpawnY = _spawnY,
                    Pucks = new List<PuckConfig>() 
                };

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

                string json = SerializeConfig(config);
                File.WriteAllText(GetProfilePath(profileName), json, Encoding.UTF8);

                RefreshProfileList();
                MessageBox.Show($"Profil '{profileName}' gespeichert!\n{config.Pucks.Count} Pucks", 
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
                string profileName = ProfileNameBox.Text.Trim();
                if (string.IsNullOrEmpty(profileName))
                {
                    MessageBox.Show("Bitte einen Profilnamen eingeben oder auswählen.", 
                        "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string configPath = GetProfilePath(profileName);
                if (!File.Exists(configPath))
                {
                    MessageBox.Show($"Profil '{profileName}' nicht gefunden.", 
                        "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string json = File.ReadAllText(configPath, Encoding.UTF8);
                ConfigFile config = DeserializeConfig(json);

                // Spawn-Position laden
                _spawnX = config.SpawnX > 0 ? config.SpawnX : 100;
                _spawnY = config.SpawnY > 0 ? config.SpawnY : 100;
                UpdateSpawnMarker();

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
                MessageBox.Show($"Profil '{profileName}' geladen!\n{config.Pucks.Count} Pucks", 
                    "Geladen", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", 
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string SerializeConfig(ConfigFile config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"SpawnX\": {config.SpawnX.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"SpawnY\": {config.SpawnY.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
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

        private ConfigFile DeserializeConfig(string json)
        {
            var config = new ConfigFile { Pucks = new List<PuckConfig>() };

            // SpawnX
            int spawnXIdx = json.IndexOf("\"SpawnX\"");
            if (spawnXIdx >= 0)
            {
                int colonIdx = json.IndexOf(":", spawnXIdx);
                int endIdx = json.IndexOfAny(new[] { ',', '}' }, colonIdx);
                string val = json.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim();
                double spawnX;
                if (double.TryParse(val, System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out spawnX))
                {
                    config.SpawnX = spawnX;
                }
            }

            // SpawnY
            int spawnYIdx = json.IndexOf("\"SpawnY\"");
            if (spawnYIdx >= 0)
            {
                int colonIdx = json.IndexOf(":", spawnYIdx);
                int endIdx = json.IndexOfAny(new[] { ',', '}' }, colonIdx);
                string val = json.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim();
                double spawnY;
                if (double.TryParse(val, System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out spawnY))
                {
                    config.SpawnY = spawnY;
                }
            }

            // Pucks array
            int pucksStart = json.IndexOf("[");
            int pucksEnd = json.LastIndexOf("]");
            if (pucksStart < 0 || pucksEnd < 0) return config;

            string pucksSection = json.Substring(pucksStart + 1, pucksEnd - pucksStart - 1);

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
