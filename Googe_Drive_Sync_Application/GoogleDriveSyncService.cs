using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Google_Drive_Sync_Application
{
    public class GoogleDriveSyncService
    {
        private readonly Configuration _configuration;
        public GoogleDriveSyncService()
        {

            using (StreamReader r = new StreamReader(AppDomain.CurrentDomain.BaseDirectory+"\\credentials.json"))
            {
                string json = r.ReadToEnd();
                _configuration = JsonConvert.DeserializeObject<Configuration>(json);
            }
        }

        public void StartSync()
        {
            WriteToFile("StartSync invoked at " + DateTime.Now);
            UploadFilesToDrive();
            WriteToFile("UploadFilesToDrive finished at " + DateTime.Now);
        }

        public void WriteToFile(string Message)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath))
            {
                // Create a file to write to. 
                using (StreamWriter sw = File.CreateText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
        }

        private void UploadFilesToDrive()
        {
            try
            {
                DriveService service = GetService();
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

        private DriveService GetService()
        {
            string[] scopes = new string[] { DriveService.Scope.Drive,
                               DriveService.Scope.DriveFile,};
            var clientId = _configuration.installed.client_id;
            var clientSecret = _configuration.installed.client_secret;
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
            return service;
        }

        public void uploadFile(DriveService _service, string _descrp = "Uploaded with .NET!")
        {
            var syncDate = DateTime.UtcNow;
            //var directories = Directory.GetDirectories(_configuration.installed.folderPath);
            var directories = Directory.GetFileSystemEntries(_configuration.installed.folderPath);
            foreach (var directory in directories)
            {
                var fileInfo = new FileInfo(directory);
                if (fileInfo == null || fileInfo.Extension.Length <= 0)
                {
                    foreach (var files in Directory.GetFiles(directory))
                    {
                        if (System.IO.File.Exists(files))
                        {
                            Upload(_descrp, files, directory, _service, syncDate, true);
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
                else
                {
                    if (System.IO.File.Exists(directory))
                    {
                        Upload(_descrp, directory, directory, _service, syncDate, false);
                    }
                    else
                    {
                        continue;
                    }
                }

            }

        }


        private void Upload(string _descrp, string files, string directory, DriveService _service, DateTime syncDate, bool createDirectory)
        {
            Google.Apis.Drive.v3.Data.File body = new Google.Apis.Drive.v3.Data.File();
            body.Name = System.IO.Path.GetFileName(files);
            body.Description = _descrp;
            body.MimeType = GetMimeType(files);

            byte[] byteArray = System.IO.File.ReadAllBytes(files);
            System.IO.MemoryStream stream = new System.IO.MemoryStream(byteArray);
            try
            {
                var list = CheckIfExists(body.Name, _service);

                if (list.Where(x => x.Name == body.Name).Any())
                {
                    var request = _service.Files.Update(body, list.Where(x => x.Name == body.Name).FirstOrDefault().Id, stream, GetMimeType(files));
                    var updateRequest = request.Upload();
                    var file = request.ResponseBody;
                }
                else
                {
                    var parent = CreateDirectory(directory.Replace(_configuration.installed.folderPath, ""), createDirectory);
                    if (parent.Length > 0)
                        body.Parents = new List<string> { parent };// UN comment if you want to upload to a folder(ID of parent folder need to be send as paramter in above method)

                    FilesResource.CreateMediaUpload request = _service.Files.Create(body, stream, GetMimeType(files));

                    request.SupportsTeamDrives = true;
                    // You can bind event handler with progress changed event and response recieved(completed event)
                    request.ProgressChanged += Request_ProgressChanged;                   
                    var ddd = request.Upload();
                    var response = request.ResponseBody;
                    Directory.SetLastAccessTime(files, syncDate);
                }
            }
            catch (Exception ex)
            {

            }
            LogWrite(new Sync()
            {
                lastSyncDateTime = syncDate
            });
        }

        private void LogWrite(Sync logMessage)
        {
            var m_exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            try
            {
                using (StreamWriter txtWriter = File.CreateText(m_exePath + "\\" + "log.json"))
                {
                    txtWriter.Write(JsonConvert.SerializeObject(logMessage));
                }
            }
            catch (Exception ex)
            {
            }
        }

        private string CreateDirectory(string _description, bool createDirectory)
        {
            string dir = string.Empty;
            try
            {
                if (!createDirectory)
                    _description = _description.Split("\\")[_description.Split("\\").Length - 2];

                foreach (var directory in _description.Split("\\").Where(x => x.Length > 1))
                {
                    var service = GetService();
                    IEnumerable<Google.Apis.Drive.v3.Data.File> exists = CheckIfExists(directory, service);
                    try
                    {
                        if (!exists.Any())
                        {
                            //service = GetService();
                            Google.Apis.Drive.v3.Data.File FileMetaData = new Google.Apis.Drive.v3.Data.File();
                            FileMetaData.Name = directory;
                            FileMetaData.MimeType = "application/vnd.google-apps.folder";
                            Google.Apis.Drive.v3.FilesResource.CreateRequest request;
                            request = service.Files.Create(FileMetaData);
                            request.Fields = directory;
                            var file = request.Execute();
                            dir = file.Id;
                        }
                        dir = exists.FirstOrDefault().Id;
                    }
                    catch
                    {
                        if (!exists.Any() && dir.Length <= 0)
                        {
                            IEnumerable<Google.Apis.Drive.v3.Data.File> checkAgain = CheckIfExists(directory, service);
                            dir = checkAgain.FirstOrDefault().Id;
                        }
                    }

                }
            }
            catch (Exception e)
            {

            }
            return dir;
        }

        private static IEnumerable<Google.Apis.Drive.v3.Data.File> CheckIfExists(string directory, DriveService service)
        {
            var fileRequest = service.Files.List();
            fileRequest.Spaces = "drive";
            fileRequest.Fields = "nextPageToken, files(id, name)";
            var fileRequestResponse = fileRequest.Execute();
            var exists = fileRequestResponse.Files.Where(x => x.Name == directory);
            return exists;
        }

        private void Request_ProgressChanged(Google.Apis.Upload.IUploadProgress obj)
        {
            //textBox1.Text += obj.Status + " " + obj.BytesSent;
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
    }
}
