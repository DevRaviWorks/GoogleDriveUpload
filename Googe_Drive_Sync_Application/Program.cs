﻿using System;

namespace Google_Drive_Sync_Application
{
    class Program
    {
        static void Main(string[] args)
        {
            GoogleDriveSyncService syncService = new GoogleDriveSyncService();
            syncService.StartSync();
        }
    }
}
