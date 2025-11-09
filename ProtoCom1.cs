//---------------------------------------------------------------------------
//      class:          ProtoCom1
//      
//      purpose:        Simple serial protocol for Industrial/IoT/Embedded automation.
//                      Message starts with a single character and data follows to \n.
//                      Can be used in conditional scripts or manually in console.
//      motivation:     The author wants to popularize the concept and encorages
//                      freely use and contributions to the open-source project.
//                      * see at the bottom for more details *
//
//      responsibility: Distributed AS IS. Use at your own risk.
//      github:         https://github.com/alex-penchev/ProtoCom1
//---------------------------------------------------------------------------
//      version:        0.0.1
//      created on:     Nov 01, 2025
//      last update:    Nov 01, 2025
//      authors (c):    Alexander Penchev (Distributed under GPLv3 License)
//      contact:        penchev.office@icloud.com
//--------------------------------------------------------------------------- 
//      dependencies:   .Net Core 8.0 and above, System.IO.Ports package
//
//      Protocol description and action chars:  
//          [1B Action]([1B Length for D,Z])([data 0...L-1])[1B \n termination]
//          \r is used to show new line within message
//
//          М Message/Manifesto (direct to user console, not sent)
//          
//          ? [$var]=[value], (last=)[value] (condition, if empty always true)
//          $ var(=value), $$ default
//          # comment (not executed, not displayed, only script)
//          : subroutine marker (terminates previous ":")
//          J Jump to valid marker (Go-To)
//            empty row (skipped, like #)
//          ` Adds current date/time
//          
//          U User request (prompts user interaction)
//          > set the last data to a variable
//          W Write the last data to a [file] 
//          L Load from a [file] and send (use with caution)
//          Q Quit (aborts script and exits)
//          
//          C Command (sends/receives)
//
//          Dn binary Data [d0...d255] (sends/receives)
//          H Hex bytes [h1...hL] (sends/receives)
//          T Text [A1...AN] (sends/receives)
//          E End/Exit: terminates previous sequence (sends/receives)
//          
//          B Back (sends)
//          K Kill: emergency off (sends)
//          G Get [param] (sends)
//          P Put/Proceed/Program [param]=[value] (sends)
//          
//          H Halt application (sends)
//          I Initialize/clear current state to default (sends)
//          A Activate/run/start execution (sends)
//          
//          S device Status (sends)
//          R Report: print all information (sends)
//          V tell me device Version
//          F Firmware version
//          
//          X XOR(bin) to the previous data/command [8bit-H-CRC]
//          Zn other binary checksum [z0...z255] (folows)
//          
//          Y true/Yes/acknowledge
//          N false/No/Negative
//          
//          \n - execute command, confirmation
//          * represents \n - shown as remote reception confirmation
//          
//          O Original: MD5 hash to the current line of script
//---------------------------------------------------------------------------
//      usage:        
//              ProtoCom1 obj = new(params);
//              obj.DoSomeStuff(params);
//---------------------------------------------------------------------------
//      revisions:
//      0.0.1   2025-11-01  Initial release
//--------------------------------------------------------------------------- 

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Text;

namespace ProtoCom1Emb
{
    /// <summary>
    ///     Simple conditional protocol for industrial/IoT/embedded automation.
    ///     Handles scripts and commands sent/received through any serial communication.   
    /// </summary>
    public class ProtoCom1
    {
        // --- Public interfaces ---
        public delegate void InfoHandler(string message, bool bRequiresUI=false);
        public delegate void CommandCallback(string sentCmd, string retText, byte[] retData);

        // --- Private members ---
        TaskCompletionSource<string> _tcs = new();
        int _maxSubCalls = 5;
        char _command;
        int _length;
        string _sCOMPort = "";
        Dictionary<string, List<CommandInfo>> _dRoutines = [];
        Dictionary<string, string> _dVariables = [];
        SerialPort? _serialPort=null;

        string _lastRow = "";
        byte[] _lastData = [];
        string _currentCommand = "";
        bool _quitSignal = false;
        CommandCallback? _commandCallback = null;
        InfoHandler? _infoHandler = null;

