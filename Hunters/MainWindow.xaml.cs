using OfficeOpenXml;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Diagnostics;
using System.Data;
using Hunters.Dto;
using ClosedXML.Excel;
using System.Text.Json;
using Hunters;

namespace Report
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string selectedFilePath = string.Empty;
        private Configuration? _configuration { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            LoadConfiguration();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            ProcessButton.IsEnabled = false; // Deshabilitar el botón de procesar inicialmente
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls",
                Title = "Seleccionar archivo de Excel"
            };

            if (openFileDialog.ShowDialog().GetValueOrDefault())
            {
                selectedFilePath = openFileDialog.FileName;
                FilePathTextBlock.Text = selectedFilePath;
                ProcessButton.IsEnabled = true; // Habilitar el botón de procesar cuando se seleccione un archivo
            }
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                MessageBox.Show("Por favor, seleccione un archivo de Excel primero.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ProcessExcelFile();
        }

        private void ProcessExcelFile()
        {
            try
            {
                using var package = new ExcelPackage(new FileInfo(selectedFilePath));

                Process(package, _configuration.Names);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar el archivo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Process(ExcelPackage package, IEnumerable<string> names)
        {
            var data = ReadExcelData(package);

            SaveExcelData(data, names);
        }

        private IEnumerable<TaskDto> ReadExcelData(ExcelPackage package)
        {
            List<TaskDto> records = [];

            var worksheet = package.Workbook.Worksheets[0];
            int rowCount = worksheet.Dimension.Rows;

            for (int row = 2; row <= rowCount; row++)
            {              
                records.Add(new TaskDto
                {
                    TaskId = worksheet.Cells[$"A{row}"].Text,  // Columna A
                    TaskName = worksheet.Cells[$"B{row}"].Text,  // Columna B
                    DepositName = worksheet.Cells[$"C{row}"].Text,  // Columna C
                    Progress = worksheet.Cells[$"D{row}"].Text,  // Columna D
                    Priority = worksheet.Cells[$"E{row}"].Text,  // Columna E
                    Assignee = worksheet.Cells[$"F{row}"].Text,  // Columna F
                    Creator = worksheet.Cells[$"G{row}"].Text,  // Columna G
                    CreatedDate = worksheet.Cells[$"H{row}"].Text,  // Columna H
                    BeginDate = worksheet.Cells[$"I{row}"].Text,  // Columna I
                    DueDate = worksheet.Cells[$"J{row}"].Text,  // Columna J
                    Periodic = worksheet.Cells[$"K{row}"].Text == "true",  // Columna K
                    Delayed = worksheet.Cells[$"L{row}"].Text == "true",  // Columna L
                    EndDate = worksheet.Cells[$"M{row}"].Text,  // Columna M
                    CompletedBy = worksheet.Cells[$"N{row}"].Text,  // Columna N
                    Elements = worksheet.Cells[$"O{row}"].Text,  // Columna O
                    ComprobationElements = worksheet.Cells[$"P{row}"].Text,  // Columna P
                    Tags = worksheet.Cells[$"Q{row}"].Text,  // Columna Q
                    Description = worksheet.Cells[$"R{row}"].Text  // Columna R      
                });
            }

            return records;
        }

        private void SaveExcelData(IEnumerable<TaskDto> records, IEnumerable<string> names)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Nombre del plan");
            var rowCount = 0;
            var columns = records.Select(x => x.DepositName).Distinct().ToList();

            foreach (var column in columns)
            {
                var currentRow = rowCount + 1;
                worksheet.Cell(currentRow, 2).Value = column;

                var headerRange = worksheet.Range(currentRow, 2, currentRow, 4);

                headerRange.Merge();

                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightPink;
                headerRange.Style.Font.SetBold();

                var columnTasks = records.Where(x => x.DepositName == column).ToList();

                foreach (var columnTask in columnTasks)
                {
                    currentRow++;

                    var nameCell = worksheet.Cell(currentRow, 2);
                    var adminName = worksheet.Cell(currentRow, 4);
                    var splittedDescription = columnTask.TaskName.Split("-");

                    worksheet.Cell(currentRow, 3).Value = $"{splittedDescription[0] ?? string.Empty}-{splittedDescription[1] ?? string.Empty}";
                    nameCell.Value = names.FirstOrDefault(x => columnTask.TaskName.Contains(x, StringComparison.CurrentCultureIgnoreCase)) ?? string.Empty;

                    var admin = columnTask.Assignee.Split(" ")[0] ?? string.Empty;

                    adminName.Value = admin.ToUpper().StartsWith("DANI") ? "Dany" : admin;
                }

                rowCount += columnTasks.Count + 3;
            }

            worksheet.Columns().AdjustToContents();

            string downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            string filePath = Path.Combine(downloadsFolder, $"Hunters_{DateTime.Now.Ticks}.xlsx");
            workbook.SaveAs(filePath);

            System.Diagnostics.Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }

        private void LoadConfiguration()
        {
            string filePath = "./configuration.json";
            _configuration = ReadConfiguration(filePath);
        }

        public static Configuration? ReadConfiguration(string filePath)
        {
            try
            {
                string jsonString = File.ReadAllText(filePath);
                Configuration? credentials = JsonSerializer.Deserialize<Configuration>(jsonString);

                return credentials;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error de configuración");
                return null;
            }
        }
    }
}