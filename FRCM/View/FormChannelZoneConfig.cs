using FRCM.Services;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FRCM
{
    public partial class FormChannelZoneConfig : Form
    {
        private TreeView? treeChannels;
        private Panel? panelRight;

        private readonly IConfigurationService _configurationService;
        private readonly string _loginRole;
        private readonly ChannelClient _channelClient;

        //private ContextMenuStrip? treeContextMenu;
        //private TreeNode? clickedNode;

        public FormChannelZoneConfig(
            IConfigurationService configurationService,
            string loginRole,
            ChannelClient channelClient)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            this._loginRole = loginRole ?? "User";
            this._channelClient = channelClient ?? throw new ArgumentNullException(nameof(channelClient));

            InitializeForm();
            BuildLayout();
            //InitializeContextMenu();
            LoadTreeView();

        }

        private void InitializeForm()
        {
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            float scalingFactor = this.DeviceDpi / 96f;

            this.Text = "Channel & Zone Configuration";
            this.Size = new Size((int)(850 * scalingFactor), (int)(600 * scalingFactor));
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        private void BuildLayout()
        {
            float scalingFactor = this.DeviceDpi / 96f;

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };
            this.Controls.Add(split);

            split.SplitterDistance = (int)(160 * scalingFactor);

            treeChannels = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 7f * scalingFactor),
                HideSelection = false,
                ItemHeight = (int)(20 * scalingFactor)
            };
            treeChannels.AfterSelect += TreeChannels_AfterSelect;
            split.Panel1.Controls.Add(treeChannels);

            panelRight = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            split.Panel2.Controls.Add(panelRight);

        }

        private string GetNewExcelFilePath()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"FRCM_Channel_{timestamp}.xlsx";

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                fileName
            );
        }
        public void ExportAllChannels()
        {
            string filePath = GetNewExcelFilePath();
            FileInfo fileInfo = new FileInfo(filePath);

            using (ExcelPackage package = new ExcelPackage())
            {
                // Sort channels by ChannelId to ensure sequential order in Excel
                foreach (var kvp in _configurationService.GetChannels().OrderBy(c => c.Value.ChannelId))
                {
                    AddChannelSheet(package, kvp.Key);
                }

                package.SaveAs(fileInfo);
            }

            MessageBox.Show("Excel exported with 8 sheets successfully!");
        }
        public void AddChannelSheet(ExcelPackage package, string channelKey)
        {
            var cfg = _configurationService.GetChannelConfig(channelKey);
            if (cfg == null)
            {
                MessageBox.Show($"Configuration for channel '{channelKey}' not found.");
                return;
            }

            var ws = package.Workbook.Worksheets.Add(channelKey);

            int row = 1;

            // Channel Info Header
            ws.Cells[row, 1].Value = "Channel Info";
            ws.Cells[row, 1].Style.Font.Bold = true;
            row += 2;

            ws.Cells[row, 1].Value = "Channel ID"; ws.Cells[row, 2].Value = cfg.ChannelId; row++;
            ws.Cells[row, 1].Value = "Name"; ws.Cells[row, 2].Value = cfg.Name; row++;
            ws.Cells[row, 1].Value = "Length"; ws.Cells[row, 2].Value = cfg.Length; row++;
            ws.Cells[row, 1].Value = "Enabled"; ws.Cells[row, 2].Value = cfg.IsEnabled; row++;
            ws.Cells[row, 1].Value = "Scan Period"; ws.Cells[row, 2].Value = cfg.ScanPeriod; row += 2;

            // Zone Header
            ws.Cells[row, 1].Value = "Zones";
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;

            ws.Cells[row, 1].Value = "Zone ID";
            ws.Cells[row, 2].Value = "Zone Name";
            ws.Cells[row, 3].Value = "Start";
            ws.Cells[row, 4].Value = "End";
            ws.Row(row).Style.Font.Bold = true;
            row++;

            // Zones
            var zones = _configurationService.GetZoneConfig(channelKey);
            if (zones != null)
            {
                foreach (var z in zones)
                {
                    ws.Cells[row, 1].Value = z.ZoneId;
                    ws.Cells[row, 2].Value = z.Name;
                    ws.Cells[row, 3].Value = z.StartPoint;
                    ws.Cells[row, 4].Value = z.EndPoint;
                    row++;
                }
            }

            ws.Cells.AutoFitColumns();
        }

        private void LoadTreeView()
        {
            if (treeChannels == null) return;
            treeChannels.Nodes.Clear();

            var allChannels = _configurationService.GetChannels();
            var allZones = _configurationService.GetZones();

            // Sort channels by ChannelId to ensure sequential display
            foreach (var kvp in allChannels.OrderBy(c => c.Value.ChannelId))
            {
                string channelKey = kvp.Key; // e.g., "Channel 1"
                var cfg = kvp.Value;

                TreeNode chNode = new TreeNode(channelKey);

                if (allZones.TryGetValue(channelKey, out var zones) && zones.Any())
                {
                    //foreach (var z in zones)
                    //{
                    //    chNode.Nodes.Add(z.Name);
                    //}
                    foreach (var z in zones.OrderBy(z => z.ZoneId))
                    {
                        chNode.Nodes.Add(z.Name);
                    }

                }
                else
                {
                    chNode.Nodes.Add("No zones configured");
                }

                treeChannels.Nodes.Add(chNode);
            }

            treeChannels.ExpandAll();
        }

        private void TreeChannels_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            SaveCurrentState();
            string selected = e.Node.Text;

            // 🔥 Channel clicked
            if (selected.StartsWith("Channel"))
            {
                // Extract channel number safely
                if (int.TryParse(selected.Replace("Channel", "").Trim(), out int channelId))
                {
                    Console.WriteLine($"[UI] Channel selected: {channelId}");

                    // 🔥 Notify Main Form (Form1)
                    if (Application.OpenForms["Form1"] is Form1 mainForm)
                    {
                        mainForm.OnChannelSelected(channelId);
                    }

                    OpenChannelConfig(selected);
                }

                return;
            }

            // No zones configured
            if (selected == "No zones configured")
            {
                ShowNoZonesMessage(e.Node.Parent.Text);
                return;
            }

            // 🔥 Zone clicked
            if (e.Node.Parent != null)
            {
                string channel = e.Node.Parent.Text;
                string zone = e.Node.Text;

                OpenZoneConfig(channel, zone);
            }
        }
        private void SaveCurrentState()
        {
            if (panelRight == null || panelRight.Controls.Count == 0)
                return;

            var currentCtrl = panelRight.Controls[0];
            if (currentCtrl is ChannelControl chCtrl)
            {
                chCtrl.SaveToStaged();
            }
            else if (currentCtrl is ZoneControl zCtrl)
            {
                zCtrl.SaveToStaged();
            }
        }
        private void ShowNoZonesMessage(string channelName)
        {
            panelRight.Controls.Clear();

            var lbl = new Label
            {
                Text = $"There are no zones configured for {channelName}.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12, FontStyle.Italic),
                ForeColor = Color.Gray
            };

            panelRight.Controls.Add(lbl);
        }


        private void OpenChannelConfig(string channelName)
        {
            panelRight.Controls.Clear();

            var ctrl = new ChannelControl(
                channelName, _configurationService, _loginRole, _channelClient); // Pass _configurationService

            ctrl.Dock = DockStyle.Fill;

            ctrl.ChannelSaved += (s, e) =>
            {
                SyncZonesWithChannelConfig(channelName);
                LoadTreeView();
            };

            panelRight.Controls.Add(ctrl);
        }

        private void OpenZoneConfig(string channelName, string zoneName)
        {
            panelRight.Controls.Clear();

            var ctrl = new ZoneControl(
                channelName, zoneName, _channelClient, _configurationService, _loginRole); // Pass _configurationService + loginRole


            ctrl.Dock = DockStyle.Fill;

            //ctrl.ZonesSaved += (s, e) =>
            //{
            //    LoadTreeView();
            //    RefreshChannelControlIfOpen(channelName);
            //};

            ctrl.ZonesSaved += (s, e) =>
            {
                LoadTreeView();
                // Preserve the currently selected zone instead of selecting first zone
                SelectZoneByName(channelName, zoneName);
            };

            panelRight.Controls.Add(ctrl);
        }
        //public async Task ApplyAllStagedChangesAsync(bool skipSaveCurrentState = false)
        //{
        //    if (!_loginRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        //    {
        //        MessageBox.Show("Access Denied. Only Admin users can modify configuration.",
        //            "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //        return;
        //    }

        //    // Skip when called from a button click that already saved staged data,
        //    // to avoid the second SaveToStaged() overwriting with stale grid state
        //    if (!skipSaveCurrentState)
        //        SaveCurrentState();

        //    var stagedChannels = _configurationService.GetStagedChannels().ToList();
        //    var stagedZonesDict = _configurationService.GetStagedZones().ToList();

        //    if (stagedChannels.Count == 0 && stagedZonesDict.Count == 0)
        //    {
        //        MessageBox.Show("No changes to apply.");
        //        return;
        //    }

        //    try
        //    {
        //        // 1. Apply Channels (includes zone additions/deletions)
        //        foreach (var kvp in stagedChannels)
        //        {
        //            await PushChannelConfigToHardwareAsync(kvp.Key, kvp.Value);
        //            _configurationService.GetChannels()[kvp.Key] = kvp.Value;
        //            _configurationService.GetStagedChannels().TryRemove(kvp.Key, out _);

        //            // 🔥 IMPORTANT: Remove from stagedZones so Loop 2 doesn't overwrite the additions
        //            _configurationService.GetStagedZones().TryRemove(kvp.Key, out _);
        //        }

        //        // 2. Apply Zones (for channels where only zones changed)
        //        // Re-fetch since we might have removed items in Loop 1
        //        var remainingStagedZones = _configurationService.GetStagedZones().ToList();
        //        await Task.Delay(300);
        //        foreach (var kvp in remainingStagedZones)
        //        {
        //            string channelName = kvp.Key;
        //            var zones = kvp.Value;

        //            // Push batch to FRMC
        //            bool ok = await _channelClient.SetChannelZonesPreserveIdsAsync(channelName, zones);
        //            if (ok)
        //            {
        //                _configurationService.GetZones()[channelName] = zones;
        //                _configurationService.GetStagedZones().TryRemove(channelName, out _);
        //            }
        //        }

        //        MessageBox.Show("All changes applied to hardware successfully.");

        //        // 🔥 Clear main form cache to prevent stale data for new/re-added zones
        //        if (Application.OpenForms["Form1"] is Form1 mainForm)
        //        {
        //            mainForm.ResetZoneStateCache();
        //        }

        //        LoadTreeView();
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Error applying changes: {ex.Message}");
        //    }
        //}
        public async Task ApplyAllStagedChangesAsync(bool skipSaveCurrentState = false)
        {
            if (!_loginRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Access Denied. Only Admin users can modify configuration.",
                    "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Save current UI state if needed
            if (!skipSaveCurrentState)
                SaveCurrentState();

            var stagedChannels = _configurationService.GetStagedChannels().ToList();
            var stagedZonesDict = _configurationService.GetStagedZones().ToList();

            if (stagedChannels.Count == 0 && stagedZonesDict.Count == 0)
            {
                MessageBox.Show("No changes to apply.");
                return;
            }

            try
            {
                // 🔥 Track channels already processed
                var processedChannels = new HashSet<string>();

                // ---------------- 1. APPLY CHANNELS ----------------
                foreach (var kvp in stagedChannels)
                {
                    await PushChannelConfigToHardwareAsync(kvp.Key, kvp.Value);

                    processedChannels.Add(kvp.Key); // 🔥 mark processed

                    _configurationService.GetChannels()[kvp.Key] = kvp.Value;
                    _configurationService.GetStagedChannels().TryRemove(kvp.Key, out _);

                    // 🔥 Remove zones for this channel to prevent duplicate apply
                    //_configurationService.GetStagedZones().TryRemove(kvp.Key, out _);
                }

                // ---------------- 2. APPLY REMAINING ZONES ----------------
                var remainingStagedZones = _configurationService.GetStagedZones().ToList();

                // 🔥 Give FRMC time before applying zones
                await Task.Delay(300);

                foreach (var kvp in remainingStagedZones)
                {
                    // 🔥 Skip zones already handled in channel loop
                    if (processedChannels.Contains(kvp.Key))
                        continue;

                    string channelName = kvp.Key;
                    var zones = kvp.Value;

                    bool ok = await _channelClient.SetChannelZonesPreserveIdsAsync(channelName, zones);
                    if (ok)
                    {
                        _configurationService.GetZones()[channelName] = zones;
                        _configurationService.GetStagedZones().TryRemove(channelName, out _);
                    }
                }

                MessageBox.Show("All changes applied to hardware successfully.");

                // 🔥 Clear main form cache
                if (Application.OpenForms["Form1"] is Form1 mainForm)
                {
                    mainForm.ResetZoneStateCache();
                }

                LoadTreeView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying changes: {ex.Message}");
            }
        }

        //private async Task PushChannelConfigToHardwareAsync(string channelName, ChannelConfigurationInfo cfg)
        //{
        //    // Zone sync logic (from ChannelControl)
        //    var zonesCache = _configurationService.GetZones().GetOrAdd(channelName, new List<ZoneInfo>());
        //    int oldCount = zonesCache.Count;
        //    int newCount = cfg.NumberOfZones;

        //    if (newCount > oldCount)
        //    {
        //        int channelId = ExtractChannelIdFromName(channelName);

        //        // Find the highest Zone ID ever used in the current list
        //        int nextZoneId = zonesCache.Any() ? zonesCache.Max(z => z.ZoneId) + 1 : 1;

        //        // Also ensure the Name number starts after the highest name number found
        //        int nextZoneNameNumber = GetNextZoneNumberFromExistingNames(zonesCache);

        //        var newZones = new List<ZoneInfo>();
        //        for (int i = oldCount; i < newCount; i++)
        //        {
        //            var newZone = new ZoneInfo
        //            {
        //                ChannelId = channelId,
        //                ZoneId = nextZoneId,
        //                Name = $"Zone {nextZoneNameNumber}",
        //                Enabled = false
        //            };

        //            newZones.Add(newZone);
        //            nextZoneId++;
        //            nextZoneNameNumber++;
        //        }
        //        var allZones = zonesCache.Concat(newZones).ToList();
        //        await _channelClient.SetChannelZonesPreserveIdsAsync(channelName, allZones);
        //        zonesCache.AddRange(newZones);

        //        // Clear staged zones so UI refreshes from the updated global cache
        //        _configurationService.GetStagedZones().TryRemove(channelName, out _);
        //    }
        //    else if (newCount < oldCount)
        //    {
        //        var sortedZones = zonesCache.OrderBy(z => z.ZoneId).ToList();
        //        var zonesToKeep = sortedZones.Take(newCount).ToList();
        //        await _channelClient.SetChannelZonesPreserveIdsAsync(channelName, zonesToKeep);
        //        zonesCache.Clear();
        //        zonesCache.AddRange(zonesToKeep);

        //        // Clear staged zones so UI refreshes from the updated global cache
        //        _configurationService.GetStagedZones().TryRemove(channelName, out _);
        //    }

        //    await _channelClient.SetChannelConfigAsync(cfg);
        //}
        private async Task PushChannelConfigToHardwareAsync(string channelName, ChannelConfigurationInfo cfg)
        {
            // ---------------- STEP 0: DETERMINE FINAL ZONES ----------------

            // 🔥 Step 4: Use staged zones (CRITICAL FIX) - ensure latest state including deletions
            var stagedZones = _configurationService.GetStagedZones();
            List<ZoneInfo> workingZones;

            var baseZones = _configurationService.GetZoneConfig(channelName) ?? new List<ZoneInfo>();

            if (stagedZones.TryGetValue(channelName, out var staged))
            {
                // 🔥 FIX: merge staged + base (prevents missing/deleted issues)
                workingZones = staged
                    .Concat(baseZones)
                    .GroupBy(z => z.ZoneId)
                    .Select(g => g.First())
                    .ToList();
            }
            else
            {
                workingZones = baseZones.ToList();
            }
            // Calculate how many zones to actually add or remove based on target total
            int targetTotal = cfg.NumberOfZones;
            int currentCount = workingZones.Count;

            if (targetTotal > currentCount)
            {
                //int zonesToActuallyAdd = targetTotal - currentCount;
                int zonesToActuallyAdd = Math.Max(0, targetTotal - workingZones.Count);
                int channelId = ExtractChannelIdFromName(channelName);

                // 🔥 Step 1: Find the absolute maximum zone number (from both IDs and Names)
                int maxExistingNumber = 0;
                foreach (var z in workingZones)
                {
                    // Check ZoneId
                    if (z.ZoneId > maxExistingNumber) maxExistingNumber = z.ZoneId;

                    // Check all numbers in Name
                    if (!string.IsNullOrWhiteSpace(z.Name))
                    {
                        var matches = Regex.Matches(z.Name, @"\d+");
                        foreach (Match match in matches)
                        {
                            if (int.TryParse(match.Value, out int n))
                            {
                                if (n > maxExistingNumber) maxExistingNumber = n;
                            }
                        }
                    }
                }

                int nextZoneNameNumber = maxExistingNumber + 1;
                int nextZoneId = maxExistingNumber + 1;

                // 🔥 Step 3: Add only the difference needed to reach target total
                for (int i = 0; i < zonesToActuallyAdd; i++)
                {
                    // Ensure BOTH the Name number and the ZoneId are unique and sequentially increasing
                    while (workingZones.Any(z => z.ZoneId == nextZoneId)) nextZoneId++;
                    while (workingZones.Any(z => string.Equals(z.Name, $"Zone {nextZoneNameNumber}", StringComparison.OrdinalIgnoreCase)))
                    {
                        nextZoneNameNumber++;
                    }

                    workingZones.Add(new ZoneInfo
                    {
                        ChannelId = channelId,
                        ZoneId = nextZoneId++,
                        Name = $"Zone {nextZoneNameNumber++}",
                        Enabled = false
                    });
                }
            }
            else if (targetTotal < currentCount)
            {
                // 🔥 Reduction: Keep only the first 'targetTotal' zones based on ZoneId
                // This fulfills the user request: "if I reduce it no.of zones as 5 it should show first 5 zones"
                workingZones = workingZones
                    .OrderBy(z => z.ZoneId)
                    .Take(targetTotal)
                    .ToList();
            }

            // 🔥 Step 3: Sync hardware config count to the ACTUAL resulting list size
            cfg.NumberOfZones = workingZones.Count;

            // ---------------- STEP 1: APPLY CHANNEL ----------------
            bool channelOk = await _channelClient.SetChannelConfigAsync(cfg);
            if (!channelOk)
            {
                MessageBox.Show($"Failed to apply channel config for {channelName}");
                return;
            }

            // 🔥 Allow FRMC to stabilize after count change before sending zones
            await Task.Delay(500);

            // ---------------- STEP 2: APPLY ZONES ----------------
            // We ALWAYS send the zones if we have them, to ensure the device is in sync
            // with our local list (especially important after count changes or deletions).
            if (workingZones.Count >= 0)
            {
                bool zoneOk = await _channelClient.SetChannelZonesPreserveIdsAsync(channelName, workingZones);
                if (!zoneOk)
                {
                    MessageBox.Show($"Failed to apply zones for {channelName}");
                    return;
                }

                // 🔥 Update Global Cache and clear staging
                _configurationService.GetZones()[channelName] = workingZones;
                _configurationService.GetStagedZones().TryRemove(channelName, out _);
            }
        }
        private int ExtractChannelIdFromName(string name)
        {
            var m = Regex.Match(name, @"\d+");
            return (m.Success && int.TryParse(m.Value, out int n)) ? n : 1;
        }

        private int GetNextZoneNumberFromExistingNames(List<ZoneInfo> zones)
        {
            int max = 0;
            foreach (var z in zones)
            {
                if (string.IsNullOrWhiteSpace(z.Name)) continue;
                var match = Regex.Match(z.Name, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int n)) { if (n > max) max = n; }
            }
            return max + 1;
        }

        private async void SyncZonesWithChannelConfig(string channelName)
        {
            var channelConfig = _configurationService.GetChannelConfig(channelName);
            if (channelConfig == null)
                return;

            int required = channelConfig.NumberOfZones;
            var zones = _configurationService.GetZoneConfig(channelName);

            // 🔑 NEVER create zones locally
            if (zones == null || zones.Count != required)
            {
                await ReloadZonesFromFrmc(channelName);
                return;
            }
        }

        private void SelectZoneOrChannelAfterDelete(string channelKey)
        {
            if (treeChannels == null) return;

            foreach (TreeNode chNode in treeChannels.Nodes)
            {
                if (chNode.Text == channelKey)
                {
                    // If zones exist, select the first zone
                    foreach (TreeNode zNode in chNode.Nodes)
                    {
                        if (zNode.Text != "No zones configured")
                        {
                            treeChannels.SelectedNode = zNode;
                            return;
                        }
                    }

                    // Otherwise select the channel itself
                    treeChannels.SelectedNode = chNode;
                    return;
                }
            }
        }

        /// <summary>
        /// Selects a specific zone by name within a channel after tree refresh.
        /// Preserves the current selection when applying changes to a zone.
        /// </summary>
        private void SelectZoneByName(string channelKey, string zoneName)
        {
            if (treeChannels == null) return;

            foreach (TreeNode chNode in treeChannels.Nodes)
            {
                if (chNode.Text == channelKey)
                {
                    // Find the zone by name
                    foreach (TreeNode zNode in chNode.Nodes)
                    {
                        if (zNode.Text == zoneName)
                        {
                            treeChannels.SelectedNode = zNode;
                            return;
                        }
                    }

                    // Zone not found (maybe renamed), try to find by ZoneInfo in Tag
                    foreach (TreeNode zNode in chNode.Nodes)
                    {
                        if (zNode.Tag is ZoneInfo zi && zi.Name == zoneName)
                        {
                            treeChannels.SelectedNode = zNode;
                            return;
                        }
                    }

                    // Fallback: select first zone or channel
                    SelectZoneOrChannelAfterDelete(channelKey);
                    return;
                }
            }
        }
        /// <summary>
        /// Refreshes the TreeView from local cache without reloading from FRMC.
        /// Call this after local cache has been updated to reflect changes in UI.
        /// </summary>
        internal void RefreshTreeView()
        {
            LoadTreeView();
        }

        internal async Task ReloadZonesFromFrmc(string channelName)
        {
            int channelId = int.Parse(Regex.Match(channelName, @"\d+").Value);

            var ch = await _channelClient.GetChannelConfigAsync(channelId);
            if (ch == null)
                return;

            var existing =
                _configurationService.GetZoneConfig(channelName)
                ?? new List<ZoneInfo>();

            // Build lookup by ZoneId (use GroupBy to handle potential duplicates gracefully)
            var existingById = existing
                .GroupBy(z => z.ZoneId)
                .ToDictionary(g => g.Key, g => g.First());

            var merged = new List<ZoneInfo>();

            for (int i = 0; i < ch.NumberOfZones; i++)
            {
                var z = await _channelClient.GetZoneConfigAsync(channelId, i);
                if (z == null) continue;

                // 🔑 Preserve name if this zone already existed
                if (existingById.TryGetValue(z.ZoneId, out var oldZone))
                {
                    z.Name = oldZone.Name;
                }

                merged.Add(z);
            }

            _configurationService.GetZones()[channelName] = merged;
            LoadTreeView();
        }
    }
}
