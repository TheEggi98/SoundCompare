using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Audio;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x407 dokumentiert.

// Links:
// Audiograph https://docs.microsoft.com/de-de/windows/uwp/audio-video-camera/audio-graphs#audiograph-class
// Slider https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.slider?view=winrt-19041


namespace SoundCompare
{
    public sealed partial class MainPage : Page
    {
        AudioGraph audioGraph;
        AudioDeviceOutputNode deviceOutputNode;
        AudioFileInputNode firstFileInputNode;
        AudioFileInputNode secondFileInputNode;

        EchoEffectDefinition echo;
        LimiterEffectDefinition limiter;
        ReverbEffectDefinition reverb;

        EqualizerEffectDefinition equalizerLowFreq;
        EqualizerEffectDefinition equalizerMidFreq;
        EqualizerEffectDefinition equalizerHighFreq;

        Boolean isPlaying = false;
        Boolean isHearingAudio1 = true;
        Boolean manipulating = false;
        Boolean SettingsfileIsOpen = false;

        TimeSpan end;
        TimeSpan currentTime;

        ImageSource Sound;
        ImageSource noSound;
        ImageBrush Play = new ImageBrush();
        ImageBrush Pause = new ImageBrush();

        DispatcherTimer dispatcherTimer = new DispatcherTimer();

        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        StorageFolder localFolder = ApplicationData.Current.LocalFolder;
        IList<string> Settings;

        double customEQ63HzGain;
        double customEQ125HzGain;
        double customEQ250HzGain;
        double customEQ500HzGain;
        double customEQ1kHzGain;
        double customEQ2kHzGain;
        double customEQ4kHzGain;
        double customEQ8kHzGain;
        double customEQ16kHzGain;

        public MainPage()
        {
            this.InitializeComponent();
            this.InitAudioGraph();

            this.Sound = new BitmapImage(new Uri("ms-appx:///Assets/Sound.png"));
            this.noSound = new BitmapImage(new Uri("ms-appx:///Assets/noSound.png"));
            this.Play.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/Play.png"));
            this.Pause.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/Pause.png"));

            this.dispatcherTimer.Tick += DispatcherTimer_Tick;
            this.dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 10);
        }

        #region Audiograph + Nodes

        private async Task InitAudioGraph()
        {

            AudioGraphSettings settings = new AudioGraphSettings(Windows.Media.Render.AudioRenderCategory.Media);

            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);

