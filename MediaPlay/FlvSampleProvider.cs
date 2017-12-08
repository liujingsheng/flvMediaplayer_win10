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
        public ConcurrentQueue<VideoTag> VideoTagQueue;
        public ConcurrentQueue<AudioTag> AudioTagQueue;
        public CancellationTokenSource cancellationTokenSource;
        public FlvStreamParser FlvStreamParser;
        private TimeSpan pts;
        private Stopwatch watch = new Stopwatch();
        public bool Paused = false;
        public bool IsAlive = false;

        public FlvSampleProvider(FlvStreamParser flvStreamParser)
        {
            FlvStreamParser = flvStreamParser;
            cancellationTokenSource = new CancellationTokenSource();
            VideoTagQueue = new ConcurrentQueue<VideoTag>();
            AudioTagQueue = new ConcurrentQueue<AudioTag>();
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
        public MediaStreamSample GetNextVideoSample()
        {

            var tag = PopVideoTag();
            // pts += TimeSpan.FromMilliseconds(tag.PtsInterval);
            pts = TimeSpan.FromMilliseconds(tag.TimeStamp);
            var sample = MediaStreamSample.CreateFromBuffer(tag.data.AsBuffer(), pts);
            // sample.KeyFrame = tag.FrameType == FrameType.keyframe ? true : false;
            // sample.Duration = TimeSpan.FromMilliseconds(tag.PtsInterval);

            return sample;

        }
        public MediaStreamSample GetNextAudioSample()
        {

            var tag = PopAudioTag();
            //pts += TimeSpan.FromMilliseconds(tag.PtsInterval);
            pts = TimeSpan.FromMilliseconds(tag.TimeStamp);
            var sample = MediaStreamSample.CreateFromBuffer(tag.data.AsBuffer(), pts);
            //sample.DecodeTimestamp = pts;

           // sample.Duration = TimeSpan.FromMilliseconds(tag.PtsInterval);
            return sample;

        }
        public void StartQueueTags()
        {
            var task = Task.Run(async () =>
            {

                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    FlvTag tag = null;
                    try
                    {
                        tag = await FlvStreamParser.ReadTagAsync();
                    }
                    catch
                    {
                        Debug.WriteLine("ReadTagAsync failed!");
                        break;
                    }

                    if (tag == null)
                        break;

                    if (!Paused)
                    {
                        if (tag.Type == TagType.Video)
                        {

                            VideoTagQueue.Enqueue((VideoTag)tag);
                            //Debug.WriteLine($"Video Enqueue length: {VideoTagQueue.Count}");
                        }
                        else if (tag.Type == TagType.Audio)
                        {

                            AudioTagQueue.Enqueue((AudioTag)tag);
                            //Debug.WriteLine($"Audio Enqueue length: {AudioTagQueue.Count}");
                        }
                        else if (tag.Type == TagType.Script)
                        {
                            //todo
                        }
                        else
                        {
                            //todo
                        }

                    }
                    if (!IsAlive)
                    {
                        if ((AudioTagQueue.Count) >10)
                            await Task.Delay(30);
                        else
                            await Task.Delay(5);
                    }

                }



            }, cancellationTokenSource.Token);


        }
        public VideoTag PopVideoTag()
        {

            VideoTag tag = null;
            //Debug.WriteLine($"Video Dequeue length: {VideoTagQueue.Count}");
            SpinWait.SpinUntil(() => !VideoTagQueue.IsEmpty);
            VideoTagQueue.TryDequeue(out tag);       
            //if (VideoTagQueue.Count > 50)
            //{
            //    tag.PtsInterval = tag.PtsInterval > 0 ? 0 : tag.PtsInterval;
            //}
            return tag;
        }
        public AudioTag PopAudioTag()
        {

            AudioTag tag = null;
            //Debug.WriteLine($"Audio Dequeue length: {AudioTagQueue.Count}");
            SpinWait.SpinUntil(() => !AudioTagQueue.IsEmpty);
            AudioTagQueue.TryDequeue(out tag);
            if (tag.data == null)
            {
                SpinWait.SpinUntil(() => !AudioTagQueue.IsEmpty);
                AudioTagQueue.TryDequeue(out tag);
            }
            //if (AudioTagQueue.Count > 50)
            //{
            //    tag.PtsInterval = tag.PtsInterval > 0 ? 0 : tag.PtsInterval;
            //}
            return tag;
        }
    }
}
