using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using com.Xgensoftware.Core;

namespace EncryptionTest
{
    public partial class Form1 : Form
    {
        public readonly byte[] salt = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }; 
        public const int iterations = 1042;
        Logging _log = null;
        List<string> _filesToencrypt = new List<string>();        
        List<Task> _tasks;
        string _password = string.Empty;
        #region Private Methods 
        private void DisableControls()
        {
            txtToEncrypt.Enabled = false;
            txtOutputFolder.Enabled = false;
            txtEncryptionKey.Enabled = false;
            btnProcess.Enabled = false;
            btnDecrypt.Enabled = false;
        }

        private void EnableControls()
        {
            txtToEncrypt.Enabled = true;
            txtOutputFolder.Enabled = true;
            txtEncryptionKey.Enabled = true;
            btnProcess.Enabled = true;
            btnDecrypt.Enabled = true;
        }

        private void WriteLog(string message)
        {
            string logMsg = string.Format("{0}:  {1}", DateTime.Now.ToString("yyyyMMdd HH:mm:ss"), message);
            _log.LogMessage(Logging.LogMessageType.INFO, logMsg);
        }

        private void Encrypt(string inputFile, string outputFile)
        {
            try
            {
                AesManaged aes = new AesManaged();
                aes.BlockSize = aes.LegalBlockSizes[0].MaxSize;
                aes.KeySize = aes.LegalKeySizes[0].MaxSize;
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(_password, salt, iterations);
                aes.Key = key.GetBytes(aes.KeySize / 8);
                aes.IV = key.GetBytes(aes.BlockSize / 8);
                aes.Mode = CipherMode.CBC;
                ICryptoTransform transform = aes.CreateEncryptor(aes.Key, aes.IV);

                using (FileStream destination = new FileStream(outputFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(destination, transform, CryptoStreamMode.Write))
                    {
                        using (FileStream source = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            source.CopyTo(cryptoStream);

                            WriteLog(string.Format("Encrypted: {0} : {1}", inputFile, outputFile));
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                WriteLog(string.Format("Failed to encrypt {0}.ERROR: {1}",inputFile,ex.Message));
            }
            
        }

        private void Decrypt(string inputFile, string outputFile)
        {
            try
            {
                AesManaged aes = new AesManaged();
                aes.BlockSize = aes.LegalBlockSizes[0].MaxSize;
                aes.KeySize = aes.LegalKeySizes[0].MaxSize;
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(_password, salt, iterations);
                aes.Key = key.GetBytes(aes.KeySize / 8);
                aes.IV = key.GetBytes(aes.BlockSize / 8);
                aes.Mode = CipherMode.CBC;
                ICryptoTransform transform = aes.CreateDecryptor(aes.Key, aes.IV);

                using (FileStream destination = new FileStream(outputFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(destination, transform, CryptoStreamMode.Write))
                    {
                        using (FileStream source = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            source.CopyTo(cryptoStream);
                            WriteLog(string.Format("Decrypting: {0} : {1}", inputFile, outputFile));
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                WriteLog(string.Format("Failed to decrypt {0}.ERROR: {1}", inputFile, ex.Message));
            }
        }

        public void CreateZipFile(string fileName, DirectoryInfo source)
        {
            // Create and open a new ZIP file
            var zip = ZipFile.Open(fileName, ZipArchiveMode.Create);
            foreach (var file in source.GetFiles())
            {
                // Add the entry for each filezip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
                zip.CreateEntryFromFile(file.Name, file.FullName, CompressionLevel.Fastest);
            }
            // Dispose of the object when we are done
            zip.Dispose();
        }

        private void RunProcess(string cmd, DirectoryInfo source, DirectoryInfo target)
        {            
            foreach (DirectoryInfo dir in source.GetDirectories())
            {
                RunProcess(cmd, dir, target.CreateSubdirectory(dir.Name));
            }

            var newTask = Task.Factory.StartNew(() =>
            {

                Parallel.ForEach(source.GetFiles(), file =>
                {
                    switch (cmd)
                    {
                        case "0":
                            Encrypt(string.Format(@"\\?\{0}", file.FullName), string.Format(@"\\?\{0}\{1}", target.FullName, file.Name));
                            break;

                        case "1":
                            Decrypt(string.Format(@"\\?\{0}", file.FullName), string.Format(@"\\?\{0}\{1}", target.FullName, file.Name));
                            break;
                    }
                });
            });

            _tasks.Add(newTask);
        }

        private void RunProcess(string cmd, string source, string target)
        {
            DirectoryInfo s = new DirectoryInfo(source);
            DirectoryInfo t = new DirectoryInfo(target);

            foreach (DirectoryInfo sourceDirPath in s.GetDirectories())
            {
                RunProcess(cmd, sourceDirPath.FullName, t.CreateSubdirectory(s.Name).FullName);
            }

            var newTask = Task.Factory.StartNew(() =>
            {
                Parallel.ForEach(s.GetFiles(), file => {
                    switch (cmd)
                    {
                        case "0":
                            Encrypt(file.FullName, string.Format(@"{0}\{1}", t.FullName, file.Name));
                            break;

                        case "1":
                            Decrypt(file.FullName, string.Format(@"{0}\{1}", t.FullName, file.Name));
                            break;
                    }
                });

            });

            _tasks.Add(newTask);
        }
           

        private void Test()
        {
            string reallyLongDirectory = @"C:\Test\abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ\abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ\abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            reallyLongDirectory = reallyLongDirectory + @"\abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ\abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ\abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            reallyLongDirectory = reallyLongDirectory + @"\abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ\abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ\abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

            Console.WriteLine($"Creating a directory that is {reallyLongDirectory.Length} characters long");
            Directory.CreateDirectory(reallyLongDirectory);
        }
        #endregion


        public Form1()
        {
            InitializeComponent();

            _log = new Logging("EncryptionTest.log");

            txtToEncrypt.Text = @"D:\TestFiles";
            txtOutputFolder.Text = @"D:\MoveTest";
        }

        private void RunProcess_Click(object sender, EventArgs e)
        {
            _tasks = new List<Task>();
            Button cmd = (Button)sender;

            if (string.IsNullOrEmpty(txtToEncrypt.Text))
            {
                MessageBox.Show("You must enter a path to encrypt");
                txtToEncrypt.Focus();
                return;
            }

            if (string.IsNullOrEmpty(txtOutputFolder.Text))
            {
                MessageBox.Show("You must enter an output path");
                txtOutputFolder.Focus();
                return;
            }

            if (string.IsNullOrEmpty(txtEncryptionKey.Text))
            {
                MessageBox.Show("You must enter an encryption key.  Please keep this in a safe place as it will be required to decrypt files.");
                txtEncryptionKey.Focus();
                return;
            }


            if (!string.IsNullOrEmpty(txtToEncrypt.Text))
            {
                DisableControls();

                DateTime startTime = DateTime.Now;
                WriteLog(string.Format("********************** Starting Process at {0}", DateTime.Now.ToShortTimeString()));

                //Parallel.ForEach(Directory.GetDirectories(txtToEncrypt.Text), dirPath => {
                //    RunProcess(cmd.Tag.ToString(), string.Format(@"\\?\{0}", dirPath), string.Format(@"\\?\{0}", txtOutputFolder.Text));
                //});

                foreach (string dirPath in Directory.GetDirectories(txtToEncrypt.Text))
                {
                    RunProcess(cmd.Tag.ToString(), string.Format(@"\\?\{0}", dirPath), string.Format(@"\\?\{0}", txtOutputFolder.Text));
                }

                Task.WaitAll(_tasks.ToArray());

                WriteLog(string.Format("********************** Ending Process at {0}", DateTime.Now.ToShortTimeString()));
                DateTime stopTime = DateTime.Now;

                MessageBox.Show("Process Complete");
                WriteLog(string.Format("StartTime: {0} StopTime: {1}", startTime.ToShortTimeString(), stopTime.ToShortTimeString()));

                EnableControls();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {            
            this.Close();
        }

        private void btnEncryptFldDlg_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                txtToEncrypt.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                txtOutputFolder.Text = folderBrowserDialog1.SelectedPath;
            }
        }
    }
}
