using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
namespace MediaPlay
{
    // 简化的AudioSpecificConfig 2字节定义如下：AAC Profile 5bits | 采样率 4bits | 声道数 4bits | 其他 3bits |
    public class AudioInfo
    {
        static uint[] SamplingFrequencyTable = {
  96000, 88200, 64000, 48000,
  44100, 32000, 24000, 22050,
  16000, 12000, 11025, 8000,
  7350,  0,     0,      0
};
        public bool IsReady { get; set; }
        private byte[] payload;
        public uint Profile
        {
            get
            {
                var profile = ((payload[0] & 0xF8) >> 3);
                return (uint)profile;

            }
        }
        public void SetData(byte[] data)
        {
            payload = data;
        }
        public uint SampleRate
        {
            get
            {
                if (payload == null)
                    return 0;
                var samplingFrequencyIndex = ((payload[0] & 0x7) << 1) | (payload[1] >> 7);
                return SamplingFrequencyTable[samplingFrequencyIndex];

            }
        }
        public uint ChannleCount
        {
            get
            {
                if (payload == null)
                    return 0;
                var channel = (payload[1] >> 3) & 0x0F;
                return (uint)channel;

            }
        }
        public uint BitRate
        {
            get
            {   //todo:add BitiRate Calcuate;             
                return 0;
            }
        }
        public uint FrameLengthFlag
        {
            get
            {
                if (payload == null)
                    return 0;
                var frameLengthFlag = (payload[1] >> 2) & 0x01;
                return (uint)frameLengthFlag;

            }
        }
        public uint DependsOnCoreCoder
        {
            get
            {
                if (payload == null)
                    return 0;
                var dependsOnCoreCoder = (payload[1] >> 1) & 0x01;
                return (uint)dependsOnCoreCoder;

            }
        }
        public uint ExtensionFlag
        {
            get
            {
                if (payload == null)
                    return 0;
                var extensionFlag = payload[1] & 0x01;
                return (uint)extensionFlag;

            }
        }
    }

    public class FLVHeader
    {
        public byte[] signature;
        public byte version;
        public byte typeflag;
        public int dataoffset;
        public byte[] data;
        public FLVHeader()
        {
            signature = new byte[3];
            version = 0;
            typeflag = 0;
            dataoffset = 0;
        }


        public bool IsFlv
        {
            get
            {
                if (signature == null || signature.Length != 3)
                    return false;
                return (signature[0] == 0x46) &&
                    (signature[1] == 0x4C) &&
                    (signature[2] == 0x56);
            }
        }
        public int Version
        {
            get { return version; }
        }
        public bool HasVideo
        {
            get { return (typeflag & 0x1) == 0x1; }
        }
        public bool HasAudio
        {
            get { return (typeflag & 0x4) == 0x4; }
        }
        public int Length
        {
            get
            {
                return dataoffset;
            }
        }
    }

    public enum VideoCodecType
    {
        JPEG=1,
        Sorenson_H263 = 2,
        Screen_video = 3,
        On2_VP6 = 4,
        On2_VP6_with_alpha_channel = 5,
        Screen_video_version2 = 6,
        AVC = 7,
        Unknow = 0xff
    }

    public enum AudioCodecType
    {
        Linear_PCM_platform_endian = 0x00,
        ADPCM = 0x01,
        MP3 = 0x02,
        Linear_PCM_little_endian = 0x03,
        Nellymoser_16_kHz_mono = 0x04,
        Nellymoser_8_kHz_mono = 0x05,
        Nellymoser = 0x06,
        G711_Alaw_logarithmic_PCM = 0x07,
        G711_mulaw_logarithmic_PCM = 0x08,
        reserved = 0x09,
        AAC = 0x0A,
        Speex = 0x0B,
        MP3_8Khz = 0x0E,
        Device_specific_sound = 0x0F,
        Unknow = 0xff
    }
    public enum TagType
    {
        None = 0,
        Audio = 8,
        Video = 9,
        Script = 0x12
    }
    public enum FrameType
    {

        keyframe = 1,
        interframe = 2,
        disposable_inter_frame = 3,
        generated_keyframe = 4,
        video_info_or_command_frame = 5,
        unknow = 0xff,
    }
    public class FlvTag
    {
        public uint presize;
        public int tagtype;
        public uint datasize;
        public uint timestamp; // 单位ms
        public int timestamp_ex;
        public uint streamid;
        public byte taginfo;
        public byte avcpaktype;
        public byte[] data;
        public uint PtsInterval;
        public FlvTag() { }     

