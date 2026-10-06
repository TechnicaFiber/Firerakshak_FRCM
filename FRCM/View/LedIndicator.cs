using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FRCM.View
{
    /// <summary>
    /// Custom LED indicator control with better graphics and 3D effect
    /// </summary>
    [DesignerCategory("Code")]
    [ToolboxItem(true)]
    public class LedIndicator : Control
    {
        private Color _ledColor = Color.Gray;
        private bool _isOn = false;
        private string _label = string.Empty;

        public LedIndicator()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(90, 45);  // Smaller size
            BackColor = Color.Transparent;
        }

        /// <summary>
        /// Gets or sets the LED color when ON
        /// </summary>
        public Color LedColor
        {
            get => _ledColor;
            set
            {
                _ledColor = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets whether the LED is ON or OFF
        /// </summary>
        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn != value)
                {
                    _isOn = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the label text displayed below the LED
        /// </summary>
        public string Label
        {
            get => _label;
            set
            {
                _label = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Define a scaling factor based on DPI. 96 DPI is the baseline.
            float dpiScale = g.DpiX / 96f;

            // Calculate font height for the label dynamically.
            // This ensures we always reserve enough space regardless of DPI or font choice.
            int fontHeight = this.Font.Height;
            int labelPadding = (int)(2 * dpiScale);
            int labelHeight = string.IsNullOrEmpty(_label) ? 0 : fontHeight + labelPadding;

            // Calculate LED circle position and size proportionally.
            // We reserve the bottom area for the label and a small margin.
            int topMargin = (int)(4 * dpiScale);
            int availableLedHeight = Height - labelHeight - topMargin - (int)(2 * dpiScale);
            int availableLedWidth = Width - (int)(4 * dpiScale);
            
            int ledSize = Math.Min(availableLedWidth, availableLedHeight);
            
            // Center the LED horizontally.
            int ledX = (Width - Math.Max(0, ledSize)) / 2;
            int ledY = topMargin;

            if (ledSize > 0)
            {
                Rectangle ledRect = new Rectangle(ledX, ledY, ledSize, ledSize);

                // Determine current color
                Color currentColor = _isOn ? _ledColor : Color.FromArgb(80, 80, 80);

                // Scale pen widths and offsets based on the LED size.
                float borderPenWidth = Math.Max(1f, ledSize / 14f);
                int glowOffset = (int)(ledSize * 0.1f);
                
                // Draw outer border (dark ring)
                using (Pen borderPen = new Pen(Color.FromArgb(60, 60, 60), borderPenWidth))
                {
                    g.DrawEllipse(borderPen, ledRect);
                }

                // Draw LED with gradient for 3D effect
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(ledRect);

                    Color lightColor = _isOn ? ControlPaint.Light(currentColor) : Color.FromArgb(100, 100, 100);
                    Color darkColor = _isOn ? ControlPaint.Dark(currentColor) : Color.FromArgb(60, 60, 60);

                    using (LinearGradientBrush gradientBrush = new LinearGradientBrush(
                        ledRect,
                        lightColor,
                        darkColor,
                        LinearGradientMode.ForwardDiagonal))
                    {
                        g.FillPath(gradientBrush, path);
                    }

                    // Add highlight for glossy effect
                    if (_isOn)
                    {
                        Rectangle highlightRect = new Rectangle(
                            ledRect.X + ledRect.Width / 4,
                            ledRect.Y + ledRect.Height / 8,
                            ledRect.Width / 3,
                            ledRect.Height / 3);

                        using (GraphicsPath highlightPath = new GraphicsPath())
                        {
                            highlightPath.AddEllipse(highlightRect);

                            using (PathGradientBrush highlightBrush = new PathGradientBrush(highlightPath))
                            {
                                highlightBrush.CenterColor = Color.FromArgb(180, Color.White);
                                highlightBrush.SurroundColors = new[] { Color.FromArgb(0, Color.White) };
                                g.FillPath(highlightBrush, highlightPath);
                            }
                        }
                    }

                    // Add glow effect when ON
                    if (_isOn)
                    {
                        using (GraphicsPath glowPath = new GraphicsPath())
                        {
                            Rectangle glowRect = ledRect;
                            glowRect.Inflate(glowOffset, glowOffset);
                            glowPath.AddEllipse(glowRect);

                            using (PathGradientBrush glowBrush = new PathGradientBrush(glowPath))
                            {
                                glowBrush.CenterColor = Color.FromArgb(100, currentColor);
                                glowBrush.SurroundColors = new[] { Color.FromArgb(0, currentColor) };
                                g.FillPath(glowBrush, glowPath);
                            }
                        }
                    }
                }
            }

            // Draw label text using the control's ForeColor and Font
            if (!string.IsNullOrEmpty(_label))
            {
                // Position text at the very bottom of the control
                int textY = Height - fontHeight - (int)(1 * dpiScale);
                Rectangle labelRect = new Rectangle(0, textY, Width, fontHeight + (int)(2 * dpiScale));

                using (StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Near,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                {
                    using (Brush textBrush = new SolidBrush(this.ForeColor))
                    {
                        g.DrawString(_label, this.Font, textBrush, labelRect, format);
                    }
                }
            }
        }
    }
}
