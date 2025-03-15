using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using System.Net.Http.Headers;

namespace MPKEditor
{
    public partial class MainWindow : Window
    {
        private string selectedMpkFile = "";
        private string extractedMprFile = "";
        private readonly string tempFolder = Path.Combine(Path.GetTempPath(), "MPKEditor");

        public MainWindow()
        {
            InitializeComponent();
            LoadComboBoxDataFromUrl("https://www.lowcodeconnect.nl/mendixversions.txt");
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "MPK files (*.mpk)|*.mpk"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedMpkFile = openFileDialog.FileName;
                ExtractMpk(selectedMpkFile);
            }
        }

        private void ExtractMpk(string mpkPath)
        {
            Directory.CreateDirectory(tempFolder);
            Array.ForEach(Directory.GetFiles(tempFolder), File.Delete);
            ZipFile.ExtractToDirectory(mpkPath, tempFolder, true);

            extractedMprFile = Directory.GetFiles(tempFolder, "*.mpr")[0];
            LoadMetadata();
        }

        private void LoadMetadata()
        {
            using var connection = new SQLiteConnection($"Data Source={extractedMprFile};Version=3;Pooling=false");
            connection.Open();
            var command = new SQLiteCommand("SELECT _ProductVersion, _BuildVersion FROM _MetaData", connection);
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    TxtProductVersion.Text = reader["_ProductVersion"].ToString();
                    TxtBuildVersion.Text = reader["_BuildVersion"].ToString();
                }
                reader.Close();
            }
            command.Dispose();
            connection.Close();
            SQLiteConnection.ClearAllPools();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (extractedMprFile != "")
            {
                using (var connection = new SQLiteConnection($"Data Source={extractedMprFile};Version=3;Pooling=false"))
                {
                    connection.Open();
                    var command = new SQLiteCommand("UPDATE _MetaData SET _ProductVersion = @productVersion, _BuildVersion = @buildVersion", connection);
                    command.Parameters.AddWithValue("@productVersion", TxtProductVersion.Text);
                    command.Parameters.AddWithValue("@buildVersion", TxtBuildVersion.Text);
                    command.ExecuteNonQuery();
                    command.Dispose();
                    connection.Close();
                }
                SaveMpk();
            }
        }

        private void SaveMpk()
        {
            int count = 0;
            string newMpkPath = selectedMpkFile.Replace(".mpk", $"_{TxtBuildVersion.Text}.mpk");
            while (File.Exists(newMpkPath)) {
                newMpkPath = selectedMpkFile.Replace(".mpk", $"_{TxtBuildVersion.Text}_{++count}.mpk");
            }
            ZipFile.CreateFromDirectory(tempFolder, newMpkPath);
            MessageBox.Show($"MPK file save as {newMpkPath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void LoadComboBoxDataFromUrl(string url)
        {
            try
            {
                var versions = new List<Version>();

                // Download the text file from the URL
                string fileContent = await DownloadTextFileAsync(url);

                // Split the file content into lines
                string[] lines = fileContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

                // Add the lines to the ComboBox
                foreach (string line in lines)
                {
                    versions.Add(new Version(line));
                }
                versions.Sort();
                versions.Reverse();
                foreach (Version version in versions)
                {
                    ProductVersionCombobox.Items.Add(version);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static async Task<string> DownloadTextFileAsync(string url)
        {
            using HttpClient client = new();
            // Download the file content as a string
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders
                  .Accept
                  .Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MxVerChanger/1.0");
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode(); // Throw if not successful
            return await response.Content.ReadAsStringAsync();
        }

        private void ComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender != null && sender is ComboBox)
            {
                TxtBuildVersion.Text = ProductVersionCombobox.SelectedItem.ToString();
                TxtProductVersion.Text = ProductVersionCombobox.SelectedItem.ToString(); ;
            }
        }
    }
}
