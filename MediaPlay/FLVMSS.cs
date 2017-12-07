using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Foundation;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;
using System.Threading;

namespace MediaPlay
{
    public class FLVMSS
    {
        private AudioStreamDescriptor audioStreamDescriptor;
        private VideoStreamDescriptor videoStreamDescriptor;

        private string videoCodecName;
        private string audioCodecName;
        private MediaStreamSource mss;
        // Properties
        public AudioStreamDescriptor AudioDescriptor => audioStreamDescriptor;
        public VideoStreamDescriptor VideoDescriptor => videoStreamDescriptor;
        public string VideoCodecName => videoCodecName;
        public string AudioCodecName => audioCodecName;
        FlvSampleProvider _sampleProvider;
        StreamWebSocket _ws;
        DataReader _reader;
        FlvStreamParser _flvStreamParser;
        private SpinLock spinLock = new SpinLock();
        private FLVMSS()
        {
        }
        public static async Task<FLVMSS> CreateMSSFromWebSocketAsync(string uri)
        {
            var flvmss = new FLVMSS();
            flvmss._ws = new StreamWebSocket();
            try
            {
                await flvmss._ws.ConnectAsync(new Uri(uri, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return null;
            }

            return await CreateMSSFromRandomIInputStream(flvmss._ws.InputStream);

        }
        public static async Task<FLVMSS> CreateMSSFromRandomAccessStream(IRandomAccessStream stream)
        {

            var reader = new DataReader(stream);
            return await CreateMSSFromRandomDataReader(reader);

        }
        public static async Task<FLVMSS> CreateMSSFromRandomIInputStream(IInputStream stream)
        {

            var reader = new DataReader(stream);
            return await CreateMSSFromRandomDataReader(reader,true);

        }
        public static async Task<FLVMSS> CreateMSSFromRandomDataReader(DataReader reader, bool isAlive=false)
        {
            var flvmss = new FLVMSS();
            flvmss._reader = reader;
            flvmss._flvStreamParser = new FlvStreamParser(flvmss._reader);
            flvmss._sampleProvider = new FlvSampleProvider(flvmss._flvStreamParser);
            flvmss._sampleProvider.IsAlive = isAlive;
           var header = await flvmss._sampleProvider.ReadFlvHeaderAsync();
            flvmss._sampleProvider.StartQueueTags();
            if (header == null)
            {
                Debug.WriteLine("flv header error!");
                return null;

            }
            if (header.HasVideo)
            {
                SpinWait.SpinUntil(() => flvmss._flvStreamParser.VideoCodecType != VideoCodecType.Unknow);
                flvmss.CreateVideoStreamDescriptor(flvmss._flvStreamParser.VideoCodecType);

            }

            if (header.HasAudio)
            {
                SpinWait.SpinUntil(() => flvmss._flvStreamParser.AudioInfo != null && flvmss._flvStreamParser.AudioCodecType != AudioCodecType.Unknow);
                var audioinfo = flvmss._flvStreamParser.AudioInfo;
                flvmss.CreateAudioStreamDescriptor(flvmss._flvStreamParser.AudioCodecType, audioinfo.SampleRate, audioinfo.ChannleCount, 0);
            }

            bool result = flvmss.InitializeFLVMSS(isAlive);
            if (result)
            {

                return flvmss;
            }
            else
            {
                return null;
            }

        }

        private bool InitializeFLVMSS(bool isLive=false)
        {
            if (videoStreamDescriptor != null)
            {
                mss = new MediaStreamSource(videoStreamDescriptor);

            }
            else
            {
                return false;
            }

            if (audioStreamDescriptor != null)
            {
                mss.AddStreamDescriptor(audioStreamDescriptor);
            }

            mss.BufferTime = TimeSpan.FromMilliseconds(0);
            mss.Starting += OnStarting;
            mss.Closed += OnClosed;
            mss.SampleRequested += OnSampleRequested;
            mss.Paused += Mss_Paused;
            mss.IsLive = isLive;
            return true;

        }



        private void CreateAudioStreamDescriptor(AudioCodecType type, uint sampleRate, uint channelCount, uint bitRate)
        {
            if (type == AudioCodecType.AAC)
            {
                var audioProperties = AudioEncodingProperties.CreateAac(sampleRate, channelCount, bitRate);
                audioStreamDescriptor = new AudioStreamDescriptor(audioProperties);
            }
            else
            {

                throw new Exception("Unsupport Audio Codec");
            }


        }
        private void CreateVideoStreamDescriptor(VideoCodecType type)
        {
            if (type == VideoCodecType.AVC)
            {
                var videoProperties = VideoEncodingProperties.CreateH264();
                videoStreamDescriptor = new VideoStreamDescriptor(videoProperties);
            }
            else
            {
                throw new Exception("Unsupport Video Codec");
            }

        }

        public MediaStreamSource GetMediaStreamSource()
        {
            return mss;
        }

        private void Mss_Paused(MediaStreamSource sender, object args)
        {
            spinLock.Enter(ref _sampleProvider.Paused);
            spinLock.Exit();


        }
        public void OnStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            _sampleProvider.Paused = false;
            args.Request.SetActualStartPosition(new TimeSpan(0));
        }
        public void OnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            if (args.Request.StreamDescriptor is VideoStreamDescriptor)
            {
                args.Request.Sample = _sampleProvider.GetNextVideoSample();

            }
            else if (args.Request.StreamDescriptor is AudioStreamDescriptor)
            {
                args.Request.Sample = _sampleProvider.GetNextAudioSample();
            }
            else
            {
                args.Request.Sample = null;
            }

        }
        private void OnClosed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            mss.Closed -= OnClosed;
            mss.Starting -= OnStarting;
            mss.SampleRequested -= OnSampleRequested;
            mss = null;

            _sampleProvider?.cancellationTokenSource.Cancel();
            _sampleProvider = null;
            _flvStreamParser = null;
            _reader?.Dispose();
            _reader = null;

            if (_ws != null)
            {
                _ws.Close(1005, string.Empty);
                _ws.Dispose();
                _ws = null;

            }

        }

    }

}
