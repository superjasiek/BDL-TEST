using BdlGusExporter.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Threading.RateLimiting;
using System.Windows.Controls;
using System.Windows.Input;

namespace BdlGusExporterWPF
{
    public partial class MainWindow : Window
    {
        private readonly GusApiService _gusApiService = new GusApiService();
        private readonly ExcelExportService _excelExportService = new ExcelExportService();
        private readonly RateLimiterService _rateLimiterService = new RateLimiterService();
        private readonly List<string> _selectedIds = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            Closing += OnClosing;

            chkUseApiKey.Checked += (_, __) => UpdateApiKey();
            chkUseApiKey.Unchecked += (_, __) => UpdateApiKey();
            txtApiKey.TextChanged += (_, __) => { if (chkUseApiKey.IsChecked == true) UpdateApiKey(); };

            treeUnits.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(TreeItem_Expanded));
            LoadRootUnit();
            treeUnits.SelectedItemChanged += TreeUnits_SelectedItemChanged;
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _rateLimiterService.Dispose();
        }

        private void UpdateApiKey()
        {
            var key = chkUseApiKey.IsChecked == true ? txtApiKey.Text.Trim() : null;
            _gusApiService.SetApiKey(key);
        }

        private async void LoadRootUnit()
        {
            UpdateApiKey();
            try
            {
                using (await AcquireRateLimitPermit())
                {
                    using var doc = await _gusApiService.GetUnitsAsync();
                    if (doc.RootElement.TryGetProperty("results", out var results))
                    {
                        foreach (var el in results.EnumerateArray().OrderBy(x => x.GetProperty("name").GetString()))
                        {
                            var id = el.GetProperty("id").GetString();
                            var name = el.GetProperty("name").GetString();
                            var item = new TreeViewItem { Header = name, Tag = new UnitInfo(id, 0) };
                            item.Items.Add(null);
                            treeUnits.Items.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TreeItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem tvi && tvi.Tag is UnitInfo info && tvi.Items.Count == 1 && tvi.Items[0] == null)
            {
                tvi.Items.Clear();
                int childLevel = info.Level switch { 0 => 2, 2 => 5, 5 => 6, _ => -1 };
                if (childLevel < 0) return;

                UpdateApiKey();
                try
                {
                    using (await AcquireRateLimitPermit())
                    {
                        using var doc = await _gusApiService.GetUnitsAsync(info.Id, childLevel);
                        if (doc.RootElement.TryGetProperty("results", out var results))
                        {
                            foreach (var el in results.EnumerateArray().OrderBy(x => x.GetProperty("name").GetString()))
                            {
                                var id = el.GetProperty("id").GetString();
                                var name = el.GetProperty("name").GetString();
                                var child = new TreeViewItem { Header = name, Tag = new UnitInfo(id, childLevel) };
                                if (childLevel != 6) child.Items.Add(null);
                                tvi.Items.Add(child);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd pobierania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TreeUnits_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.NewValue is TreeViewItem tvi && tvi.Tag is UnitInfo ui)
            {
                var label = $"{tvi.Header} ({ui.Id})";
                if (!_selectedIds.Contains(ui.Id))
                {
                    listSelected.Items.Add(label);
                    _selectedIds.Add(ui.Id);
                }
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (treeUnits.SelectedItem is TreeViewItem tvi && tvi.Tag is UnitInfo ui)
            {
                var label = $"{tvi.Header} ({ui.Id})";
                if (!_selectedIds.Contains(ui.Id))
                {
                    listSelected.Items.Add(label);
                    _selectedIds.Add(ui.Id);
                }
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var toRemove = listSelected.SelectedItems.Cast<string>().ToList();
            foreach (var item in toRemove)
            {
                var id = item.Substring(item.IndexOf('(') + 1).TrimEnd(')');
                _selectedIds.Remove(id);
                listSelected.Items.Remove(item);
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            listSelected.Items.Clear();
            _selectedIds.Clear();
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIds.Count == 0) { MessageBox.Show("Brak jednostek do eksportu.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!File.Exists("variables.txt")) { MessageBox.Show("Brak pliku variables.txt", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            progressBar.Value = 0;
            txtStatus.Text = "Start eksportu...";
            this.IsEnabled = false;

            try
            {
                var varList = File.ReadAllLines("variables.txt").Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#")).Select(l => l.Split(',', 2)).Select(parts => (Id: parts[0].Trim(), Name: parts.Length > 1 ? parts[1].Trim() : parts[0].Trim())).ToList();
                var years = Enumerable.Range(DateTime.Now.Year - 5, 6).ToList();
                var dataToExport = new Dictionary<string, List<ExportDataRow>>();

                for (int i = 0; i < varList.Count; i++)
                {
                    var (varId, varName) = varList[i];
                    txtStatus.Text = $"Pobieram zmienną {varName} ({i + 1}/{varList.Count})";
                    var rowsForVar = new List<ExportDataRow>();
                    dataToExport[varId] = rowsForVar;

                    foreach (string label in listSelected.Items)
                    {
                        var unitName = label.Substring(0, label.IndexOf('(')).Trim();
                        var unitId = label.Substring(label.IndexOf('(') + 1).TrimEnd(')');

                        UpdateApiKey();

                        using (await AcquireRateLimitPermit())
                        {
                            using var doc = await _gusApiService.GetDataForUnitAsync(unitId, varId, years);
                            var dataDict = ParseDataFromDoc(doc);
                            foreach (int y in years)
                            {
                                dataDict.TryGetValue(y, out decimal? val);
                                rowsForVar.Add(new ExportDataRow(varName, unitName, y, val));
                            }
                        }
                    }
                    progressBar.Value = ((double)(i + 1) / varList.Count) * 100;
                }

                txtStatus.Text = "Generowanie pliku Excel...";
                using var wb = _excelExportService.CreateWorkbook(dataToExport);
                var dlg = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = "export.xlsx" };
                if (dlg.ShowDialog() == true)
                {
                    wb.SaveAs(dlg.FileName);
                    txtStatus.Text = "Eksport zakończony!";
                    MessageBox.Show("Eksport zakończony pomyślnie.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    txtStatus.Text = "Eksport anulowany.";
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Błąd!";
                MessageBox.Show($"Wystąpił błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;
                progressBar.Value = 0;
            }
        }

        private Dictionary<int, decimal?> ParseDataFromDoc(JsonDocument doc)
        {
            var dict = new Dictionary<int, decimal?>();
            if (doc.RootElement.TryGetProperty("results", out var resArr) && resArr.GetArrayLength() > 0)
            {
                if (resArr[0].TryGetProperty("values", out var values))
                {
                    foreach (var valElement in values.EnumerateArray())
                    {
                        if (valElement.TryGetProperty("year", out var yearEl) && valElement.TryGetProperty("val", out var valEl) && yearEl.TryGetInt32(out int year) && valEl.TryGetDecimal(out decimal val))
                        {
                            dict[year] = val;
                        }
                    }
                }
            }
            return dict;
        }

        private bool IsUserRegistered() => chkUseApiKey.IsChecked == true && !string.IsNullOrWhiteSpace(txtApiKey.Text);

        private async Task<IDisposable> AcquireRateLimitPermit()
        {
            var limiters = _rateLimiterService.GetLimiters(IsUserRegistered());
            var leases = new List<RateLimitLease>();
            try
            {
                foreach (var limiter in limiters)
                {
                    leases.Add(await limiter.AcquireAsync(1));
                }
                return new CompositeDisposable(leases);
            }
            catch
            {
                foreach (var lease in leases) lease.Dispose();
                throw;
            }
        }

        private sealed class CompositeDisposable : IDisposable
        {
            private readonly IEnumerable<IDisposable> _disposables;
            public CompositeDisposable(IEnumerable<IDisposable> disposables) => _disposables = disposables;
            public void Dispose()
            {
                foreach (var d in _disposables.Reverse()) d.Dispose();
            }
        }
    }
}
