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
        // Properties
        public AudioStreamDescriptor AudioDescriptor => _audioStreamDescriptor;
        public VideoStreamDescriptor VideoDescriptor => _videoStreamDescriptor;
        private FlvSampleProvider _sampleProvider;
        private StreamWebSocket _ws;
        private DataReader _reader;
        private FlvStreamParser _flvStreamParser;
        private AudioStreamDescriptor _audioStreamDescriptor;
        private VideoStreamDescriptor _videoStreamDescriptor;
        private MediaStreamSource _mss;
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
            return await CreateMSSFromRandomDataReader(reader, true);

        }
        public static async Task<FLVMSS> CreateMSSFromRandomDataReader(DataReader reader, bool isLive = false)
        {
            var flvmss = new FLVMSS();
            flvmss._reader = reader;
            flvmss._flvStreamParser = new FlvStreamParser(flvmss._reader);
            flvmss._sampleProvider = new FlvSampleProvider(flvmss._flvStreamParser);
            flvmss._sampleProvider.IsAlive = isLive;
            var header = await flvmss._sampleProvider.ReadFlvHeaderAsync();
         
            if (header == null||!header.IsFlv)
            {
                Debug.WriteLine("flv header error!");
                return null;

            }
            flvmss._sampleProvider.StartQueueTags();
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

            bool result = flvmss.InitializeFLVMSS(isLive);
            if (result)
            {

                return flvmss;
            }
            else
            {
                return null;
            }

        }

        private bool InitializeFLVMSS(bool isLive = false)
        {
            if (_videoStreamDescriptor != null)
            {
                _mss = new MediaStreamSource(_videoStreamDescriptor);
            }
            else
            {
                return false;
            }

            if (_audioStreamDescriptor != null)
            {
                _mss.AddStreamDescriptor(_audioStreamDescriptor);
            }

            _mss.BufferTime = TimeSpan.FromMilliseconds(0);
            _mss.Starting += OnStarting;
            _mss.Closed += OnClosed;
            _mss.SampleRequested += OnSampleRequested;
            _mss.Paused += Mss_Paused;
            _mss.IsLive = isLive;
            return true;

        }


        private void CreateAudioStreamDescriptor(AudioCodecType type, uint sampleRate, uint channelCount, uint bitRate)
        {
            if (type == AudioCodecType.AAC)
            {
                var audioProperties = AudioEncodingProperties.CreateAac(sampleRate, channelCount, bitRate);
                _audioStreamDescriptor = new AudioStreamDescriptor(audioProperties);
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
                _videoStreamDescriptor = new VideoStreamDescriptor(videoProperties);
            }
            else
            {
                throw new Exception("Unsupport Video Codec");
            }

        }

        public MediaStreamSource GetMediaStreamSource()
        {
            return _mss;
        }

        private void Mss_Paused(MediaStreamSource sender, object args)
        {
            _sampleProvider.Paused = true;
          


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
            _mss.Closed -= OnClosed;
            _mss.Starting -= OnStarting;
            _mss.SampleRequested -= OnSampleRequested;
            _mss = null;

            _sampleProvider?.cancellationTokenSource.Cancel();
            _sampleProvider = null;
            _flvStreamParser = null;
            _reader?.Dispose();
            _reader = null;

            _ws?.Close(1005, string.Empty);
            _ws?.Dispose();
            _ws = null;


        }

    }

}
