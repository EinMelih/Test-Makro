using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClickMapper.Controls
{
    /// <summary>
    /// Ein verschiebbares Klick-Ziel (Puck), das einer Taste zugewiesen werden kann.
    /// </summary>
    public partial class ClickPuck : UserControl
    {
        #region Fields

        private bool _isDragging = false;
        private Point _dragStartPoint;
        private Point _elementStartPosition;
        private bool _isEditMode = true;

        #endregion

        #region Properties

        /// <summary>
        /// Die zugewiesene Taste für diesen Puck.
        /// </summary>
        public Key? AssignedKey { get; set; }

        /// <summary>
        /// Eindeutige ID des Pucks.
        /// </summary>
        public int PuckId { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// Wird ausgelöst, wenn der Benutzer eine neue Taste zuweisen möchte.
        /// </summary>
        public event EventHandler RequestKeyAssignment;

        /// <summary>
        /// Wird ausgelöst, wenn der Benutzer den Puck löschen möchte.
        /// </summary>
        public event EventHandler RequestDelete;

        #endregion

        #region Constructor

        public ClickPuck()
        {
            InitializeComponent();
            UpdateDisplay();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Aktualisiert die Anzeige basierend auf der zugewiesenen Taste.
        /// </summary>
        public void UpdateDisplay()
        {
            if (AssignedKey.HasValue)
            {
                KeyLabel.Text = GetKeyDisplayName(AssignedKey.Value);
            }
            else
            {
                KeyLabel.Text = "?";
            }

            IdLabel.Text = "#" + PuckId.ToString();
        }

        /// <summary>
        /// Setzt den Edit-Modus des Pucks.
        /// </summary>
        public void SetEditMode(bool editMode)
        {
            _isEditMode = editMode;

            if (editMode)
            {
                // Edit Mode: Sichtbar und interaktiv
                Opacity = 1.0;
                IsHitTestVisible = true;
                Cursor = Cursors.Hand;
            }
            else
            {
                // Play Mode: Halbtransparent und nicht interaktiv
                Opacity = 0.3;
                IsHitTestVisible = false;
            }
        }

        #endregion

        #region Private Methods

        private string GetKeyDisplayName(Key key)
        {
            // Spezielle Tastennamen für bessere Lesbarkeit
            switch (key)
            {
                case Key.D0: return "0";
                case Key.D1: return "1";
                case Key.D2: return "2";
                case Key.D3: return "3";
                case Key.D4: return "4";
                case Key.D5: return "5";
                case Key.D6: return "6";
                case Key.D7: return "7";
                case Key.D8: return "8";
                case Key.D9: return "9";
                case Key.NumPad0: return "N0";
                case Key.NumPad1: return "N1";
                case Key.NumPad2: return "N2";
                case Key.NumPad3: return "N3";
                case Key.NumPad4: return "N4";
                case Key.NumPad5: return "N5";
                case Key.NumPad6: return "N6";
                case Key.NumPad7: return "N7";
                case Key.NumPad8: return "N8";
                case Key.NumPad9: return "N9";
                case Key.Space: return "␣";
                case Key.Enter: return "↵";
                case Key.Escape: return "Esc";
                case Key.Tab: return "Tab";
                case Key.Back: return "←";
                case Key.Delete: return "Del";
                case Key.Left: return "◄";
                case Key.Right: return "►";
                case Key.Up: return "▲";
                case Key.Down: return "▼";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.OemMinus: return "-";
                case Key.OemPlus: return "+";
                default:
                    string name = key.ToString();
                    // Entferne "Oem" Prefix wenn vorhanden
                    if (name.StartsWith("Oem"))
                    {
                        name = name.Substring(3);
                    }
                    return name.Length > 3 ? name.Substring(0, 3) : name;
            }
        }

        #endregion

        #region Mouse Events

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!_isEditMode) return;

            _isDragging = true;
            _dragStartPoint = e.GetPosition(Parent as UIElement);
            _elementStartPosition = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));

            // Wenn NaN, auf 0 setzen
            if (double.IsNaN(_elementStartPosition.X)) _elementStartPosition.X = 0;
            if (double.IsNaN(_elementStartPosition.Y)) _elementStartPosition.Y = 0;

            CaptureMouse();

            // Visuelles Feedback
            MainEllipse.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x42, 0xA5, 0xF5));
            GlowEllipse.Stroke = new SolidColorBrush(Color.FromArgb(0x80, 0x21, 0x96, 0xF3));

            e.Handled = true;
            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_isDragging || !_isEditMode) return;

            Point currentPos = e.GetPosition(Parent as UIElement);
            double deltaX = currentPos.X - _dragStartPoint.X;
            double deltaY = currentPos.Y - _dragStartPoint.Y;

            double newLeft = _elementStartPosition.X + deltaX;
            double newTop = _elementStartPosition.Y + deltaY;

            // Grenzen des Canvas beachten
            Canvas parentCanvas = Parent as Canvas;
            if (parentCanvas != null)
            {
                newLeft = Math.Max(0, Math.Min(newLeft, parentCanvas.ActualWidth - ActualWidth));
                newTop = Math.Max(0, Math.Min(newTop, parentCanvas.ActualHeight - ActualHeight));
            }

            Canvas.SetLeft(this, newLeft);
            Canvas.SetTop(this, newTop);

            e.Handled = true;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();

                // Visuelles Feedback zurücksetzen
                MainEllipse.Fill = new SolidColorBrush(Color.FromArgb(0xDD, 0x21, 0x96, 0xF3));
                GlowEllipse.Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0x21, 0x96, 0xF3));
            }

            e.Handled = true;
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            if (!_isEditMode) return;

            // Rechtsklick = Taste zuweisen
            RequestKeyAssignment?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            base.OnMouseRightButtonUp(e);
        }

        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            if (!_isEditMode) return;

            // Doppelklick = Löschen
            if (e.ChangedButton == MouseButton.Left)
            {
                RequestDelete?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
            base.OnMouseDoubleClick(e);
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            if (!_isEditMode) return;

            // Hover-Effekt
            GlowEllipse.Opacity = 0.8;
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            if (!_isEditMode) return;

            // Hover-Effekt zurücksetzen
            GlowEllipse.Opacity = 0.5;
            base.OnMouseLeave(e);
        }

        #endregion
    }
}
