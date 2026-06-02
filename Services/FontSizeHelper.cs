using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FRCM.Services
{
    public static class FontSizeHelper
    {
        private static float _currentMultiplier = 1.0f;

        // Dictionary to track the 'baseline' font size for each control (the size at multiplier 1.0)
        private static readonly Dictionary<Control, float> _baselineSizes = new Dictionary<Control, float>();
        private static readonly Dictionary<ToolStripItem, float> _baselineItemSizes = new Dictionary<ToolStripItem, float>();

        public static float CurrentMultiplier => _currentMultiplier;

        public static void SetGlobalFontSize(float multiplier)
        {
            _currentMultiplier = multiplier;

            // Apply to all open forms - copy to a list to avoid modification exceptions
            List<Form> openForms = new List<Form>();
            foreach (Form f in Application.OpenForms)
            {
                openForms.Add(f);
            }

            //foreach (Form form in openForms)
            //{
            //    UpdateControlRecursive(form, multiplier);
            //}

            foreach (Form form in openForms)
            {
                form.Tag = null; // 🔥 IMPORTANT
                UpdateControlRecursive(form, multiplier);
            }
        }
        public static void UpdateControlRecursive(Control control, float multiplier)
        {
            if (control == null || control.Font == null) return;

            // 1. Recurse children FIRST
            // This ensures we capture the baseline font size of children 
            // BEFORE the parent's font change affects them via inheritance.
            foreach (Control child in control.Controls)
            {
                UpdateControlRecursive(child, multiplier);
            }

            // 2. Handle MenuStrips and ToolStrips
            if (control is ToolStrip ts)
            {
                foreach (ToolStripItem item in ts.Items)
                {
                    UpdateToolStripItemRecursive(item, multiplier);
                }
            }

            // 3. Capture/Update Font Size for current control
            if (!_baselineSizes.ContainsKey(control))
            {
                _baselineSizes[control] = control.Font.Size;
            }

            float newSize = _baselineSizes[control] * multiplier;

            try
            {
                if (Math.Abs(control.Font.Size - newSize) > 0.01f)
                {
                    control.Font = new Font(control.Font.FontFamily, newSize, control.Font.Style);
                }
            }
            catch { }

            // 4. Special handling for DataGridView
            if (control is DataGridView dgv)
            {
                try
                {
                    dgv.DefaultCellStyle.Font = new Font(dgv.DefaultCellStyle.Font.FontFamily, newSize, dgv.DefaultCellStyle.Font.Style);
                    dgv.ColumnHeadersDefaultCellStyle.Font = new Font(dgv.ColumnHeadersDefaultCellStyle.Font.FontFamily, newSize, dgv.ColumnHeadersDefaultCellStyle.Font.Style);
                }
                catch { }
            }
        }

        /// <summary>
        /// Calculates a balanced scaling factor based on the control's DPI and the screen's resolution.
        /// This ensures the UI is readable on large 4K TVs while remaining compact on standard laptops.
        /// </summary>
        /// <param name="control">The control (usually a Form) to calculate scaling for.</param>
        /// <returns>A float representing the recommended font scaling multiplier.</returns>
        public static float GetAutoScaling(Control control)
        {
            if (control == null) return 1.0f;

            float scalingFactor = control.DeviceDpi / 96f;
            var screen = Screen.FromControl(control);
            float fontScaling = scalingFactor;

            // Detection for "Small" vs "Large" screens based on logical resolution
            if (screen.Bounds.Width <= 1920)
            {
                // For laptops and standard monitors, dampen the scaling boost to prevent the UI from feeling cramped.
                fontScaling = 1.0f + (scalingFactor - 1.0f) * 0.50f;
            }
            // else if (screen.Bounds.Width >= 3840) { ... future 4K optimizations ... }

            return fontScaling;
        }

        private static void UpdateToolStripItemRecursive(ToolStripItem item, float multiplier)
        {
            if (item == null || item.Font == null) return;

            // Handle dropdown items first
            if (item is ToolStripDropDownItem dd)
            {
                foreach (ToolStripItem sub in dd.DropDownItems)
                {
                    UpdateToolStripItemRecursive(sub, multiplier);
                }
            }

            if (!_baselineItemSizes.ContainsKey(item))
            {
                _baselineItemSizes[item] = item.Font.Size;
            }

            float newSize = _baselineItemSizes[item] * multiplier;

            try
            {
                if (Math.Abs(item.Font.Size - newSize) > 0.01f)
                {
                    item.Font = new Font(item.Font.FontFamily, newSize, item.Font.Style);
                }
            }
            catch { }
        }
    }
}
