using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoogleDriveUpload
{
    public partial class Form1 : Form
    {
        private static string[] Scopes = { DriveService.Scope.DriveReadonly };
        private static string ApplicationName = "Drive API .NET Quickstart";
       private readonly Configuration _configuration;
        public Form1()
        {
            using (StreamReader r = new StreamReader("credentials.json"))
            {
                string json = r.ReadToEnd();
                _configuration = JsonConvert.DeserializeObject<Configuration>(json);
            }
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                string[] scopes = new string[] { DriveService.Scope.Drive,
                               DriveService.Scope.DriveFile,};
                var clientId = _configuration.installed.client_id;
                var clientSecret = _configuration.installed.client_secret;       // From https://console.developers.google.com  
                                                                             // here is where we Request the user to give us access, or use the Refresh Token that was previously stored in %AppData%  
                var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                }, scopes,
                Environment.UserName, CancellationToken.None, new FileDataStore("MyAppsToken")).Result;
                //Once consent is recieved, your token will be stored locally on the AppData directory, so that next time you wont be prompted for consent.   

                DriveService service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "MyAppName",

                });
                service.HttpClient.Timeout = TimeSpan.FromMinutes(100);
                //Long Operations like file uploads might timeout. 100 is just precautionary value, can be set to any reasonable value depending on what you use your service for  

                // team drive root https://drive.google.com/drive/folders/0AAE83zjNwK-GUk9PVA   

                uploadFile(service);
                // Third parameter is empty it means it would upload to root directory, if you want to upload under a folder, pass folder's id here.

            }
            catch (Exception ex)
            {

            }
        }

        public void uploadFile(DriveService _service, string _descrp = "Uploaded with .NET!")
        {            
            var directories = Directory.GetDirectories(_configuration.installed.folderPath);
            foreach (var directory in directories)
            {
                foreach(var files in Directory.GetFiles(directory))
                {
                    if (System.IO.File.Exists(files))
                    {
                        Google.Apis.Drive.v3.Data.File body = new Google.Apis.Drive.v3.Data.File();
                        body.Name = System.IO.Path.GetFileName(files);
                        body.Description = _descrp;
                        body.MimeType = GetMimeType(files);
                        // body.Parents = new List<string> { _parent };// UN comment if you want to upload to a folder(ID of parent folder need to be send as paramter in above method)
                        byte[] byteArray = System.IO.File.ReadAllBytes(files);
                        System.IO.MemoryStream stream = new System.IO.MemoryStream(byteArray);
                        try
                        {
                            FilesResource.CreateMediaUpload request = _service.Files.Create(body, stream, GetMimeType(files));
                            request.SupportsTeamDrives = true;
                            // You can bind event handler with progress changed event and response recieved(completed event)
                            request.ProgressChanged += Request_ProgressChanged;
                            request.ResponseReceived += Request_ResponseReceived;
                            var ddd = request.Upload();
                            var response = request.ResponseBody;
                        }
                        catch (Exception e)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        MessageBox.Show("The file does not exist.", "404");
                        continue;
                    }
                }
                
            }

        }

        private void Request_ProgressChanged(Google.Apis.Upload.IUploadProgress obj)
        {
            //textBox1.Text += obj.Status + " " + obj.BytesSent;
        }

        private void Request_ResponseReceived(Google.Apis.Drive.v3.Data.File obj)
        {
            if (obj != null)
            {
                MessageBox.Show("File was uploaded sucessfully--" + obj.Id);
            }
        }

        private static string GetMimeType(string fileName)
        {
            string mimeType = "application/unknown";
            string ext = System.IO.Path.GetExtension(fileName).ToLower();
            Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
            if (regKey != null && regKey.GetValue("Content Type") != null)
                mimeType = regKey.GetValue("Content Type").ToString();
            System.Diagnostics.Debug.WriteLine(mimeType); return mimeType;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