        public ProtoCom1(string sCOMPort = "", 
            InfoHandler? infoHandler = null, 
            CommandCallback? commandCallback = null)
        {
            if (sCOMPort != "")
            {
                _sCOMPort = sCOMPort;
                OpenCOMPort(sCOMPort);
            }
            if(infoHandler != null)
                _infoHandler = infoHandler;
            if (commandCallback != null)
                _commandCallback = commandCallback;
        }
        ~ProtoCom1()
        {
            CloseCOMPort();
        }
        // --- Methods ---
        void Log(string message)
        {
            _infoHandler?.Invoke(message);
            Console.WriteLine(message);
        }
        public async void LoadFromFile(string filePath)
        {
            try
            {
                // Reset state
                _dRoutines = [];
                _dVariables = [];

                string [] scriptRows = File.ReadAllLines(filePath);
                //_dRoutines.Add(Path.GetFileNameWithoutExtension(filePath), lines);
                List<CommandInfo> commands = [];
                int lineNumber = 0;
                foreach (var line in scriptRows)
                {
                    lineNumber++;
                    string lineClean = line.Trim();
                    if (lineClean == "" || lineClean.StartsWith('#'))
                        continue;
                    if(lineClean.StartsWith('O'))
                    {
                        string combined = "";
                        for(int i=0; i<lineNumber-1; i++)
                        {
                            combined += scriptRows[i] + "\n";
                        }
                        if(CalcMD5(combined, lineClean) == false)
                        {
                            Log("Script error: MD5 hash mismatch");
                            return;
                        }
                    }
                    else if(!lineClean.StartsWith('D'))
                        commands.Add(new CommandInfo(line));
                    else
                    {
                        // Must parse binary data
                        byte[] asciiBytes = Encoding.ASCII.GetBytes(
                            line.Substring(2,line.Length));
                        commands.Add(new CommandInfo("D", asciiBytes));
                    }
                }
                //
                string? lastRoutine = "";
                List<CommandInfo> routineCommands = [];
                foreach (var command in commands)
                {
                    if (command.Command == ':')
                    {
                        _dRoutines.Add(lastRoutine, routineCommands);
                        lastRoutine = command.Text;
                        routineCommands = [];
                        if (lastRoutine == null)
                        {
                            Log("Script error: Routine name missing after ':'");
                            return;
                        }
                    }
                    else
                        routineCommands.Add(command);                  
                }
                _dRoutines.Add(lastRoutine, routineCommands);

                await ExecuteScript(); // async method
                return;
            }
            catch (Exception ex)
            {
                Log($"Error scripting file: {filePath}");
                Log($"{ex.Message}");
            }
            return;
        }
        public bool SaveToFile(string filePath)
        {
            try
            {
                using StreamWriter file = new(filePath, append: true);
                file.WriteLine(_lastRow);
               
                return true;
            }
            catch { }
            return false;
        }
        /// <summary>
        /// Conditional check
        /// == equal | <> not equal
        /// </summary>
        public bool CheckCondition(string condition)
        {
            string c1 = "";
            string c2 = "";
            condition = condition.Trim();
            string delimiter = "==";
            if (condition.Contains("<>"))
                delimiter = "<>";

            if (condition.Length > 1)
            {
                c1 = condition = condition[1..].Trim();
                if (condition.Contains(delimiter))
                {
                    c1 = condition[..condition.IndexOf(delimiter)].Trim();
                    c2 = condition[(condition.IndexOf(delimiter) + 2)..].Trim();
                }
                else
                    c2 = _lastRow;

                if (c1 == "" || c2 == "")
                {
                    Log($"Error: cannot process condition '{condition}'");
                    _quitSignal = true;
                    return false;
                }

                if (c1[0] == '$')
                    c1 = _dVariables[c1];
                if (c2[0] == '$')
                    c2 = _dVariables[c2];
            }

            if(delimiter == "<>")
                return c1 != c2;
            return c1 == c2;
        }
        public void AddVariable(string variable, string? value = null)
        {
            string varName = variable;
            if (variable.Contains('='))
            {
                varName = variable[..variable.IndexOf('=')].Trim();
                value ??= variable[(variable.IndexOf('=') + 1)..].Trim();
                if (value[0] == '$')
                    value = _dVariables[value];
            }
            varName = varName.Replace(" ", "");
            _dVariables.Add(varName, value ?? "");
        }
        public void UserInput(string text)
        {
            _tcs.SetResult(text);
        }
        public async Task WaitForUserInputAsync()
        {
            string result = await _tcs.Task;
            _tcs = new();

            // TBD
            
        }
        public async void Jump(string location)
        {
            // TBD
            // check recursion level
            if (location.Length > 0)
                location = location[1..].Trim();   
            await ExecuteScript(location);
        }
        public bool Send(string command, byte[]? data = null)
        {
            
            // Variants: serial or other type of serial communication
            return false;
        }
        public bool SendHex(string command)
        {
            // TBD Format hex data
            // Send
            return false;
        }
        public bool SendCRC(string command)
        {
            // TBD Calculate CRC
            // Send
            return false;
        }
        public string LoadCommandFromFile(string filePath)
        {
            // TBD Load command from file and send
            return "";
        }
        public static string CalcChecksum(string text)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(text);
            return CalcChecksum(inputBytes);
        }
        public static string CalcChecksum(byte[] bytes)
        {
            byte checksum = 0;
            foreach (byte b in bytes)
            {
                checksum ^= b;
            }
            return checksum.ToString("x2");
        }
        public static bool CalcMD5(string lines, string sMD5Compare)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(lines);
            byte[] hashBytes = MD5.HashData(inputBytes);
            StringBuilder sb = new();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sMD5Compare.Contains(sb.ToString());
        }
        public static byte[] GetBytes(string hexString)
        {             
            int length = hexString.Length;
            byte[] bytes = new byte[length / 2];
            for (int i = 0; i < length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }
            return bytes;
        }
        public bool OpenCOMPort(string portName)
        {
            try
            {
                _serialPort = new SerialPort(portName);
                _serialPort.Open();
                return true;
            }
            catch { }

            return false;
        }
        public bool CloseCOMPort()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                return true;
            }
            catch { }

            return false;
        }
        public async Task ExecuteScript(string routine="")
        {
            List<CommandInfo> commands = _dRoutines[routine];
            if (commands == null)
            {
                Log($"Error: Routine '{routine}' not found");
                return;
            }

            bool bOpen = true;
            foreach (var command in commands)
            {
                if (command.Command == '?')
                {
                    bOpen = CheckCondition(command.RawCommand);
                    if(_quitSignal)
                        return;
                }
                else if (bOpen)
                {
                    switch (command.Command)
                    {
                        case '$':
                            AddVariable(command.RawCommand);
                            break;
                        case 'J':
                            Jump(command.RawCommand);
                            break;
                        case '>':
                            AddVariable(command.RawCommand);
                            break;
                        case 'D':
                            // prepare binary data
                            Send("D", []);
                            break;
                        case 'H':
                            SendHex(command.RawCommand);
                            break;
                        case 'L':
                            LoadCommandFromFile(command.RawCommand);
                            break;
                        case 'M':
                            Log(command.RawCommand);
                            break;
                        case 'Q':
                            _quitSignal = true;
                            _infoHandler?.Invoke("-QUIT!-");
                            return;
                        case 'U':
                            _infoHandler?.Invoke(command.RawCommand, true);
                            await WaitForUserInputAsync();
                            break;
                        case 'W':
                            SaveToFile(_lastRow);
                            break;
                        case 'X':
                            SendCRC(command.RawCommand);
                            break;
                        case 'Z':
                            SendHex(command.RawCommand);
                            break;
                        default:
                            // sends any command to the device
                            Send(command.RawCommand);
                            break;
                    }
                }

                _commandCallback?.Invoke(command.RawCommand, _lastRow, _lastData);
                if(_quitSignal)
                    return;
            
               
            }
            return;
        }
        // --- Private Methods ---
    }
    class CommandInfo
    {
        public char Command { get; set; }
        public string RawCommand { get; set; }
        public string? Text { get; set; }
        public byte[]? Data { get; set; }
        public List<string>? Parameters { get; set; }
        public CommandInfo(string rawCommand, byte[]? data = null)
        {
            RawCommand = rawCommand;
            if (rawCommand.Length > 0)
                Command = rawCommand[0];
            if (rawCommand.Length > 2)
                Text = rawCommand[2..];

            Data = data;

            Parameters = [];
            if (Text != null)
            {
                Parameters.AddRange(Text.Split(' ',
                    StringSplitOptions.RemoveEmptyEntries));
            }
        }
    }
}

