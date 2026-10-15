using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// Decodes an audio file Windows can read (m4a/AAC, mp4, mp3...) to a 16-bit PCM WAV file, with
    /// Windows Media Foundation. The game plays sound through FMOD, which cannot read AAC.
    /// Calls the COM objects through their vtables directly (the game's Mono runtime has no
    /// reliable COM interop). Call from a background thread.
    /// </summary>
    internal static class MediaFoundationDecoder
    {
        private const int MfVersion = 0x00020070;
        private const uint FirstAudioStream = 0xFFFFFFFD;
        private const uint AllStreams = 0xFFFFFFFE;
        private const uint EndOfStream = 0x2;

        private static readonly Guid MajorType = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private static readonly Guid Subtype = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        private static readonly Guid AudioMajor = new Guid("73647561-0000-0010-8000-00AA00389B71");
        private static readonly Guid PcmSubtype = new Guid("00000001-0000-0010-8000-00AA00389B71");
        private static readonly Guid Channels = new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a");
        private static readonly Guid SampleRate = new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
        private static readonly Guid BitsPerSample = new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");

        // Vtable slots (IUnknown takes 0-2; IMFAttributes adds 30 methods from slot 3)
        private const int ReleaseSlot = 2;
        private const int ReaderSetStreamSelection = 4;
        private const int ReaderGetCurrentMediaType = 6;
        private const int ReaderSetCurrentMediaType = 7;
        private const int ReaderReadSample = 9;
        private const int AttributesGetUInt32 = 7;
        private const int AttributesSetUInt32 = 21;
        private const int AttributesSetGuid = 24;
        private const int SampleConvertToContiguousBuffer = 41;
        private const int BufferLock = 3;
        private const int BufferUnlock = 4;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetStreamSelectionFn(IntPtr self, uint stream, int selected);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetCurrentMediaTypeFn(IntPtr self, uint stream, out IntPtr mediaType);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetCurrentMediaTypeFn(IntPtr self, uint stream, IntPtr reserved, IntPtr mediaType);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ReadSampleFn(IntPtr self, uint stream, uint control, out uint actualStream, out uint flags, out long timestamp, out IntPtr sample);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetUInt32Fn(IntPtr self, ref Guid key, out int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetUInt32Fn(IntPtr self, ref Guid key, int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetGuidFn(IntPtr self, ref Guid key, ref Guid value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ConvertToContiguousBufferFn(IntPtr self, out IntPtr buffer);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int LockFn(IntPtr self, out IntPtr data, out int maxLength, out int currentLength);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int UnlockFn(IntPtr self);

        private static readonly Dictionary<IntPtr, Delegate> Delegates = new Dictionary<IntPtr, Delegate>();

        /// <summary>Decodes source to a WAV at destination, at most maxSeconds long. Throws on failure.</summary>
        public static void ToWav(string source, string destination, int maxSeconds)
        {
            CoInitializeEx(IntPtr.Zero, 0);
            Check(MFStartup(MfVersion, 0), "MFStartup");
            IntPtr reader = IntPtr.Zero;
            try
            {
                Check(MFCreateSourceReaderFromURL(source, IntPtr.Zero, out reader), "open");
                Check(Call<SetStreamSelectionFn>(reader, ReaderSetStreamSelection)(reader, AllStreams, 0), "select");
                Check(Call<SetStreamSelectionFn>(reader, ReaderSetStreamSelection)(reader, FirstAudioStream, 1), "select audio");

                Check(MFCreateMediaType(out IntPtr wanted), "media type");
                try
                {
                    Guid key = MajorType, value = AudioMajor;
                    Check(Call<SetGuidFn>(wanted, AttributesSetGuid)(wanted, ref key, ref value), "major type");
                    key = Subtype;
                    value = PcmSubtype;
                    Check(Call<SetGuidFn>(wanted, AttributesSetGuid)(wanted, ref key, ref value), "subtype");
                    key = BitsPerSample;
                    Check(Call<SetUInt32Fn>(wanted, AttributesSetUInt32)(wanted, ref key, 16), "bits");
                    Check(Call<SetCurrentMediaTypeFn>(reader, ReaderSetCurrentMediaType)(reader, FirstAudioStream, IntPtr.Zero, wanted), "PCM output");
                }
                finally
                {
                    Release(wanted);
                }

                Check(Call<GetCurrentMediaTypeFn>(reader, ReaderGetCurrentMediaType)(reader, FirstAudioStream, out IntPtr actual), "output type");
                int channels, rate, bits;
                try
                {
                    channels = ReadUInt32(actual, Channels);
                    rate = ReadUInt32(actual, SampleRate);
                    bits = ReadUInt32(actual, BitsPerSample);
                }
                finally
                {
                    Release(actual);
                }
                if (channels <= 0 || rate <= 0 || bits != 16)
                    throw new InvalidDataException($"unexpected audio format ({channels} ch, {rate} Hz, {bits} bits)");

                long maxBytes = (long)maxSeconds * rate * channels * 2;
                using (var file = new FileStream(destination, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(file))
                {
                    WriteHeader(writer, channels, rate, 0);
                    long written = 0;
                    var chunk = new byte[0];
                    ReadSampleFn read = Call<ReadSampleFn>(reader, ReaderReadSample);
                    int reads = 0, samples = 0;
                    uint seenFlags = 0;
                    while (written < maxBytes)
                    {
                        Check(read(reader, FirstAudioStream, 0, out _, out uint flags, out _, out IntPtr sample), "read");
                        reads++;
                        seenFlags |= flags;
                        if (sample != IntPtr.Zero)
                            samples++;
                        if (sample != IntPtr.Zero)
                        {
                            try
                            {
                                int length = CopySample(sample, ref chunk);
                                int take = (int)Math.Min(length, maxBytes - written);
                                writer.Write(chunk, 0, take);
                                written += take;
                            }
                            finally
                            {
                                Release(sample);
                            }
                        }
                        if ((flags & EndOfStream) != 0)
                            break;
                    }
                    if (written == 0)
                        throw new InvalidDataException($"no audio decoded ({reads} reads, {samples} samples, flags 0x{seenFlags:X})");
                    writer.Seek(0, SeekOrigin.Begin);
                    WriteHeader(writer, channels, rate, written);
                }
            }
            finally
            {
                Release(reader);
                MFShutdown();
            }
        }

        private static int CopySample(IntPtr sample, ref byte[] chunk)
        {
            Check(Call<ConvertToContiguousBufferFn>(sample, SampleConvertToContiguousBuffer)(sample, out IntPtr buffer), "buffer");
            try
            {
                Check(Call<LockFn>(buffer, BufferLock)(buffer, out IntPtr data, out _, out int length), "lock");
                try
                {
                    if (chunk.Length < length)
                        chunk = new byte[length];
                    Marshal.Copy(data, chunk, 0, length);
                    return length;
                }
                finally
                {
                    Call<UnlockFn>(buffer, BufferUnlock)(buffer);
                }
            }
            finally
            {
                Release(buffer);
            }
        }

        private static int ReadUInt32(IntPtr attributes, Guid key)
        {
            return Call<GetUInt32Fn>(attributes, AttributesGetUInt32)(attributes, ref key, out int value) < 0 ? 0 : value;
        }

        private static T Call<T>(IntPtr instance, int slot) where T : class
        {
            IntPtr vtable = Marshal.ReadIntPtr(instance);
            IntPtr function = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
            lock (Delegates)
            {
                if (!Delegates.TryGetValue(function, out Delegate cached))
                {
                    cached = Marshal.GetDelegateForFunctionPointer(function, typeof(T));
                    Delegates[function] = cached;
                }
                return (T)(object)cached;
            }
        }

        private static void Release(IntPtr instance)
        {
            if (instance != IntPtr.Zero)
                Call<ReleaseFn>(instance, ReleaseSlot)(instance);
        }

        private static void WriteHeader(BinaryWriter writer, int channels, int rate, long dataBytes)
        {
            int size = (int)Math.Min(dataBytes, int.MaxValue - 44);
            writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write(36 + size);
            writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(rate);
            writer.Write(rate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write(size);
        }

        private static void Check(int hresult, string step)
        {
            if (hresult < 0)
                throw new IOException($"Media Foundation: {step} failed (0x{hresult:X8})");
        }

        [DllImport("ole32.dll", ExactSpelling = true)]
        private static extern int CoInitializeEx(IntPtr reserved, int coInit);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFStartup(int version, int flags);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFShutdown();

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateMediaType(out IntPtr mediaType);

        [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int MFCreateSourceReaderFromURL(string url, IntPtr attributes, out IntPtr reader);
    }
}
