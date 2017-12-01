using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MediaPlay
{
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
    public enum TagType
    {
        None = 0,
        Audio = 8,
        Video = 9,
        Script = 0x12
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

        public override string ToString()
        {
            return "#0: frame info"
                + "\r\n#1: {"
                + "\r\n  TagType: " + this.Type
                + "\r\n  DataSize: " + this.DataSize
                + "\r\n  StreamsID: " + this.StreamID

                + "\r\n}"
                ;
        }

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

        public virtual string Info1 { get { return "-"; } }
        public virtual string Info2 { get { return "-"; } }
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
        public string Codec
        {
            get
            {
                int codec = (taginfo >> 4) & 0xF;
                switch (codec)
                {
                    case 0:
                        return "Linear PCM, platform endian";
                    case 1:
                        return "ADPCM";
                    case 2:
                        return "MP3";
                    case 3:
                        return "Linear PCM, little endian";
                    case 4:
                        return "Nellymoser 16-kHz momo";
                    case 5:
                        return "Nellymoser 8-kHz momo";
                    case 6:
                        return "Nellymoser";
                    case 7:
                        return "G.711 A-law logarithmic PCM";
                    case 8:
                        return "G.711 mu-law logarithmic PCM";
                    case 9:
                        return "(reserved)";
                    case 10:
                        return "AAC";
                    case 11:
                        return "Speex";
                    case 14:
                        return "MP3 8-kHz";
                    case 15:
                        return "Device-specific sound";
                    default:
                        return "(unrecognized #" + codec + ")";
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
        public override string Info1 { get { return Codec; } }

    }
    public class VideoTag : FlvTag
    {
        public string FrameType
        {
            get
            {
                int type = (taginfo >> 4) & 0xF;
                switch (type)
                {
                    case 1:
                        return "keyframe";
                    case 2:
                        return "inter frame";
                    case 3:
                        return "disposable inter frame";
                    case 4:
                        return "generated keyframe";
                    case 5:
                        return "video info/command frame";
                    default:
                        return "(unrecognized #" + type + ")";
                }
            }
        }
        public string Codec
        {
            get
            {
                int codec = taginfo & 0xF;
                switch (codec)
                {
                    case 1:
                        return "JPEG (currently unused)";
                    case 2:
                        return "H.263";
                    case 3:
                        return "Screen video";
                    case 4:
                        return "On2 VP6";
                    case 5:
                        return "On2 VP6 with alpha channel";
                    case 6:
                        return "Screen video version 2";
                    case 7:
                        return "H.264";
                    default:
                        return "(unrecognized #" + codec + ")";
                }
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
        public override string Info1 { get { return Codec; } }
        public override string Info2 { get { return FrameType; } }

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
        public override string Info2 { get { return Values.Count + " 元素"; } }

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
        private uint _pts1 = 0;
        private uint _pts2 = 0;
        private uint count = 0;
        public FLVHeader Header;
        public byte[] SPSPPS;

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

            FlvTag tag;
            await _reader.LoadAsync(4);
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

            if (count % 2 == 0)
            {
                _pts1 = tag.TimeStamp;
            }
            else
            {
                _pts2 = tag.TimeStamp;

            }         
            count++;          
            tag.PtsInterval = count > 0 ? (uint)Math.Abs((int)(_pts2 - _pts1)) : 0;
            tag.PtsInterval = tag.PtsInterval > 500 ? 40 : tag.PtsInterval;
            tag.streamid = ReadUI24(_reader);
            await _reader.LoadAsync(tag.datasize);

            if (tag is ScriptTag)
            {


            }
            else if (tag is AudioTag)
            {
                tag.taginfo = _reader.ReadByte();

            }
            else if (tag is VideoTag)
            {
                tag.taginfo = _reader.ReadByte();
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
