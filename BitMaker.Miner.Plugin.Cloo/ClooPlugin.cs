using System;
using System.IO;
using System.Reflection;

using Cloo;
using Cloo.Bindings;

namespace BitMaker.Miner.Plugin.Cloo
{

    [Plugin]
    public class ClooPlugin : IPlugin
    {

        string clProgramSource = @"

constant uint h[8] =
{
    0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
    0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19,
};

constant char hash1_pad[32] =
{
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
};

uint Shr(uint word, int shift)
{
    return word >> shift;
}

uint Rotr(uint word, int shift)
{
    return (word >> shift) | (word << (32 - shift));
}

uint Ch(uint x, uint y, uint z)
{
    return z ^ (x & (y ^ z));
}

uint Maj(uint x, uint y, uint z)
{
    return (x & y) | (z & (x | y));
}

uint e0(uint x)
{
    return Rotr(x, 2) ^ Rotr(x, 13) ^ Rotr(x, 22);
}

uint e1(uint x)
{
    return Rotr(x, 6) ^ Rotr(x, 11) ^ Rotr(x, 25);
}

uint s0(uint x)
{
    return Rotr(x, 7) ^ Rotr(x, 18) ^ Shr(x, 3);
}

uint s1(uint x)
{
    return Rotr(x, 17) ^ Rotr(x, 19) ^ Shr(x, 10);
}

void transform(char* state, char* input)
{
    uint* stateU = (uint*)state;

    uint a, b, c, d, e, f, g, h, t1, t2;
    uint W[64];

    for (int i = 0; i < 16; i++)
        W[i] = ((uint*)(input))[i];

    for (int i = 16; i < 64; i++)
        W[i] = s1(W[i - 2]) + W[i - 7] + s0(W[i - 15]) + W[i - 16];

    a = stateU[0]; b = stateU[1]; c = stateU[2]; d = stateU[3];
    e = stateU[4]; f = stateU[5]; g = stateU[6]; h = stateU[7];

    t1 = h + e1(e) + Ch(e, f, g) + 0x428a2f98 + W[0];
    t2 = e0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
    t1 = g + e1(d) + Ch(d, e, f) + 0x71374491 + W[1];
    t2 = e0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
    t1 = f + e1(c) + Ch(c, d, e) + 0xb5c0fbcf + W[2];
    t2 = e0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
    t1 = e + e1(b) + Ch(b, c, d) + 0xe9b5dba5 + W[3];
    t2 = e0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
    t1 = d + e1(a) + Ch(a, b, c) + 0x3956c25b + W[4];
    t2 = e0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
    t1 = c + e1(h) + Ch(h, a, b) + 0x59f111f1 + W[5];
    t2 = e0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
    t1 = b + e1(g) + Ch(g, h, a) + 0x923f82a4 + W[6];
    t2 = e0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
    t1 = a + e1(f) + Ch(f, g, h) + 0xab1c5ed5 + W[7];
    t2 = e0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

    t1 = h + e1(e) + Ch(e, f, g) + 0xd807aa98 + W[8];
    t2 = e0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
    t1 = g + e1(d) + Ch(d, e, f) + 0x12835b01 + W[9];
    t2 = e0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
    t1 = f + e1(c) + Ch(c, d, e) + 0x243185be + W[10];
    t2 = e0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
    t1 = e + e1(b) + Ch(b, c, d) + 0x550c7dc3 + W[11];
    t2 = e0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
    t1 = d + e1(a) + Ch(a, b, c) + 0x72be5d74 + W[12];
    t2 = e0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
    t1 = c + e1(h) + Ch(h, a, b) + 0x80deb1fe + W[13];
    t2 = e0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
    t1 = b + e1(g) + Ch(g, h, a) + 0x9bdc06a7 + W[14];
    t2 = e0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
    t1 = a + e1(f) + Ch(f, g, h) + 0xc19bf174 + W[15];
    t2 = e0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

    t1 = h + e1(e) + Ch(e, f, g) + 0xe49b69c1 + W[16];
    t2 = e0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
    t1 = g + e1(d) + Ch(d, e, f) + 0xefbe4786 + W[17];
    t2 = e0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
    t1 = f + e1(c) + Ch(c, d, e) + 0x0fc19dc6 + W[18];
    t2 = e0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
    t1 = e + e1(b) + Ch(b, c, d) + 0x240ca1cc + W[19];
    t2 = e0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
    t1 = d + e1(a) + Ch(a, b, c) + 0x2de92c6f + W[20];
    t2 = e0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
    t1 = c + e1(h) + Ch(h, a, b) + 0x4a7484aa + W[21];
    t2 = e0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
    t1 = b + e1(g) + Ch(g, h, a) + 0x5cb0a9dc + W[22];
    t2 = e0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
    t1 = a + e1(f) + Ch(f, g, h) + 0x76f988da + W[23];
    t2 = e0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

    t1 = h + e1(e) + Ch(e, f, g) + 0x983e5152 + W[24];
    t2 = e0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
    t1 = g + e1(d) + Ch(d, e, f) + 0xa831c66d + W[25];
    t2 = e0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
    t1 = f + e1(c) + Ch(c, d, e) + 0xb00327c8 + W[26];
    t2 = e0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
    t1 = e + e1(b) + Ch(b, c, d) + 0xbf597fc7 + W[27];
    t2 = e0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
    t1 = d + e1(a) + Ch(a, b, c) + 0xc6e00bf3 + W[28];
    t2 = e0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
    t1 = c + e1(h) + Ch(h, a, b) + 0xd5a79147 + W[29];
    t2 = e0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
    t1 = b + e1(g) + Ch(g, h, a) + 0x06ca6351 + W[30];
    t2 = e0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
    t1 = a + e1(f) + Ch(f, g, h) + 0x14292967 + W[31];
    t2 = e0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

    t1 = h + e1(e) + Ch(e, f, g) + 0x27b70a85 + W[32];
    t2 = e0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
    t1 = g + e1(d) + Ch(d, e, f) + 0x2e1b2138 + W[33];
    t2 = e0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
    t1 = f + e1(c) + Ch(c, d, e) + 0x4d2c6dfc + W[34];
    t2 = e0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
    t1 = e + e1(b) + Ch(b, c, d) + 0x53380d13 + W[35];
    t2 = e0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
    t1 = d + e1(a) + Ch(a, b, c) + 0x650a7354 + W[36];
    t2 = e0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
    t1 = c + e1(h) + Ch(h, a, b) + 0x766a0abb + W[37];
    t2 = e0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
    t1 = b + e1(g) + Ch(g, h, a) + 0x81c2c92e + W[38];
    t2 = e0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
    t1 = a + e1(f) + Ch(f, g, h) + 0x92722c85 + W[39];
    t2 = e0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

    t1 = h + e1(e) + Ch(e, f, g) + 0xa2bfe8a1 + W[40];
    t2 = e0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
    t1 = g + e1(d) + Ch(d, e, f) + 0xa81a664b + W[41];
    t2 = e0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
    t1 = f + e1(c) + Ch(c, d, e) + 0xc24b8b70 + W[42];
    t2 = e0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
    t1 = e + e1(b) + Ch(b, c, d) + 0xc76c51a3 + W[43];
    t2 = e0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
    t1 = d + e1(a) + Ch(a, b, c) + 0xd192e819 + W[44];
    t2 = e0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
    t1 = c + e1(h) + Ch(h, a, b) + 0xd6990624 + W[45];
    t2 = e0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
    t1 = b + e1(g) + Ch(g, h, a) + 0xf40e3585 + W[46];
    t2 = e0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
    t1 = a + e1(f) + Ch(f, g, h) + 0x106aa070 + W[47];
    t2 = e0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

    t1 = h + e1(e) + Ch(e, f, g) + 0x19a4c116 + W[48];
    t2 = e0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
    t1 = g + e1(d) + Ch(d, e, f) + 0x1e376c08 + W[49];
    t2 = e0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
    t1 = f + e1(c) + Ch(c, d, e) + 0x2748774c + W[50];
    t2 = e0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
    t1 = e + e1(b) + Ch(b, c, d) + 0x34b0bcb5 + W[51];
    t2 = e0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
    t1 = d + e1(a) + Ch(a, b, c) + 0x391c0cb3 + W[52];
    t2 = e0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
    t1 = c + e1(h) + Ch(h, a, b) + 0x4ed8aa4a + W[53];
    t2 = e0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
    t1 = b + e1(g) + Ch(g, h, a) + 0x5b9cca4f + W[54];
    t2 = e0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
    t1 = a + e1(f) + Ch(f, g, h) + 0x682e6ff3 + W[55];
    t2 = e0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

    t1 = h + e1(e) + Ch(e, f, g) + 0x748f82ee + W[56];
    t2 = e0(a) + Maj(a, b, c); d += t1; h = t1 + t2;
    t1 = g + e1(d) + Ch(d, e, f) + 0x78a5636f + W[57];
    t2 = e0(h) + Maj(h, a, b); c += t1; g = t1 + t2;
    t1 = f + e1(c) + Ch(c, d, e) + 0x84c87814 + W[58];
    t2 = e0(g) + Maj(g, h, a); b += t1; f = t1 + t2;
    t1 = e + e1(b) + Ch(b, c, d) + 0x8cc70208 + W[59];
    t2 = e0(f) + Maj(f, g, h); a += t1; e = t1 + t2;
    t1 = d + e1(a) + Ch(a, b, c) + 0x90befffa + W[60];
    t2 = e0(e) + Maj(e, f, g); h += t1; d = t1 + t2;
    t1 = c + e1(h) + Ch(h, a, b) + 0xa4506ceb + W[61];
    t2 = e0(d) + Maj(d, e, f); g += t1; c = t1 + t2;
    t1 = b + e1(g) + Ch(g, h, a) + 0xbef9a3f7 + W[62];
    t2 = e0(c) + Maj(c, d, e); f += t1; b = t1 + t2;
    t1 = a + e1(f) + Ch(f, g, h) + 0xc67178f2 + W[63];
    t2 = e0(b) + Maj(b, c, d); e += t1; a = t1 + t2;

    stateU[0] += a; stateU[1] += b; stateU[2] += c; stateU[3] += d;
    stateU[4] += e; stateU[5] += f; stateU[6] += g; stateU[7] += h;
}

kernel void test(
    global read_only  char* header_tail,
    global read_only  char* midstate,
    global write_only uint* output)
{
    // dimensions define nonce value to be tested
    uint d1 = get_global_id(0);
    uint d2 = get_global_id(1);
    uint d3 = get_global_id(2);
    uint nonce = (d1 * 268435456) + (d2 * 1048576) + d3;

    //char header_tail[64];

    // copy midstate to hash1[0:32], [33:32] should be hard coded to SHA256 padding
    // transform hash1 with modified header_tail 

    // init hash2[0:32] with h[]
    // transform hash2 with hash1

    // test that hash2[7] is equal to 0, if so, set nonce in output

    return;
}
";