            this.audioGraph = result.Graph;
            this.equalizerLowFreq = new EqualizerEffectDefinition(this.audioGraph);
            this.equalizerMidFreq = new EqualizerEffectDefinition(this.audioGraph);
            this.equalizerHighFreq = new EqualizerEffectDefinition(this.audioGraph);
            await this.CreateDeviceOutputNode();
            //this.ReadSettings();
        }

        private async Task CreateDeviceOutputNode()
        {
            // Create a device output node
            CreateAudioDeviceOutputNodeResult result = await audioGraph.CreateDeviceOutputNodeAsync();

            if (result.Status != AudioDeviceNodeCreationStatus.Success)
            {
                // Cannot create device output node
                return;
            }

            this.deviceOutputNode = result.DeviceOutputNode;
            this.deviceOutputNode.EffectDefinitions.Add(this.equalizerLowFreq);
            this.deviceOutputNode.EffectDefinitions.Add(this.equalizerMidFreq);
            this.deviceOutputNode.EffectDefinitions.Add(this.equalizerHighFreq);
            this.deviceOutputNode.EnableEffectsByDefinition(this.equalizerLowFreq);
            this.deviceOutputNode.EnableEffectsByDefinition(this.equalizerMidFreq);
            this.deviceOutputNode.EnableEffectsByDefinition(this.equalizerHighFreq);
            this.ReadSettings();
        }

        private async Task CreateFileInputNode(int Audionumber)
        {
            if (audioGraph == null)
                return;

            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            filePicker.FileTypeFilter.Add(".mp3");
            filePicker.FileTypeFilter.Add(".wav");
            filePicker.FileTypeFilter.Add(".wma");
            filePicker.FileTypeFilter.Add(".m4a");
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            StorageFile file = await filePicker.PickSingleFileAsync();

            // File can be null if cancel is hit in the file picker
            if (file == null)
            {
                return;
            }

            CreateAudioFileInputNodeResult result = await audioGraph.CreateFileInputNodeAsync(file);

            if (Audionumber == 1)
            {
                firstFileInputNode = result.FileInputNode;
                Audio1Name.Text = file.Name;

            }
            else
            {
                secondFileInputNode = result.FileInputNode;
                Audio2Name.Text = file.Name;
            }

        }

        #endregion

        #region Events


        #region Clickevents

        #region AudioUI Clickevents

        private async void chooseAudio1_Click(object sender, RoutedEventArgs e)
        {
            if (this.firstFileInputNode != null)
            {
                this.dispatcherTimer.Stop();
                this.firstFileInputNode.Dispose();
                this.isPlaying = false;
            }

            await this.CreateFileInputNode(1);
            if (this.firstFileInputNode != null)
            {
                if ((this.secondFileInputNode != null && this.firstFileInputNode.Duration <= this.secondFileInputNode.Duration) || this.secondFileInputNode == null)
                {
                    this.end = this.firstFileInputNode.Duration;
                    AudioLength.Text = this.parseTimeSpan(this.end);
                    PlaytimeSlider.Maximum = this.end.TotalSeconds;
                }
                else if (this.secondFileInputNode != null && this.firstFileInputNode.Duration >= this.secondFileInputNode.Duration)
                {
                    this.end = this.secondFileInputNode.Duration;
                    AudioLength.Text = this.parseTimeSpan(this.end);
                    PlaytimeSlider.Maximum = this.end.TotalSeconds;
                }
                hearingAudio1.Source = this.Sound;
            }

        }

        private async void chooseAudio2_Click(object sender, RoutedEventArgs e)
        {
            if (this.secondFileInputNode != null)
            {
                this.dispatcherTimer.Stop();
                this.secondFileInputNode.Dispose();
                this.isPlaying = false;
            }

            await this.CreateFileInputNode(2);
            if (this.secondFileInputNode != null)
            {
                if ((this.firstFileInputNode != null && this.secondFileInputNode.Duration <= this.firstFileInputNode.Duration) || this.firstFileInputNode == null)
                {
                    this.end = this.secondFileInputNode.Duration;
                    AudioLength.Text = this.parseTimeSpan(this.end);
                    PlaytimeSlider.Maximum = this.end.TotalSeconds;
                }
                else if (this.firstFileInputNode != null && this.secondFileInputNode.Duration >= this.firstFileInputNode.Duration)
                {
                    this.end = this.firstFileInputNode.Duration;
                    AudioLength.Text = this.parseTimeSpan(this.end);
                    PlaytimeSlider.Maximum = this.end.TotalSeconds;
                }
                hearingAudio2.Source = this.noSound;
                this.secondFileInputNode.Stop();
            }

        }

        private void Playbutton_Click(object sender, RoutedEventArgs e)
        {
            if (this.firstFileInputNode != null && this.secondFileInputNode != null)
            {
                if (this.firstFileInputNode.OutgoingConnections.Count() == 0)
                {
                    this.firstFileInputNode.AddOutgoingConnection(this.deviceOutputNode);
                    this.firstFileInputNode.OutgoingGain = (Volumeslider.Value / 100);
                }

                if (this.secondFileInputNode.OutgoingConnections.Count() == 0)
                {
                    this.secondFileInputNode.AddOutgoingConnection(this.deviceOutputNode);
                    this.secondFileInputNode.OutgoingGain = (Volumeslider.Value / 100);
                }

                if (!this.isPlaying)
                {
                    this.audioGraph.Start();
                    this.dispatcherTimer.Start();
                    this.isPlaying = true;
                    Playbutton.Background = this.Pause;
                }
                else
                {
                    this.audioGraph.Stop();
                    this.dispatcherTimer.Stop();
                    this.isPlaying = false;
                    Playbutton.Background = this.Play;
                }
            }

        }

        private void Stopbutton_Click(object sender, RoutedEventArgs e)
        {
            this.ResetAudio();
        }

        private void SwitchAudio_Click(object sender, RoutedEventArgs e)
        {
            if (this.firstFileInputNode != null && this.secondFileInputNode != null)
            {
                if (this.isHearingAudio1)
                {
                    if (this.firstFileInputNode.Position < this.secondFileInputNode.Duration)
                    {
                        this.secondFileInputNode.StartTime = this.firstFileInputNode.Position;
                    }
                    else
                    {
                        this.ResetAudio();
                    }

                    this.firstFileInputNode.Stop();
                    this.secondFileInputNode.Start();
                    this.secondFileInputNode.StartTime = new TimeSpan(0, 0, 0);
                    this.isHearingAudio1 = false;
                    hearingAudio1.Source = this.noSound;
                    hearingAudio2.Source = this.Sound;
                }
                else
                {
                    if (this.secondFileInputNode.Position < this.firstFileInputNode.Duration)
                    {
                        this.firstFileInputNode.StartTime = this.secondFileInputNode.Position;
                    }
                    else
                    {
                        this.ResetAudio();
                    }

                    this.secondFileInputNode.Stop();
                    this.firstFileInputNode.Start();
                    this.firstFileInputNode.StartTime = new TimeSpan(0, 0, 0);
                    this.isHearingAudio1 = true;
                    hearingAudio1.Source = this.Sound;
                    hearingAudio2.Source = this.noSound;
                }
            }


        }

        private void ShowSettings_Click(object sender, RoutedEventArgs e)
        {
            this.switchView();
        }
        #endregion

        #region Effects Clickevents

        private void showEffects_Click(object sender, RoutedEventArgs e)
        {
            if (effectStackPanel.Visibility == Visibility.Collapsed)
            {
                effectStackPanel.Visibility = Visibility.Visible;
                showEffects.Content = ">>";
            }
            else
            {
                effectStackPanel.Visibility = Visibility.Collapsed;
                showEffects.Content = "<<";
            }
        }

        private void toggleEcho_Click(object sender, RoutedEventArgs e)
        {
            if (!deviceOutputNode.EffectDefinitions.Contains(echo))
            {
                deviceOutputNode.EffectDefinitions.Add(this.echo);
            }
            else
            {
                if ((bool)toggleEcho.IsChecked)
                {
                    deviceOutputNode.EnableEffectsByDefinition(this.echo);
                }
                else
                {
                    deviceOutputNode.DisableEffectsByDefinition(this.echo);
                }
            }
        }

        private void toggleLimiter_Click(object sender, RoutedEventArgs e)
        {
            if (!deviceOutputNode.EffectDefinitions.Contains(this.limiter))
            {
                deviceOutputNode.EffectDefinitions.Add(this.limiter);
            }
            else
            {
                if ((bool)toggleLimiter.IsChecked)
                {
                    deviceOutputNode.EnableEffectsByDefinition(this.limiter);
                }
                else
                {
                    deviceOutputNode.DisableEffectsByDefinition(this.limiter);
                }
            }

        }

        private void toggleReverb_Click(object sender, RoutedEventArgs e)
        {
            if (!deviceOutputNode.EffectDefinitions.Contains(this.reverb))
            {
                deviceOutputNode.EffectDefinitions.Add(this.reverb);
            }
            else
            {
                if ((bool)toggleReverb.IsChecked)
                {
                    deviceOutputNode.EnableEffectsByDefinition(this.reverb);
                }
                else
                {
                    deviceOutputNode.DisableEffectsByDefinition(this.reverb);
                }
            }

        }

        #endregion

        #endregion

        #region Timer

        private void DispatcherTimer_Tick(object sender, object e)
        {
            if (isHearingAudio1)
            {
                this.currentTime = this.firstFileInputNode.Position;
                if (!manipulating)
                {
                    PlaytimeSlider.Value = this.firstFileInputNode.Position.TotalSeconds;
                }

            }
            else
            {
                this.currentTime = this.secondFileInputNode.Position;
                if (!manipulating)
                {
                    PlaytimeSlider.Value = this.secondFileInputNode.Position.TotalSeconds;
                }
            }

            if (this.currentTime >= this.end.Subtract(new TimeSpan(0, 0, 0, 0, 10)))
            {
                this.Stopbutton_Click(sender, (RoutedEventArgs)e);
                return;
            }
            CurrentPlayime.Text = this.parseTimeSpan(this.currentTime);
        }

        #endregion

        #region SliderEvents

        private void PlaytimeSlider_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
        {
            if (this.firstFileInputNode != null && this.secondFileInputNode != null)
            {
                this.firstFileInputNode.Seek(new TimeSpan(0, 0, (int)PlaytimeSlider.Value));
                this.secondFileInputNode.Seek(new TimeSpan(0, 0, (int)PlaytimeSlider.Value));
            }
            this.manipulating = false;
        }

        private void PlaytimeSlider_ManipulationStarted(object sender, Windows.UI.Xaml.Input.ManipulationStartedRoutedEventArgs e)
        {
            this.manipulating = true;
        }

        private void Volumeslider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.deviceOutputNode != null && this.firstFileInputNode != null && this.secondFileInputNode != null)
            {
                this.deviceOutputNode.OutgoingGain = (Volumeslider.Value / 100);
                this.firstFileInputNode.OutgoingGain = (Volumeslider.Value / 100);
                this.secondFileInputNode.OutgoingGain = (Volumeslider.Value / 100);
            }

            if (Volumeimage != null)
            {
                if (Volumeslider.Value == 0)
                {
                    Volumeimage.Source = this.noSound;
                }
                else { Volumeimage.Source = this.Sound; }
            }
        }

        #endregion

        #endregion

        #region Helpermethods

        private string parseTimeSpan(TimeSpan time)
        {
            int hours = (int)time.TotalHours;
            int minutes = ((int)time.TotalMinutes % 60);
            int seconds = ((int)time.TotalSeconds % 60);
            string output = "";

            if (hours > 0)
            {
                output += hours.ToString() + ":";
            }

            if (hours > 0 && minutes < 10)
            {
                output += "0";
            }
            output += minutes.ToString() + ":";

            if (seconds < 10)
            {
                output += "0";
            }
            output += seconds.ToString();

            return output;
        }

        private void ResetAudio()
        {
            if (this.firstFileInputNode != null && this.secondFileInputNode != null)
            {
                this.audioGraph.Stop();
                this.dispatcherTimer.Stop();
                this.firstFileInputNode.StartTime = new TimeSpan(0, 0, 0, 0);
                this.secondFileInputNode.StartTime = new TimeSpan(0, 0, 0, 0);
                PlaytimeSlider.Value = 0;
                CurrentPlayime.Text = "0:00";
                this.isPlaying = false;
                this.audioGraph.ResetAllNodes();
                Playbutton.Background = this.Play;
            }
        }

        private async void WriteSettings()
        {
            StorageFile EffectSettingsFile = await localFolder.CreateFileAsync("EffectSettings.txt", CreationCollisionOption.OpenIfExists);
            if (!this.SettingsfileIsOpen)
            {
                this.SettingsfileIsOpen = true;
                await FileIO.WriteLinesAsync(EffectSettingsFile, this.Settings);
                this.SettingsfileIsOpen = false;
            }
        }

        private async void ReadSettings()
        {
            this.SettingsfileIsOpen = true;
            StorageFile EffectSettingsFile = await localFolder.CreateFileAsync("EffectSettings.txt", CreationCollisionOption.OpenIfExists);
            this.Settings = await FileIO.ReadLinesAsync(EffectSettingsFile);
            if (this.Settings.Count() == 0)
            {
                this.Settings.Add("5");         //Echo Delay                    = Settings[0]
                this.Settings.Add("1");       //Echo Feedback                 = Settings[1]
                this.Settings.Add("0.1");       //Echo WetDryMix                = Settings[2]

                this.Settings.Add("1");         //Limiter Loudness              = Settings[3]
                this.Settings.Add("1");         //Limiter Release               = Settings[4]

                this.Settings.Add("1");         //Reverb DecayTime              = Settings[5]
                this.Settings.Add("2");         //Reverb Density                = Settings[6]
                this.Settings.Add("false");     //Reverb DisableLateField       = Settings[7]
                this.Settings.Add("1");         //Reverb EarlyDiffusion         = Settings[8]
                this.Settings.Add("1");         //Reverb HighEQCutoff           = Settings[9]
                this.Settings.Add("1");         //Reverb HighEQGain             = Settings[10]
                this.Settings.Add("1");         //Reverb LateDiffusion          = Settings[11]
                this.Settings.Add("1");         //Reverb LowEQCutoff            = Settings[12]
                this.Settings.Add("1");         //Reverb LowEQGain              = Settings[13]
                this.Settings.Add("1");         //Reverb PositionLeft           = Settings[14]
                this.Settings.Add("1");         //Reverb PositionMatrixLeft     = Settings[15]
                this.Settings.Add("1");         //Reverb PositionMatrixRight    = Settings[16]
                this.Settings.Add("1");         //Reverb PositionRight          = Settings[17]
                this.Settings.Add("1");         //Reverb RearDelay              = Settings[18]
                this.Settings.Add("1");         //Reverb ReflectionsDelay       = Settings[19]
                this.Settings.Add("1");         //Reverb ReflectionsGain        = Settings[20]
                this.Settings.Add("1");         //Reverb ReverbDelay            = Settings[21]
                this.Settings.Add("1");         //Reverb ReverbGain             = Settings[22]
                this.Settings.Add("200");       //Reverb RoomFilterFreq         = Settings[23]
                this.Settings.Add("-1");       //Reverb RoomFilterHF           = Settings[24]
                this.Settings.Add("-1");       //Reverb RoomFilterMain         = Settings[25]
                this.Settings.Add("1");         //Reverb RoomSize               = Settings[26]
                this.Settings.Add("0.5");       //Reverb WetDryMix              = Settings[27]

                this.Settings.Add("0.126");     //Echo EQ63Hz                   = Settings[28]
                this.Settings.Add("0.126");     //Echo EQ125Hz                  = Settings[29]
                this.Settings.Add("0.126");     //Echo EQ250Hz                  = Settings[30]
                this.Settings.Add("0.126");     //Echo EQ500Hz                  = Settings[31]
                this.Settings.Add("0.126");     //Echo EQ1kHz                   = Settings[32]
                this.Settings.Add("0.126");     //Echo EQ2kHz                   = Settings[33]
                this.Settings.Add("0.126");     //Echo EQ4kHz                   = Settings[34]
                this.Settings.Add("0.126");     //Echo EQ8kHz                   = Settings[35]
                this.Settings.Add("0.126");     //Echo EQ16kHz                  = Settings[36]
                this.Settings.Add("Normal");    //Echo SelectedEQ               = Settings[37]
                
                await FileIO.WriteLinesAsync(EffectSettingsFile, this.Settings);
                this.SettingsfileIsOpen = false;
            }
                // Read local Settings
                this.echo = new EchoEffectDefinition(this.audioGraph);
                this.echo.Delay = Double.Parse(this.Settings[0].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                EchoDelaySlider.Value = this.echo.Delay;
                this.echo.Feedback = Double.Parse(this.Settings[1].Replace(",","."), System.Globalization.CultureInfo.InvariantCulture);
                EchoFeedBackSlider.Value = this.echo.Feedback;
                this.echo.WetDryMix = Double.Parse(this.Settings[2].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                EchoWetDryMixSlider.Value = this.echo.WetDryMix;
                this.limiter = new LimiterEffectDefinition(this.audioGraph);
                this.limiter.Loudness = UInt32.Parse(this.Settings[3]);
                LimiterLoudnessSlider.Value = this.limiter.Loudness;
                this.limiter.Release = UInt32.Parse(this.Settings[4]);
                LimiterReleaseSlider.Value = this.limiter.Release;

                this.reverb = new ReverbEffectDefinition(this.audioGraph);
                this.reverb.DecayTime = Double.Parse(this.Settings[5], System.Globalization.CultureInfo.InvariantCulture);
                ReverbDecayTimeSlider.Value = this.reverb.DecayTime;
                this.reverb.Density = Double.Parse(this.Settings[6], System.Globalization.CultureInfo.InvariantCulture);
                ReverbDensitySlider.Value = this.reverb.Density;
                this.reverb.DisableLateField = Boolean.Parse(this.Settings[7]);
                ReverbDisableLateFieldToggleSwitch.IsOn = this.reverb.DisableLateField;
                this.reverb.EarlyDiffusion = Byte.Parse(this.Settings[8]);
                ReverbEarlyDiffusionSlider.Value = this.reverb.EarlyDiffusion;
                this.reverb.HighEQCutoff = Byte.Parse(this.Settings[9]);
                ReverbHighEQCutOffSlider.Value = this.reverb.HighEQCutoff;
                this.reverb.HighEQGain = Byte.Parse(this.Settings[10]);
                ReverbHighEQGainSlider.Value = this.reverb.HighEQGain;
                this.reverb.LateDiffusion = Byte.Parse(this.Settings[11]);
                ReverbLateDiffusionSlider.Value = this.reverb.LateDiffusion;
                this.reverb.LowEQCutoff = Byte.Parse(this.Settings[12]);
                ReverbLowEQCutOffSlider.Value = this.reverb.LowEQCutoff;
                this.reverb.LowEQGain = Byte.Parse(this.Settings[13]);
                ReverbLowEQGainSlider.Value = this.reverb.LowEQGain;
                this.reverb.PositionLeft = Byte.Parse(this.Settings[14]);
                ReverbPositionLeftSlider.Value = this.reverb.PositionLeft;
                this.reverb.PositionMatrixLeft = Byte.Parse(this.Settings[15]);
                ReverbPositionMatrixLeftSlider.Value = this.reverb.PositionMatrixLeft;
                this.reverb.PositionMatrixRight = Byte.Parse(this.Settings[16]);
                ReverbPositionMatrixRightSlider.Value = this.reverb.PositionMatrixRight;
                this.reverb.PositionRight = Byte.Parse(this.Settings[17]);
                ReverbPositionRightSlider.Value = this.reverb.PositionRight;
                this.reverb.RearDelay = Byte.Parse(this.Settings[18]);
                ReverbRearDelaySlider.Value = this.reverb.RearDelay;
                this.reverb.ReflectionsDelay = UInt32.Parse(this.Settings[19]);
                ReverbReflectionsDelaySlider.Value = this.reverb.ReflectionsDelay;
                this.reverb.ReflectionsGain = Double.Parse(this.Settings[20].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                ReverbReflectionsGainSlider.Value = this.reverb.ReflectionsGain;
                this.reverb.ReverbDelay = Byte.Parse(this.Settings[21]);
                ReverbReverbDelaySlider.Value = this.reverb.ReverbDelay;
                this.reverb.ReverbGain = Double.Parse(this.Settings[22].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                ReverbReverbGainSlider.Value = this.reverb.ReverbGain;
                this.reverb.RoomFilterFreq = Double.Parse(this.Settings[23].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                ReverbRoomFilterFreqSlider.Value = this.reverb.RoomFilterFreq;
                this.reverb.RoomFilterHF = Double.Parse(this.Settings[24].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                ReverbRoomFilterHFSlider.Value = this.reverb.RoomFilterHF;
                this.reverb.RoomFilterMain = Double.Parse(this.Settings[25].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                ReverbRoomFilterMainSlider.Value = this.reverb.RoomFilterMain;
                this.reverb.RoomSize = Double.Parse(this.Settings[26].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                ReverbRoomSizeSlider.Value = this.reverb.RoomSize;
                this.reverb.WetDryMix = Double.Parse(this.Settings[27].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                ReverbWetDryMixSlider.Value = this.reverb.WetDryMix;

                this.equalizerLowFreq = new EqualizerEffectDefinition(this.audioGraph);
                this.equalizerLowFreq.Bands[0].FrequencyCenter = 63;
                //this.equalizerLowFreq.Bands[0].Bandwidth = 5;
                this.equalizerLowFreq.Bands[1].FrequencyCenter = 125;
                //this.equalizerLowFreq.Bands[1].Bandwidth = 20;
                this.equalizerLowFreq.Bands[2].FrequencyCenter = 250;
                //this.equalizerLowFreq.Bands[2].Bandwidth = 20;

                this.equalizerMidFreq = new EqualizerEffectDefinition(this.audioGraph);
                this.equalizerMidFreq.Bands[0].FrequencyCenter = 500;
                //this.equalizerMidFreq.Bands[0].Bandwidth = 20;
                this.equalizerMidFreq.Bands[1].FrequencyCenter = 1000;
                //this.equalizerMidFreq.Bands[1].Bandwidth = 20;
                this.equalizerMidFreq.Bands[2].FrequencyCenter = 2000;
                //this.equalizerMidFreq.Bands[2].Bandwidth = 20;

                this.equalizerHighFreq = new EqualizerEffectDefinition(this.audioGraph);
                this.equalizerHighFreq.Bands[0].FrequencyCenter = 4000;
                //this.equalizerHighFreq.Bands[0].Bandwidth = 20;
                this.equalizerHighFreq.Bands[1].FrequencyCenter = 8000;
                //this.equalizerHighFreq.Bands[1].Bandwidth = 20;
                this.equalizerHighFreq.Bands[2].FrequencyCenter = 16000;
                //this.equalizerHighFreq.Bands[2].Bandwidth = 20;

                this.customEQ63HzGain = Double.Parse(this.Settings[28].Replace(",", "."));
                this.customEQ125HzGain = Double.Parse(this.Settings[29].Replace(",", "."));
                this.customEQ250HzGain = Double.Parse(this.Settings[30].Replace(",", "."));

                this.customEQ500HzGain = Double.Parse(this.Settings[31].Replace(",", "."));
                this.customEQ1kHzGain = Double.Parse(this.Settings[32].Replace(",", "."));
                this.customEQ2kHzGain = Double.Parse(this.Settings[33].Replace(",", "."));

                this.customEQ4kHzGain = Double.Parse(this.Settings[34].Replace(",", "."));
                this.customEQ8kHzGain = Double.Parse(this.Settings[35].Replace(",", "."));
                this.customEQ16kHzGain = Double.Parse(this.Settings[36].Replace(",", "."));
                this.chooseEQ(this.Settings[37]);
                this.SettingsfileIsOpen = false;
        }

        //private void ReadSettings()
        //{
        //    // Set Local Settings if not existing
        //    if (this.localSettings.Values["SettingsExists"] == null)
        //    {
        //        this.localSettings.Values["SettingsExists"] = true;

        //        this.localSettings.Values["EchoDelay"] = 5;
        //        this.localSettings.Values["EchoFeedback"] = 0.2;
        //        this.localSettings.Values["EchoWetDryMix"] = 0.5;

        //        this.localSettings.Values["LimiterLoudness"] = 1;
        //        this.localSettings.Values["LimiterRelease"] = 1;

        //        this.localSettings.Values["ReverbDecayTime"] = 1;
        //        this.localSettings.Values["ReverbDensity"] = 2;
        //        this.localSettings.Values["ReverbDisableLateField"] = false;
        //        this.localSettings.Values["ReverbEarlyDiffusion"] = 1;
        //        this.localSettings.Values["ReverbHighEQCutoff"] = 1;
        //        this.localSettings.Values["ReverbHighEQGain"] = 1;
        //        this.localSettings.Values["ReverbLateDiffusion"] = 1;
        //        this.localSettings.Values["ReverbLowEQCutoff"] = 1;
        //        this.localSettings.Values["ReverbLowEQGain"] = 1;
        //        this.localSettings.Values["ReverbPositionLeft"] = 1;
        //        this.localSettings.Values["ReverbPositionMatrixLeft"] = 1;
        //        this.localSettings.Values["ReverbPositionMatrixRight"] = 1;
        //        this.localSettings.Values["ReverbPositionRight"] = 1;
        //        this.localSettings.Values["ReverbRearDelay"] = 1;
        //        this.localSettings.Values["ReverbReflectionsDelay"] = 1;
        //        this.localSettings.Values["ReverbReflectionsGain"] = 1;
        //        this.localSettings.Values["ReverbReverbDelay"] = 1;
        //        this.localSettings.Values["ReverbReverbGain"] = 1;
        //        this.localSettings.Values["ReverbRoomFilterFreq"] = 200;
        //        this.localSettings.Values["ReverbRoomFilterHF"] = 200;
        //        this.localSettings.Values["ReverbRoomFilterMain"] = 200;
        //        this.localSettings.Values["ReverbRoomSize"] = 1;
        //        this.localSettings.Values["ReverbWetDryMix"] = 0.5;

        //        this.localSettings.Values["EQ63Hz"] = 1;
        //        this.localSettings.Values["EQ125Hz"] = 1;
        //        this.localSettings.Values["EQ250Hz"] = 1;
        //        this.localSettings.Values["EQ500Hz"] = 1;
        //        this.localSettings.Values["EQ1kHz"] = 1;
        //        this.localSettings.Values["EQ2kHz"] = 1;
        //        this.localSettings.Values["EQ4kHz"] = 1;
        //        this.localSettings.Values["EQ8kHz"] = 1;
        //        this.localSettings.Values["EQ16kHz"] = 1;

        //        this.localSettings.Values["selectedEQ"] = "Normal";
        //    }
        //    // Read local Settings
        //    this.echo = new EchoEffectDefinition(this.audioGraph);
        //    this.echo.Delay = Double.Parse(this.localSettings.Values["EchoDelay"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    EchoDelaySlider.Value = this.echo.Delay;
        //    this.echo.Feedback = Double.Parse(this.localSettings.Values["EchoFeedback"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    EchoFeedBackSlider.Value = this.echo.Feedback;
        //    this.echo.WetDryMix = Double.Parse(this.localSettings.Values["EchoWetDryMix"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    EchoWetDryMixSlider.Value = this.echo.WetDryMix;
        //    this.limiter = new LimiterEffectDefinition(this.audioGraph);
        //    this.limiter.Loudness = UInt32.Parse(this.localSettings.Values["LimiterLoudness"].ToString());
        //    LimiterLoudnessSlider.Value = this.limiter.Loudness;
        //    this.limiter.Release = UInt32.Parse(this.localSettings.Values["LimiterRelease"].ToString());
        //    LimiterReleaseSlider.Value = this.limiter.Release;

        //    this.reverb = new ReverbEffectDefinition(this.audioGraph);
        //    this.reverb.DecayTime = Double.Parse(this.localSettings.Values["ReverbDecayTime"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    ReverbDecayTimeSlider.Value = this.reverb.DecayTime;
        //    this.reverb.Density = Double.Parse(this.localSettings.Values["ReverbDensity"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    ReverbDensitySlider.Value = this.reverb.Density;
        //    this.reverb.DisableLateField = Boolean.Parse(this.localSettings.Values["ReverbDisableLateField"].ToString());
        //    ReverbDisableLateFieldToggleSwitch.IsOn = this.reverb.DisableLateField;
        //    this.reverb.EarlyDiffusion = Byte.Parse(this.localSettings.Values["ReverbEarlyDiffusion"].ToString());
        //    ReverbEarlyDiffusionSlider.Value = this.reverb.EarlyDiffusion;
        //    this.reverb.HighEQCutoff = Byte.Parse(this.localSettings.Values["ReverbHighEQCutoff"].ToString());
        //    ReverbHighEQCutOffSlider.Value = this.reverb.HighEQCutoff;
        //    this.reverb.HighEQGain = Byte.Parse(this.localSettings.Values["ReverbHighEQGain"].ToString());
        //    ReverbHighEQGainSlider.Value = this.reverb.HighEQGain;
        //    this.reverb.LateDiffusion = Byte.Parse(this.localSettings.Values["ReverbLateDiffusion"].ToString());
        //    ReverbLateDiffusionSlider.Value = this.reverb.LateDiffusion;
        //    this.reverb.LowEQCutoff = Byte.Parse(this.localSettings.Values["ReverbLowEQCutoff"].ToString());
        //    ReverbLowEQCutOffSlider.Value = this.reverb.LowEQCutoff;
        //    this.reverb.LowEQGain = Byte.Parse(this.localSettings.Values["ReverbLowEQGain"].ToString());
        //    ReverbLowEQGainSlider.Value = this.reverb.LowEQGain;
        //    this.reverb.PositionLeft = Byte.Parse(this.localSettings.Values["ReverbPositionLeft"].ToString());
        //    ReverbPositionLeftSlider.Value = this.reverb.PositionLeft;
        //    this.reverb.PositionMatrixLeft = Byte.Parse(this.localSettings.Values["ReverbPositionMatrixLeft"].ToString());
        //    ReverbPositionMatrixLeftSlider.Value = this.reverb.PositionMatrixLeft;
        //    this.reverb.PositionMatrixRight = Byte.Parse(this.localSettings.Values["ReverbPositionMatrixRight"].ToString());
        //    ReverbPositionMatrixRightSlider.Value = this.reverb.PositionMatrixRight;
        //    this.reverb.PositionRight = Byte.Parse(this.localSettings.Values["ReverbPositionRight"].ToString());
        //    ReverbPositionRightSlider.Value = this.reverb.PositionRight;
        //    this.reverb.RearDelay = Byte.Parse(this.localSettings.Values["ReverbRearDelay"].ToString());
        //    ReverbRearDelaySlider.Value = this.reverb.RearDelay;
        //    this.reverb.ReflectionsDelay = UInt32.Parse(this.localSettings.Values["ReverbReflectionsDelay"].ToString());
        //    ReverbReflectionsDelaySlider.Value = this.reverb.ReflectionsDelay;
        //    this.reverb.ReflectionsGain = Double.Parse(this.localSettings.Values["ReverbReflectionsGain"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    ReverbReflectionsGainSlider.Value = this.reverb.ReflectionsGain;
        //    this.reverb.ReverbDelay = Byte.Parse(this.localSettings.Values["ReverbReverbDelay"].ToString());
        //    ReverbReverbDelaySlider.Value = this.reverb.ReverbDelay;
        //    this.reverb.ReverbGain = Double.Parse(this.localSettings.Values["ReverbReverbGain"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    ReverbReverbGainSlider.Value = this.reverb.ReverbGain;
        //    this.reverb.RoomFilterFreq = Double.Parse(this.localSettings.Values["ReverbRoomFilterFreq"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    ReverbRoomFilterFreqSlider.Value = this.reverb.RoomFilterFreq;
        //    this.reverb.RoomFilterHF = Double.Parse(this.localSettings.Values["ReverbRoomFilterHF"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    ReverbRoomFilterHFSlider.Value = this.reverb.RoomFilterHF;
        //    this.reverb.RoomFilterMain = Double.Parse(this.localSettings.Values["ReverbRoomFilterMain"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    ReverbRoomFilterMainSlider.Value = this.reverb.RoomFilterMain;
        //    this.reverb.RoomSize = Double.Parse(this.localSettings.Values["ReverbRoomSize"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    ReverbRoomSizeSlider.Value = this.reverb.RoomSize;
        //    this.reverb.WetDryMix = Double.Parse(this.localSettings.Values["ReverbWetDryMix"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        //    ReverbWetDryMixSlider.Value = this.reverb.WetDryMix;

        //    this.equalizerLowFreq = new EqualizerEffectDefinition(this.audioGraph);
        //    this.equalizerLowFreq.Bands[0].FrequencyCenter = 63;
        //    this.equalizerLowFreq.Bands[0].Bandwidth = 20;
        //    this.equalizerLowFreq.Bands[1].FrequencyCenter = 125;
        //    this.equalizerLowFreq.Bands[1].Bandwidth = 20;
        //    this.equalizerLowFreq.Bands[2].FrequencyCenter = 250;
        //    this.equalizerLowFreq.Bands[2].Bandwidth = 20;

        //    this.equalizerMidFreq = new EqualizerEffectDefinition(this.audioGraph);
        //    this.equalizerMidFreq.Bands[0].FrequencyCenter = 500;
        //    this.equalizerMidFreq.Bands[0].Bandwidth = 20;
        //    this.equalizerMidFreq.Bands[1].FrequencyCenter = 1000;
        //    this.equalizerMidFreq.Bands[1].Bandwidth = 20;
        //    this.equalizerMidFreq.Bands[2].FrequencyCenter = 2000;
        //    this.equalizerMidFreq.Bands[2].Bandwidth = 20;

        //    this.equalizerHighFreq = new EqualizerEffectDefinition(this.audioGraph);
        //    this.equalizerHighFreq.Bands[0].FrequencyCenter = 4000;
        //    this.equalizerHighFreq.Bands[0].Bandwidth = 20;
        //    this.equalizerHighFreq.Bands[1].FrequencyCenter = 8000;
        //    this.equalizerHighFreq.Bands[1].Bandwidth = 20;
        //    this.equalizerHighFreq.Bands[2].FrequencyCenter = 16000;
        //    this.equalizerHighFreq.Bands[2].Bandwidth = 20;

        //    this.customEQ63HzGain = Double.Parse(this.localSettings.Values["EQ63Hz"].ToString());
        //    this.customEQ125HzGain = Double.Parse(this.localSettings.Values["EQ125Hz"].ToString());
        //    this.customEQ250HzGain = Double.Parse(this.localSettings.Values["EQ250Hz"].ToString());

        //    this.customEQ500HzGain = Double.Parse(this.localSettings.Values["EQ500Hz"].ToString());
        //    this.customEQ1kHzGain = Double.Parse(this.localSettings.Values["EQ1kHz"].ToString());
        //    this.customEQ2kHzGain = Double.Parse(this.localSettings.Values["EQ2kHz"].ToString());

        //    this.customEQ4kHzGain = Double.Parse(this.localSettings.Values["EQ4kHz"].ToString());
        //    this.customEQ8kHzGain = Double.Parse(this.localSettings.Values["EQ8kHz"].ToString());
        //    this.customEQ16kHzGain = Double.Parse(this.localSettings.Values["EQ16kHz"].ToString());
        //    this.chooseEQ(this.localSettings.Values["selected EQ"].ToString());
        //}

        private void switchView()
        {
            if (Page.ActualWidth > 1600 || (SettingsPage.Visibility == Visibility.Visible && HomePage.Visibility == Visibility.Visible))
            {
                if (SettingsPage.Visibility == Visibility.Collapsed)
                {
                    SettingsPage.Margin = new Thickness(0, 0, 750, 0);
                    HomePage.Margin = new Thickness(750, 0, 0, 0);
                    SettingsPage.Visibility = Visibility.Visible;
                }
                else
                {
                    SettingsPage.Margin = new Thickness(0, 0, 0, 0);
                    HomePage.Margin = new Thickness(0, 0, 0, 0);
                    SettingsPage.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                if (SettingsPage.Visibility == Visibility.Collapsed)
                {
                    SettingsPage.Margin = new Thickness(0, 0, 0, 0);
                    HomePage.Margin = new Thickness(0, 0, 0, 0);
                    HomePage.Visibility = Visibility.Collapsed;
                    SettingsPage.Visibility = Visibility.Visible;
                }
                else
                {
                    SettingsPage.Margin = new Thickness(0, 0, 0, 0);
                    HomePage.Margin = new Thickness(0, 0, 0, 0);
                    HomePage.Visibility = Visibility.Visible;
                    SettingsPage.Visibility = Visibility.Collapsed;
                }
            }
        }

        #endregion

        #region Settings

        #region Settings Events

        #region Settings Clickevents

        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            this.switchView();
        }


        #endregion

        #region Settings Sliderevents

        #region Echo Effect

        private void EchoDelaySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.echo != null)
            {
                this.echo.Delay = EchoDelaySlider.Value;
                //this.localSettings.Values["EchoDelay"] = EchoDelaySlider.Value;
                this.Settings[0] = EchoDelaySlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void EchoFeedBackSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.echo != null)
            {
                this.echo.Feedback = EchoFeedBackSlider.Value;
                //this.localSettings.Values["EchoFeedback"] = EchoFeedBackSlider.Value;
                this.Settings[1] = EchoFeedBackSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void EchoWetDryMixSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.echo != null)
            {
                this.echo.WetDryMix = EchoWetDryMixSlider.Value;
                //this.localSettings.Values["EchoWetDryMix"] = EchoWetDryMixSlider.Value;
                this.Settings[2] = EchoWetDryMixSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        #endregion

        #region Limiter Effect
        private void LimiterLoudnessSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.limiter != null)
            {
                this.limiter.Loudness = (uint)LimiterLoudnessSlider.Value;
                //this.localSettings.Values["LimiterLoudness"] = (uint)LimiterLoudnessSlider.Value;
                this.Settings[3] = LimiterLoudnessSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void LimiterReleaseSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.limiter != null)
            {
                this.limiter.Release = (uint)LimiterReleaseSlider.Value;
                //this.localSettings.Values["LimiterRelease"] = (uint)LimiterReleaseSlider.Value;
                this.Settings[4] = LimiterReleaseSlider.Value.ToString();
                this.WriteSettings();
            }

        }


        #endregion

        #region Reverb Effect
        private void ReverbDecayTimeSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.DecayTime = ReverbDecayTimeSlider.Value;
                //this.localSettings.Values["ReverbDecayTime"] = ReverbDecayTimeSlider.Value;
                this.Settings[5] = ReverbDecayTimeSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbDensitySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.Density = ReverbDensitySlider.Value;
                //this.localSettings.Values["ReverbDensity"] = ReverbDensitySlider.Value;
                this.Settings[6] = ReverbDensitySlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbDisableLateFieldToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.DisableLateField = ReverbDisableLateFieldToggleSwitch.IsOn;
                //this.localSettings.Values["ReverbDisableLateField"] = ReverbDisableLateFieldToggleSwitch.IsOn;
                this.Settings[7] = ReverbDisableLateFieldToggleSwitch.IsOn.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbEarlyDiffusionSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.EarlyDiffusion = (byte)ReverbEarlyDiffusionSlider.Value;
                //this.localSettings.Values["ReverbEarlyDiffusion"] = (byte)ReverbEarlyDiffusionSlider.Value;
                this.Settings[8] = ReverbEarlyDiffusionSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbHighEQCutOffSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.HighEQCutoff = (byte)ReverbHighEQCutOffSlider.Value;
                //this.localSettings.Values["ReverbHighEQCutoff"] = (byte)ReverbHighEQCutOffSlider.Value;
                this.Settings[9] = ReverbHighEQCutOffSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbHighEQGainSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.HighEQGain = (byte)ReverbHighEQGainSlider.Value;
                //this.localSettings.Values["ReverbHighEQGain"] = (byte)ReverbHighEQGainSlider.Value;
                this.Settings[10] = ReverbHighEQGainSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbLateDiffusionSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.LateDiffusion = (byte)ReverbLateDiffusionSlider.Value;
                //this.localSettings.Values["ReverbLateDiffusion"] = (byte)ReverbLateDiffusionSlider.Value;
                this.Settings[11] = ReverbLateDiffusionSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbLowEQCutOffSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.LowEQCutoff = (byte)ReverbLowEQCutOffSlider.Value;
                //this.localSettings.Values["ReverbLowEQCutoff"] = (byte)ReverbLowEQCutOffSlider.Value;
                this.Settings[12] = ReverbLowEQCutOffSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbLowEQGainSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.LowEQGain = (byte)ReverbLowEQGainSlider.Value;
                //this.localSettings.Values["ReverbLowEQGain"] = (byte)ReverbLowEQGainSlider.Value;
                this.Settings[13] = ReverbLowEQGainSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbPositionLeftSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.PositionLeft = (byte)ReverbPositionLeftSlider.Value;
                //this.localSettings.Values["ReverbPositionLeft"] = (byte)ReverbPositionLeftSlider.Value;
                this.Settings[14] = ReverbPositionLeftSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbPositionMatrixLeftSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.PositionMatrixLeft = (byte)ReverbPositionMatrixLeftSlider.Value;
                //this.localSettings.Values["ReverbPositionMatrixLeft"] = (byte)ReverbPositionMatrixLeftSlider.Value;
                this.Settings[15] = ReverbPositionMatrixLeftSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbPositionMatrixRightSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.PositionMatrixRight = (byte)ReverbPositionMatrixRightSlider.Value;
                //this.localSettings.Values["ReverbPositionMatrixRight"] = (byte)ReverbPositionMatrixRightSlider.Value;
                this.Settings[16] = ReverbPositionMatrixRightSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbPositionRightSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.PositionRight = (byte)ReverbPositionRightSlider.Value;
                //this.localSettings.Values["ReverbPositionRight"] = (byte)ReverbPositionRightSlider.Value;
                this.Settings[17] = ReverbPositionRightSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbRearDelaySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.RearDelay = (byte)ReverbRearDelaySlider.Value;
                //this.localSettings.Values["ReverbRearDelay"] = (byte)ReverbRearDelaySlider.Value;
                this.Settings[18] = ReverbRearDelaySlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbReflectionsDelaySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.ReflectionsDelay = (uint)ReverbReflectionsDelaySlider.Value;
                //this.localSettings.Values["ReverbReflectionsDealy"] = (uint)ReverbReflectionsDelaySlider.Value;
                this.Settings[19] = ReverbReflectionsDelaySlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbReflectionsGainSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.ReflectionsGain = ReverbReflectionsGainSlider.Value;
                //this.localSettings.Values["ReverbReflectionsGain"] = ReverbReflectionsGainSlider.Value;
                this.Settings[20] = ReverbReflectionsGainSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbReverbDelaySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.ReverbDelay = (byte)ReverbReverbDelaySlider.Value;
                //this.localSettings.Values["ReverbReverbDelay"] = (byte)ReverbReverbDelaySlider.Value;
                this.Settings[21] = ReverbReverbDelaySlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbReverbGainSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.ReverbGain = ReverbReverbGainSlider.Value;
                //this.localSettings.Values["ReverbReverbGain"] = ReverbReverbGainSlider.Value;
                this.Settings[22] = ReverbReverbGainSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbRoomFilterFreqSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.RoomFilterFreq = ReverbRoomFilterFreqSlider.Value;
                //this.localSettings.Values["ReverbRoomFilterFreq"] = ReverbRoomFilterFreqSlider.Value;
                this.Settings[23] = ReverbRoomFilterFreqSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbRoomFilterHFSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.RoomFilterHF = ReverbRoomFilterHFSlider.Value;
                //this.localSettings.Values["ReverbRoomFilterHF"] = ReverbRoomFilterHFSlider.Value;
                this.Settings[24] = ReverbRoomFilterHFSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbRoomFilterMainSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.RoomFilterMain = ReverbRoomFilterMainSlider.Value;
                //this.localSettings.Values["ReverbRoomFilterMain"] = ReverbRoomFilterMainSlider.Value;
                this.Settings[25] = ReverbRoomFilterMainSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbRoomSizeSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.RoomSize = ReverbRoomSizeSlider.Value;
                //this.localSettings.Values["ReverbRoomSize"] = ReverbRoomSizeSlider.Value;
                this.Settings[26] = ReverbRoomSizeSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        private void ReverbWetDryMixSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.reverb != null)
            {
                this.reverb.WetDryMix = ReverbWetDryMixSlider.Value;
                //this.localSettings.Values["ReverbWetDryMix"] = ReverbWetDryMixSlider.Value;
                this.Settings[27] = ReverbWetDryMixSlider.Value.ToString();
                this.WriteSettings();
            }
        }

        #endregion

        #endregion

        #endregion

        #region Equalizer Settings

        #region Equalizer Settings Clickevents

        private void EQNormal_Click(object sender, RoutedEventArgs e)
        {
            this.chooseEQ("Normal");
        }

        private void EQPop_Click(object sender, RoutedEventArgs e)
        {
            this.chooseEQ("Pop");
        }

        private void EQClassic_Click(object sender, RoutedEventArgs e)
        {
            this.chooseEQ("Classic");
        }

        private void EQJazz_Click(object sender, RoutedEventArgs e)
        {
            this.chooseEQ("Jazz");
        }

        private void EQRock_Click(object sender, RoutedEventArgs e)
        {
            this.chooseEQ("Rock");
        }

        private void EQParty_Click(object sender, RoutedEventArgs e)
        {
            this.chooseEQ("Party");
        }

        private void EQCustom_Click(object sender, RoutedEventArgs e)
        {
            this.chooseEQ("Custom");
        }

        #endregion

        #region Equalizer Settings Sliderevents

        #region ValueChanged

        private void EQ63HzSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.equalizerLowFreq != null /* && EQCustom.IsChecked == true */ )
            {
                this.customEQ63HzGain = EQ63HzSlider.Value;
                //this.localSettings.Values["EQ63Hz"] = EQ63HzSlider.Value;
                this.Settings[28] = EQ63HzSlider.Value.ToString();
                this.WriteSettings();
                this.chooseEQ("Custom");
            }
        }

        private void EQ125HzSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.equalizerLowFreq != null /* && EQCustom.IsChecked == true */ )
            {
                this.customEQ125HzGain = EQ125HzSlider.Value;
                //this.localSettings.Values["EQ125Hz"] = EQ125HzSlider.Value;
                this.Settings[29] = EQ125HzSlider.Value.ToString();
                this.WriteSettings();
                this.chooseEQ("Custom");
            }
        }

        private void EQ250HzSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.equalizerLowFreq != null /* && EQCustom.IsChecked == true */ )
            {
                this.customEQ250HzGain = EQ250HzSlider.Value;
                //this.localSettings.Values["EQ250Hz"] = EQ250HzSlider.Value;
                this.Settings[30] = EQ250HzSlider.Value.ToString();
                this.WriteSettings();
                this.chooseEQ("Custom");
            }
        }

        private void EQ500HzSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.equalizerMidFreq != null /* && EQCustom.IsChecked == true */ )
            {
                this.customEQ500HzGain = EQ500HzSlider.Value;
                //this.localSettings.Values["EQ500Hz"] = EQ500HzSlider.Value;
                this.Settings[31] = EQ500HzSlider.Value.ToString();
                this.WriteSettings();
                this.chooseEQ("Custom");
            }
        }

        private void EQ1kHzSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.equalizerMidFreq != null /* && EQCustom.IsChecked == true */ )
            {
                this.customEQ1kHzGain = EQ1kHzSlider.Value;
                //this.localSettings.Values["EQ1kHz"] = EQ1kHzSlider.Value;
                this.Settings[32] = EQ1kHzSlider.Value.ToString();
                this.WriteSettings();
                this.chooseEQ("Custom");
            }
        }

        private void EQ2kHzSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.equalizerMidFreq != null /* && EQCustom.IsChecked == true */ )
            {
                this.customEQ2kHzGain = EQ2kHzSlider.Value;
                //this.localSettings.Values["EQ2kHz"] = EQ2kHzSlider.Value;
                this.Settings[33] = EQ2kHzSlider.Value.ToString();
                this.WriteSettings();
                this.chooseEQ("Custom");
            }
        }

        private void EQ4kHzSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.equalizerHighFreq != null /* && EQCustom.IsChecked == true */ )
            {
                this.customEQ4kHzGain = EQ4kHzSlider.Value;
                //this.localSettings.Values["EQ4kHz"] = EQ4kHzSlider.Value;
                this.Settings[34] = EQ4kHzSlider.Value.ToString();
                this.WriteSettings();
                this.chooseEQ("Custom");
            }
        }

        private void EQ8kHzSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.equalizerHighFreq != null /* && EQCustom.IsChecked == true */ )
            {
                this.customEQ8kHzGain = EQ8kHzSlider.Value;
                //this.localSettings.Values["EQ8kHz"] = EQ8kHzSlider.Value;
                this.Settings[35] = EQ8kHzSlider.Value.ToString();
                this.WriteSettings();
                this.chooseEQ("Custom");
            }
        }

        private void EQ16kHzSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (this.equalizerHighFreq != null /* && EQCustom.IsChecked == true */ )
            {
                this.customEQ16kHzGain = EQ16kHzSlider.Value;
                //this.localSettings.Values["EQ16kHz"] = EQ16kHzSlider.Value;
                this.Settings[36] = EQ16kHzSlider.Value.ToString();
                this.WriteSettings();
                this.chooseEQ("Custom");
            }
        }

        #endregion

        #endregion

        #endregion

        #region Settings Helpermethods

        private void chooseEQ(string EQ)
        {
            // switch back old EQ button
            switch (this.Settings[37])
            {
                case "Normal":
                    EQNormal.IsChecked = false;
                    break;

                case "Pop":
                    EQPop.IsChecked = false;
                    break;

                case "Classic":
                    EQClassic.IsChecked = false;
                    break;

                case "Jazz":
                    EQJazz.IsChecked = false;
                    break;

                case "Rock":
                    EQRock.IsChecked = false;
                    break;

                case "Party":
                    EQParty.IsChecked = false;
                    break;

                case "Custom":
                    EQCustom.IsChecked = false;
                    break;
            }

            // set EQ
            switch (EQ)
            {
                case "Normal":
                    //this.setEQBands(0, 0, 0, 0, 0, 0, 0, 0, 0, false);
                    this.setEQBands(0.126, 0.126, 0.126, 0.126, 0.126, 0.126, 0.126, 0.126, 0.126, false);
                    //this.localSettings.Values["selectedEQ"] = "Normal";
                    this.Settings[37] = "Normal";
                    EQNormal.IsChecked = true;
                    break;

                case "Pop":
                    //this.setEQBands(0, 0, 0, 2, 3, -2, -4, -4, -4, false);
                    this.setEQBands(1, 1, 1, 1, 1, 1, 1, 1, 1, false);
                    //this.localSettings.Values["selectedEQ"] = "Pop";
                    this.Settings[37] = "Pop";
                    EQPop.IsChecked = true;
                    break;

                case "Classic":
                    //this.setEQBands(0, 0, -6, 0, 1, 0, 6, 6, 6, false);
                    this.setEQBands(1, 1, 1, 1, 1, 1, 1, 1, 1, false);
                    //this.localSettings.Values["selectedEQ"] = "Classic";
                    this.Settings[37] = "Classic";
                    EQClassic.IsChecked = true;
                    break;

                case "Jazz":
                    //this.setEQBands(0, 0, 5, -5, -2, 2, -1, -1, -1, false);
                    this.setEQBands(1, 1, 1, 1, 1, 1, 1, 1, 1, false);
                    //this.localSettings.Values["selectedEQ"] = "Jazz";
                    this.Settings[37] = "Jazz";
                    EQJazz.IsChecked = true;
                    break;

                case "Rock":
                    //this.setEQBands(0, 0, 3, -9, -2, 3, 3, 3, 3, false);
                    this.setEQBands(1, 1, 1, 1, 1, 1, 1, 1, 1, false);
                    //this.localSettings.Values["selectedEQ"] = "Rock";
                    this.Settings[37] = "Rock";
                    EQRock.IsChecked = true;
                    break;

                case "Party":
                    //this.setEQBands(8, 6, 3, 0, -3, -1, 1, 5, 9, false);
                    this.setEQBands(1, 1, 1, 1, 1, 1, 1, 1, 1, false);
                    //this.localSettings.Values["selectedEQ"] = "Party";
                    this.Settings[37] = "Party";
                    EQParty.IsChecked = true;
                    break;

                case "Custom":
                    this.setEQBands(1, 1, 1, 1, 1, 1, 1, 1, 1, true);
                    //this.localSettings.Values["selectedEQ"] = "Custom";
                    this.Settings[37] = "Custom";
                    EQCustom.IsChecked = true;
                    break;
            }
        }

        private void setEQBands(double low1, double low2, double low3, double mid1, double mid2, double mid3, double high1, double high2, double high3, bool isCustomEQ)
        {
            if (isCustomEQ)
            {
                this.equalizerLowFreq.Bands[0].Gain = this.customEQ63HzGain;
                EQ63HzSlider.Value = this.customEQ63HzGain;
                this.equalizerLowFreq.Bands[1].Gain = this.customEQ125HzGain;
                EQ125HzSlider.Value = this.customEQ125HzGain;
                this.equalizerLowFreq.Bands[2].Gain = this.customEQ250HzGain;
                EQ250HzSlider.Value = this.customEQ250HzGain;

                this.equalizerMidFreq.Bands[0].Gain = this.customEQ500HzGain;
                EQ500HzSlider.Value = this.customEQ500HzGain;
                this.equalizerMidFreq.Bands[1].Gain = this.customEQ1kHzGain;
                EQ1kHzSlider.Value = this.customEQ500HzGain;
                this.equalizerMidFreq.Bands[2].Gain = this.customEQ2kHzGain;
                EQ2kHzSlider.Value = this.customEQ500HzGain;

                this.equalizerHighFreq.Bands[0].Gain = this.customEQ4kHzGain;
                EQ4kHzSlider.Value = this.customEQ500HzGain;
                this.equalizerHighFreq.Bands[1].Gain = this.customEQ8kHzGain;
                EQ8kHzSlider.Value = this.customEQ500HzGain;
                this.equalizerHighFreq.Bands[2].Gain = this.customEQ16kHzGain;
                EQ16kHzSlider.Value = this.customEQ500HzGain;
            }
            else
            {
                this.equalizerLowFreq.Bands[0].Gain = low1;
                EQ63HzSlider.Value = low1;
                this.equalizerLowFreq.Bands[1].Gain = low2;
                EQ125HzSlider.Value = low2;
                this.equalizerLowFreq.Bands[2].Gain = low3;
                EQ250HzSlider.Value = low3;

                this.equalizerMidFreq.Bands[0].Gain = mid1;
                EQ500HzSlider.Value = mid1;
                this.equalizerMidFreq.Bands[1].Gain = mid2;
                EQ1kHzSlider.Value = mid2;
                this.equalizerMidFreq.Bands[2].Gain = mid3;
                EQ2kHzSlider.Value = mid3;

                this.equalizerHighFreq.Bands[0].Gain = high1;
                EQ4kHzSlider.Value = high1;
                this.equalizerHighFreq.Bands[1].Gain = high2;
                EQ8kHzSlider.Value = high2;
                this.equalizerHighFreq.Bands[2].Gain = high3;
                EQ16kHzSlider.Value = high3;
            }
        }



        #endregion

        #endregion
    }
}