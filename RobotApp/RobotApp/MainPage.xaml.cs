using System;
using System.IO;
using System.Diagnostics;
using Windows.UI.Xaml.Controls;
using Windows.Storage;

namespace RobotApp
{
    public sealed partial class MainPage : Page
    {
        private static String defaultHostName = "tak-hp-laptop";
        public static String serverHostName = defaultHostName; // read from config file
        public static bool isRobot = true; // determined by existence of hostName

        public static Stopwatch stopwatch;

        /// <summary>
        /// MainPage initialize all asynchronous functions
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();

            GetRunningMode();

            stopwatch = new Stopwatch();
            stopwatch.Start();

            Controllers.XboxJoystickInit();
            NetworkCmd.NetworkInit(serverHostName);
            if (isRobot) MotorCtrl.MotorsInit();

        }

        /// <summary>
        /// Show the current running mode
        /// </summary>
        public void ShowStartupStatus()
        {
            this.CurrentState.Text = "Robot-Kit Sample";
            this.Connection.Text = (isRobot ? ("Robot to " + serverHostName) : "Controller");
        }

        /// <summary>
        /// Switch and store the current running mode in local config file
        /// </summary>
        public async void SwitchRunningMode ()
        {
            try
            {
                if (serverHostName.Length > 0) serverHostName = "";
                else serverHostName = defaultHostName;

                StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
                StorageFile configFile = await storageFolder.CreateFileAsync("config.txt", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(configFile, serverHostName);

                isRobot = serverHostName.Length > 0;
                ShowStartupStatus();
                NetworkCmd.NetworkInit(serverHostName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetRunningMode() - " + ex.Message);
            }
        }

        /// <summary>
        /// Read the current running mode (controller host name) from local config file
        /// </summary>
        public async void GetRunningMode()
        {
            try
            {
                StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
                StorageFile configFile = await storageFolder.GetFileAsync("config.txt");
                String fileContent = await FileIO.ReadTextAsync(configFile);
                serverHostName = fileContent;
                isRobot = (serverHostName.Length > 0);
                ShowStartupStatus();
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine("GetRunningMode() - configuration does not exist yet.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GetRunningMode() - " + ex.Message);
            }

        }
    }
}
