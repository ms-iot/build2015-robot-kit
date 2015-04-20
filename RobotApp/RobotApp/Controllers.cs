using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Diagnostics;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;

namespace RobotApp
{
    /// <summary>
    /// **** MainPage class - controller input ****
    ///   Things in the MainPage class handle the App level startup, and App XAML level Directional inputs to the robot.
    ///   XAML sourced input controls, include screen buttons, and keyboard input
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region ----- on-screen click/touch controls -----
        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            TouchDir(Controllers.CtrlCmds.Forward);
        }
        private void Left_Click(object sender, RoutedEventArgs e)
        {
            TouchDir(Controllers.CtrlCmds.Left);
        }
        private void Right_Click(object sender, RoutedEventArgs e)
        {
            TouchDir(Controllers.CtrlCmds.Right);
        }
        private void Backward_Click(object sender, RoutedEventArgs e)
        {
            TouchDir(Controllers.CtrlCmds.Backward);
        }
        private void ForwardLeft_Click(object sender, RoutedEventArgs e)
        {
            TouchDir(Controllers.CtrlCmds.ForwardLeft);
        }
        private void ForwardRight_Click(object sender, RoutedEventArgs e)
        {
            TouchDir(Controllers.CtrlCmds.ForwardRight);
        }
        private void BackwardLeft_Click(object sender, RoutedEventArgs e)
        {
            TouchDir(Controllers.CtrlCmds.BackLeft);
        }
        private void BackwardRight_Click(object sender, RoutedEventArgs e)
        {
            TouchDir(Controllers.CtrlCmds.BackRight);
        }
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            TouchDir(Controllers.CtrlCmds.Stop);
        }
        private void Status_Click(object sender, RoutedEventArgs e)
        {
            // just update the display, without affecting direction of robot.  useful for diagnosting state
            UpdateClickStatus();
        }
        private void SwitchMode_Click(object sender, RoutedEventArgs e)
        {
            SwitchRunningMode();
        }
        private void TouchDir (Controllers.CtrlCmds dir)
        {
            Controllers.FoundLocalControlsWorking = true;
            Controllers.SetRobotDirection(dir, (int)Controllers.CtrlSpeeds.Max);
            UpdateClickStatus();
        }

        /// <summary>
        /// Virtual Key input handlers.  Keys directed here from XAML settings in MainPage.XAML
        /// </summary>
        private void Background_KeyDown_1(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            Debug.WriteLine("KeyDn: \"" + e.Key.ToString() + "\"");
            VKeyToRobotDirection(e.Key);
            UpdateClickStatus();
        }
        private void Background_KeyUp_1(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            VKeyToRobotDirection(Windows.System.VirtualKey.Enter);
            UpdateClickStatus();
        }
        static void VKeyToRobotDirection(Windows.System.VirtualKey vkey)
        {
            switch (vkey)
            {
                case Windows.System.VirtualKey.Down: Controllers.SetRobotDirection(Controllers.CtrlCmds.Backward,   (int)Controllers.CtrlSpeeds.Max); break;
                case Windows.System.VirtualKey.Up: Controllers.SetRobotDirection(Controllers.CtrlCmds.Forward,      (int)Controllers.CtrlSpeeds.Max); break;
                case Windows.System.VirtualKey.Left: Controllers.SetRobotDirection(Controllers.CtrlCmds.Left,       (int)Controllers.CtrlSpeeds.Max); break;
                case Windows.System.VirtualKey.Right: Controllers.SetRobotDirection(Controllers.CtrlCmds.Right,     (int)Controllers.CtrlSpeeds.Max); break;

                case Windows.System.VirtualKey.X: Controllers.SetRobotDirection(Controllers.CtrlCmds.Backward,      (int)Controllers.CtrlSpeeds.Max); break;
                case Windows.System.VirtualKey.W: Controllers.SetRobotDirection(Controllers.CtrlCmds.Forward,       (int)Controllers.CtrlSpeeds.Max); break;
                case Windows.System.VirtualKey.A: Controllers.SetRobotDirection(Controllers.CtrlCmds.Left,          (int)Controllers.CtrlSpeeds.Max); break;
                case Windows.System.VirtualKey.D: Controllers.SetRobotDirection(Controllers.CtrlCmds.Right,         (int)Controllers.CtrlSpeeds.Max); break;
                case Windows.System.VirtualKey.Z: Controllers.SetRobotDirection(Controllers.CtrlCmds.BackLeft,      (int)Controllers.CtrlSpeeds.Max); break;
                case Windows.System.VirtualKey.C: Controllers.SetRobotDirection(Controllers.CtrlCmds.BackRight,     (int)Controllers.CtrlSpeeds.Max); break;
                case Windows.System.VirtualKey.Q: Controllers.SetRobotDirection(Controllers.CtrlCmds.ForwardLeft,   (int)Controllers.CtrlSpeeds.Max); break;
                case Windows.System.VirtualKey.E: Controllers.SetRobotDirection(Controllers.CtrlCmds.ForwardRight,  (int)Controllers.CtrlSpeeds.Max); break;

                case Windows.System.VirtualKey.Enter:
                default: Controllers.SetRobotDirection(Controllers.CtrlCmds.Stop, (int)Controllers.CtrlSpeeds.Max); break;
            }
            Controllers.FoundLocalControlsWorking = true;
        }

        /// <summary>
        /// UpdateClickStatus() - fill in Connection status, and current direction State on screen after each button touch/click
        /// </summary>
        private void UpdateClickStatus()
        {
            this.CurrentState.Text = Controllers.lastSetCmd.ToString();
            if (MainPage.isRobot)
            {
                this.Connection.Text = "Robot mode";
            }
            else
            {
                if ((stopwatch.ElapsedMilliseconds - NetworkCmd.msLastSendTime) > 6000)
                {
                    this.Connection.Text = "NOT SENDING";
                }
                else
                {
                    this.Connection.Text = "OK";
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// **** Controllers Class ****
    /// HID Controller devices - XBox controller
    ///   Data transfer helpers: message parsers, direction to motor value translatores, etc.
    /// </summary>
    public class Controllers
    {
        public static bool FoundLocalControlsWorking = false;

        #region ----- Xbox HID-Controller -----

        private static XboxHidController controller;
        public static async void XboxJoystickInit()
        {
            string deviceSelector = HidDevice.GetDeviceSelector(0x01, 0x05);
            DeviceInformationCollection deviceInformationCollection = await DeviceInformation.FindAllAsync(deviceSelector);

            if (deviceInformationCollection.Count == 0)
            {
                Debug.WriteLine("No Xbox360 controller found!");
            }

            foreach (DeviceInformation d in deviceInformationCollection)
            {
                Debug.WriteLine("Device ID: " + d.Id);

                HidDevice hidDevice = await HidDevice.FromIdAsync(d.Id, Windows.Storage.FileAccessMode.Read);

                if (hidDevice == null)
                {
                    try
                    {
                        var deviceAccessStatus = DeviceAccessInformation.CreateFromId(d.Id).CurrentStatus;

                        if (!deviceAccessStatus.Equals(DeviceAccessStatus.Allowed))
                        {
                            Debug.WriteLine("DeviceAccess: " + deviceAccessStatus.ToString());
                            FoundLocalControlsWorking = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Xbox init - " + e.Message);
                    }

                    Debug.WriteLine("Failed to connect to the controller!");
                }

                controller = new XboxHidController(hidDevice);
                controller.DirectionChanged += Controller_DirectionChanged;
            }
        }

        private static void Controller_DirectionChanged(ControllerVector sender)
        {
            FoundLocalControlsWorking = true;
            Debug.WriteLine("Direction: " + sender.Direction + ", Magnitude: " + sender.Magnitude);
            XBoxToRobotDirection((sender.Magnitude < 2500) ? ControllerDirection.None : sender.Direction, sender.Magnitude);

            MotorCtrl.speedValue = sender.Magnitude;
        }
    
        static void XBoxToRobotDirection(ControllerDirection dir, int magnitude)
        {
            switch (dir)
            {
                case ControllerDirection.Down:      SetRobotDirection(CtrlCmds.Backward, magnitude);        break;
                case ControllerDirection.Up:        SetRobotDirection(CtrlCmds.Forward, magnitude);         break;
                case ControllerDirection.Left:      SetRobotDirection(CtrlCmds.Left, magnitude);            break;
                case ControllerDirection.Right:     SetRobotDirection(CtrlCmds.Right, magnitude);           break;
                case ControllerDirection.DownLeft:  SetRobotDirection(CtrlCmds.BackLeft, magnitude);        break;
                case ControllerDirection.DownRight: SetRobotDirection(CtrlCmds.BackRight, magnitude);       break;
                case ControllerDirection.UpLeft:    SetRobotDirection(CtrlCmds.ForwardLeft, magnitude);     break;
                case ControllerDirection.UpRight:   SetRobotDirection(CtrlCmds.ForwardRight, magnitude);    break;
                default:                            SetRobotDirection(CtrlCmds.Stop, (int)CtrlSpeeds.Max);  break;
            }
        }
        #endregion

        #region ----- general command/control helpers -----

        public enum CtrlCmds { Stop, Forward, Backward, Left, Right, ForwardLeft, ForwardRight, BackLeft, BackRight };
        public enum CtrlSpeeds { Min=0, Mid=5000, Max=10000 }

        public static long msLastDirectionTime;
        public static CtrlCmds lastSetCmd;
        public static void SetRobotDirection(CtrlCmds cmd, int speed)
        {
            if (MainPage.isRobot)
            {
                switch (cmd)
                {
                    case CtrlCmds.Forward:      MotorCtrl.waitTimeLeft = MotorCtrl.PulseMs.ms2;     MotorCtrl.waitTimeRight = MotorCtrl.PulseMs.ms1;    break;
                    case CtrlCmds.Backward:     MotorCtrl.waitTimeLeft = MotorCtrl.PulseMs.ms1;     MotorCtrl.waitTimeRight = MotorCtrl.PulseMs.ms2;    break;
                    case CtrlCmds.Left:         MotorCtrl.waitTimeLeft = MotorCtrl.PulseMs.ms1;     MotorCtrl.waitTimeRight = MotorCtrl.PulseMs.ms1;    break;
                    case CtrlCmds.Right:        MotorCtrl.waitTimeLeft = MotorCtrl.PulseMs.ms2;     MotorCtrl.waitTimeRight = MotorCtrl.PulseMs.ms2;    break;
                    case CtrlCmds.ForwardLeft:  MotorCtrl.waitTimeLeft = MotorCtrl.PulseMs.stop;    MotorCtrl.waitTimeRight = MotorCtrl.PulseMs.ms1;    break;
                    case CtrlCmds.ForwardRight: MotorCtrl.waitTimeLeft = MotorCtrl.PulseMs.ms2;     MotorCtrl.waitTimeRight = MotorCtrl.PulseMs.stop;   break;
                    case CtrlCmds.BackLeft:     MotorCtrl.waitTimeLeft = MotorCtrl.PulseMs.stop;    MotorCtrl.waitTimeRight = MotorCtrl.PulseMs.ms2;    break;
                    case CtrlCmds.BackRight:    MotorCtrl.waitTimeLeft = MotorCtrl.PulseMs.ms1;     MotorCtrl.waitTimeRight = MotorCtrl.PulseMs.stop;   break;
                    default:
                    case CtrlCmds.Stop:         MotorCtrl.waitTimeLeft = MotorCtrl.PulseMs.stop;    MotorCtrl.waitTimeRight = MotorCtrl.PulseMs.stop;   break;
                }
                if (speed < (int)CtrlSpeeds.Min) speed = (int)CtrlSpeeds.Min;
                if (speed > (int)CtrlSpeeds.Max) speed = (int)CtrlSpeeds.Max;
                MotorCtrl.speedValue = speed;

                dumpOnDiff(cmd.ToString());
            }
            else
            {
                String sendStr = "[" + (Convert.ToInt32(cmd)).ToString() + "]:" + cmd.ToString();
                NetworkCmd.SendCommandToRobot(sendStr);
            }
            msLastDirectionTime = MainPage.stopwatch.ElapsedMilliseconds;
            lastSetCmd = cmd;
        }

        private static MotorCtrl.PulseMs lastWTL, lastWTR;
        private static int lastSpeed;
        static void dumpOnDiff(String title)
        {
            if ((lastWTR == MotorCtrl.waitTimeRight) && (lastWTL == MotorCtrl.waitTimeLeft) && (lastSpeed == MotorCtrl.speedValue)) return;
            Debug.WriteLine("Motors {0}: Left={1}, Right={2}, Speed={3}", title, MotorCtrl.waitTimeLeft, MotorCtrl.waitTimeRight, MotorCtrl.speedValue);
            lastWTL = MotorCtrl.waitTimeLeft;
            lastWTR = MotorCtrl.waitTimeRight;
            lastSpeed = MotorCtrl.speedValue;
        }

        public static long msLastMessageInTime;
        static bool lastHidCheck = false;
        public static void ParseCtrlMessage(String str)
        {
            char[] delimiterChars = { '[', ']', ':' };
            string[] words = str.Split(delimiterChars);
            if (words.Length >= 2)
            {
                int id = Convert.ToInt32(words[1]);
                if (id >= 0 && id <= 8)
                {
                    CtrlCmds cmd = (CtrlCmds)id;
                    if (FoundLocalControlsWorking)
                    {
                        if (lastHidCheck != FoundLocalControlsWorking) Debug.WriteLine("LOCAL controls found - skipping messages.");
                    }
                    else
                    {
                        if (lastHidCheck != FoundLocalControlsWorking) Debug.WriteLine("No local controls yet - using messages.");
                        SetRobotDirection(cmd, (int)CtrlSpeeds.Max);
                    }
                    lastHidCheck = FoundLocalControlsWorking;
                }
            }
            msLastMessageInTime = MainPage.stopwatch.ElapsedMilliseconds;
        }

        #endregion
    }


}
