using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace BdlGusExporterWPF
{
    public partial class MainWindow : Window
    {
        private const string ApiBase = "https://bdl.stat.gov.pl/api/v1";
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly List<string> _selectedIds = new List<string>();

        public MainWindow()
        {
            InitializeComponent();

            // Obsługa klucza API
            chkUseApiKey.Checked   += (_, __) => SetApiKey();
            chkUseApiKey.Unchecked += (_, __) => SetApiKey();
            txtApiKey.TextChanged  += (_, __) => { if (chkUseApiKey.IsChecked == true) SetApiKey(); };

            // Inicjalizacja drzewa
            treeUnits.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(TreeItem_Expanded));
            LoadRootUnit();

            // Obsługa Ctrl+klik dla szybkiego dodawania
            treeUnits.SelectedItemChanged += TreeUnits_SelectedItemChanged;
        }

        private void SetApiKey()
        {
            if (_httpClient.DefaultRequestHeaders.Contains("X-ClientId"))
                _httpClient.DefaultRequestHeaders.Remove("X-ClientId");

            if (chkUseApiKey.IsChecked == true)
            {
                var key = txtApiKey.Text.Trim();
                if (!string.IsNullOrEmpty(key))
                    _httpClient.DefaultRequestHeaders.Add("X-ClientId", key);
            }
        }

        private async void LoadRootUnit()
        {
            SetApiKey();
            try
            {
                var urlRoot = $"{ApiBase}/units?level=0&format=json";
                var json    = await _httpClient.GetStringAsync(urlRoot);
                var doc     = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("results", out var results))
                {
                    foreach (var el in results.EnumerateArray()
                                              .OrderBy(x => x.GetProperty("name").GetString()))
                    {
                        var id   = el.GetProperty("id").GetString();
                        var name = el.GetProperty("name").GetString();
                        var item = new TreeViewItem { Header = name, Tag = new UnitInfo(id, 0) };
                        item.Items.Add(null); // placeholder dla lazy-load
                        treeUnits.Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania danych: {ex.Message}", "Błąd",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TreeItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem tvi 
                && tvi.Tag is UnitInfo info 
                && tvi.Items.Count == 1)
            {
                tvi.Items.Clear();
                int childLevel = info.Level switch
                {
                    0 => 2,
                    2 => 5,
                    5 => 6,
                    _ => -1
                };
                if (childLevel < 0) return;

                SetApiKey();
                try
                {
                    var url  = $"{ApiBase}/units?parent-id={info.Id}&level={childLevel}&format=json&page-size=100";
                    var json = await _httpClient.GetStringAsync(url);
                    var doc  = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("results", out var results))
                    {
                        foreach (var el in results.EnumerateArray()
                                                  .OrderBy(x => x.GetProperty("name").GetString()))
                        {
                            var id    = el.GetProperty("id").GetString();
                            var name  = el.GetProperty("name").GetString();
                            var child = new TreeViewItem { Header = name, Tag = new UnitInfo(id, childLevel) };
                            if (childLevel != 6) child.Items.Add(null);
                            tvi.Items.Add(child);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd pobierania danych: {ex.Message}", "Błąd",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TreeUnits_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Ctrl+klik: szybkie dodawanie
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                && e.NewValue is TreeViewItem tvi
                && tvi.Tag      is UnitInfo ui)
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
            if (treeUnits.SelectedItem is TreeViewItem tvi
                && tvi.Tag is UnitInfo ui)
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
            if (_selectedIds.Count == 0)
            {
                MessageBox.Show("Brak jednostek do eksportu.", "Uwaga",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!File.Exists("variables.txt"))
            {
                MessageBox.Show("Brak pliku variables.txt", "Uwaga",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Reset postępu
            progressBar.Value = 0;
            txtStatus.Text    = "Start eksportu...";

            try
            {
                var varList = File.ReadAllLines("variables.txt")
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
                    .Select(l => l.Split(',', 2))
                    .Select(parts => (Id: parts[0].Trim(), Name: parts.Length > 1 ? parts[1].Trim() : parts[0].Trim()))
                    .ToList();

                using var wb = new XLWorkbook();
                var years     = Enumerable.Range(DateTime.Now.Year - 5, 6).ToList();
                int totalVars = varList.Count;

                for (int i = 0; i < totalVars; i++)
                {
                    var (varId, varName) = varList[i];
                    txtStatus.Text = $"Pobieram zmienną {varName} ({i+1}/{totalVars})";

                    var ws = wb.Worksheets.Add($"Var_{varId}");
                    ws.Cell(1, 1).Value = "WSKAŹNIK";
                    ws.Cell(1, 2).Value = "JEDNOSTKA";
                    ws.Cell(1, 3).Value = "ROK";
                    ws.Cell(1, 4).Value = "WARTOŚĆ";

                    int row = 2;
                    foreach (string label in listSelected.Items)
                    {
                        var unitName = label.Substring(0, label.IndexOf('(')).Trim();
                        var unitId   = label.Substring(label.IndexOf('(') + 1).TrimEnd(')');
                        SetApiKey();
                        var queryYears = string.Join("&", years.Select(y => $"year={y}"));
                        var url        = $"{ApiBase}/data/by-unit/{unitId}?var-id={varId}&{queryYears}&format=json";
                        var json       = await _httpClient.GetStringAsync(url);
                        var doc        = JsonDocument.Parse(json);
                        var dict       = new Dictionary<int, decimal>();
                        if (doc.RootElement.TryGetProperty("results", out var resArr) && resArr.GetArrayLength() > 0)
                        {
                            var values = resArr[0].GetProperty("values");
                            dict = values.EnumerateArray()
                                         .Select(x =>
                                         {
                                             int year = x.GetProperty("year").ValueKind == JsonValueKind.Number
                                                 ? x.GetProperty("year").GetInt32()
                                                 : int.Parse(x.GetProperty("year").GetString());
                                             decimal val = x.GetProperty("val").ValueKind == JsonValueKind.Number
                                                 ? x.GetProperty("val").GetDecimal()
                                                 : decimal.Parse(x.GetProperty("val").GetString(),
                                                                System.Globalization.CultureInfo.InvariantCulture);
                                             return new { year, val };
                                         })
                                         .ToDictionary(k => k.year, v => v.val);
                        }

                        foreach (int y in years)
                        {
                            ws.Cell(row, 1).Value = varName;
                            ws.Cell(row, 2).Value = unitName;
                            ws.Cell(row, 3).Value = y;
                            var cell = ws.Cell(row, 4);
                            if (dict.TryGetValue(y, out var v))
                                cell.SetValue(v);
                            else
                                cell.SetValue("n/d");
                            row++;
                        }
                    }

                    // Update progress
                    progressBar.Value = ((double)(i + 1) / totalVars) * 100;
                    await Task.Delay(50);
                }

                var dlg = new SaveFileDialog { Filter = "Excel Files|*.xlsx" };
                if (dlg.ShowDialog() == true)
                {
                    wb.SaveAs(dlg.FileName);
                    txtStatus.Text    = "Eksport zakończony!";
                    progressBar.Value = 100;
                    MessageBox.Show("Eksport zakończony pomyślnie.", "Informacja",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Błąd!";
                MessageBox.Show($"Wystąpił błąd: {ex.Message}", "Błąd",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class UnitInfo
    {
        public string Id { get; }
        public int Level { get; }
        public UnitInfo(string id, int level) { Id = id; Level = level; }
    }
}
