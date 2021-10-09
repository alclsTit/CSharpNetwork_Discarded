﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// --- custom --- //
using ProjectWaterMelon.Network.Sytem;
using ProjectWaterMelon.Network.MessageWorker;
using ProjectWaterMelon.Log;
// -------------- //

namespace ProjectWaterMelon
{
   class Program
   {
        static void Main()
        {
            try
            {
                // register MessageHandler
                CMessageReceiver.Init();

                CListener lListener = new CListener();
                lListener.Start(8800);
            }
            catch(Exception ex)
            {
                CLog4Net.LogError($"Expcetion in Program.Main - {ex.Message}, {ex.StackTrace}");
            }
        }
    }
}

/*
    CLogs lLogs = new CLogs(ConstLogsFolder.gLogHandlerFolder, ConstLogsFile.gLogHandlerFile);
    lLogs.TestLog(4);
    CLog4Net.gLog4Net.Info("===============info Log4net Test=============");

    CLog4Net.gLog4Net.Warn("===============warn Log4net Test=============");

    CLogInfo.gLogInfo.Info("===============Log4net Test=============");
*/