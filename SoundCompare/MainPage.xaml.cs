using System;
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

        TimeSpan start = new TimeSpan(0, 0, 0);
        TimeSpan end;
        TimeSpan currentTime;

        ImageSource Sound;
        ImageSource noSound;
        ImageBrush Play = new ImageBrush();
        ImageBrush Pause = new ImageBrush();

        DispatcherTimer dispatcherTimer = new DispatcherTimer();

        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

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

            audioGraph = result.Graph;
            await this.CreateDeviceOutputNode();
            this.ReadSettings();
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

            deviceOutputNode = result.DeviceOutputNode;
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
            HomePage.Visibility = Visibility.Collapsed;
            SettingsPage.Visibility = Visibility.Visible;
            this.audioGraph.Stop();
            this.dispatcherTimer.Stop();
            this.isPlaying = false;
            Playbutton.Background = this.Play;
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

        private void ReadSettings()
        {
            // Set Local Settings if not existing
            if (this.localSettings.Values["Settings Exists"] == null)
            {
                this.localSettings.Values["Settings Exists"] = "true";

                this.localSettings.Values["Echo Delay"] = "5";
                this.localSettings.Values["Echo Feedback"] = "0.2";
                this.localSettings.Values["Echo WetDryMix"] = "0.5";

                this.localSettings.Values["Limiter Loudness"] = "1";
                this.localSettings.Values["Limiter Release"] = "1";

                this.localSettings.Values["Reverb DecayTime"] = "1";
                this.localSettings.Values["Reverb Density"] = "2";
                this.localSettings.Values["Reverb DisableLateField"] = "false";
                this.localSettings.Values["Reverb EarlyDiffusion"] = "1";
                this.localSettings.Values["Reverb HighEQCutoff"] = "1";
                this.localSettings.Values["Reverb HighEQGain"] = "1";
                this.localSettings.Values["Reverb LateDiffusion"] = "1";
                this.localSettings.Values["Reverb LowEQCutoff"] = "1";
                this.localSettings.Values["Reverb LowEQGain"] = "1";
                this.localSettings.Values["Reverb PositionLeft"] = "1";
                this.localSettings.Values["Reverb PositionMatrixLeft"] = "1";
                this.localSettings.Values["Reverb PositionMatrixRight"] = "1";
                this.localSettings.Values["Reverb PositionRight"] = "1";
                this.localSettings.Values["Reverb RearDelay"] = "1";
                this.localSettings.Values["Reverb ReflectionsDelay"] = "1";
                this.localSettings.Values["Reverb ReflectionsGain"] = "1";
                this.localSettings.Values["Reverb ReverbDelay"] = "1";
                this.localSettings.Values["Reverb ReverbGain"] = "1";
                this.localSettings.Values["Reverb RoomFilterFreq"] = "200";
                this.localSettings.Values["Reverb RoomFilterHF"] = "0";
                this.localSettings.Values["Reverb RoomFilterMain"] = "0";
                this.localSettings.Values["Reverb RoomSize"] = "1";
                this.localSettings.Values["Reverb WetDryMix"] = "0.5";

                this.localSettings.Values["EQ 63Hz"] = "0";
                this.localSettings.Values["EQ 125Hz"] = "0";
                this.localSettings.Values["EQ 250Hz"] = "0";
                this.localSettings.Values["EQ 500Hz"] = "0";
                this.localSettings.Values["EQ 1kHz"] = "0";
                this.localSettings.Values["EQ 2kHz"] = "0";
                this.localSettings.Values["EQ 4kHz"] = "0";
                this.localSettings.Values["EQ 8kHz"] = "0";
                this.localSettings.Values["EQ 16kHz"] = "0";

                this.localSettings.Values["selected EQ"] = "Normal";
            }
            // Read local Settings
            this.echo = new EchoEffectDefinition(this.audioGraph);
            this.echo.Delay = Double.Parse(this.localSettings.Values["Echo Delay"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            this.echo.Feedback = Double.Parse(this.localSettings.Values["Echo Feedback"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            this.echo.WetDryMix = Double.Parse(this.localSettings.Values["Echo WetDryMix"].ToString(), System.Globalization.CultureInfo.InvariantCulture);

            this.limiter = new LimiterEffectDefinition(this.audioGraph);
            this.limiter.Loudness = UInt32.Parse(this.localSettings.Values["Limiter Loudness"].ToString());
            this.limiter.Release = UInt32.Parse(this.localSettings.Values["Limiter Release"].ToString());

            this.reverb = new ReverbEffectDefinition(this.audioGraph);
            this.reverb.DecayTime = Double.Parse(this.localSettings.Values["Reverb DecayTime"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            this.reverb.Density = Double.Parse(this.localSettings.Values["Reverb Density"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            this.reverb.DisableLateField = Boolean.Parse(this.localSettings.Values["Reverb DisableLateField"].ToString());
            this.reverb.EarlyDiffusion = Byte.Parse(this.localSettings.Values["Reverb EarlyDiffusion"].ToString());
            this.reverb.HighEQCutoff = Byte.Parse(this.localSettings.Values["Reverb HighEQCutoff"].ToString());
            this.reverb.HighEQGain = Byte.Parse(this.localSettings.Values["Reverb HighEQGain"].ToString());
            this.reverb.LateDiffusion = Byte.Parse(this.localSettings.Values["Reverb LateDiffusion"].ToString());
            this.reverb.LowEQCutoff = Byte.Parse(this.localSettings.Values["Reverb LowEQCutoff"].ToString());
            this.reverb.LowEQGain = Byte.Parse(this.localSettings.Values["Reverb LowEQGain"].ToString());
            this.reverb.PositionLeft = Byte.Parse(this.localSettings.Values["Reverb PositionLeft"].ToString());
            this.reverb.PositionMatrixLeft = Byte.Parse(this.localSettings.Values["Reverb PositionMatrixLeft"].ToString());
            this.reverb.PositionMatrixRight = Byte.Parse(this.localSettings.Values["Reverb PositionMatrixRight"].ToString());
            this.reverb.PositionRight = Byte.Parse(this.localSettings.Values["Reverb PositionRight"].ToString());
            this.reverb.RearDelay = Byte.Parse(this.localSettings.Values["Reverb RearDelay"].ToString());
            this.reverb.ReflectionsDelay = UInt32.Parse(this.localSettings.Values["Reverb ReflectionsDelay"].ToString());
            this.reverb.ReflectionsGain = Double.Parse(this.localSettings.Values["Reverb ReflectionsGain"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            this.reverb.ReverbDelay = Byte.Parse(this.localSettings.Values["Reverb ReverbDelay"].ToString());
            this.reverb.ReverbGain = Double.Parse(this.localSettings.Values["Reverb ReverbGain"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            this.reverb.RoomFilterFreq = Double.Parse(this.localSettings.Values["Reverb RoomFilterFreq"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            this.reverb.RoomFilterHF = Double.Parse(this.localSettings.Values["Reverb RoomFilterHF"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            this.reverb.RoomFilterMain = Double.Parse(this.localSettings.Values["Reverb RoomFilterMain"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            this.reverb.RoomSize = Double.Parse(this.localSettings.Values["Reverb RoomSize"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            this.reverb.WetDryMix = Double.Parse(this.localSettings.Values["Reverb WetDryMix"].ToString(), System.Globalization.CultureInfo.InvariantCulture);

            this.equalizerLowFreq = new EqualizerEffectDefinition(this.audioGraph);
            this.equalizerLowFreq.Bands[0].FrequencyCenter = 63;
            this.equalizerLowFreq.Bands[0].Bandwidth = 20;
            this.equalizerLowFreq.Bands[1].FrequencyCenter = 125;
            this.equalizerLowFreq.Bands[1].Bandwidth = 20;
            this.equalizerLowFreq.Bands[2].FrequencyCenter = 250;
            this.equalizerLowFreq.Bands[2].Bandwidth = 20;

            this.equalizerMidFreq = new EqualizerEffectDefinition(this.audioGraph);
            this.equalizerMidFreq.Bands[0].FrequencyCenter = 500;
            this.equalizerMidFreq.Bands[0].Bandwidth = 20;
            this.equalizerMidFreq.Bands[1].FrequencyCenter = 1000;
            this.equalizerMidFreq.Bands[1].Bandwidth = 20;
            this.equalizerMidFreq.Bands[2].FrequencyCenter = 2000;
            this.equalizerMidFreq.Bands[2].Bandwidth = 20;

            this.equalizerHighFreq = new EqualizerEffectDefinition(this.audioGraph);
            this.equalizerHighFreq.Bands[0].FrequencyCenter = 4000;
            this.equalizerHighFreq.Bands[0].Bandwidth = 20;
            this.equalizerHighFreq.Bands[1].FrequencyCenter = 8000;
            this.equalizerHighFreq.Bands[1].Bandwidth = 20;
            this.equalizerHighFreq.Bands[2].FrequencyCenter = 16000;
            this.equalizerHighFreq.Bands[2].Bandwidth = 20;

            this.customEQ63HzGain = Double.Parse(this.localSettings.Values["EQ 63Hz"].ToString());
            this.customEQ125HzGain = Double.Parse(this.localSettings.Values["EQ 125Hz"].ToString());
            this.customEQ250HzGain = Double.Parse(this.localSettings.Values["EQ 250Hz"].ToString());

            this.customEQ500HzGain = Double.Parse(this.localSettings.Values["EQ 500Hz"].ToString());
            this.customEQ1kHzGain = Double.Parse(this.localSettings.Values["EQ 1kHz"].ToString());
            this.customEQ2kHzGain = Double.Parse(this.localSettings.Values["EQ 2kHz"].ToString());

            this.customEQ4kHzGain = Double.Parse(this.localSettings.Values["EQ 4kHz"].ToString());
            this.customEQ8kHzGain = Double.Parse(this.localSettings.Values["EQ 8kHz"].ToString());
            this.customEQ16kHzGain = Double.Parse(this.localSettings.Values["EQ 16kHz"].ToString());

            this.chooseEQ(this.localSettings.Values["selected EQ"].ToString());
        }

        #endregion

        #region Settings

        #region Settings Events

        #region Settings Clickevents

        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            HomePage.Visibility = Visibility.Visible;
            SettingsPage.Visibility = Visibility.Collapsed;
        }


        #endregion

        #region Settings Sliderevents

        #region Echo Effect

        private void EchoDelaySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.echo.Delay = EchoDelaySlider.Value;
            this.localSettings.Values["Echo Delay"] = EchoDelaySlider.Value;
        }

        private void EchoFeedBackSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.echo.Feedback = EchoFeedBackSlider.Value;
            this.localSettings.Values["Echo Feedback"] = EchoFeedBackSlider.Value;
        }

        private void EchoWetDryMixSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.echo.WetDryMix = EchoWetDryMixSlider.Value;
            this.localSettings.Values["Echo WetDryMix"] = EchoWetDryMixSlider.Value;
        }

        #endregion

        #region Limiter Effect
        private void LimiterLoudnessSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.limiter.Loudness = (uint)LimiterLoudnessSlider.Value;
            this.localSettings.Values["Limiter Loudness"] = (uint)LimiterLoudnessSlider.Value;
        }

        private void LimiterReleaseSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.limiter.Release = (uint)LimiterReleaseSlider.Value;
            this.localSettings.Values["Limiter Release"] = (uint)LimiterReleaseSlider.Value;
        }


        #endregion

        #region Reverb Effect
        private void ReverbDecayTimeSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.DecayTime = ReverbDecayTimeSlider.Value;
            this.localSettings.Values["Reverb DecayTime"] = ReverbDecayTimeSlider.Value;
        }

        private void ReverbDensitySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.Density = ReverbDensitySlider.Value;
            this.localSettings.Values["Reverb Density"] = ReverbDensitySlider.Value;
        }

        private void ReverbDisableLateFieldToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            this.reverb.DisableLateField = ReverbDisableLateFieldToggleSwitch.IsOn;
            this.localSettings.Values["Reverb DisableLateField"] = ReverbDisableLateFieldToggleSwitch.IsOn;
        }

        private void ReverbEarlyDiffusionSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.EarlyDiffusion = (byte)ReverbEarlyDiffusionSlider.Value;
            this.localSettings.Values["Reverb EarlyDiffusion"] = (byte)ReverbEarlyDiffusionSlider.Value;
        }

        private void ReverbHighEQCutOffSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.HighEQCutoff = (byte)ReverbHighEQCutOffSlider.Value;
            this.localSettings.Values["Reverb HighEQCutoff"] = (byte)ReverbHighEQCutOffSlider.Value;
        }

        private void ReverbHighEQGainSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.HighEQGain = (byte)ReverbHighEQGainSlider.Value;
            this.localSettings.Values["Reverb HighEQGain"] = (byte)ReverbHighEQGainSlider.Value;
        }

        private void ReverbLateDiffusionSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.LateDiffusion = (byte)ReverbLateDiffusionSlider.Value;
            this.localSettings.Values["Reverb LateDiffusion"] = (byte)ReverbLateDiffusionSlider.Value;
        }

        private void ReverbLowEQCutOffSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.LowEQCutoff = (byte)ReverbLowEQCutOffSlider.Value;
            this.localSettings.Values["Reverb LowEQCutoff"] = (byte)ReverbLowEQCutOffSlider.Value;
        }

        private void ReverbLowEQGainSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.LowEQGain = (byte)ReverbLowEQGainSlider.Value;
            this.localSettings.Values["Reverb LowEQGain"] = (byte)ReverbLowEQGainSlider.Value;
        }

        private void ReverbPositionLeftSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.PositionLeft = (byte)ReverbPositionLeftSlider.Value;
            this.localSettings.Values["Reverb PositionLeft"] = (byte)ReverbPositionLeftSlider.Value;
        }

        private void ReverbPositionMatrixLeftSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.PositionMatrixLeft = (byte)ReverbPositionMatrixLeftSlider.Value;
            this.localSettings.Values["Reverb PositionMatrixLeft"] = (byte)ReverbPositionMatrixLeftSlider.Value;
        }

        private void ReverbPositionMatrixRightSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.PositionMatrixRight = (byte)ReverbPositionMatrixRightSlider.Value;
            this.localSettings.Values["Reverb PositionMatrixRight"] = (byte)ReverbPositionMatrixRightSlider.Value;
        }

        private void ReverbPositionRightSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.PositionRight = (byte)ReverbPositionRightSlider.Value;
            this.localSettings.Values["Reverb PositionRight"] = (byte)ReverbPositionRightSlider.Value;
        }

        private void ReverbRearDelaySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.RearDelay = (byte)ReverbRearDelaySlider.Value;
            this.localSettings.Values["Reverb RearDelay"] = (byte)ReverbRearDelaySlider.Value;
        }

        private void ReverbReflectionsDelaySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.ReflectionsDelay = (uint)ReverbReflectionsDelaySlider.Value;
            this.localSettings.Values["Reverb ReflectionsDealy"] = (uint)ReverbReflectionsDelaySlider.Value;
        }

        private void ReverbReflectionsGainSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.ReflectionsGain = ReverbReflectionsGainSlider.Value;
            this.localSettings.Values["Reverb ReflectionsGain"] = ReverbReflectionsGainSlider.Value;
        }

        private void ReverbReverbDelaySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.ReverbDelay = (byte)ReverbReverbDelaySlider.Value;
            this.localSettings.Values["Reverb ReverbDelay"] = (byte)ReverbReverbDelaySlider.Value;
        }

        private void ReverbReverbGainSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.ReverbGain = ReverbReverbGainSlider.Value;
            this.localSettings.Values["Reverb ReverbGain"] = ReverbReverbGainSlider.Value;
        }

        private void ReverbRoomFilterFreqSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.RoomFilterFreq = ReverbRoomFilterFreqSlider.Value;
            this.localSettings.Values["Reverb RoomFilterFreq"] = ReverbRoomFilterFreqSlider.Value;
        }

        private void ReverbRoomFilterHFSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.RoomFilterHF = ReverbRoomFilterHFSlider.Value;
            this.localSettings.Values["Reverb RoomFilterHF"] = ReverbRoomFilterHFSlider.Value;
        }

        private void ReverbRoomFilterMainSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.RoomFilterMain = ReverbRoomFilterMainSlider.Value;
            this.localSettings.Values["Reverb RoomFilterMain"] = ReverbRoomFilterMainSlider.Value;
        }

        private void ReverbRoomSizeSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.RoomSize = ReverbRoomSizeSlider.Value;
            this.localSettings.Values["Reverb RoomSize"] = ReverbRoomSizeSlider.Value;
        }

        private void ReverbWetDryMixSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            this.reverb.WetDryMix = ReverbWetDryMixSlider.Value;
            this.localSettings.Values["Reverb WetDryMix"] = ReverbWetDryMixSlider.Value;
        }

        #endregion

        #endregion

        #endregion

        #region Equalizer Settings

        #region Equalizer Settings Clickevents

        #endregion

        #region Equalizer Settings Sliderevents

        #endregion

        #endregion

        #region Settings Helpermethods

        private void chooseEQ(string EQ)
        {
            switch (EQ)
            {
                case "Normal":
                    this.equalizerLowFreq.Bands[0].Gain = 0;
                    this.equalizerLowFreq.Bands[1].Gain = 0;
                    this.equalizerLowFreq.Bands[2].Gain = 0;

                    this.equalizerMidFreq.Bands[0].Gain = 0;
                    this.equalizerMidFreq.Bands[1].Gain = 0;
                    this.equalizerMidFreq.Bands[2].Gain = 0;

                    this.equalizerHighFreq.Bands[0].Gain = 0;
                    this.equalizerHighFreq.Bands[1].Gain = 0;
                    this.equalizerHighFreq.Bands[2].Gain = 0;
                    break;
                case "Pop":
                    this.equalizerLowFreq.Bands[0].Gain = 0;
                    this.equalizerLowFreq.Bands[1].Gain = 0;
                    this.equalizerLowFreq.Bands[2].Gain = 0;

                    this.equalizerMidFreq.Bands[0].Gain = 2;
                    this.equalizerMidFreq.Bands[1].Gain = 3;
                    this.equalizerMidFreq.Bands[2].Gain = -2;

                    this.equalizerHighFreq.Bands[0].Gain = -4;
                    this.equalizerHighFreq.Bands[1].Gain = -4;
                    this.equalizerHighFreq.Bands[2].Gain = -4;
                    break;
                case "Classic":
                    this.equalizerLowFreq.Bands[0].Gain = 0;
                    this.equalizerLowFreq.Bands[1].Gain = 0;
                    this.equalizerLowFreq.Bands[2].Gain = -6;

                    this.equalizerMidFreq.Bands[0].Gain = 0;
                    this.equalizerMidFreq.Bands[1].Gain = 1;
                    this.equalizerMidFreq.Bands[2].Gain = 0;

                    this.equalizerHighFreq.Bands[0].Gain = 6;
                    this.equalizerHighFreq.Bands[1].Gain = 6;
                    this.equalizerHighFreq.Bands[2].Gain = 6;
                    break;
                case "Jazz":
                    this.equalizerLowFreq.Bands[0].Gain = 0;
                    this.equalizerLowFreq.Bands[1].Gain = 0;
                    this.equalizerLowFreq.Bands[2].Gain = 5;

                    this.equalizerMidFreq.Bands[0].Gain = -5;
                    this.equalizerMidFreq.Bands[1].Gain = -2;
                    this.equalizerMidFreq.Bands[2].Gain = 2;

                    this.equalizerHighFreq.Bands[0].Gain = -1;
                    this.equalizerHighFreq.Bands[1].Gain = -1;
                    this.equalizerHighFreq.Bands[2].Gain = -1;
                    break;
                case "Rock":
                    this.equalizerLowFreq.Bands[0].Gain = 0;
                    this.equalizerLowFreq.Bands[1].Gain = 0;
                    this.equalizerLowFreq.Bands[2].Gain = 3;

                    this.equalizerMidFreq.Bands[0].Gain = -9;
                    this.equalizerMidFreq.Bands[1].Gain = -2;
                    this.equalizerMidFreq.Bands[2].Gain = 3;

                    this.equalizerHighFreq.Bands[0].Gain = 3;
                    this.equalizerHighFreq.Bands[1].Gain = 3;
                    this.equalizerHighFreq.Bands[2].Gain = 3;
                    break;
                case "Party":
                    this.equalizerLowFreq.Bands[0].Gain = 8;
                    this.equalizerLowFreq.Bands[1].Gain = 6;
                    this.equalizerLowFreq.Bands[2].Gain = 3;

                    this.equalizerMidFreq.Bands[0].Gain = 0;
                    this.equalizerMidFreq.Bands[1].Gain = -3;
                    this.equalizerMidFreq.Bands[2].Gain = -1;

                    this.equalizerHighFreq.Bands[0].Gain = 1;
                    this.equalizerHighFreq.Bands[1].Gain = 5;
                    this.equalizerHighFreq.Bands[2].Gain = 9;
                    break;
                case "Custom":
                    this.equalizerLowFreq.Bands[0].Gain = this.customEQ63HzGain;
                    this.equalizerLowFreq.Bands[1].Gain = this.customEQ125HzGain;
                    this.equalizerLowFreq.Bands[2].Gain = this.customEQ250HzGain;

                    this.equalizerMidFreq.Bands[0].Gain = this.customEQ500HzGain;
                    this.equalizerMidFreq.Bands[1].Gain = this.customEQ1kHzGain;
                    this.equalizerMidFreq.Bands[2].Gain = this.customEQ2kHzGain;

                    this.equalizerHighFreq.Bands[0].Gain = this.customEQ4kHzGain;
                    this.equalizerHighFreq.Bands[1].Gain = this.customEQ8kHzGain;
                    this.equalizerHighFreq.Bands[2].Gain = this.customEQ16kHzGain;
                    break;
            }
        }

        #endregion

        #endregion


    }
}