        ComputeContext ccontext;
        ComputeDevice device;
        ComputeProgram program;

        public void Start(IPluginContext context)
        {
            return;

            string code;
            var kernelRes = Assembly.GetExecutingAssembly().GetManifestResourceStream("BitMaker.Miner.Plugin.Cloo.Miner.cl");
            using (var rdr = new StreamReader(kernelRes))
                code = clProgramSource;

            var platform = ComputePlatform.Platforms[0];
            var properties = new ComputeContextPropertyList(platform);
            device = platform.Devices[0];
            ccontext = new ComputeContext(platform.Devices, properties, null, IntPtr.Zero);
            program = new ComputeProgram(ccontext, code);
            program.Build(null, null, notify, IntPtr.Zero);
        }

        private unsafe void notify(CLProgramHandle programHandle, IntPtr userDataPtr)
        {
            uint[] dst = new uint[16];

            fixed (uint* dstPtr = dst)
            {
                using (var queue = new ComputeCommandQueue(ccontext, device, ComputeCommandQueueFlags.None))
                {
                    var buf = new ComputeBuffer<uint>(ccontext, ComputeMemoryFlags.WriteOnly, 16);

                    var kernel = program.CreateKernel("test");
                    kernel.SetValueArgument(0, 1443351125U);
                    kernel.SetMemoryArgument(1, buf);

                    var eventList = new ComputeEventList();

                    queue.Execute(kernel, null, new long[] { 16L, 256L, 1048576L }, null, null);
                    queue.Finish();
                    queue.Read<uint>(buf, true, 0, 16, (IntPtr)dstPtr, null);
                    queue.Finish();
                    queue.Finish();
                }
            }
        }

        public void Stop()
        {
            if (ccontext != null)
                ccontext.Dispose();
        }

    }

}
