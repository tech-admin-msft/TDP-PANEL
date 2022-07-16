﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Power_Control_Panel;
using System.Threading;
using System.Diagnostics;

namespace Power_Control_Panel.PowerControlPanel.Classes.ChangeTDP
{



    public class ChangeTDP
    {
        public static string cpuType = "";
        public static string MCHBAR = "";
        static string RWDelay = Properties.Settings.Default.RWDelay;
        private static Object objLock = new Object();

        public static string BaseDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);


        //Read TDP routines
        public static void readTDP()
        {

            try
            {
                //add small delay to prevent write and read operations from interfering
                Thread.Sleep(100);

                determineCPU();

                if (cpuType == "Intel")
                {
                    if (Properties.Settings.Default.IntelMMIOMSR.Contains("MMIO")) { runIntelReadTDPMMIOKX(); }
                    if (Properties.Settings.Default.IntelMMIOMSR == "MSR") { runIntelReadTDPMSR(); }
                    //if (Properties.Settings.Default.IntelMMIOMSR == "MSRCMD") { runIntelReadTDPMSRCMD(); }
                }
                else { if (cpuType == "AMD") { runAMDReadTDP(); } }
                GlobalVariables.needTDPRead = false;

            }
            catch (Exception ex)
            {
                string errorMsg = "Error: ChangeTDP.cs:  Reading TDP: " + ex.Message;
                StreamWriterLog.startStreamWriter(errorMsg);
                MessageBox.Show(errorMsg);

                //return "Error";
            }

        }
        //Change TDP routines - Intel
        public static void changeTDP(int pl1TDP, int pl2TDP)
        {
            //Return Success as default value, otherwise alert calling routine to error
            try
            {
                determineCPU();

                if (cpuType == "Intel")
                {
                    if (Properties.Settings.Default.IntelMMIOMSR.Contains("MMIO"))
                    {
                        runIntelTDPChangeMMIOKX(pl1TDP, pl2TDP);
                    }
                    if (Properties.Settings.Default.IntelMMIOMSR.Contains("MSR")) { runIntelTDPChangeMSR(pl1TDP, pl2TDP); }
                   
                }
                else { if (cpuType == "AMD") { runAMDTDPChange(pl1TDP, pl2TDP); } }
                GlobalVariables.setPL1 = pl1TDP;
                GlobalVariables.setPL2 = pl2TDP;
            }
            catch (Exception ex)
            {
                string errorMsg = "Error: ChangeTDP.cs:  Changing TDP Intel or AMD handler: " + ex.Message;
                StreamWriterLog.startStreamWriter(errorMsg);
                MessageBox.Show(errorMsg);

            }

        }

