using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using Windows.Media.Core;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;

namespace MediaPlay
{
    public class FlvSampleProvider
    {
        public ConcurrentQueue<FlvTag> TagQueue;
        public CancellationTokenSource cancellationTokenSource;
        public FlvStreamParser FlvStreamParser;
        private TimeSpan pts;
        private SpinWait spinWait = new SpinWait();     
        private Stopwatch watch = new Stopwatch();
        public bool Paused = false;

        public FlvSampleProvider(FlvStreamParser flvStreamParser)
        {
            FlvStreamParser = flvStreamParser;
            cancellationTokenSource = new CancellationTokenSource();
            TagQueue = new ConcurrentQueue<FlvTag>();
            pts = TimeSpan.FromMilliseconds(0);
        }
        public async Task<FLVHeader> ReadFlvHeaderAsync()
        {
            FLVHeader header;
            try
            {
                header = await FlvStreamParser.ReadHeaderAsync();
            }
            catch (Exception)
            {
                header = null;

            }

            return header;
        }
        public MediaStreamSample GetNextSample()
        {

            var tag = PopTag();
            pts += TimeSpan.FromMilliseconds(tag.PtsInterval);
            var sample = MediaStreamSample.CreateFromBuffer(tag.data.AsBuffer(), pts);
            sample.Duration = TimeSpan.FromMilliseconds(tag.PtsInterval);
            return sample;

        }
        public void StartQueueTags()
        {


            var task = Task.Run(async () =>
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {

                    spinWait.SpinOnce();
                    var tag = await FlvStreamParser.ReadTagAsync();
                    
                    if (!Paused)
                        TagQueue.Enqueue(tag);
             

                }


            }, cancellationTokenSource.Token);


        }
        public FlvTag PopTag()
        {

            FlvTag tag = null;
            Debug.WriteLine($"queue length: {TagQueue.Count}");
            SpinWait.SpinUntil(() => !TagQueue.IsEmpty);
            TagQueue.TryDequeue(out tag);
            if (TagQueue.Count > 50)
            {
                tag.PtsInterval = tag.PtsInterval > 0 ? 0 : tag.PtsInterval;
            }
            return tag;
        }
    }
}
