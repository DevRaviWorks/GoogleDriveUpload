using System;
using System.Collections.Generic;
using System.Text;

namespace Google_Drive_Sync_Application
{
    public class Installed
    {
        public string client_id { get; set; }
        public string client_secret { get; set; }
        public string folderPath { get; set; }       
    }

    public class Sync
    {
        public DateTime lastSyncDateTime { get; set; }
    }

    public class Configuration
    {
        public Installed installed { get; set; }
    }
}