        public TagType Type
        {
            get { return (TagType)tagtype; }
        }
        public int DataSize
        {
            get { return (int)datasize; }
        }
        public uint TimeStamp
        {
            get { return ((uint)timestamp_ex << 24) | timestamp; }
        }
        public uint StreamID
        {
            get { return streamid; }
        }

    }
    public class AudioTag : FlvTag
    {
        public double CodecId
        {
            get
            {
                return (taginfo >> 4) & 0xF;
            }
        }
        public AudioCodecType Codec
        {
            get
            {
                int codec = (taginfo >> 4) & 0xF;
                if(codec>0x0F)
                {
                    return AudioCodecType.Unknow;
                }
                else
                {
                    return (AudioCodecType)codec;
                }
           
            }
        }
        public int Sample
        {
            get
            {
                int sample = (taginfo >> 2) & 0x3;
                switch (sample)
                {
                    case 0:
                        return 5500;
                    case 1:
                        return 11000;
                    case 2:
                        return 22000;
                    case 3:
                        return 44000;
                    default:
                        return sample;
                }
            }
        }
        public int Bit
        {
            get
            {
                int bit = (taginfo >> 1) & 0x1;
                if (bit == 0)
                    return 8;
                else if (bit == 1)
                    return 16;
                return bit;
            }
        }
        public int Channel
        {
            get
            {
                return taginfo & 0x1;
            }
        }


    }
    public class VideoTag : FlvTag
    {
        public FrameType FrameType
        {
            get
            {
                int type = (taginfo >> 4) & 0xF;
                return type > 5 ? FrameType.unknow : (FrameType)type;
            }
        }
        public VideoCodecType Codec
        {
            get
            {
                int codec = taginfo & 0xF;
                return codec > 7 ? VideoCodecType.Unknow : (VideoCodecType)codec;
            }
            
        }
        public double CodecId
        {
            get
            {
                return taginfo & 0xF;
            }
        }
        public int AVCPacketType
        {
            get { return avcpaktype; }
        }
   

    }
    public class ScriptTag : FlvTag
    {
        public List<KeyValuePair<string, object>> Values { get; private set; }
        private int offset = 0;

        public ScriptTag()
        {
            Values = new List<KeyValuePair<string, object>>();
        }
        public override string ToString()
        {
            string str = "";
            foreach (KeyValuePair<string, object> kv in this.Values)
            {
                str += kv.Key + ": " + kv.Value + "\r\n";
            }
            return str;
        }
        public  string Info2 { get { return Values.Count + " 元素"; } }

        public bool TryGet(string key, out object o)
        {
            o = null;
            foreach (KeyValuePair<string, object> kv in Values)
            {
                if (kv.Value is ScriptObject)
                {
                    o = (kv.Value as ScriptObject)[key];
                }
            }
            return o != null;
        }
    }
    public class ScriptObject
    {
        public static int indent = 0;
        private Dictionary<string, object> values = new Dictionary<string, object>();
        public object this[string key]
        {
            get
            {
                object o;
                values.TryGetValue(key, out o);
                return o;
            }
            set
            {
                if (!values.ContainsKey(key))
                {
                    values.Add(key, value);
                }
            }
        }
        public override string ToString()
        {
            string str = "{\r\n";
            ScriptObject.indent += 2;
            foreach (KeyValuePair<string, object> kv in values)
            {
                str += new string(' ', ScriptObject.indent) + kv.Key + ": " + kv.Value + "\r\n";
            }
            ScriptObject.indent -= 2;
            //if (str.Length > 1)
            //    str = str.Substring(0, str.Length - 1);
            str += "}";
            return str;
        }
    }
    public class ScriptArray
    {
        private List<object> values = new List<object>();
        public object this[int index]
        {
            get
            {
                if (index >= 0 && index < values.Count)
                    return values[index];
                return null;
            }
        }
        public void Add(object o)
        {
            values.Add(o);
        }
        public override string ToString()
        {
            string str = "[";
            int n = 0;
            foreach (object o in values)
            {
                if (n % 10 == 0)
                    str += "\r\n";
                n++;
                str += o + ",";
            }
            if (str.Length > 1)
                str = str.Substring(0, str.Length - 1);
            str += "\r\n]";
            return str;
        }
    }
    public class FlvStreamParser
    {
        private DataReader _reader;
        private byte[] _naluHeader = new byte[] { 0, 0, 0, 1 };
        private uint _audioPts1 = 0;
        private uint _audioPts2 = 0;
        private uint _audioCount = 0;
        private uint _videoPts1 = 0;
        private uint _videoPts2 = 0;
        private uint _videoCount = 0;
        public FLVHeader Header;
        public byte[] SPSPPS;
        public AudioInfo AudioInfo;
        public VideoCodecType VideoCodecType = VideoCodecType.Unknow;
        public AudioCodecType AudioCodecType = AudioCodecType.Unknow;

        public FlvStreamParser(IInputStream stream)
        {
            _reader = new DataReader(stream);
        }
        public FlvStreamParser(DataReader reader)
        {
            _reader = reader;
        }