        static void runIntelTDPChangeMMIOKX(int pl1TDP, int pl2TDP)
        {
            string processKX = "";
            string hexPL1 = "";
            string hexPL2 = "";
            string commandArgumentsPL1 = "";
            string commandArgumentsPL2 = "";
            try
            {

                processKX = BaseDir + "\\Resources\\Intel\\KX\\KX.exe";
                hexPL1 = convertTDPToHexMMIO(pl1TDP);
                hexPL2 = convertTDPToHexMMIO(pl2TDP);

                if (hexPL1 != "Error" && hexPL2 != "Error" && MCHBAR != null)
                {
                    lock (objLock)
                    {
                        commandArgumentsPL1 = " /wrmem16 " + MCHBAR + "a0 0x" + hexPL1;

                        RunCLI.RunCommand(commandArgumentsPL1, true, processKX);
                        Thread.Sleep(500);
                        commandArgumentsPL2 = " /wrmem16 " + MCHBAR + "a4 0x" + hexPL2;

                        RunCLI.RunCommand(commandArgumentsPL2, true, processKX);
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMsg = "Error: ChangeTDP.cs:  Run Intel TDP Change MMIOKX: " + ex.Message + " hexPL1 and PL2 are: " + hexPL1 + ", " + hexPL2 + ", processKX is " + processKX + ", commandarguments are " + commandArgumentsPL1 + " and " + commandArgumentsPL2;
                StreamWriterLog.startStreamWriter(errorMsg);
                MessageBox.Show(errorMsg);

            }


        }


        static void runIntelTDPChangeMSR(int pl1TDP, int pl2TDP)
        {

            string processMSR = "";
            string hexPL1 = "";
            string hexPL2 = "";
            string commandArguments = "";
            try
            {

                hexPL1 = convertTDPToHexMSR(pl1TDP);
                hexPL2 = convertTDPToHexMSR(pl2TDP);
                if (hexPL1 != "Error" && hexPL2 != "Error" && MCHBAR != null)
                {
                    lock (objLock)
                    {
                        if (hexPL1.Length < 3)
                        {
                            if (hexPL1.Length == 1) { hexPL1 = "00" + hexPL1; }
                            if (hexPL1.Length == 2) { hexPL1 = "0" + hexPL1; }
                        }
                        if (hexPL2.Length < 3)
                        {
                            if (hexPL2.Length == 1) { hexPL2 = "00" + hexPL2; }
                            if (hexPL2.Length == 2) { hexPL2 = "0" + hexPL2; }
                        }
                        commandArguments = " -s write 0x610 0x00438" + hexPL2 + " 0x00dd8" + hexPL1;
                        processMSR = BaseDir + "\\Resources\\Intel\\MSR\\msr-cmd.exe";

                        RunCLI.RunCommand(commandArguments, false, processMSR);
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMsg = "Error: ChangeTDP.cs:  Run Intel TDP Change MSR: " + ex.Message + " Hex pl1 and pl2 are " + hexPL1 + " and " + hexPL2 + ", commandargument is " + commandArguments + ", processMSR is " + processMSR;
                StreamWriterLog.startStreamWriter(errorMsg);
                MessageBox.Show(errorMsg);

            }


        }
        //End change TDP routines
        static void determineCPU()
        {
            try
            {
                if (cpuType != "Intel" && cpuType != "AMD")
                {
                    //Get the processor name to determine intel vs AMD
                    object processorNameRegistry = Registry.GetValue("HKEY_LOCAL_MACHINE\\hardware\\description\\system\\centralprocessor\\0", "ProcessorNameString", null);
                    string processorName = null;
                    if (processorNameRegistry != null)
                    {
                        //If not null, find intel or AMD string and clarify type. For Intel determine MCHBAR for rw.exe
                        processorName = processorNameRegistry.ToString();
                        if (processorName.IndexOf("Intel") >= 0) { cpuType = "Intel"; }
                        if (processorName.IndexOf("AMD") >= 0) { cpuType = "AMD"; }
                        
                        
                    }
                }
                if (cpuType == "Intel" && Properties.Settings.Default.IntelMMIOMSR.Contains("MMIO"))
                {
                    determineIntelMCHBAR();
                }
            }
            catch (Exception ex)
            {
                string errorMsg = "Error: ChangeTDP.cs: Determining CPU type: " + ex.Message;
                StreamWriterLog.startStreamWriter(errorMsg);
                MessageBox.Show(errorMsg);
            }
        }

        static void determineIntelMCHBAR()
        {
            try
            {
                //Get the processor model to determine MCHBAR, INTEL ONLY
                object processorModelRegistry = Registry.GetValue("HKEY_LOCAL_MACHINE\\hardware\\description\\system\\centralprocessor\\0", "Identifier", null);
                string processorModel = null;
                if (processorModelRegistry != null)
                {
                    //If not null, convert to string and determine MCHBAR for rw.exe
                    processorModel = processorModelRegistry.ToString();
                    if (processorModel.IndexOf("Model 140") >= 0) { MCHBAR = "0xFEDC59"; } else { MCHBAR = "0xFED159"; };
                }
            }
            catch (Exception ex)
            {
                string errorMsg = "Error: ChangeTDP.cs: Determining MCHBAR: " + ex.Message;
                StreamWriterLog.startStreamWriter(errorMsg);
                MessageBox.Show(errorMsg);
            }

        }

        //MMIO Stuff here
        static string convertTDPToHexMMIO(int tdp)
        {
            //Convert integer TDP value to Hex for rw.exe
            //Must use formula (TDP in watt   *1000/125) +32768 and convert to hex
            try
            {
                int newTDP = (tdp * 1000 / 125) + 32768;
                return newTDP.ToString("X");

            }
            catch (Exception ex)
            {
                string errorMsg = "Error: ChangeTDP.cs:  convert MMIO TDP To Hex: " + ex.Message;
                StreamWriterLog.startStreamWriter(errorMsg);
                MessageBox.Show(errorMsg);
                return "Error";
            }
        }


        static void runIntelReadTDPMMIOKX()
        {
            string processKX = "";
            string commandArgumentsPL1 = "";
            string resultPL1 = "";
            string commandArgumentsPL2 = "";
            string resultPL2 = "";

            try
            {
                processKX = BaseDir + "\\Resources\\Intel\\KX\\KX.exe";
                if (MCHBAR != null)
                {
                    lock (objLock)
                    {
                        commandArgumentsPL1 = " /rdmem16 " + MCHBAR + "a0";

                        resultPL1 = RunCLI.RunCommand(commandArgumentsPL1, true, processKX);

                        if (resultPL1 != null)
                        {
                            double dblPL1 = Convert.ToDouble(parseHexFromResultMMIOConvertToTDPKX(resultPL1, true));
                            GlobalVariables.readPL1 = dblPL1;
                        }
                        Thread.Sleep(300);
                        commandArgumentsPL2 = " /rdmem16 " + MCHBAR + "a4";

                        resultPL2 = RunCLI.RunCommand(commandArgumentsPL2, true, processKX);
                        if (resultPL2 != null)
                        {
                            double dblPL2 = Convert.ToDouble(parseHexFromResultMMIOConvertToTDPKX(resultPL2, false));
                            GlobalVariables.readPL2 = dblPL2;
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Unable to get MCHBAR for intel CPU");

                }
            }
            catch (Exception ex)
            {
                string errorMsg = "Error: ChangeTDP.cs: Reading intel tdp: " + ex.Message + ", processKX is " + processKX + ", result pl1 and pl2 are " + resultPL1 + ", " + resultPL2 + ", command arguments are " + commandArgumentsPL1 + " and " + commandArgumentsPL2;
                StreamWriterLog.startStreamWriter(errorMsg);
                MessageBox.Show(errorMsg);

            }

        }
        static string parseHexFromResultMMIOConvertToTDPKX(string result, bool isPL1)
        {
            try
            {
                int FindString;
                string hexResult;
                float intResult;
                if (isPL1)
                {
                    FindString = result.IndexOf("Memory Data") + 22;
                    hexResult = result.Substring(FindString, 7).Trim();
                    intResult = (Convert.ToInt32(hexResult, 16) - 32768) / 8;
                    return intResult.ToString();

                }
                else
                {
                    FindString = result.IndexOf("Memory Data") + 22;
                    hexResult = result.Substring(FindString, 7).Trim();
                    intResult = (Convert.ToInt32(hexResult, 16) - 32768) / 8;
                    return intResult.ToString();
                }
            }
            catch (Exception ex)
            {
                string errorMsg = "Error: ChangeTDP.cs:  Parse intel tdp from result: " + ex.Message;
                StreamWriterLog.startStreamWriter(errorMsg);
                MessageBox.Show(errorMsg);
                return "Error";
            }



        }
        //MSR stuff here


        static string parseFromResultAMD(string result, string tdpLevel)
        {
            try
            {

                if (tdpLevel == "PL1")
                {
                    
                    return result.Substring(result.IndexOf("STAPM LIMIT") + 25, 6).Trim();
                }
                if (tdpLevel == "PL2")
                {
  
                    return result.Substring(result.IndexOf("PPT LIMIT SLOW") + 25, 6).Trim();
                }
                else { return "error"; }

            }
            catch (Exception ex)
            {
                string errorMsg = "Error: ChangeTDP.cs:  Parse AMD tdp from result: " + ex.Message + " result is " + result;
                StreamWriterLog.startStreamWriter(errorMsg);
                MessageBox.Show(errorMsg);
                return "Error";
            }

        }
            static string parseHexFromResultMSRConvertToTDP(string result, bool isPL1)
            {
                int FindString = -1;
                string hexResult = "";
                try
                {

                    float intResult;
                    if (isPL1)
                    {
                        FindString = result.IndexOf("0x00dd8") + 7;
                        hexResult = result.Substring(FindString, 3).Trim();
                        intResult = (Convert.ToInt32(hexResult, 16)) / 8;
                        return intResult.ToString();

                    }
                    else
                    {
                        FindString = result.IndexOf("0x00438") + 7;
                        if (FindString == 6) { FindString = result.IndexOf("0x00428") + 7; }
                        hexResult = result.Substring(FindString, 3).Trim();
                        intResult = (Convert.ToInt32(hexResult, 16)) / 8;
                        return intResult.ToString();
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = "Error: ChangeTDP.cs:  Parse intel tdp from result: " + ex.Message + "hexresult is " + hexResult + " and intFindString is " + FindString.ToString();
                    StreamWriterLog.startStreamWriter(errorMsg);
                    MessageBox.Show(errorMsg);
                    return "Error";
                }



            }
            static void runIntelReadTDPMSR()
            {
                try
                {
                    string processMSR = BaseDir + "\\Resources\\Intel\\MSR\\msr-cmd.exe";

                    lock (objLock)
                    {
                        string commandArguments = " read 0x610";
                        
                        string result = RunCLI.RunCommand(commandArguments, true, processMSR);
                        if (result != null)
                        {

                            double dblPL1 = Convert.ToDouble(parseHexFromResultMSRConvertToTDP(result, true));
                            GlobalVariables.readPL1 = dblPL1;

                            double dblPL2 = Convert.ToDouble(parseHexFromResultMSRConvertToTDP(result, false));
                            GlobalVariables.readPL2 = dblPL2;

                        }
                    }


                }
                catch (Exception ex)
                {
                    string errorMsg = "Error: ChangeTDP.cs: Reading intel tdp MSR: " + ex.Message;
                    StreamWriterLog.startStreamWriter(errorMsg);
                    MessageBox.Show(errorMsg);
                }

            }
            static string convertTDPToHexMSR(int tdp)
            {
                //Convert integer TDP value to Hex for rw.exe
                //Must use formula (TDP in watt   *1000/125) +32768 and convert to hex
                try
                {
                    int newTDP = (tdp * 8);
                    return newTDP.ToString("X");

                }
                catch (Exception ex)
                {
                    string errorMsg = "Error: ChangeTDP.cs:  convert MSR TDP To Hex: " + ex.Message;
                    StreamWriterLog.startStreamWriter(errorMsg);
                    MessageBox.Show(errorMsg);
                    return "Error";
                }
            }
            //MSR stuff above
            static void runAMDReadTDP()
            {
                string processRyzenAdj = "";
                string result = "";
                string commandArguments = "";
                try
                {
                     processRyzenAdj = BaseDir + "\\Resources\\AMD\\RyzenAdj\\ryzenadj.exe";
               
                    lock (objLock)
                    {
                        commandArguments = " -i";

                        
                        result = RunCLI.RunCommand(commandArguments, true, processRyzenAdj);
                    
                    



                    if (result != null)
                        {
       
                            double dblPL1 = Convert.ToDouble(parseFromResultAMD(result, "PL1"));
                            GlobalVariables.readPL1 = dblPL1;
                            double dblPL2 = Convert.ToDouble(parseFromResultAMD(result, "PL2"));
                            GlobalVariables.readPL2 = dblPL2;

                        }
                    }

                }
                catch (Exception ex)
                {
                    string errorMsg = "Error: ChangeTDP.cs:  Run AMD TDP Change: " + ex.Message + ", processRyzenAdj is " + processRyzenAdj + ", result is " + result + ", commandargument is " + commandArguments;
                    StreamWriterLog.startStreamWriter(errorMsg);
                    MessageBox.Show(errorMsg);

                }


            }
            static void runAMDTDPChange(int pl1TDP, int pl2TDP)
            {
                string processRyzenAdj = "";
                string result = "";
                string commandArguments = "";
                try
                {
                processRyzenAdj = BaseDir + "\\Resources\\AMD\\RyzenAdj\\ryzenadj.exe";

                lock (objLock)
                {
                    commandArguments = " --stapm-limit=" + (pl1TDP * 1000).ToString() + " --slow-limit=" + (pl2TDP * 1000).ToString() + " --fast-limit=" + (pl2TDP * 1000).ToString();

                    result = RunCLI.RunCommand(commandArguments, true, processRyzenAdj);
                    Thread.Sleep(100);
                }


            }
            catch (Exception ex)
                {
                    string errorMsg = "Error: ChangeTDP.cs:  Run AMD TDP Change: " + ex.Message + ", processRyzenAdj is " + processRyzenAdj + ", result is " + result + ", commandargument is " + commandArguments; ;
                    StreamWriterLog.startStreamWriter(errorMsg);
                    MessageBox.Show(errorMsg);

                }


            }

            //retired RW.exe stuff here\
            static void Retired_runIntelTDPChangeMSR(int pl1TDP, int pl2TDP)
            {
                try
                {

                //OLD RW STUFF, I CHANGED CLI METHOD SO THIS RUNCLI COMMAND NEEDS TO BE REVISITED IF I BRING THIS BACK
                    string processRW = BaseDir + "\\Resources\\Intel\\RW\\Rw.exe";
                    string hexPL1 = convertTDPToHexMSR(pl1TDP);
                    string hexPL2 = convertTDPToHexMSR(pl2TDP);
                    if (hexPL1 != "Error" && hexPL2 != "Error" && MCHBAR != null)
                    {
                        if (hexPL1.Length < 3)
                        {
                            if (hexPL1.Length == 1) { hexPL1 = "00" + hexPL1; }
                            if (hexPL1.Length == 2) { hexPL1 = "0" + hexPL1; }
                        }
                        if (hexPL2.Length < 3)
                        {
                            if (hexPL2.Length == 1) { hexPL2 = "00" + hexPL2; }
                            if (hexPL2.Length == 2) { hexPL2 = "0" + hexPL2; }
                        }
                        lock (objLock)
                        {
                            string commandArguments = " /nologo /stdout /command=" + '\u0022' + "wrmsr 0x610 0x00438" + hexPL2 + " 0x00dd8" + hexPL1 + ";" + '\u0022';

                            RunCLI.RunCommand(commandArguments, false, processRW);
                            Thread.Sleep(100);
                        }
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = "Error: ChangeTDP.cs:  Run Intel TDP Change: " + ex.Message;
                    StreamWriterLog.startStreamWriter(errorMsg);
                    MessageBox.Show(errorMsg);

                }


            }
            static void Retired_changeTDPRWMSR()
            {
                //Retired since rw.exe is going away after windows 11 22H2, will keep this code for those who want to use rw or want the example
                try
                {
                    string processRW = BaseDir + "\\Resources\\Intel\\RW\\Rw.exe";

                    lock (objLock)
                    {
                        string commandArguments = " /nologo /stdout /command=" + '\u0022' + "RDMSR 0x610 0x0 0x00000000 0;" + '\u0022';

                        string result = RunCLI.RunCommand(commandArguments, true, processRW);
                        if (result != null)
                        {

                            double dblPL1 = Convert.ToDouble(parseHexFromResultMSRConvertToTDP(result, true));
                            GlobalVariables.readPL1 = dblPL1;

                            double dblPL2 = Convert.ToDouble(parseHexFromResultMSRConvertToTDP(result, false));
                            GlobalVariables.readPL2 = dblPL2;

                        }
                    }


                }
                catch (Exception ex)
                {
                    string errorMsg = "Error: ChangeTDP.cs: Reading intel tdp: " + ex.Message;
                    StreamWriterLog.startStreamWriter(errorMsg);
                    MessageBox.Show(errorMsg);
                }

            }
            static string Retired_parseHexFromResultMMIOConvertToTDP(string result, bool isPL1)
            {
                //Retired since rw.exe is going away after windows 11 22H2, will keep this code for those who want to use rw or want the example
                try
                {
                    int FindString;
                    string hexResult;
                    float intResult;
                    if (isPL1)
                    {
                        FindString = result.IndexOf(MCHBAR + "A0 =") + MCHBAR.Length + 4;
                        hexResult = result.Substring(FindString, 7).Trim();
                        intResult = (Convert.ToInt32(hexResult, 16) - 32768) / 8;
                        return intResult.ToString();

                    }
                    else
                    {
                        FindString = result.IndexOf(MCHBAR + "A4 =") + MCHBAR.Length + 4;
                        hexResult = result.Substring(FindString, 7).Trim();
                        intResult = (Convert.ToInt32(hexResult, 16) - 32768) / 8;
                        return intResult.ToString();
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = "Error: ChangeTDP.cs:  Parse intel tdp from result: " + ex.Message;
                    StreamWriterLog.startStreamWriter(errorMsg);
                    MessageBox.Show(errorMsg);
                    return "Error";
                }



            }
            static string Retired_parseHexFromResultRWMSRConvertToTDP(string result, bool isPL1)
            {
                //Retired since rw.exe is going away after windows 11 22H2, will keep this code for those who want to use rw or want the example
                try
                {
                    int FindString;
                    string hexResult;
                    float intResult;
                    if (isPL1)
                    {
                        FindString = result.IndexOf("0x00DD8") + 7;
                        hexResult = result.Substring(FindString, 3).Trim();
                        intResult = (Convert.ToInt32(hexResult, 16)) / 8;
                        return intResult.ToString();

                    }
                    else
                    {
                        FindString = result.IndexOf("0x00428") + 7;
                        hexResult = result.Substring(FindString, 3).Trim();
                        intResult = (Convert.ToInt32(hexResult, 16)) / 8;
                        return intResult.ToString();
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = "Error: ChangeTDP.cs:  Parse intel tdp from result: " + ex.Message;
                    StreamWriterLog.startStreamWriter(errorMsg);
                    MessageBox.Show(errorMsg);
                    return "Error";
                }



            }
            static void Retired_runIntelReadTDPMMIORW()
            {
                //Retired since rw.exe is going away after windows 11 22H2, will keep this code for those who want to use rw or want the example
                try
                {
                    string processRW = BaseDir + "\\Resources\\Intel\\RW\\Rw.exe";
                    if (MCHBAR != null)
                    {
                        lock (objLock)
                        {
                            string commandArguments = " /nologo /stdout /command=" + '\u0022' + "Delay " + RWDelay + "; r16 " + MCHBAR + "a0; Delay " + RWDelay + "; r16 " + MCHBAR + "a4; Delay " + RWDelay + ";" + '\u0022';

                            string result = RunCLI.RunCommand(commandArguments, true, processRW);
                            if (result != null)
                            {

                                double dblPL1 = Convert.ToDouble(Retired_parseHexFromResultMMIOConvertToTDP(result, true));
                                GlobalVariables.readPL1 = dblPL1;

                                double dblPL2 = Convert.ToDouble(Retired_parseHexFromResultMMIOConvertToTDP(result, false));
                                GlobalVariables.readPL2 = dblPL2;
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Unable to get MCHBAR for intel CPU");

                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = "Error: ChangeTDP.cs: Reading intel tdp: " + ex.Message;
                    StreamWriterLog.startStreamWriter(errorMsg);
                    MessageBox.Show(errorMsg);

                }

            }
            static void Retired_runIntelTDPChangeMMIORW(int pl1TDP, int pl2TDP)
            {
                try
                {
                    string processRW = BaseDir + "\\Resources\\Intel\\RW\\Rw.exe";
                    string hexPL1 = convertTDPToHexMMIO(pl1TDP);
                    string hexPL2 = convertTDPToHexMMIO(pl2TDP);
                    MessageBox.Show(hexPL1);
                    if (hexPL1 != "Error" && hexPL2 != "Error" && MCHBAR != null)
                    {
                        lock (objLock)
                        {
                            string commandArguments = " /nologo /stdout /command=" + '\u0022' + "Delay " + RWDelay + "; w16 " + MCHBAR + "a0 0x" + hexPL1 + "; Delay " + RWDelay + "; w16 " + MCHBAR + "a4 0x" + hexPL2 + "; Delay " + RWDelay + ";" + '\u0022';

                            RunCLI.RunCommand(commandArguments, false, processRW);
                            Thread.Sleep(100);
                        }
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = "Error: ChangeTDP.cs:  Run Intel TDP Change: " + ex.Message;
                    StreamWriterLog.startStreamWriter(errorMsg);
                    MessageBox.Show(errorMsg);

                }


            }
        }
    }

