﻿using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Stopwatch
{
    [PluginActionId("com.miga.stepwatch")]
    public class StopwatchTimerAction : PluginBase
    {

        //---------------------------------------------------
        //          BarRaider's Hall Of Fame
        // OneMouseGaming - Tip: $3.33
        // Subscriber: stea1e
        // Subscriber: leer_12345
        //---------------------------------------------------
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ResumeOnClick = false,
                    Multiline = false,
                    ClearFileOnReset = false,
                    LapMode = false,
                    FileName = String.Empty,
                    SpineFileName = @"C:\Temp\MigaSpineTimer.txt"
                };

                return instance;
            }

            [JsonProperty(PropertyName = "resumeOnClick")]
            public bool ResumeOnClick { get; set; }

            [JsonProperty(PropertyName = "multiline")]
            public bool Multiline { get; set; }

            [JsonProperty(PropertyName = "spineFileName")]
            public string SpineFileName { get; set; }

            [JsonProperty(PropertyName = "fileName")]
            public string FileName { get; set; }

            [JsonProperty(PropertyName = "clearFileOnReset")]
            public bool ClearFileOnReset { get; set; }

            [JsonProperty(PropertyName = "lapMode")]
            public bool LapMode { get; set; }
        }

        #region Private members

        private const int RESET_COUNTER_KEYPRESS_LENGTH = 1000;

        private readonly PluginSettings settings;
        private bool keyPressed = false;
        private bool longKeyPress = false;
        private DateTime keyPressStart;
        private readonly string stopwatchId;
        private readonly System.Timers.Timer tmrOnTick = new System.Timers.Timer();

        #endregion

        #region PluginBase Methods

        public StopwatchTimerAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                Connection.SetSettingsAsync(JObject.FromObject(settings));
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            stopwatchId = Connection.ContextId;

            tmrOnTick.Interval = 250;
            tmrOnTick.Elapsed += TmrOnTick_Elapsed;
            tmrOnTick.Start();
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            string fileName = settings.FileName;
            // New in StreamDeck-Tools v2.0:
            Tools.AutoPopulateSettings(settings, payload.Settings);

            if (fileName != settings.FileName)
            {
                StopwatchManager.Instance.TouchTimerFile(settings.FileName);
            }
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {}


        public override void KeyPressed(KeyPayload payload)
        {
            // Used for long press
            keyPressStart = DateTime.Now;
            keyPressed = true;
            longKeyPress = false;

            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            /*if (StopwatchManager.Instance.IsStopwatchEnabled(stopwatchId)) // Stopwatch is already running
            {
            }
            else // Stopwatch is already paused
            {
                if (!settings.ResumeOnClick)
                {
                    ResetCounter();
                }

                ResumeStopwatch();
            }*/

            
            StopwatchManager.Instance.LoadStopwatchAndRun(new StopwatchSettings()
            {
                StopwatchId = stopwatchId, 
                FileName = settings.FileName, 
                ClearFileOnReset = settings.ClearFileOnReset, 
                LapMode = settings.LapMode, 
                ResetOnStart = !settings.ResumeOnClick,
                SpineFileName = settings.SpineFileName
            });
        }

        public override void KeyReleased(KeyPayload payload)
        {
            keyPressed = false;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Released");
        }

        // Using timer instead
        public override void OnTick() { }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor called");
        }

        #endregion

        #region Private methods

        private void ResetCounter()
        {
            StopwatchManager.Instance.ResetStopwatchAndRecreateFile(new StopwatchSettings()
            {
                StopwatchId = stopwatchId, 
                FileName = settings.FileName, 
                ClearFileOnReset = settings.ClearFileOnReset, 
                LapMode = settings.LapMode, 
                ResetOnStart = !settings.ResumeOnClick,
                SpineFileName = settings.SpineFileName
            });
        }

        private void CheckIfResetNeeded()
        {
            if (!keyPressed)
            {
                return;
            }

            // Long key press
            if ((DateTime.Now - keyPressStart).TotalMilliseconds > RESET_COUNTER_KEYPRESS_LENGTH && !longKeyPress)
            {
                longKeyPress = true;
                PauseStopwatch();
                if (settings.LapMode)
                {
                    List<long> laps = StopwatchManager.Instance.GetLaps(stopwatchId);
                    List<string> lapStr = new List<string>();

                    foreach (long lap in laps)
                    {
                        lapStr.Add(SecondsToReadableFormat(lap, ":", false));
                    }
                    SaveToClipboard(string.Join("\n", lapStr.ToArray()));

                }
                ResetCounter();
            }
        }

        private void SaveToClipboard(string str)
        {
            if (str == null)
            {
                return;
            }

            Thread t = new Thread(() =>
            {
                System.Windows.Forms.Clipboard.SetText(str);
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
        }

        
        private void PauseStopwatch()
        {
            Stopwatch.StopwatchManager.Instance.StopStopwatch(stopwatchId);
        }

        private string SecondsToReadableFormat(long total, string delimiter, bool secondsOnNewLine)
        {
            long minutes, seconds, hours;
            minutes = total / 60;
            seconds = total % 60;
            hours = minutes / 60;
            minutes %= 60;

            return $"Hrs: {hours:00}\r\n{minutes:00}:{seconds:00}";
        }

        private async void TmrOnTick_Elapsed(object sender, ElapsedEventArgs e)
        {
            string delimiter = settings.Multiline ? "\n" : ":";

            // Stream Deck calls this function every second, 
            // so this is the best place to determine if we need to reset (versus the internal timer which may be paused)
            CheckIfResetNeeded();

            long total = StopwatchManager.Instance.GetStopwatchTime(stopwatchId);
            await Connection.SetTitleAsync(SecondsToReadableFormat(total, delimiter, true));
        }

        #endregion
    }
}
