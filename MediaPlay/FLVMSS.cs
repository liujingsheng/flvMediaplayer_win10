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
            var flvmss=  new FLVMSS();
            flvmss._ws = new StreamWebSocket();
            try
            {
              
                await flvmss._ws.ConnectAsync(new Uri(uri, UriKind.Absolute));
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return null;
            }
        
            flvmss._reader = new DataReader(flvmss._ws.InputStream);
            flvmss._flvStreamParser = new FlvStreamParser(flvmss._reader);
            flvmss._sampleProvider = new FlvSampleProvider(flvmss._flvStreamParser);
            var header= await flvmss._sampleProvider.ReadFlvHeaderAsync();
            if(header==null)
            {
                Debug.WriteLine("flv header error!");
                return null;

            }
            flvmss._sampleProvider.StartQueueTags();
            flvmss.CreateVideoStreamDescriptor();
            flvmss.InitializeFLVMSS();
         
            return flvmss;

        }
        private void InitializeFLVMSS()
        {
            
            mss = new MediaStreamSource(videoStreamDescriptor);
            mss.BufferTime = TimeSpan.FromMilliseconds(200);
            mss.Starting += OnStarting;
            mss.Closed += OnClosed;
            mss.SampleRequested += OnSampleRequested;
            mss.Paused += Mss_Paused;
     

        }

 

        private void CreateAudioStreamDescriptor()
        {

        }
        private void CreateVideoStreamDescriptor()
        {
            var videoProperties = VideoEncodingProperties.CreateH264();
            videoStreamDescriptor = new VideoStreamDescriptor(videoProperties);
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

            args.Request.Sample= _sampleProvider.GetNextSample();    
 
        }
        private void OnClosed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            mss.Closed -= OnClosed;
            mss.Starting -= OnStarting;
            mss.SampleRequested -= OnSampleRequested;
            mss = null;

            _sampleProvider.cancellationTokenSource.Cancel();
            _sampleProvider = null;
            _flvStreamParser = null;
            _reader.Dispose();
            _reader = null;
      
            _ws.Close(1005, string.Empty);
            _ws.Dispose();
            _ws = null;        
          

        }

    }

}