//---------------------------------------------------------------------------
//  ProtoCom1  -=Q&A=-
//
//  # What ProtoCom1 does?
//      * One simple serial protocol addresses different embedded devices but
//        also instructs the master computer to perform additional tasks.
//      * High-level protocol encapsulates different custom extensible commands 
//        and data types (text, hex, binary) into a simple sequences w/o CRCs.
//
//  # What are the strong points?
//      * The communication library does the binary conversions, the protocol
//        remains human-readable and easy to use in consoles and terminals.
//      * Compact overhead (generally 2 bytes), it can be implemented in
//        low-resource, even cheapest 8-bit microcontrollers with UART. 
//  
//  # What is the jazzy thing!
//      * The protocol is scriptable, can perform conditional execution, causal: 
//        the master can ask for user input and wait for data from the device
//        to check parameters and to branch during the script execution.
//      * The received data can be stored in script variables or to trigger callbacks 
//        for post-processing and graphical visualization with minimum coding effort.
//
//  # And even more...
//      * Different scripts can be designed for tasks like firmware update,
//        configuration, diagnostics, tests, data acquisition and saving, etc. 
//      * The script can check the device state, version, firmware ID, etc.
//        and to addapt the execution flow accordingly.
//      * The protocol is AI/LLM friendly and can be integrated with automation
//        interfaces, MCPs, external control systems at minimal cost.
//---------------------------------------------------------------------------