        public async Task<FLVHeader> ReadHeaderAsync()
        {
            Header = new FLVHeader();
            await _reader.LoadAsync(9);
            byte[] data = new byte[9];
            _reader.ReadBytes(data);
            Header.data = data;
            Array.Copy(data, Header.signature, 3);
            Header.version = data[3];
            Header.typeflag = data[4];
            Header.dataoffset = BitConverter.ToInt32(data.Reverse().ToArray(), 0);
            return Header;
        }
        public async Task<FlvTag> ReadTagAsync()
        {

            FlvTag tag = null;
            var loadsize = await _reader.LoadAsync(4);

            var presize = _reader.ReadUInt32();
            await _reader.LoadAsync(11);
            int type = _reader.ReadByte();
            if (type == 8)
                tag = new AudioTag();
            else if (type == 9)
                tag = new VideoTag();
            else if (type == 0x12)
                tag = new ScriptTag();
            else
                tag = new FlvTag();
            tag.presize = presize;
            tag.tagtype = type;
            tag.datasize = ReadUI24(_reader);
            tag.timestamp = ReadUI24(_reader);
            tag.timestamp_ex = _reader.ReadByte();
            if (type == 8)
            {

                if (_audioCount % 2 == 0)
                {
                    _audioPts1 = tag.TimeStamp;
                }
                else
                {
                    _audioPts2 = tag.TimeStamp;

                }
                _audioCount++;
                tag.PtsInterval = _audioCount > 0 ? (uint)Math.Abs((int)(_audioPts2 - _audioPts1)) : 0;
                tag.PtsInterval = tag.PtsInterval > 500 ? 40 : tag.PtsInterval;
            }
            else if (type == 9)
            {

                if (_videoCount % 2 == 0)
                {
                    _videoPts1 = tag.TimeStamp;
                }
                else
                {
                    _videoPts2 = tag.TimeStamp;

                }
                _videoCount++;
                tag.PtsInterval = _videoCount > 0 ? (uint)Math.Abs((int)(_videoPts1 - _videoPts2)) : 0;
                tag.PtsInterval = tag.PtsInterval > 500 ? 40 : tag.PtsInterval;
            }

            tag.streamid = ReadUI24(_reader);
            await _reader.LoadAsync(tag.datasize);

            if (tag is ScriptTag)
            {
                _reader.ReadBuffer(tag.datasize);
            }
            else if (tag is AudioTag)
            {

                tag.taginfo = _reader.ReadByte();
                var taga = tag as AudioTag;
                if (AudioCodecType == AudioCodecType.Unknow)
                {
                    AudioCodecType = (AudioCodecType)(taga.CodecId);
                }
                //如果acc编码
                if (taga.CodecId == (double)AudioCodecType.AAC)
                {
                    var aacSequenceHeader = _reader.ReadByte();
                    //aacSequenceHeader == 0 startAcc Sequence
                    if (aacSequenceHeader == 0)
                    {
                        byte[] payload = new byte[2];
                        _reader.ReadBytes(payload);
                        AudioInfo audioInfo = new MediaPlay.AudioInfo();
                        audioInfo.SetData(payload);
                        AudioInfo = audioInfo;

                    }
                    else
                    {
                        var buf = _reader.ReadBuffer(tag.datasize - 2);
                        tag.data = buf.ToArray();

                    }

                }
                else
                {
                    throw new Exception("Unsuport Audio Codec, not AAC");
                }

            }
            else if (tag is VideoTag)
            {
                tag.taginfo = _reader.ReadByte();
                var taga = tag as VideoTag;
                if (VideoCodecType == VideoCodecType.Unknow)
                {
                    VideoCodecType = (VideoCodecType)(taga.CodecId);
                }
                if (VideoCodecType == VideoCodecType.AVC)
                {
                    tag.avcpaktype = _reader.ReadByte();
                    var compositionTime = ReadUI24(_reader);
                    if (tag.avcpaktype == 1)
                    {
                        uint naluSize = _reader.ReadUInt32();
                        byte[] size = new byte[naluSize];
                        _reader.ReadBytes(size);
                        var data = _naluHeader.Concat(size);
                        tag.data = data.ToArray();
                    }
                    else
                    {

                        byte[] data1 = new byte[6];
                        _reader.ReadBytes(data1);
                        uint spsSize = _reader.ReadUInt16();
                        byte[] spsdata = new byte[spsSize];
                        _reader.ReadBytes(spsdata);
                        byte interbyter = _reader.ReadByte();
                        uint ppsSize = _reader.ReadUInt16();
                        byte[] ppsdata = new byte[ppsSize];
                        _reader.ReadBytes(ppsdata);
                        var sps = _naluHeader.Concat(spsdata);
                        var pps = _naluHeader.Concat(ppsdata);
                        tag.data = sps.Concat(pps).ToArray();
                        SPSPPS = tag.data;
                    }
                }
                else
                {
                    throw new Exception("Unsuport Video Codec, not Avc");
                }

            }
            return tag;

        }
        public static uint ReadUI24(DataReader src)
        {
            byte[] bs = new byte[3];
            src.ReadBytes(bs);
            return ByteToUInt(bs, 3);
        }
        public static uint ByteToUInt(byte[] bs, int length)
        {
            if (bs == null || bs.Length < length)
                return 0;
            uint rtn = 0;
            for (int i = 0; i < length; i++)
            {
                rtn <<= 8;
                rtn |= bs[i];
            }
            return rtn;
        }
        public static double ByteToDouble(byte[] bs)
        {
            if (bs == null || bs.Length < 8)
                return 0;
            byte[] b2 = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                b2[i] = bs[7 - i];
            }
            return BitConverter.ToDouble(b2, 0);
        }


    }
}
