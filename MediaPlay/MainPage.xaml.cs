using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace MediaPlay
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MediaStreamSource mss;
        private StreamWebSocket _ws;
        private string _uri;
        FlvStreamParser flvStreamParser;
        TimeSpan pts = TimeSpan.FromMilliseconds(0);
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private DataReader _reader;
        private bool Stoped = false;

        private readonly DisplayRequest _displayRequest = new DisplayRequest();


        public MainPage()
        {
            this.InitializeComponent();
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
            _displayRequest.RequestActive();
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = false;
            mediaElement.ExitDisplayModeOnAccessKeyInvoked=true;
        
            //var videoProperties = VideoEncodingProperties.CreateH264();
            //var videoStreamDec = new VideoStreamDescriptor(videoProperties);
            //mss = new MediaStreamSource(videoStreamDec);
            //mss.CanSeek = false;
            //mss.IsLive = true;

            //mss.BufferTime = TimeSpan.FromMilliseconds(0);
            //mss.Starting += MediaStreamSource_StartingAsync;
            //mss.Closed += Mss_Closed;
            //mss.SampleRequested += MediaStreamSource_SampleRequestedAsync;
            //mediaElement.CurrentStateChanged += Media_CurrentStateChanged;
        }

        private void Mss_Closed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            mss.Closed -= Mss_Closed;
            mss.Starting -= MediaStreamSource_StartingAsync;
            mss.SampleRequested -= MediaStreamSource_SampleRequestedAsync;
            pts = TimeSpan.FromMilliseconds(0);
            mss = null;

        }

        private async void MediaStreamSource_StartingAsync(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            var def = args.Request.GetDeferral();
            flvStreamParser = new FlvStreamParser(_reader);
            flvStreamParser.Header = await flvStreamParser.ReadHeaderAsync();
            def.Complete();

        }

        private void Media_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            //Debug.WriteLine(mediaElement.CurrentState);
        }

        private async void MediaStreamSource_SampleRequestedAsync(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            //MemoryBuffer m = new MemoryBuffer(10240);
            //var b = Windows.Storage.Streams.Buffer.CreateCopyFromMemoryBuffer(m);
            //await _ws.InputStream.ReadAsync(b, 10240, InputStreamOptions.None);

            //var a= b.ToArray();
            //  Debug.WriteLine(a.Length);
            //  long pts = 0;
            //  long dur = 0;
            //var deferal = args.Request.GetDeferral();
            //var sample = await MediaStreamSample.CreateFromStreamAsync(_ws.InputStream, 40960, pts);

            //args.Request.Sample = sample;
            //pts += TimeSpan.FromMilliseconds(58);
            ////args.Request.Sample.Duration = TimeSpan.FromMilliseconds(58);
            //deferal.Complete();


            //var deferal = args.Request.GetDeferral();
            //await _reader.LoadAsync(4);
            //var datasize =  _reader.ReadUInt32();
            //await _reader.LoadAsync(datasize);
            //var buff= _reader.ReadBuffer(datasize);
            //var sample = MediaStreamSample.CreateFromBuffer(buff, pts);
            //args.Request.Sample = sample;
            //pts += TimeSpan.FromMilliseconds(40);
            ////args.Request.Sample.Duration = TimeSpan.FromMilliseconds(58);
            //deferal.Complete();


            try
            {
                if (!Stoped)
                {
                    Stopwatch Watch = new Stopwatch();
                    Watch.Start();
                    var deferal = args.Request.GetDeferral();
                    FlvTag tag = await flvStreamParser.ReadTagAsync();

                    var sample = MediaStreamSample.CreateFromBuffer(tag.data.AsBuffer(), pts);
                    pts += TimeSpan.FromMilliseconds(tag.PtsInterval);
                    args.Request.Sample = sample;
                    args.Request.Sample.Duration = TimeSpan.FromMilliseconds(tag.PtsInterval);
                    deferal.Complete();
                    Watch.Stop();
                    long watchTime = Watch.ElapsedMilliseconds;
                    Debug.WriteLine($"pts: {pts}");
                    Debug.WriteLine($"TimeStamp: {tag.TimeStamp}");
                    Debug.WriteLine($"interval: {tag.PtsInterval}");
                    Debug.WriteLine($"time: {watchTime}");

                }
                else
                {
                    args.Request.Sample = null;
                }

            }
            catch (Exception ex)
            {

            }




        }

        private async void PlayButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            _uri = UriTextBox.Text;
            var flvMSS = await FLVMSS.CreateMSSFromWebSocketAsync(_uri);
            if (flvMSS != null)
            {
                mss = flvMSS.GetMediaStreamSource();
                var source = MediaSource.CreateFromMediaStreamSource(mss);
                var mediaPlayer = new MediaPlayer();
                mediaPlayer.Source = source;
                mediaElement.SetMediaPlayer(mediaPlayer);
                mediaElement.MediaPlayer.Source = source;
                mediaPlayer.Play();
            }
            //_uri = UriTextBox.Text;
            //_ws = new StreamWebSocket();
            //await _ws.ConnectAsync(new Uri(_uri, UriKind.Absolute));
            //_reader = new DataReader(_ws.InputStream);
            //var videoProperties = VideoEncodingProperties.CreateH264();
            //var videoStreamDec = new VideoStreamDescriptor(videoProperties);
            //mss = new MediaStreamSource(videoStreamDec);
            //mss.BufferTime = TimeSpan.FromMilliseconds(300);
            //mss.Starting += MediaStreamSource_StartingAsync;
            //mss.Closed += Mss_Closed;
            //mss.SampleRequested += MediaStreamSource_SampleRequestedAsync;
            //mediaElement.AutoPlay = true;
            //mediaElement.SetMediaStreamSource(mss);

            //Stoped = false;


        }

        private async void LoadButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            filePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            filePicker.FileTypeFilter.Add("*");

            StorageFile file = await filePicker.PickSingleFileAsync();
            IRandomAccessStream readStream = await file.OpenAsync(FileAccessMode.Read);     
            var flvMSS = await FLVMSS.CreateMSSFromRandomAccessStream(readStream);
            if (flvMSS != null)
            {
                mss = flvMSS.GetMediaStreamSource();
                //MediaElementO.SetMediaStreamSource(mss);
                //MediaElementO.Play();

                var source = MediaSource.CreateFromMediaStreamSource(mss);
                var mediaPlayer = new MediaPlayer();
                mediaPlayer.Source = source;
                mediaElement.SetMediaPlayer(mediaPlayer);
                mediaPlayer.Play();
                Stoped = false;
            }
        }

        private async void SaveButton_ClickAsync(object sender, RoutedEventArgs e)
        {

            _uri = "ws://192.168.1.37:8801/h5sws/Tiandi";
            _ws = new StreamWebSocket();
            await _ws.ConnectAsync(new Uri(_uri, UriKind.Absolute));
            var filePicker = new FileSavePicker();
            filePicker.FileTypeChoices.Add("flv", new List<string>() { ".flv" });
            filePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;

            StorageFile file = await filePicker.PickSaveFileAsync();
            //var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var stream = await file.OpenStreamForWriteAsync();

            try
            {
                await _ws.InputStream.AsStreamForRead().CopyToAsync(stream, 32767, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {

            }


        }

        private void StopSaveButton_ClickAsync(object sender, RoutedEventArgs e)
        {

            cancellationTokenSource.Cancel();
        }

        private void StopPlayButton_ClickAsync(object sender, RoutedEventArgs e)
        {

            mss?.NotifyError(MediaStreamSourceErrorStatus.Other);

            mediaElement.SetMediaPlayer(null);



        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {

            mediaElement.MediaPlayer?.Pause();
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            mediaElement.MediaPlayer?.Play();
        }

        private async void SnapButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            await SaveCurrentFrame();

        }
        private async Task SaveCurrentFrame()
        {

            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap();
            await renderTargetBitmap.RenderAsync(mediaElement, 1920, 1080);
            var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();

            StorageFolder currentFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var saveFile = await currentFolder.CreateFileAsync("hi33" + ".png", CreationCollisionOption.ReplaceExisting);
            if (saveFile == null)
                return;
            // Encode the image to the selected file on disk
            using (var fileStream = await saveFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);

                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    (uint)renderTargetBitmap.PixelWidth,
                    (uint)renderTargetBitmap.PixelHeight,
                    DisplayInformation.GetForCurrentView().LogicalDpi,
                    DisplayInformation.GetForCurrentView().LogicalDpi,
                    pixelBuffer.ToArray());

                await encoder.FlushAsync();
            }
            var success = await Windows.System.Launcher.LaunchFileAsync(saveFile);
        }



        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("fuu", "sfd");
            var view = ApplicationView.GetForCurrentView();
            if (view.IsFullScreenMode)
            {
                view.ExitFullScreenMode();

            }
            else
            {
                if (view.TryEnterFullScreenMode())
                {
                    mediaElement.IsFullWindow = true;
                }
                else
                {

                }
            }
        }

        public void ShowToast(string msg, string subMsg = null)
        {


            Debug.WriteLine(msg + "\n" + subMsg);



            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);

            var toastTextElements = toastXml.GetElementsByTagName("text");
            toastTextElements[0].AppendChild(toastXml.CreateTextNode(msg));
            toastTextElements[1].AppendChild(toastXml.CreateTextNode(subMsg));

            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }



        private void Img_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)

        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Hand, 1);

            var ct =  mediaElement.RenderTransform as CompositeTransform;
            ct.TranslateX += e.Delta.Translation.X;
            ct.TranslateY += e.Delta.Translation.Y;

        }

        private void ScrollViewerMain_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            mediaElement.MaxWidth = ((ScrollViewer)sender).ViewportWidth;
            mediaElement.MaxHeight = ((ScrollViewer)sender).ViewportHeight;
            var ct = mediaElement.RenderTransform as CompositeTransform;
            ct.TranslateX = 0;
            ct.TranslateY = 0;
            ((ScrollViewer)sender).ChangeView(0, 0, 1);
        }
    }
}
